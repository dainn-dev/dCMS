# Incident tabletop evidence — OPS-INC-01

**Date:** 2026-06-09  
**Duration:** 30 min (simulated)  
**Scenario:** Gateway 502 on all `/gateway/v1/*`; Umbraco backoffice reachable  
**Facilitator:** Ops (DAI-53)  
**Participants:** IC, comms lead, scribe (roles exercised in drill)

---

## Exercise log

| Time (UTC) | Event |
|---|---|
| 10:00 | Alert: elevated 5xx on gateway; customer report simulated |
| 10:05 | IC assigned; SEV2 declared; internal status doc opened |
| 10:08 | Health check: `GET /health` gateway 502; catalog-api unhealthy in `docker compose ps` |
| 10:12 | Mitigation: `docker compose restart catalog-api`; wait for healthy |
| 10:18 | Gateway routes return 200; correlation smoke pattern from observability doc verified |
| 10:22 | SEV2 downgraded; monitoring 30 min |
| 10:25 | Resolved — injected failure removed (catalog-api stop for drill) |

---

## Postmortem — INC-20260609-DRILL

### Summary

Simulated tabletop: catalog-api container stopped caused gateway 502 for commerce routes. Umbraco remained up. Total simulated customer impact window 18 minutes. No data loss.

### Timeline (UTC)

| Time | Event |
|---|---|
| 10:00 | Detection via alert + report |
| 10:05 | IC assigned, SEV2 |
| 10:12 | catalog-api restarted |
| 10:22 | Resolved |

### Impact

- Tenants affected: all (gateway commerce paths)
- Duration: 18 min (simulated)
- SLO breach: no (drill environment)

### Root cause

Upstream catalog-api unavailable; YARP forwarded to unhealthy destination.

### Action items

| Action | Owner | Due |
|---|---|---|
| Document catalog-api restart in incident-response.md escalation table | Ops | Done |
| Add gateway→catalog dependency check to observability smoke | Platform | DAI-50 |

**QA attestation:** Ready for DAI-50 OPS-INC-01 review.
