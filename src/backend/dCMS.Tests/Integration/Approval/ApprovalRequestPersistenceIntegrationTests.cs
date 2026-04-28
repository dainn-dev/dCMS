using dCMS.Infrastructure.Approvals;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Integration.Approval;

[Collection("catalog-variant-pg")]
public sealed class ApprovalRequestPersistenceIntegrationTests(dCMS.Tests.Integration.Catalog.CatalogVariantPgFixture fixture)
{
    private readonly dCMS.Tests.Integration.Catalog.CatalogVariantPgFixture _fixture = fixture;

    [Fact]
    public async Task CreatePendingAsync_then_ListAsync_returns_row()
    {
        var p = new SqlApprovalRequestPersistence(_fixture.ConnectionString);
        var now = DateTimeOffset.Parse("2026-06-01T00:00:00Z");

        var id = await p.CreatePendingAsync(
            tenantId: "t1",
            entityType: "Product",
            entityId: "p1",
            requestedByUserId: "u1",
            currentApproverUserId: "approver-1",
            payloadSnapshotJson: """{"name":"Demo"}""",
            now: now,
            ct: CancellationToken.None);

        var (items, total) = await p.ListAsync("t1", entityType: "Product", state: "PendingApproval", assignedTo: "approver-1",
            page: 1, pageSize: 50, ct: CancellationToken.None);

        total.Should().BeGreaterThanOrEqualTo(1);
        items.Should().ContainSingle(x => x.Id == id);
    }

    [Fact]
    public async Task TryTransitionAsync_moves_pending_to_approved_and_finalizes()
    {
        var p = new SqlApprovalRequestPersistence(_fixture.ConnectionString);
        var now = DateTimeOffset.Parse("2026-06-02T00:00:00Z");

        var id = await p.CreatePendingAsync(
            tenantId: "t1",
            entityType: "Campaign",
            entityId: "c1",
            requestedByUserId: "u1",
            currentApproverUserId: null,
            payloadSnapshotJson: """{"code":"C1"}""",
            now: now,
            ct: CancellationToken.None);

        var ok = await p.TryTransitionAsync(
            tenantId: "t1",
            id: id,
            expectedState: "PendingApproval",
            nextState: "Approved",
            actedByUserId: "admin",
            notes: "ok",
            now: now.AddMinutes(1),
            finalize: true,
            ct: CancellationToken.None);

        ok.Should().BeTrue();

        var row = await p.GetByIdAsync("t1", id, CancellationToken.None);
        row.Should().NotBeNull();
        row!.State.Should().Be("Approved");
        row.FinalizedAt.Should().NotBeNull();
        row.HistoryJson.Should().Contain("Approved");
    }
}

