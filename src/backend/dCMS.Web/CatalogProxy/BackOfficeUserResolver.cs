using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace dCMS.Web.CatalogProxy;

/// <summary>Resolves <see cref="IUser"/> for JWT role mapping when <see cref="IBackOfficeSecurity.CurrentUser"/> is not already <see cref="IUser"/>.</summary>
public static class BackOfficeUserResolver
{
    public static IUser? GetCurrentIUser(IBackOfficeSecurityAccessor accessor, IUserService userService)
    {
        var raw = accessor.BackOfficeSecurity?.CurrentUser;
        if (raw is null)
            return null;
        if (raw is IUser u)
            return u;
        return userService.GetUserById(raw.Id);
    }
}
