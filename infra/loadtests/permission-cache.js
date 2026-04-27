import http from "k6/http";
import { check, sleep } from "k6";
import { Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "http://umbraco-web:8080";
const LOADTEST_KEY = __ENV.LOADTEST_KEY || "";
const USER_ID = __ENV.USER_ID || "1";
const MODULE = __ENV.MODULE || "orders";
const ACTION = __ENV.ACTION || "view";

// DAI-705 — track the second (cache-hit) call latency reported by the controller.
const secondMs = new Trend("dcms_permissions_secondMs", false);
const firstMs = new Trend("dcms_permissions_firstMs", false);

export const options = {
  summaryTrendStats: ["min", "med", "avg", "p(50)", "p(95)", "p(99)", "max"],
  scenarios: {
    steady: {
      executor: "constant-vus",
      vus: Number(__ENV.VUS || 2000),
      duration: __ENV.DURATION || "60s",
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<50"],
    // AC2: cached permission check must be <1ms p95, <5ms p99 (measured server-side).
    dcms_permissions_secondMs: ["p(95)<1", "p(99)<5"],
  },
};

export default function () {
  const url =
    `${BASE_URL}/dcms-internal/perm-loadtest/check` +
    `?userId=${encodeURIComponent(USER_ID)}` +
    `&module=${encodeURIComponent(MODULE)}` +
    `&action=${encodeURIComponent(ACTION)}`;

  const res = http.get(url, {
    headers: {
      "X-LoadTest-Key": LOADTEST_KEY,
      Accept: "application/json",
    },
    timeout: "10s",
  });

  const ok = check(res, {
    "status is 200": (r) => r.status === 200,
  });
  if (!ok) return;

  try {
    const b = res.json();
    check(b, {
      "has secondMs": (x) => typeof x.secondMs === "number",
    });
    if (typeof b.firstMs === "number") firstMs.add(b.firstMs);
    if (typeof b.secondMs === "number") secondMs.add(b.secondMs);
  } catch (_e) {
    // ignore parse errors
  }

  sleep(0.1);
}

export function handleSummary(data) {
  return {
    "/results/permission-cache-summary.json": JSON.stringify(data, null, 2),
    stdout: textSummary(data),
  };
}

function textSummary(data) {
  const m = data.metrics || {};
  const fmt = (t) => {
    if (!t || !t.values) return "n/a";
    const v = t.values;
    return `min=${v.min?.toFixed(3)} med=${v.med?.toFixed(3)} avg=${v.avg?.toFixed(3)} p95=${v["p(95)"]?.toFixed(3)} p99=${v["p(99)"]?.toFixed(3)} max=${v.max?.toFixed(3)}`;
  };
  return [
    "─── DAI-705 PermissionService cache load test ───",
    `http_req_duration : ${fmt(m.http_req_duration)}`,
    `firstMs (server)  : ${fmt(m.dcms_permissions_firstMs)}`,
    `secondMs (server) : ${fmt(m.dcms_permissions_secondMs)}`,
    `http_req_failed   : ${m.http_req_failed?.values?.rate?.toFixed(4)}`,
    "",
  ].join("\n");
}
