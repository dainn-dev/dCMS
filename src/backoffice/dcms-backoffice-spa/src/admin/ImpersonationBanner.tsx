import { useState } from "react";
import { useAuth } from "../auth/AuthProvider";

/**
 * DAI-752 (US-5) — fixed banner shown whenever the active session carries
 * `impersonated_by`. Mount once near the SPA root.
 */
export function ImpersonationBanner() {
  const { claims, impersonating, endImpersonation } = useAuth();
  const [busy, setBusy] = useState(false);

  if (!impersonating || !claims) return null;

  const onEnd = async () => {
    setBusy(true);
    try { await endImpersonation(); } finally { setBusy(false); }
  };

  return (
    <div role="alert" className="dcms-impersonation-banner" style={{
      position: "sticky", top: 0, zIndex: 1000,
      background: "#aa0014", color: "white", padding: "8px 16px",
      display: "flex", alignItems: "center", justifyContent: "space-between",
      fontFamily: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
      fontSize: 13,
    }}>
      <span>
        Impersonating tenant <strong>{claims.tenantId ?? "—"}</strong> as <strong>{claims.role}</strong>.
        Original role: {claims.originalRole ?? "ClientAdmin"}. All writes are audited.
      </span>
      <button type="button" onClick={onEnd} disabled={busy} style={{
        background: "white", color: "#aa0014", border: "none",
        padding: "6px 14px", borderRadius: 6, cursor: busy ? "wait" : "pointer",
        fontWeight: 600,
      }}>
        {busy ? "Ending…" : "End impersonation"}
      </button>
    </div>
  );
}
