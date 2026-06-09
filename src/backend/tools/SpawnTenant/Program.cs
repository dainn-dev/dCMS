using dCMS.Tools.SpawnTenant;
using Microsoft.Extensions.Logging;

// DAI-714 / DAI-29: Tenant bootstrap with explicit provisioning lifecycle.
// PII policy: passwords, connection strings, and email addresses are NEVER logged.

using var loggerFactory = LoggerFactory.Create(b =>
    b.AddSimpleConsole(o =>
    {
        o.IncludeScopes = false;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ ";
    }).SetMinimumLevel(LogLevel.Information));

var log = loggerFactory.CreateLogger("SpawnTenant");

try
{
    return await SpawnTenantCli.RunAsync(args, log, loggerFactory).ConfigureAwait(false);
}
catch (InvalidOperationException ex)
{
    log.LogError("Command failed: {Message}", ex.Message);
    return 65;
}
