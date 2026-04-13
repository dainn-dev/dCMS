using dCMS.Payment.Core;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace dCMS.Payment.Infrastructure.Persistence;

public sealed class PostgresPaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly string _connectionString;

    public PostgresPaymentTransactionRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Payment")
            ?? throw new InvalidOperationException("ConnectionStrings:Payment is required.");
    }

    public async Task InsertInitiatedAsync(PaymentTransactionInsert row, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "PaymentTransactions" (
                "Id", "OrderId", "TenantId", "StoreId", "CustomerId", "PaymentMethod",
                "PaymentIntentId", "Amount", "Currency", "Status", "Provider", "CreatedAt")
            VALUES (
                @Id, @OrderId, @TenantId, @StoreId, @CustomerId, @PaymentMethod,
                @PaymentIntentId, @Amount, @Currency, @Status, @Provider, NOW())
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("Id", row.Id);
        cmd.Parameters.AddWithValue("OrderId", row.OrderId);
        cmd.Parameters.AddWithValue("TenantId", row.TenantId);
        cmd.Parameters.AddWithValue("StoreId", row.StoreId);
        cmd.Parameters.AddWithValue("CustomerId", row.CustomerId);
        cmd.Parameters.AddWithValue("PaymentMethod", row.PaymentMethod);
        cmd.Parameters.AddWithValue("PaymentIntentId", row.PaymentIntentId);
        cmd.Parameters.AddWithValue("Amount", row.Amount);
        cmd.Parameters.AddWithValue("Currency", row.Currency);
        cmd.Parameters.AddWithValue("Status", "initiated");
        cmd.Parameters.AddWithValue("Provider", row.Provider);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PaymentTransaction?> GetLatestByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "Id", "OrderId", "TenantId", "StoreId", "CustomerId", "PaymentMethod",
                "PaymentIntentId", "Amount", "Currency", "Status", "Provider", "CreatedAt"
            FROM "PaymentTransactions"
            WHERE "OrderId" = @OrderId
            ORDER BY "CreatedAt" DESC
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("OrderId", orderId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new PaymentTransaction(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetString(8),
            MapStatus(reader.GetString(9)),
            reader.GetString(10),
            reader.GetFieldValue<DateTimeOffset>(11));
    }

    public async Task<PaymentTransaction?> GetLatestByPaymentIntentIdAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "Id", "OrderId", "TenantId", "StoreId", "CustomerId", "PaymentMethod",
                "PaymentIntentId", "Amount", "Currency", "Status", "Provider", "CreatedAt"
            FROM "PaymentTransactions"
            WHERE "PaymentIntentId" = @PaymentIntentId
            ORDER BY "CreatedAt" DESC
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("PaymentIntentId", paymentIntentId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new PaymentTransaction(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetDecimal(7),
            reader.GetString(8),
            MapStatus(reader.GetString(9)),
            reader.GetString(10),
            reader.GetFieldValue<DateTimeOffset>(11));
    }

    public async Task UpdateStatusByIdAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "PaymentTransactions"
            SET "Status" = @Status
            WHERE "Id" = @Id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("Id", id);
        cmd.Parameters.AddWithValue("Status", status);
        var n = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (n != 1)
            throw new InvalidOperationException($"PaymentTransactions update expected1 row, got {n} for id {id}.");
    }

    private static PaymentTransactionStatus MapStatus(string raw)
    {
        return raw.ToLowerInvariant() switch
        {
            "pending" => PaymentTransactionStatus.Pending,
            "initiated" => PaymentTransactionStatus.Initiated,
            "completed" or "succeeded" => PaymentTransactionStatus.Succeeded,
            "failed" => PaymentTransactionStatus.Failed,
            "refunded" => PaymentTransactionStatus.Refunded,
            _ => PaymentTransactionStatus.Pending,
        };
    }
}
