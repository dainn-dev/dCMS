using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Core.Security;

namespace dCMS.Web.Access.Filters;

/// <summary>Declares that the decorated action/controller requires a specific dCMS module/action grant.
/// Apply above <c>[Authorize]</c>:
/// <code>[RequirePermission("products", "write")]</code>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string module, string action)
        : base(typeof(PermissionFilter))
    {
        Arguments = [module, action];
    }
}

/// <summary>Action filter that enforces a module/action permission check using <see cref="Services.IPermissionService"/>.</summary>
public sealed class PermissionFilter : IAsyncActionFilter
{
    private readonly Services.IPermissionService _permSvc;
    private readonly IBackOfficeSecurityAccessor _securityAccessor;
    private readonly string _module;
    private readonly string _action;

    public PermissionFilter(
        Services.IPermissionService permSvc,
        IBackOfficeSecurityAccessor securityAccessor,
        string module,
        string action)
    {
        _permSvc = permSvc;
        _securityAccessor = securityAccessor;
        _module = module;
        _action = action;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var currentUser = _securityAccessor.BackOfficeSecurity?.CurrentUser;
        if (currentUser is null)
        {
            context.Result = new ObjectResult(new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new { code = "UNAUTHORIZED", message = "Not authenticated." },
            })
            { StatusCode = 401 };
            return;
        }

        var userId = currentUser.Id;
        var granted = await _permSvc.HasPermissionAsync(userId, _module, _action, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!granted)
        {
            context.Result = new ObjectResult(new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new { code = "FORBIDDEN", message = $"Permission denied: {_module}/{_action}." },
            })
            { StatusCode = 403 };
            return;
        }

        await next().ConfigureAwait(false);
    }
}
