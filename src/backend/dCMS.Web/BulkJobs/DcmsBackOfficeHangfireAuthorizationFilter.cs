using Hangfire.Dashboard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace dCMS.Web.BulkJobs;
/// <summary>Restrict Hangfire dashboard to backoffice sign-in and optional shared key (Dcms:Hangfire:DashboardKey).</summary>
public sealed class DcmsBackOfficeHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http is null) return false;

        if (http.User.Identity?.IsAuthenticated == true)
            return true;

        var config = http.RequestServices.GetService<IConfiguration>();
        var key = config?["Dcms:Hangfire:DashboardKey"]?.Trim();
        if (string.IsNullOrEmpty(key)) return false;
        var header = http.Request.Headers["X-Dcms-Hangfire-Key"].FirstOrDefault();
        return string.Equals(header, key, StringComparison.Ordinal);
    }
}
