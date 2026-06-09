using dCMS.Infrastructure.Provisioning;
using dCMS.Provisioning.Domain;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace dCMS.Tests.Unit.Provisioning;

public sealed class SqlTenantProvisioningRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string _connectionString = "";

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dcms_provisioning_test")
            .WithUsername("dcms")
            .WithPassword("test")
            .Build();
        await _postgres.StartAsync().ConfigureAwait(false);
        _connectionString = _postgres.GetConnectionString();

        var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "043_CreateTenantProvisioning.sql");
        if (!File.Exists(migrationPath))
            throw new FileNotFoundException("Migration 043 not found in test output.", migrationPath);

        var sql = await File.ReadAllTextAsync(migrationPath).ConfigureAwait(false);
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task CreateRequestedAsync_persists_record_and_onboarding_checklist()
    {
        var repo = new SqlTenantProvisioningRepository(_connectionString);
        await repo.CreateRequestedAsync("t-prov-a", "prov-a", "starter", "test", CancellationToken.None)
            .ConfigureAwait(false);
        await repo.SeedOnboardingChecklistAsync("t-prov-a", CancellationToken.None).ConfigureAwait(false);

        var record = await repo.GetByTenantIdAsync("t-prov-a", CancellationToken.None).ConfigureAwait(false);
        record.Should().NotBeNull();
        record!.Status.Should().Be(ProvisioningStatus.Requested);
        record.PlanTier.Should().Be("starter");

        var onboarding = await repo.ListOnboardingAsync("t-prov-a", CancellationToken.None).ConfigureAwait(false);
        onboarding.Should().NotBeEmpty();
        onboarding.Count(i => i.IsRequired).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TransitionStatusAsync_and_audit_append_work()
    {
        var repo = new SqlTenantProvisioningRepository(_connectionString);
        await repo.CreateRequestedAsync("t-prov-b", "prov-b", "starter", "ops", CancellationToken.None)
            .ConfigureAwait(false);

        var runId = Guid.NewGuid();
        var ok = await repo.TransitionStatusAsync(
            "t-prov-b", ProvisioningStatus.Requested, ProvisioningStatus.Provisioning,
            runId, null, "test", CancellationToken.None).ConfigureAwait(false);
        ok.Should().BeTrue();

        var record = await repo.GetByTenantIdAsync("t-prov-b", CancellationToken.None).ConfigureAwait(false);
        record!.Status.Should().Be(ProvisioningStatus.Provisioning);
        record.CurrentRunId.Should().Be(runId);

        await repo.AppendAuditAsync("t-prov-b", runId, "step_succeeded", null, null, "test",
            """{"step":"validate_request"}""", CancellationToken.None).ConfigureAwait(false);
        var audit = await repo.ListAuditAsync("t-prov-b", 10, CancellationToken.None).ConfigureAwait(false);
        audit.Should().ContainSingle(a => a.Operation == "step_succeeded");
    }
}
