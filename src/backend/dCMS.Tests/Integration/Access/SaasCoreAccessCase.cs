using System.Net;

namespace dCMS.Tests.Integration.Access;

/// <summary>One row in the SaaS Core RBAC matrix — used by parametrized integration tests.</summary>
public sealed record SaasCoreAccessCase(
    string Service,
    string Method,
    string PathTemplate,
    string? Role,
    string TokenTenantId,
    string RouteTenantId,
    string? StoreHeader,
    HttpStatusCode ExpectedStatus,
    string? ExpectedErrorCode = null,
    string? Notes = null);
