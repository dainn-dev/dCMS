# Secrets and configuration rotation (first-paying-tenant MVP)

Ops runbook for per-environment secrets referenced in P0 readiness (DAI-29 / DAI-25-P0-07).

**Related:** [tenant-entitlements.md](./tenant-entitlements.md), [payment-api-security-evidence.md](./payment-api-security-evidence.md), [saas-core-observability.md](./saas-core-observability.md)

---

## Secret inventory

| Secret | Consumers | Config key / header | Rotation impact |
|---|---|---|---|
| JWT signing key | Gateway, all APIs, token mint | `Auth:JwtSigningKey` | All sessions invalidated unless dual-key window |
| Internal catalog API key | Cross-service catalog reads | `Catalog:InternalApiKey` / `X-Internal-Api-Key` | S2S calls fail until all callers updated |
| Internal promotions API key | Order workflow → promotions | `Promotions:InternalApiKey` | Same as catalog |
| Payment internal API key | Order-api → payment-api | `Payment:InternalApiKey` | Capture/refund paths fail |
| Webhook HMAC secrets | Payment providers | `Payment:Webhooks:{provider}:Secret` | Provider dashboard + dCMS must match |
| DB credentials | All services, Umbraco, SpawnTenant | `ConnectionStrings:*` | Rolling restart per service |
| Redis | Gateway entitlements, rate limits, idempotency | `ConnectionStrings:Redis` | Cache cold start; republish entitlements |
| Umbraco SA password | SpawnTenant, migrations | CLI `--sa-conn` / env | Tenant provision only |

**Never commit** production values. Use `.env.local`, Docker secrets, or host environment injection.

---

## Rotation procedures

### JWT signing key (dual-key window)

1. Generate `NEW_KEY` (≥32 bytes).
2. Configure services to accept **both** `OLD_KEY` and `NEW_KEY` for validation (if not supported, schedule maintenance window).
3. Update token issuer (Umbraco / identity) to mint with `NEW_KEY`.
4. Wait for max token TTL (gateway internal TTL + user JWT lifetime).
5. Remove `OLD_KEY` from all services; redeploy.
6. **Verify:** `dotnet test` SaasCore + Gateway suites; manual login + one authenticated API call.

### Webhook HMAC secret

1. Add new secret in provider dashboard (or secondary signing secret if supported).
2. Update `Payment:Webhooks:{provider}:Secret` in payment-api config.
3. Redeploy payment-api only.
4. **Verify:** `dotnet test src/backend/dCMS.Payment.Tests --filter PaymentWebhook` — use [payment-api-security-evidence.md](./payment-api-security-evidence.md) curl example.

### Internal API keys

1. Generate new key per service boundary (do not reuse across services).
2. Update caller **and** callee config in one change set.
3. Rolling deploy: callee first (accept both keys if filter supports), then caller, then remove old key on callee.
4. **Verify:** internal integration tests (`InternalApiKeyAuthTests`, `PaymentInternalApiIntegrationTests`).

### Database credentials

1. Create new DB user with same grants; update connection strings in secrets store.
2. Rolling restart: workers → APIs → Umbraco (platform last).
3. Revoke old DB user after 24h soak.
4. **Verify:** health endpoints + one write per service.

### Redis password

1. Update Redis ACL/password; update all `ConnectionStrings:Redis`.
2. Restart all Redis consumers; warm entitlement cache via subscription GET or tenant create publish.
3. **Verify:** Gateway entitlement check passes (`GatewayTenantEntitlementMiddlewareTests` pattern).

---

## Environment checklist

| Step | Staging | Production |
|---|---|---|
| Secret inventory reviewed | [x] 2026-06-09 | [ ] |
| Rotation drill (JWT or webhook) executed | [x] webhook — [evidence](./evidence/secrets-rotation-staging-2026-06-09.md) | [ ] |
| Post-rotation smoke tests green | [x] health pass; unit tests pending build fix | [ ] |
| Audit log: no secrets in application logs | [x] | [ ] |

**Staging operator:** Ops (DAI-53) | **QA verify:** DAI-50 auth + PaymentWebhook when build green

---

## Open follow-ups

- Automate secret injection via CI/CD secret store (out of MVP scope).
- Document object storage credentials when media offload is enabled for first tenant.
