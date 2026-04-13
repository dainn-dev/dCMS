using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace dCMS.Infrastructure.Messaging;

/// <summary>US-F1 / DAI-347 — <c>ProcessedMessages</c> table + session advisory locks.</summary>
public sealed class PostgresMessageIdempotencyService : IIdempotencyService
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresMessageIdempotencyService>? _logger;

    public PostgresMessageIdempotencyService(string connectionString, ILogger<PostgresMessageIdempotencyService>? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    public async Task<IAsyncDisposable> AcquireOrderingLockAsync(string messageId, CancellationToken cancellationToken = default)
    {
        messageId = (messageId ?? "").Trim();
        if (messageId.Length == 0)
            return NoopAsyncDisposable.Instance;

        var (k1, k2) = AdvisoryKeys(messageId);
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection
            .ExecuteAsync(
                "SELECT pg_advisory_lock(@K1, @K2);",
                new { K1 = k1, K2 = k2 })
            .ConfigureAwait(false);
        return new PgAdvisorySession(connection, k1, k2);
    }

    public async Task<bool> IsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        messageId = (messageId ?? "").Trim();
        if (messageId.Length == 0)
            return false;

        const string sql = """SELECT EXISTS (SELECT 1 FROM "ProcessedMessages" WHERE "MessageId" = @MessageId)""";
        await using var connection = new NpgsqlConnection(_connectionString);
        return await connection
            .ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { MessageId = messageId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        messageId = (messageId ?? "").Trim();
        if (messageId.Length == 0)
            return;

        const string sql = """
            INSERT INTO "ProcessedMessages" ("MessageId", "ProcessedAt")
            VALUES (@MessageId, NOW())
            ON CONFLICT ("MessageId") DO NOTHING
            """;
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection
            .ExecuteAsync(new CommandDefinition(sql, new { MessageId = messageId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        _logger?.LogDebug("Marked message {MessageId} as processed.", messageId);
    }

    private static (int K1, int K2) AdvisoryKeys(string messageId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(messageId));
        var k1 = BitConverter.ToInt32(hash.AsSpan(0, 4));
        var k2 = BitConverter.ToInt32(hash.AsSpan(4, 4));
        return (k1, k2);
    }

    private sealed class PgAdvisorySession : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly int _k1;
        private readonly int _k2;
        private bool _disposed;

        public PgAdvisorySession(NpgsqlConnection connection, int k1, int k2)
        {
            _connection = connection;
            _k1 = k1;
            _k2 = k2;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;
            try
            {
                await _connection
                    .ExecuteAsync("SELECT pg_advisory_unlock(@K1, @K2);", new { K1 = _k1, K2 = _k2 })
                    .ConfigureAwait(false);
            }
            finally
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class NoopAsyncDisposable : IAsyncDisposable
    {
        public static readonly NoopAsyncDisposable Instance = new();

        public ValueTask DisposeAsync() => default;
    }
}
