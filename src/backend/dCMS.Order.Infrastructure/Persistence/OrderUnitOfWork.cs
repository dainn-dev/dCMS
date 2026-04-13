using System.Text.Json;
using Dapper;
using Npgsql;

namespace dCMS.Order.Infrastructure.Persistence;

/// <summary>Single Postgres transaction for order rows + outbox (DAI-313).</summary>
public sealed class OrderUnitOfWork : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _connectionString;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public OrderUnitOfWork(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
            throw new InvalidOperationException("Transaction already started.");

        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveOrderAsync(Core.Domain.Order order, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();

        if (!Guid.TryParse(order.Id, out var orderGuid))
            throw new ArgumentException("Order id must be a UUID string.", nameof(order));

        var now = DateTimeOffset.UtcNow;
        var shipJson = JsonSerializer.Serialize(order.ShippingAddress, Json);
        var statusName = order.Status.ToString();
        var subTotal = order.Total.Amount;
        const decimal taxTotal = 0m;

        if (string.IsNullOrWhiteSpace(order.PaymentIntentId))
            throw new InvalidOperationException("PaymentIntentId must be set before persisting an order (DAI-315).");

        const string insertOrder = """
            INSERT INTO "Orders" (
                "Id", "TenantId", "StoreId", "CustomerId", "Status", "Currency", "SubTotal", "TaxTotal", "Total",
                "PaymentIntentId", "IdempotencyKey", "CreatedAt", "UpdatedAt", "ShippingAddress")
            VALUES (
                @Id, @TenantId, @StoreId, @CustomerId, @Status, @Currency, @SubTotal, @TaxTotal, @Total,
                @PaymentIntentId, @IdempotencyKey, @Now, @Now, @ShippingAddress::jsonb)
            """;

        await conn.ExecuteAsync(new CommandDefinition(insertOrder,
            new
            {
                Id = orderGuid,
                order.TenantId,
                order.StoreId,
                order.CustomerId,
                Status = statusName,
                Currency = order.Total.Currency,
                SubTotal = subTotal,
                TaxTotal = taxTotal,
                Total = order.Total.Amount,
                PaymentIntentId = order.PaymentIntentId,
                IdempotencyKey = idempotencyKey,
                Now = now,
                ShippingAddress = shipJson,
            },
            tx,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        const string insertItem = """
            INSERT INTO "OrderItems" (
                "Id", "OrderId", "VariantId", "ProductId", "Quantity", "UnitPrice", "LineTotal", "ProductName", "VariantSnapshot")
            VALUES (
                @Id, @OrderId, @VariantId, @ProductId, @Quantity, @UnitPrice, @LineTotal, @ProductName::jsonb, @VariantSnapshot::jsonb)
            """;

        foreach (var line in order.Items)
        {
            var productNameJson = JsonSerializer.Serialize(line.ProductNameSnapshot, Json);
            var variantJson = string.IsNullOrWhiteSpace(line.VariantSnapshotJson) ? "{}" : line.VariantSnapshotJson;
            await conn.ExecuteAsync(new CommandDefinition(insertItem,
                new
                {
                    line.Id,
                    OrderId = orderGuid,
                    line.VariantId,
                    line.ProductId,
                    line.Quantity,
                    UnitPrice = line.UnitPrice.Amount,
                    LineTotal = line.LineTotal().Amount,
                    ProductName = productNameJson,
                    VariantSnapshot = variantJson,
                },
                tx,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    public async Task AppendOutboxAsync(IReadOnlyList<Core.Domain.IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        var (conn, tx) = Require();
        const string outSql = """
            INSERT INTO "OutboxEvents" ("EventType", "Payload", "CreatedAt", "RetryCount")
            VALUES (@EventType, @Payload, @CreatedAt, 0)
            """;

        var now = DateTimeOffset.UtcNow;
        foreach (var ev in events)
        {
            var (eventType, payload) = OrderOutboxSerializer.ToOutboxRow(ev);
            await conn.ExecuteAsync(new CommandDefinition(outSql,
                    new { EventType = eventType, Payload = payload, CreatedAt = now },
                    tx,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction.");
        return _transaction.CommitAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        _transaction is null ? Task.CompletedTask : _transaction.RollbackAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync().ConfigureAwait(false);

        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);

        _transaction = null;
        _connection = null;
    }

    private (NpgsqlConnection Connection, NpgsqlTransaction Transaction) Require()
    {
        if (_connection is null || _transaction is null)
            throw new InvalidOperationException("Call BeginAsync before persistence operations.");

        return (_connection, _transaction);
    }
}
