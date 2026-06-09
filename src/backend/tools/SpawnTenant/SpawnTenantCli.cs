using dCMS.Infrastructure.Provisioning;
using dCMS.Provisioning.Domain;
using dCMS.Tools.SpawnTenant.Steps;
using Microsoft.Extensions.Logging;

namespace dCMS.Tools.SpawnTenant;

internal static class SpawnTenantCli
{
    public static async Task<int> RunAsync(string[] args, ILogger log, ILoggerFactory loggerFactory)
    {
        if (args.Length == 0)
        {
            PrintUsage(log);
            return 64;
        }

        var command = args[0].ToLowerInvariant();
        var argsMap = ParseArgs(args[1..]);

        return command switch
        {
            "spawn-tenant" or "provision" => await RunProvisionAsync(argsMap, log, loggerFactory),
            "status" => await RunStatusAsync(argsMap, log),
            "retry" => await RunRetryAsync(argsMap, log, loggerFactory),
            "rollback" => await RunRollbackAsync(argsMap, log, loggerFactory),
            "suspend" => await RunSuspendAsync(argsMap, log, loggerFactory),
            "reactivate" => await RunReactivateAsync(argsMap, log, loggerFactory),
            _ => UnknownCommand(log, command)
        };
    }

    private static int UnknownCommand(ILogger log, string command)
    {
        log.LogError("Unknown command '{Command}'", command);
        PrintUsage(log);
        return 64;
    }

    private static void PrintUsage(ILogger log)
    {
        log.LogInformation("""
            Usage:
              provision|spawn-tenant --tenant <id> --tenant-code <code> --sa-conn <cs> --catalog-conn <cs>
                  --umbraco-platform-conn <cs> --admin-email <email> --admin-password <pw>
                  [--plan starter|pro|enterprise] [--redis <cs>] [--domain <host>]
                  [--out infra/tenants/<tenant>.env] [--compose] [--health <url>]
              status --tenant <id> --catalog-conn <cs>
              retry --tenant <id> --catalog-conn <cs> [--sa-conn ...] [--umbraco-platform-conn ...] ...
              rollback --tenant <id> --catalog-conn <cs> [--force] [--sa-conn ...] ...
              suspend|reactivate --tenant <id> --catalog-conn <cs> [--umbraco-platform-conn <cs>]
            """);
    }

    private static async Task<int> RunProvisionAsync(
        Dictionary<string, string> map,
        ILogger log,
        ILoggerFactory loggerFactory)
    {
        var request = BuildRequest(map, requireAdmin: true);
        var orchestrator = CreateOrchestrator(request.CatalogConnectionString, loggerFactory);
        log.LogInformation("Starting provision tenant {TenantId}", request.TenantId);
        var result = await orchestrator.ProvisionAsync(request).ConfigureAwait(false);
        LogResult(log, result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> RunStatusAsync(Dictionary<string, string> map, ILogger log)
    {
        var tenantId = Required(map, "--tenant");
        var catalogConn = Required(map, "--catalog-conn");
        var repo = new SqlTenantProvisioningRepository(catalogConn);
        var record = await repo.GetByTenantIdAsync(tenantId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No provisioning record for tenant '{tenantId}'.");

        log.LogInformation(
            "tenant={TenantId} status={Status} plan={Plan} run={RunId} failures={Failures} message={Message}",
            record.TenantId,
            record.Status.ToApiString(),
            record.PlanTier,
            record.CurrentRunId,
            record.FailureCount,
            record.LastFailureMessage ?? "");

        if (record.CurrentRunId is { } runId)
        {
            var steps = await repo.GetStepsAsync(tenantId, runId).ConfigureAwait(false);
            foreach (var step in steps)
            {
                log.LogInformation(
                    "  step {Order} {Name} status={Status} attempts={Attempts} error={Error}",
                    step.StepOrder, step.StepName, step.Status, step.AttemptCount, step.ErrorMessage ?? "");
            }
        }

        return 0;
    }

    private static async Task<int> RunRetryAsync(
        Dictionary<string, string> map,
        ILogger log,
        ILoggerFactory loggerFactory)
    {
        var request = BuildRequest(map, requireAdmin: false);
        var orchestrator = CreateOrchestrator(request.CatalogConnectionString, loggerFactory);
        var result = await orchestrator.RetryAsync(request).ConfigureAwait(false);
        LogResult(log, result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> RunRollbackAsync(
        Dictionary<string, string> map,
        ILogger log,
        ILoggerFactory loggerFactory)
    {
        var request = BuildRequest(map, requireAdmin: false);
        var force = map.ContainsKey("--force");
        var orchestrator = CreateOrchestrator(request.CatalogConnectionString, loggerFactory);
        var result = await orchestrator.RollbackAsync(request, force).ConfigureAwait(false);
        LogResult(log, result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> RunSuspendAsync(
        Dictionary<string, string> map,
        ILogger log,
        ILoggerFactory loggerFactory)
    {
        var request = BuildRequest(map, requireAdmin: false);
        var orchestrator = CreateOrchestrator(request.CatalogConnectionString, loggerFactory);
        var result = await orchestrator.SuspendAsync(request).ConfigureAwait(false);
        LogResult(log, result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> RunReactivateAsync(
        Dictionary<string, string> map,
        ILogger log,
        ILoggerFactory loggerFactory)
    {
        var request = BuildRequest(map, requireAdmin: false);
        var orchestrator = CreateOrchestrator(request.CatalogConnectionString, loggerFactory);
        var result = await orchestrator.ReactivateAsync(request).ConfigureAwait(false);
        LogResult(log, result);
        return result.Success ? 0 : 1;
    }

    private static ProvisioningOrchestrator CreateOrchestrator(string catalogConn, ILoggerFactory loggerFactory)
    {
        var repo = new SqlTenantProvisioningRepository(catalogConn);
        IReadOnlyList<IProvisioningStep> steps =
        [
            new ValidateRequestStep(repo),
            new CreatePlatformTenantStep(),
            new CreateUmbracoDbStep(),
            new WriteEnvFileStep(),
            new VerifyDbConnectionStep(),
            new ComposeUpStep(loggerFactory.CreateLogger<ComposeUpStep>()),
            new HealthPollStep(loggerFactory.CreateLogger<HealthPollStep>()),
            new BindDomainStep(repo),
            new SeedDefaultStoreStep(),
            new MarkOnboardingPendingStep(repo),
        ];
        return new ProvisioningOrchestrator(repo, steps, loggerFactory.CreateLogger<ProvisioningOrchestrator>());
    }

    private static ProvisioningRequest BuildRequest(Dictionary<string, string> map, bool requireAdmin)
    {
        var tenant = Required(map, "--tenant");
        var tenantCode = Optional(map, "--tenant-code") ?? DeriveTenantCode(tenant);
        var saConn = Optional(map, "--sa-conn") ?? "";
        var catalogConn = Required(map, "--catalog-conn");
        var platformConn = Optional(map, "--umbraco-platform-conn") ?? "";
        var redis = Optional(map, "--redis") ?? "";
        var adminEmail = requireAdmin ? Required(map, "--admin-email") : Optional(map, "--admin-email") ?? "";
        var adminPassword = requireAdmin ? Required(map, "--admin-password") : Optional(map, "--admin-password") ?? "";
        var plan = Optional(map, "--plan") ?? "starter";
        var domain = Optional(map, "--domain");
        var outEnv = Optional(map, "--out") ?? Path.Combine("infra", "tenants", $"{tenant}.env");
        var compose = map.ContainsKey("--compose");
        var health = Optional(map, "--health");
        var actor = Optional(map, "--actor") ?? "cli:system";

        if (!IsSafeIdentifier(tenant))
            throw new InvalidOperationException("Invalid tenant identifier.");

        return new ProvisioningRequest(
            tenant,
            tenantCode.ToLowerInvariant(),
            plan.ToLowerInvariant(),
            saConn,
            catalogConn,
            platformConn,
            redis,
            adminEmail,
            adminPassword,
            domain,
            Optional(map, "--store-id") ?? "default",
            outEnv,
            Optional(map, "--compose-project") ?? $"dcms-{tenantCode}",
            actor,
            compose,
            health);
    }

    private static void LogResult(ILogger log, ProvisioningResult result)
    {
        if (result.Success)
        {
            log.LogInformation(
                "Provisioning operation succeeded tenant {TenantId} status {Status} run {RunId} message {Message}",
                result.TenantId, result.Status.ToApiString(), result.RunId, result.Message ?? "");
        }
        else
        {
            log.LogError(
                "Provisioning operation failed tenant {TenantId} status {Status} run {RunId} message {Message}",
                result.TenantId, result.Status.ToApiString(), result.RunId, result.Message ?? "");
        }
    }

    private static string DeriveTenantCode(string tenantId)
    {
        var code = tenantId.StartsWith("t-", StringComparison.OrdinalIgnoreCase)
            ? tenantId[2..]
            : tenantId;
        return code.ToLowerInvariant();
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var k = args[i];
            if (!k.StartsWith("--", StringComparison.Ordinal)) continue;
            var v = (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) ? args[i + 1] : "true";
            map[k] = v;
            if (v != "true") i++;
        }
        return map;
    }

    private static string Required(Dictionary<string, string> m, string key) =>
        m.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v : throw new InvalidOperationException($"Missing required arg {key}");

    private static string? Optional(Dictionary<string, string> m, string key) =>
        m.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static bool IsSafeIdentifier(string s) =>
        !string.IsNullOrWhiteSpace(s) && s.All(c => char.IsLetterOrDigit(c) || c is '_' or '-');
}
