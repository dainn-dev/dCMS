using System;

namespace dCMS.Core.Persistence;

/// <summary>
/// DAI-702 — Marks a method as intentionally cross-tenant. Suppresses analyzer DCMS001.
/// Apply sparingly — typical cases: SuperAdmin diagnostics, cross-tenant reporting jobs,
/// outbox/relay infrastructure that operates over all tenants by design.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public sealed class CrossTenantAllowedAttribute : Attribute
{
    public CrossTenantAllowedAttribute(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
