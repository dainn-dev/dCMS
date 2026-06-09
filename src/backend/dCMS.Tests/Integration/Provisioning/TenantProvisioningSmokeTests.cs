using dCMS.Infrastructure.Provisioning;
using dCMS.Provisioning.Domain;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Integration.Provisioning;

/// <summary>
/// Provisioning lifecycle smoke: persisted state in catalog PG + tenant code isolation.
/// Cross-tenant JWT isolation is covered by SaasCore RBAC integration suites (see runbook).
/// </summary>
public sealed class TenantProvisioningSmokeTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string _catalogCs = "";

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dcms_prov_smoke")
            .WithUsername("dcms")
            .WithPassword("test")
            .Build();
        await _postgres.StartAsync();
        _catalogCs = _postgres.GetConnectionString();

        var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "043_CreateTenantProvisioning.sql");
        var sql = await File.ReadAllTextAsync(migrationPath);
        await using var conn = new NpgsqlConnection(_catalogCs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Two_active_tenants_have_isolated_provisioning_records()
    {
        const string tenantA = "t-prov-a";
        const string tenantB = "t-prov-b";
        var repo = new SqlTenantProvisioningRepository(_catalogCs);

        await repo.CreateRequestedAsync(tenantA, "prov-a", "starter", "smoke", CancellationToken.None);
        await repo.CreateRequestedAsync(tenantB, "prov-b", "starter", "smoke", CancellationToken.None);
        await repo.SeedOnboardingChecklistAsync(tenantA, CancellationToken.None);
        await repo.SeedOnboardingChecklistAsync(tenantB, CancellationToken.None);

        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        (await repo.TransitionStatusAsync(tenantA, ProvisioningStatus.Requested, ProvisioningStatus.Provisioning, runA, null, "smoke", CancellationToken.None)).Should().BeTrue();
        (await repo.TransitionStatusAsync(tenantB, ProvisioningStatus.Requested, ProvisioningStatus.Provisioning, runB, null, "smoke", CancellationToken.None)).Should().BeTrue();
        (await repo.TransitionStatusAsync(tenantA, ProvisioningStatus.Provisioning, ProvisioningStatus.Active, runA, null, "smoke", CancellationToken.None)).Should().BeTrue();
        (await repo.TransitionStatusAsync(tenantB, ProvisioningStatus.Provisioning, ProvisioningStatus.Active, runB, null, "smoke", CancellationToken.None)).Should().BeTrue();

        var a = await repo.GetByTenantIdAsync(tenantA, CancellationToken.None);
        var b = await repo.GetByTenantIdAsync(tenantB, CancellationToken.None);
        a!.Status.Should().Be(ProvisioningStatus.Active);
        b!.Status.Should().Be(ProvisioningStatus.Active);
        a.TenantId.Should().NotBe(b.TenantId);

        var byCodeA = await repo.GetByTenantCodeAsync("prov-a", CancellationToken.None);
        var byCodeB = await repo.GetByTenantCodeAsync("prov-b", CancellationToken.None);
        byCodeA!.TenantId.Should().Be(tenantA);
        byCodeB!.TenantId.Should().Be(tenantB);

        var onboardingA = await repo.ListOnboardingAsync(tenantA, CancellationToken.None);
        onboardingA.Should().OnlyContain(i => i.TenantId == tenantA);
    }
}
