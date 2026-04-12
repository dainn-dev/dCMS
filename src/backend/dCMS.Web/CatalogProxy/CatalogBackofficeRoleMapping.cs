using dCMS.AspNetCore.Auth;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;

namespace dCMS.Web.CatalogProxy;

/// <summary>Maps Umbraco backoffice users to Catalog.Api JWT role claims (US-5 / DAI-296).</summary>
public static class CatalogBackofficeRoleMapping
{
    /// <summary>Optional Umbraco user-group alias → JWT <see cref="DcmsRoles.BrandManager"/>.</summary>
    public const string BrandManagerGroupAlias = "dCMSBrandManager";

    /// <summary>Optional Umbraco user-group alias → JWT <see cref="DcmsRoles.StoreStaff"/> (catalog:read only).</summary>
    public const string StoreStaffGroupAlias = "dCMSStoreStaff";

    public static IReadOnlyList<string> GetDcmsRolesForCatalogJwt(IUser? user)
    {
        var roles = new HashSet<string>(StringComparer.Ordinal);
        if (user?.Id == Constants.Security.SuperUserId)
            roles.Add(DcmsRoles.SuperAdmin);

        if (user?.Groups != null)
        {
            foreach (var g in user.Groups)
            {
                var alias = (g.Alias ?? "").Trim();
                if (alias.Length == 0)
                    continue;
                MapGroupAlias(alias, roles);
            }
        }

        if (roles.Count == 0)
            roles.Add(DcmsRoles.StoreManager);

        return roles.ToList();
    }

    public static bool CanRunCatalogApprovalActions(IReadOnlyCollection<string> roles) =>
        roles.Contains(DcmsRoles.BrandManager) || roles.Contains(DcmsRoles.ChainAdmin) || roles.Contains(DcmsRoles.SuperAdmin);

    public static bool CanCatalogWrite(IReadOnlyCollection<string> roles) =>
        roles.Contains(DcmsRoles.SuperAdmin) || roles.Contains(DcmsRoles.ChainAdmin) || roles.Contains(DcmsRoles.BrandManager) ||
        roles.Contains(DcmsRoles.StoreManager);

    private static void MapGroupAlias(string alias, HashSet<string> roles)
    {
        if (string.Equals(alias, Constants.Security.AdminGroupAlias, StringComparison.OrdinalIgnoreCase))
            roles.Add(DcmsRoles.ChainAdmin);
        else if (string.Equals(alias, Constants.Security.EditorGroupAlias, StringComparison.OrdinalIgnoreCase))
            roles.Add(DcmsRoles.StoreManager);
        else if (string.Equals(alias, BrandManagerGroupAlias, StringComparison.OrdinalIgnoreCase))
            roles.Add(DcmsRoles.BrandManager);
        else if (string.Equals(alias, StoreStaffGroupAlias, StringComparison.OrdinalIgnoreCase))
            roles.Add(DcmsRoles.StoreStaff);
    }
}
