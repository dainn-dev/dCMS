MVP boundary for the first paying tenant:
- Ship one paid tenant with tenant-admin and store/brand scoped access, gateway-enforced auth, and manual provisioning if needed.
- Include catalog, orders, fulfillment, reports, notifications, approvals, and payment capture/refund flows only to the extent needed to run live operations for that tenant.
- Defer self-serve tenant signup, subscription management UI, advanced entitlements, multi-tenant billing automation, and cross-tenant admin tooling beyond Super Admin.

P0 readiness checklist:
- Tenant provisioning: create/activate/deactivate tenant records, assign client/tenant/store claims, and verify new tenant access is isolated by default.
- RBAC: enforce Super Admin, Client Admin, Tenant Admin, Chain Admin, Brand Manager, Store Manager, Store Staff, and Customer scopes end-to-end in API and UI.
- Gateway security: require valid bearer tokens, re-mint short-lived internal JWTs, and block client/tenant/store mismatches before requests reach services.
- Observability: request correlation, auth failures, tenant mismatch logs, audit log persistence, and alerting for webhook/payment failures.
- Billing/entitlements: record paid status, seat/plan or feature entitlement source of truth, and hard fail when a tenant is inactive or over entitlement.
- Tenant isolation: verify every tenant-scoped query, background job, webhook handler, cache key, and export/report path cannot cross tenant boundaries.
- Secrets/config: per-environment signing keys, webhook secrets, DB creds, object storage creds, and safe secret rotation process.
- Migrations: idempotent tenant/role/payment schema migrations with rollback plan and smoke tests on fresh and upgraded databases.
- Backup/restore: scheduled backups, restore drill, and validation that tenant data, auth data, and payment state can be recovered together.

Role/scope outline:
- Super Admin: full system bypass for operations and recovery.
- Client Admin: read across the client, write only by impersonation or explicit elevation.
- Tenant Admin: full write inside one tenant, including provisioning and configuration.
- Chain Admin: legacy tenant-wide admin, same practical scope as Tenant Admin during transition.
- Brand Manager: brand-level catalog/content control within assigned tenant.
- Store Manager: store-level operational control within assigned tenant.
- Store Staff: limited store operations, no tenant or brand administration.
- Customer: storefront-only, no backoffice access.

Open decisions:
- Billing model: manual invoicing first vs self-serve subscription billing; recommend manual invoicing for MVP because it avoids building a billing portal before product-market fit is confirmed.
- Entitlement source: JWT claim vs live lookup in authz middleware; recommend live lookup with cached claims fallback so revocation is immediate.
- Tenant setup: single tenant per paying customer vs multiple tenants per customer; recommend one tenant per paying customer for the first release to reduce isolation and support complexity.
- Recovery target: nightly backup only vs point-in-time restore; recommend nightly backup plus a restore drill now, PITR later if retention requirements demand it.
