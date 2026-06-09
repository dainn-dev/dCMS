# SaaS Core Observability Baseline

This runbook defines the first-paying-tenant observability baseline for gateway,
APIs, and workers.

## Log Fields

Every HTTP host should emit request completion logs with:

- `service`
- `correlationId`
- `tenantId` when present in route, header, or token claim
- `storeId` when present in route, header, or token claim
- `route`
- `statusCode`
- `failureReason`
- `elapsedMs`

Workers should emit:

- `service`
- `operation`
- `status`
- `failureReason`
- `correlationId` when the message carries one, otherwise `messageId`
- `tenantId` and job/event identifiers where available

Do not log bearer tokens, webhook signatures, passwords, card data, customer
email, customer phone, customer name, addresses, or raw webhook/import payloads.
Identifiers needed for operations are allowed: tenant ID, store ID, job ID,
event type, provider/carrier name, order/payment/shipment IDs, and failure
reason codes.

**CI guards:** `PiiLogPathAuditTests` (production log call sites) and
`PiiMaskingAuditTests` (structured template hygiene). See DAI-25-P0-04b in
[.multica/dai-25-child-issues.md](../../.multica/dai-25-child-issues.md).

## Metrics

Scrape `/metrics` on gateway and every API host.

Required panels:

- HTTP request rate by `service`, `route`, `status`, and `failure_reason`:
  `sum by (service, route, status, failure_reason) (rate(dcms_http_requests_total[5m]))`
- Webhook failures by service/provider/reason:
  `sum by (service, provider, reason) (rate(dcms_webhook_failures_total[5m]))`
- Worker operation outcomes:
  `sum by (service, operation, status, failure_reason) (rate(dcms_worker_operations_total[5m]))`
- RabbitMQ DLQ depth:
  `max by (service, queue) (dlq_depth)`

## Alerts

Start with these alert definitions and tune after the first production week:

- Gateway 5xx: `sum(rate(dcms_http_requests_total{service="gateway",status=~"5.."}[5m])) > 0`
  for 5 minutes.
- Auth failures spike:
  `sum(rate(dcms_http_requests_total{failure_reason=~"unauthorized|tenant_mismatch|store_mismatch"}[5m])) > 5`
  for 10 minutes.
- Webhook failures:
  `sum(rate(dcms_webhook_failures_total[5m])) > 0` for 10 minutes.
- Worker failures:
  `sum(rate(dcms_worker_operations_total{status="failed"}[5m])) > 0`
  for 10 minutes.
- DLQ depth:
  `max(dlq_depth) > 0` for 5 minutes.

## Smoke Checks

1. Send a gateway request with `X-Correlation-Id: smoke-corr-1` and verify the
   response carries the same header.
2. Follow the same request into the target API logs and verify `correlationId`
   remains `smoke-corr-1`.
3. Exercise one tenant/store route and verify logs contain the expected
   `tenantId` and `storeId`.
4. Submit a request with a mismatched tenant token and route. Verify a 403 and
   `failureReason=tenant_mismatch`, without token contents in logs.
5. Submit a payment webhook with a bad signature. Verify a 401,
   `dcms_webhook_failures_total{reason="invalid_signature"}`, and no signature
   or raw payload in logs.
6. Run or replay a catalog import worker message in a non-production
   environment and verify worker logs include operation, status, correlation or
   message ID, and tenant ID.

---

## First-Paying-Tenant Operations

### Which alerts fire during onboarding

| Alert | Expected during onboarding? | Action |
|---|---|---|
| `DcmsAuthFailureRateHigh` | Possibly — integrators misconfigure tokens | Check logs for `failure_reason=unauthorized`, verify tenant JWT config |
| `DcmsTenantMismatchDetected` | Rare but critical | Immediately investigate — potential isolation breach; escalate |
| `DcmsWebhookFailureRateHigh` | Possibly — provider sandbox vs prod keys | Verify `Payment__Providers__{provider}__WebhookSecret` matches provider config |
| `DcmsDlqDepthHigh` | Unlikely unless messaging mis-configured | Check RabbitMQ management UI (`http://rabbitmq:15672`); inspect DLQ messages |
| `DcmsHttpErrorRateHigh` | Should not fire; investigate immediately | Check gateway and API logs filtered by `status=5xx` and `correlationId` |
| `DcmsWorkerFailureRateHigh` | Possibly — first import, template missing | Check notification/catalog worker logs for `failure_reason` |

### End-to-end correlation for a tenant's first transaction

Use `correlationId` to follow a request across services:

```bash
# 1. Make a request (or find a real correlationId from the gateway access log)
CORR="tenant-onboard-$(date +%s)"
curl -H "X-Correlation-Id: $CORR" https://gateway.your-domain/api/...

# 2. Find it in gateway logs
docker compose logs gateway | grep "$CORR"

# 3. Follow it into the target API
docker compose logs order-api | grep "$CORR"
docker compose logs catalog-api | grep "$CORR"

# 4. Follow into workers (correlation propagated via MassTransit headers)
docker compose logs catalog-worker | grep "$CORR"
docker compose logs notification-worker | grep "$CORR"
```

All log entries with the same `correlationId` form the full trace for that transaction. No distributed tracing backend is required for this baseline.

### PII masking expectations

The following must **never** appear in structured log output:

| Category | Examples | Why |
|---|---|---|
| Bearer / API tokens | `Authorization: Bearer eyJ...` | Enables impersonation |
| Webhook signatures | `X-Stripe-Signature: t=...` | Enables replay attacks |
| Passwords | admin password, SMTP password | Obvious |
| Card data | PAN, CVV, expiry | PCI DSS |
| Customer email | `user@example.com` | GDPR / PII |
| Customer phone | `+84 09xxxxxxxx` | GDPR / PII |
| Customer name and address | billing/shipping address fields | GDPR / PII |
| Raw JWT claims | full decoded payload | May contain email/sub details |
| Raw webhook/import payloads | full JSON body | May contain any of the above |

**Safe to log:** correlation ID, tenant ID, store ID, order/payment/shipment IDs, event type, operation name, failure reason code, HTTP method, route path, status code, message ID, job ID.

**JWT exception:** `sub`, `tenant_id`, and `store_id` claims are internal UUIDs — safe to log.

#### Automated coverage

- `ObservabilityBaselineAuditTests.cs` verifies that all HTTP hosts call `UseDcmsRequestObservability` and all workers call `ObserveWorkerOperation`.
- `PiiMaskingAuditTests.cs` (see Phase 5) passes email and card-shaped strings through the logging pipeline and asserts they do not appear in rendered log output.
- These tests run on every CI push. A passing suite does **not** replace periodic manual review of production log samples — run the grep commands below monthly or after any new log field is added.

#### Manual PII spot-check (run periodically)

```bash
# Pull a sample of recent gateway logs and check for PII patterns
docker compose logs --tail=500 gateway \
  | grep -E '[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}' \
  && echo "WARN: possible email in gateway logs" || echo "OK: no email pattern found"

# Check for card-number patterns (16 consecutive digits)
docker compose logs --tail=500 gateway \
  | grep -E '\b[0-9]{16}\b' \
  && echo "WARN: possible card number in gateway logs" || echo "OK: no card pattern found"

# Repeat for other services
for svc in catalog-api order-api payment-api notification-worker catalog-worker; do
  docker compose logs --tail=500 "$svc" \
    | grep -qE '[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}' \
    && echo "WARN: possible email in $svc logs" || echo "OK $svc"
done
```

### Webhook failures

1. Check `dcms_webhook_failures_total` metric labels: `service`, `provider`, `failure_reason`.
2. Common `failure_reason` values: `invalid_signature`, `invalid_tenant_id`, `processing_error`.
3. `invalid_signature` → verify webhook secret matches provider dashboard.
4. `invalid_tenant_id` → the inbound request path contains a malformed tenant UUID; check provider configuration.
5. `processing_error` → inspect payment-api logs filtered by `correlationId` from the webhook request.

### DLQ depth

1. Open RabbitMQ management UI: `http://rabbitmq:15672` (guest/guest in dev).
2. Filter queues by prefix `dlq.` to see dead letter queues.
3. Click a DLQ message to view headers — `x-correlation-id` and `x-tenant-id` are safe identifiers to use when searching logs.
4. Do not log or display message body in runbook entries — it may contain PII.
5. Re-queue via RabbitMQ UI "Move messages" or by republishing with corrected routing.

### Auth failures

1. Filter gateway logs: `failure_reason=unauthorized` with the request's `correlationId`.
2. Check whether the token was missing entirely or failed validation (gateway logs the branch in structured fields).
3. `tenant_mismatch` or `store_mismatch` on a protected route → token's `tenant_id`/`store_id` claim doesn't match the route parameter. This is always a configuration or client bug — never silently pass.
4. Escalate `tenant_mismatch` events immediately — they indicate a possible cross-tenant access attempt.

### HTTP errors

1. Filter by `status=~"5.."` and the request's `correlationId` across all services.
2. Gateway logs `failure_reason=internal_error` with the full exception (stack trace excluded from the metric label but present in the log entry).
3. Check the target API's own logs using the same `correlationId` for the root cause.

### Worker failures

1. Filter worker logs by `status=failed` and `failure_reason`.
2. Common values: `send_error` (email), `import_error` (catalog), `processing_error` (generic).
3. MassTransit retry policy will retry transiently; check DLQ depth after retries are exhausted.
4. Use `correlationId` or `messageId` from the worker log to find the originating HTTP request in gateway logs.
