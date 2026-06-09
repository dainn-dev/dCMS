using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dapper;
using dCMS.Payment.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace dCMS.Payment.Tests.Integration;

[Collection("PaymentPostgres")]
public sealed class PaymentInternalApiIntegrationTests(PaymentPostgresFixture pg)
{
    private PaymentInternalApiFactory? _factory;

    private void Skip()
    {
        Xunit.Skip.IfNot(pg.IsReady, "Docker / Testcontainers not available.");
        _factory ??= new PaymentInternalApiFactory(pg.ConnectionString);
    }

    private HttpClient Client()
    {
        Skip();
        return _factory!.CreateClient();
    }

    private static void WithApiKey(HttpClient client, string? key = null)
    {
        client.DefaultRequestHeaders.Remove("X-Internal-Api-Key");
        if (key is not null)
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", key);
    }

    [SkippableFact]
    public async Task CreateIntent_without_api_key_returns_401()
    {
        using var client = Client();
        WithApiKey(client, key: null);

        var body = JsonSerializer.Serialize(new
        {
            orderId = Guid.NewGuid().ToString("D"),
            tenantId = PaymentTestSeeds.TenantAStr,
            storeId = PaymentTestSeeds.StoreA1Str,
            customerId = "cust-1",
            amount = 10m,
            currency = "USD",
        });

        var response = await client.PostAsync(
            "/internal/payment/create-intent",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNAUTHORIZED", json);
    }

    [SkippableFact]
    public async Task CreateIntent_wrong_api_key_returns_401()
    {
        using var client = Client();
        WithApiKey(client, "wrong-key");

        var body = JsonSerializer.Serialize(new
        {
            orderId = Guid.NewGuid().ToString("D"),
            tenantId = PaymentTestSeeds.TenantAStr,
            storeId = PaymentTestSeeds.StoreA1Str,
            customerId = "cust-1",
            amount = 10m,
            currency = "USD",
        });

        var response = await client.PostAsync(
            "/internal/payment/create-intent",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task CreateIntent_valid_key_returns_200_with_payment_intent_id()
    {
        using var client = Client();
        WithApiKey(client, PaymentTestSeeds.InternalApiKey);

        var body = JsonSerializer.Serialize(new
        {
            orderId = Guid.NewGuid().ToString("D"),
            tenantId = PaymentTestSeeds.TenantAStr,
            storeId = PaymentTestSeeds.StoreA1Str,
            customerId = "cust-1",
            amount = 10m,
            currency = "USD",
        });

        var response = await client.PostAsync(
            "/internal/payment/create-intent",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("paymentIntentId", json);
    }

    [SkippableFact]
    public async Task Capture_wrong_tenant_returns_404()
    {
        const string intentId = "pi_cap_wrong_tenant";
        await pg.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        using var client = Client();
        WithApiKey(client, PaymentTestSeeds.InternalApiKey);

        var body = JsonSerializer.Serialize(new { tenantId = PaymentTestSeeds.TenantBStr, storeId = PaymentTestSeeds.StoreA1Str });
        var response = await client.PostAsync(
            $"/internal/payment/{intentId}/capture",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("PAYMENT_NOT_FOUND", json);
    }

    [SkippableFact]
    public async Task Capture_wrong_store_returns_404()
    {
        const string intentId = "pi_cap_wrong_store";
        await pg.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId));

        using var client = Client();
        WithApiKey(client, PaymentTestSeeds.InternalApiKey);

        var body = JsonSerializer.Serialize(new { tenantId = PaymentTestSeeds.TenantAStr, storeId = PaymentTestSeeds.StoreB1Str });
        var response = await client.PostAsync(
            $"/internal/payment/{intentId}/capture",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Capture_wrong_client_config_returns_404()
    {
        const string intentId = "pi_cap_wrong_client";
        await pg.SeedTransactionAsync(PaymentPostgresFixture.SampleInsert(intentId, clientId: PaymentTestSeeds.OtherClientId));

        using var client = Client();
        WithApiKey(client, PaymentTestSeeds.InternalApiKey);

        var body = JsonSerializer.Serialize(new { tenantId = PaymentTestSeeds.TenantAStr, storeId = PaymentTestSeeds.StoreA1Str });
        var response = await client.PostAsync(
            $"/internal/payment/{intentId}/capture",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Capture_order_mismatch_returns_403()
    {
        const string intentId = "pi_cap_order_mismatch";
        var insert = PaymentPostgresFixture.SampleInsert(intentId);
        await pg.SeedTransactionAsync(insert);

        using var client = Client();
        WithApiKey(client, PaymentTestSeeds.InternalApiKey);

        var body = JsonSerializer.Serialize(new
        {
            tenantId = PaymentTestSeeds.TenantAStr,
            storeId = PaymentTestSeeds.StoreA1Str,
            orderId = Guid.NewGuid(),
        });
        var response = await client.PostAsync(
            $"/internal/payment/{intentId}/capture",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("ORDER_MISMATCH", json);
    }

    [SkippableFact]
    public async Task Capture_valid_scope_returns_200_and_updates_status()
    {
        const string intentId = "pi_cap_ok";
        var insert = PaymentPostgresFixture.SampleInsert(intentId);
        await pg.SeedTransactionAsync(insert);

        using var client = Client();
        WithApiKey(client, PaymentTestSeeds.InternalApiKey);

        var body = JsonSerializer.Serialize(new
        {
            tenantId = PaymentTestSeeds.TenantAStr,
            storeId = PaymentTestSeeds.StoreA1Str,
            orderId = insert.OrderId,
            amount = insert.Amount,
            currency = insert.Currency,
        });
        var response = await client.PostAsync(
            $"/internal/payment/{intentId}/capture",
            new StringContent(body, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();

        await using var conn = new NpgsqlConnection(pg.ConnectionString);
        var status = await conn.ExecuteScalarAsync<string>(
            """
            SELECT "Status" FROM "PaymentTransactions"
            WHERE "PaymentIntentId" = @IntentId AND "TenantId" = @TenantId
            """,
            new { IntentId = intentId, TenantId = PaymentTestSeeds.TenantA });

        Assert.Equal("completed", status);
    }
}

public sealed class PaymentInternalApiFactory : WebApplicationFactory<PaymentApiProgram>
{
    private readonly string _connectionString;

    public PaymentInternalApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Payment", _connectionString);
        builder.UseSetting("Payment:InternalApiKey", PaymentTestSeeds.InternalApiKey);
        builder.UseSetting("Dcms:Client:Id", PaymentTestSeeds.ClientId);
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.ConfigureServices(services =>
        {
            PaymentTestHost.RemoveHostedServices(services);
            PaymentTestHost.RemoveMassTransit(services);
        });
    }
}

internal static class PaymentTestHost
{
    internal static void RemoveHostedServices(IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList())
            services.Remove(d);
    }

    internal static void RemoveMassTransit(IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType.FullName?.StartsWith("MassTransit", StringComparison.Ordinal) == true).ToList())
            services.Remove(d);
    }
}
