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
                "Id", "OrderId", "TenantId", "StoreId", "ClientId", "CustomerId", "PaymentMethod",
                "PaymentIntentId", "Amount", "Currency", "Status", "Provider", "CreatedAt")
            VALUES (
                @Id, @OrderId, @TenantId, @StoreId, @ClientId, @CustomerId, @PaymentMethod,
                @PaymentIntentId, @Amount, @Currency, @Status, @Provider, NOW())
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("Id", row.Id);
        cmd.Parameters.AddWithValue("OrderId", row.OrderId);
        cmd.Parameters.AddWithValue("TenantId", row.TenantId);
        cmd.Parameters.AddWithValue("StoreId", row.StoreId);
        cmd.Parameters.AddWithValue("ClientId", row.ClientId);
        cmd.Parameters.AddWithValue("CustomerId", row.CustomerId);
        cmd.Parameters.AddWithValue("PaymentMethod", row.PaymentMethod);
        cmd.Parameters.AddWithValue("PaymentIntentId", row.PaymentIntentId);
        cmd.Parameters.AddWithValue("Amount", row.Amount);
        cmd.Parameters.AddWithValue("Currency", row.Currency);
        cmd.Parameters.AddWithValue("Status", "initiated");
        cmd.Parameters.AddWithValue("Provider", row.Provider);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PaymentTransaction?> GetLatestByOrderIdAsync(
        Guid orderId,
        Guid tenantId,
        string clientId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "Id", "OrderId", "TenantId", "StoreId", "ClientId", "CustomerId", "PaymentMethod",
                "PaymentIntentId", "Amount", "Currency", "Status", "Provider", "CreatedAt"
            FROM "PaymentTransactions"
            WHERE "OrderId" = @OrderId
              AND "TenantId" = @TenantId
              AND "ClientId" = @ClientId
              AND "Provider" = @Provider
            ORDER BY "CreatedAt" DESC
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("OrderId", orderId);
        cmd.Parameters.AddWithValue("TenantId", tenantId);
        cmd.Parameters.AddWithValue("ClientId", clientId);
        cmd.Parameters.AddWithValue("Provider", provider);
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
            reader.GetString(7),
            reader.GetDecimal(8),
            reader.GetString(9),
            MapStatus(reader.GetString(10)),
            reader.GetString(11),
            reader.GetFieldValue<DateTimeOffset>(12));
    }

    public async Task<PaymentTransaction?> GetLatestByPaymentIntentIdAsync(
        string paymentIntentId,
        Guid tenantId,
        string clientId,
        string provider,
        Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                "Id", "OrderId", "TenantId", "StoreId", "ClientId", "CustomerId", "PaymentMethod",
                "PaymentIntentId", "Amount", "Currency", "Status", "Provider", "CreatedAt"
            FROM "PaymentTransactions"
            WHERE "PaymentIntentId" = @PaymentIntentId
              AND "TenantId" = @TenantId
              AND "ClientId" = @ClientId
              AND "Provider" = @Provider
              AND (@StoreId IS NULL OR "StoreId" = @StoreId)
            ORDER BY "CreatedAt" DESC
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("PaymentIntentId", paymentIntentId);
        cmd.Parameters.AddWithValue("TenantId", tenantId);
        cmd.Parameters.AddWithValue("ClientId", clientId);
        cmd.Parameters.AddWithValue("Provider", provider);
        cmd.Parameters.AddWithValue("StoreId", (object?)storeId ?? DBNull.Value);
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
            reader.GetString(7),
            reader.GetDecimal(8),
            reader.GetString(9),
            MapStatus(reader.GetString(10)),
            reader.GetString(11),
            reader.GetFieldValue<DateTimeOffset>(12));
    }

    public async Task UpdateStatusByIdAsync(
        Guid id,
        Guid tenantId,
        Guid storeId,
        string clientId,
        string provider,
        string status,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE "PaymentTransactions"
            SET "Status" = @Status
            WHERE "Id" = @Id
              AND "TenantId" = @TenantId
              AND "StoreId" = @StoreId
              AND "ClientId" = @ClientId
              AND "Provider" = @Provider
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("Id", id);
        cmd.Parameters.AddWithValue("TenantId", tenantId);
        cmd.Parameters.AddWithValue("StoreId", storeId);
        cmd.Parameters.AddWithValue("ClientId", clientId);
        cmd.Parameters.AddWithValue("Provider", provider);
        cmd.Parameters.AddWithValue("Status", status);
        var n = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (n != 1)
            throw new InvalidOperationException($"PaymentTransactions update expected1 row, got {n} for id {id}.");
    }

    public async Task<bool> TryRecordWebhookDeliveryAsync(
        string provider,
        string eventId,
        string signatureDigest,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO "PaymentWebhookDeliveries" ("Provider", "EventId", "SignatureDigest", "ReceivedAt")
            VALUES (@Provider, @EventId, @SignatureDigest, @ReceivedAt)
            ON CONFLICT ("Provider", "EventId") DO NOTHING
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("Provider", provider);
        cmd.Parameters.AddWithValue("EventId", eventId);
        cmd.Parameters.AddWithValue("SignatureDigest", signatureDigest);
        cmd.Parameters.AddWithValue("ReceivedAt", receivedAt);
        var n = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return n == 1;
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
