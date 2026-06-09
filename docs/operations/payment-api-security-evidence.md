# Payment API Security Evidence Runbook

Reproducible evidence for Payment internal API auth, tenant/client/store scope enforcement, PostgreSQL repository isolation, and webhook replay idempotency.

**Harness:** `src/backend/dCMS.Payment.Tests/PaymentTestSeeds.cs`, `Webhooks/PaymentWebhookTestHelpers.cs`, `Integration/PaymentPostgresFixture.cs`

---

## Fixture table

| Constant | Value | Usage |
|---|---|---|
| `ClientId` | `saas-test-client` | `Dcms:Client:Id` in WAF factories |
| `OtherClientId` | `other-client-id` | Client-column isolation negative test |
| `Provider` | `stub` | Gateway + webhook route segment |
| `InternalApiKey` | `payment-internal-test-key-32chars!!` | `Payment:InternalApiKey` / `X-Internal-Api-Key` |
| `WebhookSecret` | `webhook-test-secret` | HMAC verification (`Payment:Webhooks:stub:Secret`) |
| `TenantA` | `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa` | Home tenant — positive scope |
| `TenantB` | `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb` | Foreign tenant — `PAYMENT_NOT_FOUND` |
| `StoreA1` | `cccccccc-cccc-cccc-cccc-cccccccccccc` | Home store — positive scope |
| `StoreB1` | `dddddddd-dddd-dddd-dddd-dddddddddddd` | Foreign store — `PAYMENT_NOT_FOUND` |

Sample webhook `eventId` values used in automation: `evt_first`, `evt_dup_repo`, `evt_single_row`, `evt_replay_*` (replay suite).

---

## Signature example

Webhook HMAC: `sha256=` + lowercase hex of HMAC-SHA256 over the **raw JSON body** using the provider secret.

```powershell
$secret = "webhook-test-secret"
$body = '{"paymentIntentId":"pi_sample","tenantId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","status":"succeeded","occurredAt":"2026-06-09T12:00:00.0000000Z","eventId":"evt_manual"}'
$hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$sig = "sha256=" + ([BitConverter]::ToString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($body))).Replace("-","").ToLower())
# Header: X-Payment-Signature: $sig
# POST /api/webhooks/payment/stub
```

```bash
# curl (requires openssl)
BODY='{"paymentIntentId":"pi_sample","tenantId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","status":"succeeded","occurredAt":"2026-06-09T12:00:00.0000000Z","eventId":"evt_manual"}'
SIG="sha256=$(printf '%s' "$BODY" | openssl dgst -sha256 -hmac "webhook-test-secret" | awk '{print $2}')"
curl -s -o /dev/null -w "%{http_code}" -X POST "http://localhost:PORT/api/webhooks/payment/stub" \
  -H "Content-Type: application/json" -H "X-Payment-Signature: $SIG" -d "$BODY"
```

---

## Commands

Run from repository root. Expected exit code: **0** (Docker required for Testcontainers integration suites).

```powershell
dotnet test src/backend/dCMS.Payment.Tests --filter "FullyQualifiedName~PaymentInternalAuth"
dotnet test src/backend/dCMS.Payment.Tests --filter "FullyQualifiedName~PaymentInternalApi"
dotnet test src/backend/dCMS.Payment.Tests --filter "FullyQualifiedName~PostgresPaymentTransaction"
dotnet test src/backend/dCMS.Payment.Tests --filter "FullyQualifiedName~PaymentWebhook"
```

---

## Representative outcomes

| Suite | Case | HTTP | Error code |
|---|---|---|---|
| `PaymentInternalAuthFilterTests` | Anonymous | 401 | `UNAUTHORIZED` |
| `PaymentInternalAuthFilterTests` | Wrong API key | 401 | `UNAUTHORIZED` |
| `PaymentInternalAuthFilterTests` | Valid API key | pass filter | — |
| `PaymentInternalApiIntegrationTests` | `POST /internal/payment/create-intent` no key | 401 | `UNAUTHORIZED` |
| `PaymentInternalApiIntegrationTests` | Wrong `X-Internal-Api-Key` | 401 | `UNAUTHORIZED` |
| `PaymentInternalApiIntegrationTests` | Valid create-intent | 200 | `paymentIntentId` present |
| `PaymentInternalApiIntegrationTests` | Capture wrong tenant | 404 | `PAYMENT_NOT_FOUND` |
| `PaymentInternalApiIntegrationTests` | Capture wrong store | 404 | `PAYMENT_NOT_FOUND` |
| `PaymentInternalApiIntegrationTests` | Capture wrong client config | 404 | `PAYMENT_NOT_FOUND` |
| `PaymentInternalApiIntegrationTests` | Capture order mismatch | 403 | `ORDER_MISMATCH` |
| `PaymentInternalApiIntegrationTests` | Capture valid scope | 200 | status updated in DB |
| `PaymentWebhookRouteTests` | Invalid HMAC | 401 | — |
| `PaymentWebhookRouteTests` | Stale `occurredAt` (>5 min) | 400 | `REPLAY_REJECTED` |
| `PaymentWebhookRouteTests` | Duplicate `eventId` (mocked repo) | 200 | processor not called twice |
| `PaymentWebhookReplayIntegrationTests` | Fresh delivery | 200 | `PaymentWebhookDeliveries` row inserted |
| `PaymentWebhookReplayIntegrationTests` | Duplicate `eventId` (DB) | 200 | processor `Times.Once`, delivery count = 1 |
| `PostgresPaymentTransactionRepositoryIntegrationTests` | Cross-tenant lookup | null row | — |
| `PostgresPaymentTransactionRepositoryIntegrationTests` | Cross-client lookup | null row | — |
| `PostgresPaymentTransactionRepositoryIntegrationTests` | Cross-store lookup | null row | — |

---

## DB assertions

After webhook replay or capture tests, reviewers can verify persistence:

```sql
-- Single delivery row per (provider, eventId)
SELECT COUNT(*) FROM "PaymentWebhookDeliveries"
WHERE "Provider" = 'stub' AND "EventId" = @eventId;
-- Expected: 1 after duplicate POST

-- Transaction status after capture / webhook processing
SELECT "Status" FROM "PaymentTransactions"
WHERE "PaymentIntentId" = @intentId AND "TenantId" = @tenantId;

-- Repository isolation: no row when client/tenant/store mismatch
SELECT COUNT(*) FROM "PaymentTransactions"
WHERE "PaymentIntentId" = @intentId
  AND "TenantId" = @tenantId
  AND "ClientId" = @clientId
  AND "StoreId" = @storeId;
```

---

## Last run

| Field | Value |
|---|---|
| Date | 2026-06-09 |
| Commit | `e21b9c2` (local working tree) |
| `PaymentInternalAuth` | 6 passed |
| `PaymentInternalApi` | 8 passed |
| `PostgresPaymentTransaction` | 7 passed |
| `PaymentWebhook` | 9 passed |
| **Combined security filter** | 29 passed |

**Production fix from integration coverage:** `GetLatestByPaymentIntentIdAsync` now binds `@StoreId` as explicit `NpgsqlDbType.Uuid` so nullable store filter works under PostgreSQL (`42P08` resolved).

---

## Manual-only / mocked

| Item | Why manual | Automation coverage |
|---|---|---|
| External PSP (Stripe, Adyen) | No live credentials in CI | `StubPaymentGateway` only |
| Live gateway callbacks | Network + provider sandbox | WAF tests with `stub` provider |
| Gateway → Payment routing E2E | Order saga has separate tests | Out of scope |
| Duplicate dedup by signature digest only | By design only `(provider, eventId)` is deduped | `PaymentWebhookReplayIntegrationTests` documents new `eventId` = new delivery |
