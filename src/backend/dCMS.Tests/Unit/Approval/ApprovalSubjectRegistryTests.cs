using System.Text.Json;
using dCMS.Approval.Api.Routes;
using dCMS.Core.Approvals;
using FluentAssertions;
using Xunit;

namespace dCMS.Tests.Unit.Approval;

public sealed class ApprovalSubjectRegistryTests
{
    private sealed class FakeSubject(string entityType) : IApprovalSubject
    {
        public string EntityType { get; } = entityType;
        public Task<string?> ValidateAsync(string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, CancellationToken ct)
            => Task.FromResult<string?>(null);
        public Task ApplyAsync(string tenantId, string entityId, ApprovalAction action, JsonDocument payloadSnapshot, string actedByUserId, CancellationToken ct)
            => Task.CompletedTask;
    }

    [Fact]
    public void TryGet_returns_subject_by_exact_entity_type()
    {
        var registry = new ApprovalSubjectRegistry(new IApprovalSubject[]
        {
            new FakeSubject("Product"), new FakeSubject("Campaign"), new FakeSubject("PromoCode"),
        });

        registry.TryGet("Product", out var s).Should().BeTrue();
        s.EntityType.Should().Be("Product");
    }

    [Fact]
    public void TryGet_is_case_insensitive()
    {
        var registry = new ApprovalSubjectRegistry(new IApprovalSubject[] { new FakeSubject("Product") });

        registry.TryGet("product", out var s).Should().BeTrue();
        s.EntityType.Should().Be("Product");

        registry.TryGet("PRODUCT", out var s2).Should().BeTrue();
        s2.EntityType.Should().Be("Product");
    }

    [Fact]
    public void TryGet_returns_false_for_unknown_entity_type()
    {
        var registry = new ApprovalSubjectRegistry(new IApprovalSubject[] { new FakeSubject("Product") });

        registry.TryGet("Order", out var s).Should().BeFalse();
        s.Should().BeNull();
    }

    [Fact]
    public void TryGet_trims_whitespace()
    {
        var registry = new ApprovalSubjectRegistry(new IApprovalSubject[] { new FakeSubject("Product") });

        registry.TryGet("  Product  ", out var s).Should().BeTrue();
        s.EntityType.Should().Be("Product");
    }
}
