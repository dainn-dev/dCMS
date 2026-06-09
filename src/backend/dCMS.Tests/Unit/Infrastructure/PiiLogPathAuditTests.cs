using System.Text.RegularExpressions;
using FluentAssertions;

namespace dCMS.Tests.Unit.Infrastructure;

/// <summary>
/// DAI-25-P0-04b: static audit of production log call sites — forbids logging secrets/PII in gateway, payment webhooks, and provisioning.
/// Complements <see cref="PiiMaskingAuditTests"/> (structured field hygiene) with real source-path guards.
/// </summary>
public sealed class PiiLogPathAuditTests
{
    private static string BackendSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "dCMS.Gateway");
            if (Directory.Exists(candidate))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate backend source root from test output directory.");
    }

    private static IEnumerable<string> ExtractLogStatements(string content)
    {
        foreach (Match match in Regex.Matches(content, @"\b(?:logger|_log)\.Log(?:Trace|Debug|Information|Warning|Error|Critical)\s*\([^;]+;", RegexOptions.Singleline))
            yield return match.Value;
    }

    private static void AssertLogStatementsClean(string relativePath, string[] forbiddenInLogs)
    {
        var fullPath = Path.Combine(BackendSrcRoot(), relativePath);
        File.Exists(fullPath).Should().BeTrue($"expected file at {fullPath}");

        var logStatements = ExtractLogStatements(File.ReadAllText(fullPath)).ToList();
        logStatements.Should().NotBeEmpty($"{relativePath} should contain structured log calls to audit");

        foreach (var statement in logStatements)
        {
            foreach (var pattern in forbiddenInLogs)
            {
                statement.Should().NotContain(pattern,
                    because: $"log statement in {relativePath} must not reference {pattern}");
            }
        }
    }

    [Fact]
    public void Gateway_auth_log_statements_do_not_emit_secrets()
    {
        AssertLogStatementsClean(
            "dCMS.Gateway/GatewayAuthMiddleware.cs",
            ["Authorization", "Bearer ", "WriteToken", "password", "Password", "token,"]);
    }

    [Fact]
    public void Gateway_entitlement_log_statements_do_not_emit_secrets()
    {
        AssertLogStatementsClean(
            "dCMS.Gateway/GatewayTenantEntitlementMiddleware.cs",
            ["Authorization", "Bearer ", "password", "Password"]);
    }

    [Fact]
    public void Payment_webhook_log_statements_do_not_emit_signature_or_raw_body()
    {
        AssertLogStatementsClean(
            "dCMS.Payment.Api/Routes/PaymentWebhookRoutes.cs",
            ["X-Payment-Signature", "signature,", "rawJson", "bodyBytes", "password"]);
    }

    [Fact]
    public void Provisioning_orchestrator_log_statements_do_not_emit_credentials()
    {
        AssertLogStatementsClean(
            "tools/SpawnTenant/ProvisioningOrchestrator.cs",
            ["AdminPassword", "ConnectionString", "SaConn", "password", "Password"]);
    }

    [Fact]
    public void SpawnTenant_cli_does_not_use_console_for_output()
    {
        var root = BackendSrcRoot();
        var cli = File.ReadAllText(Path.Combine(root, "tools/SpawnTenant/SpawnTenantCli.cs"));

        cli.Should().NotContain("Console.WriteLine");
        cli.Should().NotContain("Console.Error.WriteLine");
    }

    [Fact]
    public void Gateway_auth_logs_use_failure_reason_not_token_content()
    {
        var middleware = File.ReadAllText(Path.Combine(BackendSrcRoot(), "dCMS.Gateway/GatewayAuthMiddleware.cs"));

        middleware.Should().Contain("SetDcmsFailureReason");
        middleware.Should().Contain("unauthorized");
    }

    [Fact]
    public void Payment_webhook_failure_logs_provider_and_reason()
    {
        var routes = File.ReadAllText(Path.Combine(BackendSrcRoot(), "dCMS.Payment.Api/Routes/PaymentWebhookRoutes.cs"));

        routes.Should().Contain("MarkWebhookFailure");
        routes.Should().Contain("FailureReason");
    }
}
