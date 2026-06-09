## DAI-32 — Tenant/JWT route filters

### Kết quả
Đã rà soát và xác nhận mọi route group tenant-scoped trong các service được gọi tên đều gắn filter JWT scope khi `Auth:Enabled`:

| Service | Filter | Route file |
|---|---|---|
| Fulfillment | `WithTenantAccess` | `FulfillmentRoutes.cs` |
| Approval | `WithTenantAccess` | `ApprovalRoutes.cs` |
| Promotions (campaigns, promo-codes) | `WithTenantAccess` | `CampaignRoutes.cs`, `PromoCodeRoutes.cs` |
| Loyalty | `WithTenantAccess` | `LoyaltyRoutes.cs` |
| Voucher | `WithTenantAccess` | `VoucherRoutes.cs` |
| Notification feed | `WithTenantStoreAccess` | `NotificationFeedRoutes.cs` |

**Ngoại lệ có chủ đích:** `InternalPromotionsRoutes` dùng `InternalPromotionsApiKeyEndpointFilter` (service-to-service), không dùng JWT tenant filter — đã có test audit.

### Thay đổi code
- `NotificationFeedRoutes.cs` — thêm `WithTenantStoreAccess` (commit trước trên branch)
- `ScopeFilterTests.cs` — bổ sung 401 unauthenticated + missing tenant claim cho `TenantOnlyAccessEndpointFilter`
- `TenantScopedRouteAuditTests.cs` — regression guard cho 7 route files + internal promotions
- `PromotionsApiAuthIntegrationTests.cs` — integration auth: 401/no token, 403 tenant mismatch, 200 match, SuperAdmin cross-tenant

### Test
```
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~dCMS.Tests.Unit.Access"  → 26 passed
dotnet test src/backend/dCMS.Tests --filter "FullyQualifiedName~PromotionsApiAuthIntegrationTests"  → 4 passed
```

### Handoff
Sẵn sàng cho QA Test Agent kiểm tra permission matrix và Code Review Agent review diff trên branch `feat/dai-32-tenant-jwt-filters`.
