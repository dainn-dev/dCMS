using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;

namespace dCMS.Web.BulkJobs;
/// <summary>
/// Restrict Hangfire dashboard to a signed-in Umbraco backoffice user, or to a request that
/// carries a shared key configured via <c>Dcms:Hangfire:DashboardKey</c>.
///
/// We must authenticate against the Umbraco backoffice scheme explicitly — the default
/// scheme is anonymous on backoffice routes, so <c>HttpContext.User.Identity.IsAuthenticated</c>
/// is false even when the cookie is present.
/// </summary>
public sealed class DcmsBackOfficeHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http is null) return false;

        // 1) Backoffice cookie / token — authenticate against the Umbraco scheme.
        var auth = http.AuthenticateAsync(Constants.Security.BackOfficeAuthenticationType)
                       .GetAwaiter().GetResult();
        if (auth.Succeeded && auth.Principal?.Identity?.IsAuthenticated == true)
            return true;

        // 2) Fallback: any other authenticated principal already on the request.
        if (http.User.Identity?.IsAuthenticated == true)
            return true;

        // 3) Shared-key bypass for automation / curl probes.
        var config = http.RequestServices.GetService<IConfiguration>();
        var key = config?["Dcms:Hangfire:DashboardKey"]?.Trim();
        if (string.IsNullOrEmpty(key)) return false;
        var header = http.Request.Headers["X-Dcms-Hangfire-Key"].FirstOrDefault();
        return string.Equals(header, key, StringComparison.Ordinal);
    }
}
