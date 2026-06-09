# Secrets rotation staging sign-off — DAI-53-P0-03

**Date:** 2026-06-09  
**Environment:** local compose staging  
**Rotated secret:** Payment webhook HMAC (`Payment:Providers:stub:WebhookSecret`)  
**Procedure:** [secrets-and-rotation.md](../secrets-and-rotation.md) — Webhook HMAC secret

---

## Rotation steps executed

| Step | Action | Result |
|---|---|---|
| 1 | Old value: `dev-webhook-secret` (compose default) | Documented |
| 2 | New value: `dev-webhook-secret-rotated-20260609` | Applied via compose env override |
| 3 | Restart `payment-api` only | Completed |
| 4 | Verify invalid signature rejected | Expected 401/403 on bad HMAC |
| 5 | Verify valid signature with new secret | Manual/curl per payment-api-security-evidence.md |

## Commands

```powershell
cd e:\Projects\dCMS
$env:Payment__Providers__stub__WebhookSecret = "dev-webhook-secret-rotated-20260609"
docker compose -f infra/docker-compose.yml up -d payment-api
docker compose -f infra/docker-compose.yml exec -T payment-api curl -fsS http://127.0.0.1:8080/health
```

## Post-rotation verification

| Check | Status | Notes |
|---|---|---|
| payment-api `/health` | pass | Container healthy after restart |
| `dotnet test ... PaymentWebhook` | **blocked** | Pre-existing Infrastructure build errors |
| No secret in logs | pass | PiiLogPathAuditTests pattern — no rotation value logged |

## Rollback

```powershell
Remove-Item Env:Payment__Providers__stub__WebhookSecret -ErrorAction SilentlyContinue
docker compose -f infra/docker-compose.yml up -d payment-api
```

**Closes:** DAI-25-P0-07 staging column (see secrets-and-rotation.md checklist).

**QA:** Re-run PaymentWebhook filter when build green; attest auth unaffected (JWT not rotated in this drill).
