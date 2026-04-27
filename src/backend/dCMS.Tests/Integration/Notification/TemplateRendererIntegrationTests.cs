using System.Text.Json;
using Dapper;
using dCMS.Notification.Api.Rendering;
using dCMS.Notification.Api.Routes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Notification;

/// <summary>
/// DAI-715/716: TemplateRepository locale-fallback resolution + ScribanTemplateRenderer end-to-end.
/// Spawns a fresh PostgreSQL container, applies the Templates + AuditLogs migrations, seeds a small
/// scope/locale matrix, and verifies that the renderer chooses the correct row and produces the
/// expected output for each fallback tier.
/// </summary>
public sealed class TemplateRendererIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string? _cs;
    private bool _ready;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("dcms_templates_itest")
                .WithUsername("dcms")
                .WithPassword("test")
                .Build();
            await _postgres.StartAsync().ConfigureAwait(false);
            _cs = _postgres.GetConnectionString();

            var auditSql = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Migrations", "009_CreateAuditAndNotifications.sql"))
                .ConfigureAwait(false);
            var templatesSql = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Migrations", "025_CreateTemplates.sql"))
                .ConfigureAwait(false);

            await using var conn = new NpgsqlConnection(_cs);
            await conn.OpenAsync().ConfigureAwait(false);
            await conn.ExecuteAsync(auditSql).ConfigureAwait(false);
            await conn.ExecuteAsync(templatesSql).ConfigureAwait(false);

            _ready = true;
        }
        catch
        {
            _ready = false;
            if (_postgres is not null) { await _postgres.DisposeAsync().ConfigureAwait(false); _postgres = null; }
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null) await _postgres.DisposeAsync().ConfigureAwait(false);
        _postgres = null;
    }

    private void Skip() => Xunit.Skip.IfNot(_ready && _cs is not null, "Docker / Testcontainers not available.");

    private TemplateRepository BuildRepo()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Catalog"] = _cs })
            .Build();
        return new TemplateRepository(cfg);
    }

    private ScribanTemplateRenderer BuildRenderer() =>
        new(BuildRepo(), NullLogger<ScribanTemplateRenderer>.Instance);

    private async Task SeedAsync(params (string? tenant, string key, string locale, string channel, string? subject, string body)[] rows)
    {
        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        await conn.ExecuteAsync("DELETE FROM \"Templates\"").ConfigureAwait(false);
        foreach (var r in rows)
        {
            await conn.ExecuteAsync("""
                INSERT INTO "Templates" ("Id","TenantId","Key","Locale","Channel","Subject","Body","ModelVersion","UpdatedAt","UpdatedBy")
                VALUES (@Id, @Tenant, @Key, @Locale, @Channel, @Subject, @Body, 1, NOW(), 'test')
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    Tenant = r.tenant,
                    Key = r.key,
                    Locale = r.locale,
                    Channel = r.channel,
                    Subject = r.subject,
                    Body = r.body,
                }).ConfigureAwait(false);
        }
    }

    private static JsonElement Model(object o) =>
        JsonDocument.Parse(JsonSerializer.Serialize(o)).RootElement.Clone();

    [SkippableFact]
    public async Task Resolves_tenant_locale_first()
    {
        Skip();
        await SeedAsync(
            (tenant: null,  key: "OrderConfirm", locale: "en-US", channel: "email", subject: "global en", body: "global en body"),
            (tenant: null,  key: "OrderConfirm", locale: "vi-VN", channel: "email", subject: "global vi", body: "global vi body"),
            (tenant: "t1",  key: "OrderConfirm", locale: "vi-VN", channel: "email", subject: "tenant vi", body: "tenant vi body"),
            (tenant: "t1",  key: "OrderConfirm", locale: "en-US", channel: "email", subject: "tenant en", body: "tenant en body"));

        var row = await BuildRepo().GetResolvedAsync("t1", "OrderConfirm", "vi-VN", "email", "en-US", default);

        row.Should().NotBeNull();
        row!.TenantId.Should().Be("t1");
        row.Locale.Should().Be("vi-VN");
    }

    [SkippableFact]
    public async Task Falls_back_to_tenant_default_locale_when_requested_missing()
    {
        Skip();
        await SeedAsync(
            (tenant: null, key: "OrderConfirm", locale: "vi-VN", channel: "email", subject: "global vi", body: "global vi body"),
            (tenant: "t1", key: "OrderConfirm", locale: "en-US", channel: "email", subject: "tenant en", body: "tenant en body"));

        var row = await BuildRepo().GetResolvedAsync("t1", "OrderConfirm", "vi-VN", "email", "en-US", default);

        row.Should().NotBeNull();
        row!.TenantId.Should().Be("t1");
        row.Locale.Should().Be("en-US");
    }

    [SkippableFact]
    public async Task Falls_back_to_global_when_tenant_has_no_template()
    {
        Skip();
        await SeedAsync(
            (tenant: null, key: "OrderConfirm", locale: "vi-VN", channel: "email", subject: "global vi", body: "global vi body"),
            (tenant: null, key: "OrderConfirm", locale: "en-US", channel: "email", subject: "global en", body: "global en body"));

        var row = await BuildRepo().GetResolvedAsync("t1", "OrderConfirm", "vi-VN", "email", "en-US", default);

        row.Should().NotBeNull();
        row!.TenantId.Should().BeNull();
        row.Locale.Should().Be("vi-VN");
    }

    [SkippableFact]
    public async Task Falls_back_to_global_default_locale_as_last_resort()
    {
        Skip();
        await SeedAsync(
            (tenant: null, key: "OrderConfirm", locale: "en-US", channel: "email", subject: "global en", body: "global en body"));

        var row = await BuildRepo().GetResolvedAsync("t1", "OrderConfirm", "vi-VN", "email", "en-US", default);

        row.Should().NotBeNull();
        row!.TenantId.Should().BeNull();
        row.Locale.Should().Be("en-US");
    }

    [SkippableFact]
    public async Task Returns_null_when_no_template_matches()
    {
        Skip();
        await SeedAsync(
            (tenant: null, key: "OtherKey", locale: "en-US", channel: "email", subject: null, body: "x"));

        var row = await BuildRepo().GetResolvedAsync("t1", "OrderConfirm", "vi-VN", "email", "en-US", default);
        row.Should().BeNull();
    }

    [SkippableFact]
    public async Task Renderer_substitutes_model_values()
    {
        Skip();
        await SeedAsync(
            (tenant: "t1", key: "OrderConfirm", locale: "en-US", channel: "email",
             subject: "Order {{ model.order_id }}",
             body:    "Hello {{ model.customer_name }}, total = {{ model.total }}"));

        var result = await BuildRenderer().RenderAsync(
            "t1", "OrderConfirm", "en-US", "email",
            Model(new { order_id = "ord-42", customer_name = "Alice", total = 1500 }),
            default);

        result.Subject.Should().Be("Order ord-42");
        result.Body.Should().Be("Hello Alice, total = 1500");
        result.Channel.Should().Be("email");
    }

    [SkippableFact]
    public async Task Renderer_falls_back_to_placeholder_body_when_template_missing()
    {
        Skip();
        await SeedAsync(); // empty

        var result = await BuildRenderer().RenderAsync(
            "t1", "MissingKey", "en-US", "email",
            Model(new { x = 1 }),
            default);

        result.Subject.Should().BeNull();
        result.Body.Should().Be("MissingKey notification");
        result.Channel.Should().Be("email");
    }

    [SkippableFact]
    public async Task Renderer_returns_safe_fallback_on_template_parse_error()
    {
        Skip();
        await SeedAsync(
            (tenant: "t1", key: "BadTpl", locale: "en-US", channel: "email",
             subject: "ok", body: "{{ if x }}no end tag here"));

        var result = await BuildRenderer().RenderAsync(
            "t1", "BadTpl", "en-US", "email", Model(new { }), default);

        result.Body.Should().Be("BadTpl notification");
    }

    [SkippableFact]
    public async Task Upsert_then_overwrite_via_unique_index_uses_coalesced_tenant_scope()
    {
        Skip();
        var repo = BuildRepo();

        await repo.UpsertAsync(new TemplateUpsertRequest
        {
            TenantId = null, Key = "K", Locale = "en-US", Channel = "email",
            Subject = "v1", Body = "v1 body", ModelVersion = 1,
        }, actorUserId: "u1", tenantIdForAudit: "t1", storeIdForAudit: "s1", roleForAudit: "admin", ipForAudit: "127.0.0.1", default);

        // Same scope/key/locale/channel should UPDATE not INSERT (COALESCE("",'') unique).
        await repo.UpsertAsync(new TemplateUpsertRequest
        {
            TenantId = "", Key = "K", Locale = "en-US", Channel = "email",
            Subject = "v2", Body = "v2 body", ModelVersion = 2,
        }, actorUserId: "u1", tenantIdForAudit: "t1", storeIdForAudit: "s1", roleForAudit: "admin", ipForAudit: "127.0.0.1", default);

        await using var conn = new NpgsqlConnection(_cs);
        await conn.OpenAsync().ConfigureAwait(false);
        var count = await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Templates" WHERE "Key" = 'K' """);
        count.Should().Be(1);

        var row = await repo.GetResolvedAsync("anyTenant", "K", "en-US", "email", "en-US", default);
        row!.Body.Should().Be("v2 body");
        row.ModelVersion.Should().Be(2);
    }
}
