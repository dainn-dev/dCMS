using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.BulkJobs;

/// <summary>
/// Restrict Hangfire dashboard to a signed-in Umbraco backoffice user (resolved via
/// <see cref="IBackOfficeSecurityAccessor"/>) and, optionally, a shared key fallback
/// (<c>Dcms:Hangfire:DashboardKey</c>) for non-interactive checks.
/// </summary>
public sealed class DcmsBackOfficeHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http is null) return false;

        // Primary: rely on Umbraco's backoffice security accessor — populated by Umbraco's
        // cookie auth scheme regardless of which pipeline branch handles the request.
        var accessor = http.RequestServices.GetService<IBackOfficeSecurityAccessor>();
        if (accessor?.BackOfficeSecurity?.IsAuthenticated() == true)
            return true;

        // Fallback: ASP.NET Core principal (covers cases where the backoffice principal
        // hasn't been populated yet but the cookie has been decoded).
        if (http.User.Identity?.IsAuthenticated == true)
            return true;

        // Optional shared-key fallback for headless / scripted access. Keep disabled in prod
        // unless you really mean it: configure Dcms:Hangfire:DashboardKey to enable.
        var config = http.RequestServices.GetService<IConfiguration>();
        var key = config?["Dcms:Hangfire:DashboardKey"]?.Trim();
        if (string.IsNullOrEmpty(key)) return false;

        var header = http.Request.Headers["X-Dcms-Hangfire-Key"].FirstOrDefault();
        if (string.Equals(header, key, StringComparison.Ordinal))
            return true;

        var queryKey = http.Request.Query["accessKey"].FirstOrDefault();
        return string.Equals(queryKey, key, StringComparison.Ordinal);
    }
}
