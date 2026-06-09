using FluentAssertions;
using dCMS.Billing.Domain;

namespace dCMS.Tests.Unit.Infrastructure;

public sealed class ObservabilityBaselineAuditTests
{
    private static string BackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "dCMS.Infrastructure");
            if (Directory.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate backend source root from test output directory.");
    }

    public static TheoryData<string, string> HttpHosts => new()
    {
        { "dCMS.Gateway/Program.cs", "gateway" },
        { "dCMS.Catalog.Api/Program.cs", "catalog-api" },
        { "dCMS.Inventory.Api/Program.cs", "inventory-api" },
        { "dCMS.Order.Api/Program.cs", "order-api" },
        { "dCMS.Payment.Api/Program.cs", "payment-api" },
        { "dCMS.Fulfillment.Api/Program.cs", "fulfillment-api" },
        { "dCMS.Promotions.Api/Program.cs", "promotions-api" },
        { "dCMS.Identity.Api/Program.cs", "identity-api" },
        { "dCMS.Approval.Api/Program.cs", "approval-api" },
        { "dCMS.Reports.Api/Program.cs", "reports-api" },
        { "dCMS.Notification.Api/Program.cs", "notification-api" },
        { "dCMS.Loyalty.Api/Program.cs", "loyalty-api" },
        { "dCMS.Voucher.Api/Program.cs", "voucher-api" },
    };

    [Theory]
    [MemberData(nameof(HttpHosts))]
    public void Http_hosts_wire_correlation_and_request_observability(string relativePath, string serviceName)
    {
        var content = File.ReadAllText(Path.Combine(BackendSrcRoot(), relativePath));

        content.Should().Contain("UseDcmsCorrelationId()");
        content.Should().Contain($"UseDcmsRequestObservability(\"{serviceName}\")");
    }

    [Fact]
    public void Worker_paths_emit_operation_metrics_with_correlation_and_tenant_labels()
    {
        var root = BackendSrcRoot();
        var importConsumer = File.ReadAllText(Path.Combine(root, "dCMS.Catalog.Worker/Imports/ImportJobConsumer.cs"));
        var relay = File.ReadAllText(Path.Combine(root, "dCMS.Catalog.Worker/Workers/CatalogOutboxRelayHostedService.cs"));

        importConsumer.Should().Contain("DcmsObservabilityMetrics.ObserveWorkerOperation");
        importConsumer.Should().Contain("CorrelationId");
        importConsumer.Should().Contain("TenantId");
        relay.Should().Contain("DcmsObservabilityMetrics.ObserveWorkerOperation");
    }

    [Fact]
    public void Notification_worker_consumers_emit_operation_metrics()
    {
        var root = BackendSrcRoot();
        var emailConsumer = File.ReadAllText(Path.Combine(root, "dCMS.Notification.Worker/Consumers/EmailQueuedConsumer.cs"));
        var userNotifConsumer = File.ReadAllText(Path.Combine(root, "dCMS.Notification.Worker/Consumers/UserNotificationCreatedConsumer.cs"));

        emailConsumer.Should().Contain("DcmsObservabilityMetrics.ObserveWorkerOperation");
        emailConsumer.Should().Contain("correlationId");
        userNotifConsumer.Should().Contain("DcmsObservabilityMetrics.ObserveWorkerOperation");
        userNotifConsumer.Should().Contain("correlationId");
    }

    [Fact]
    public void Gateway_auth_middleware_sets_failure_reason_on_unauthorized()
    {
        var root = BackendSrcRoot();
        var middleware = File.ReadAllText(Path.Combine(root, "dCMS.Gateway/GatewayAuthMiddleware.cs"));

        middleware.Should().Contain("SetDcmsFailureReason");
        middleware.Should().Contain("unauthorized");
    }

    [Fact]
    public void Gateway_entitlement_middleware_sets_failure_reason_on_denial()
    {
        var root = BackendSrcRoot();
        var middleware = File.ReadAllText(Path.Combine(root, "dCMS.Gateway/GatewayTenantEntitlementMiddleware.cs"));

        middleware.Should().Contain("SetDcmsFailureReason");
        middleware.Should().Contain(nameof(EntitlementErrorCodes.EntitlementUnavailable));
    }

    [Fact]
    public void SpawnTenant_uses_structured_logging_not_console()
    {
        var root = BackendSrcRoot();
        var program = File.ReadAllText(Path.Combine(root, "tools/SpawnTenant/Program.cs"));

        program.Should().Contain("ILogger");
        program.Should().Contain("LogInformation");
        program.Should().NotContain("Console.WriteLine");
        program.Should().NotContain("Console.Error.WriteLine");
    }
}
