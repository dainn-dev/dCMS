import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { getToken } from "../auth/identityClient";

interface BranchSummary {
  tenantId: string;
  name: string;
  region?: string | null;
  status?: string | null;
}

interface BranchListEnvelope {
  data: BranchSummary[] | null;
  error: { code: string; message: string } | null;
}

const BRANCH_LIST_ENDPOINT = "/api/v1/storefront/branches";

/**
 * DAI-752 (US-5) — chain-wide HQ landing page. ClientAdmin sees every tenant
 * under the deployment's client and can launch a 30-min impersonation session.
 */
export function ClientAdminDashboard() {
  const { claims, impersonate } = useAuth();
  const [branches, setBranches] = useState<BranchSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const [target, setTarget] = useState<BranchSummary | null>(null);
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const loadBranches = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const token = getToken();
      const res = await fetch(BRANCH_LIST_ENDPOINT, {
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      });
      const env = (await res.json()) as BranchListEnvelope;
      if (!res.ok || env.error) {
        setError(env.error?.message ?? `HTTP ${res.status}`);
        return;
      }
      setBranches(env.data ?? []);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void loadBranches(); }, [loadBranches]);

  if (!claims) return <div>Sign in required.</div>;
  if (claims.role !== "ClientAdmin" && claims.role !== "SuperAdmin")
    return <div>This page is only available to ClientAdmin / SuperAdmin.</div>;

  const onSubmitImpersonate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!target || reason.trim().length < 4) return;
    setSubmitting(true);
    try {
      await impersonate(target.tenantId, reason.trim(), "TenantAdmin");
      window.location.reload();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <main className="dcms-client-admin" style={{ padding: 24, maxWidth: 1100, margin: "0 auto" }}>
      <header style={{ marginBottom: 24 }}>
        <h1 style={{ fontSize: 24, margin: 0 }}>Client Admin — {claims.clientId}</h1>
        <p style={{ color: "#475569", marginTop: 4 }}>
          Read-only view across all tenants. Use “Switch into” to impersonate a TenantAdmin
          for up to 30 minutes; every write made during impersonation is audited.
        </p>
      </header>

      {error && <div role="alert" style={{ background: "#fee2e2", padding: 12, borderRadius: 8, marginBottom: 16 }}>{error}</div>}

      {loading && <div>Loading tenants…</div>}

      {!loading && branches && (
        <table style={{ width: "100%", borderCollapse: "collapse", background: "white", boxShadow: "0 1px 2px rgba(0,0,0,0.05)" }}>
          <thead>
            <tr style={{ background: "#f8fafc" }}>
              <th style={th}>Tenant ID</th>
              <th style={th}>Name</th>
              <th style={th}>Region</th>
              <th style={th}>Status</th>
              <th style={th}></th>
            </tr>
          </thead>
          <tbody>
            {branches.map(b => (
              <tr key={b.tenantId} style={{ borderTop: "1px solid #e2e8f0" }}>
                <td style={td}><code>{b.tenantId}</code></td>
                <td style={td}>{b.name}</td>
                <td style={td}>{b.region ?? "—"}</td>
                <td style={td}>{b.status ?? "—"}</td>
                <td style={{ ...td, textAlign: "right" }}>
                  <button type="button" onClick={() => setTarget(b)} style={btn}>Switch into</button>
                </td>
              </tr>
            ))}
            {branches.length === 0 && (
              <tr><td colSpan={5} style={{ ...td, textAlign: "center", color: "#64748b" }}>No tenants registered.</td></tr>
            )}
          </tbody>
        </table>
      )}

      {target && (
        <div role="dialog" aria-modal="true" style={modalBackdrop} onClick={() => !submitting && setTarget(null)}>
          <form onSubmit={onSubmitImpersonate} style={modal} onClick={e => e.stopPropagation()}>
            <h2 style={{ margin: 0, fontSize: 18 }}>Switch into {target.name}</h2>
            <p style={{ color: "#475569", margin: "8px 0 16px" }}>
              You are about to assume <strong>TenantAdmin</strong> on tenant <code>{target.tenantId}</code>.
              The session is capped at 30 minutes and is fully audited.
            </p>
            <label style={{ display: "block", fontWeight: 600, marginBottom: 6 }}>Reason (required)</label>
            <textarea
              value={reason}
              onChange={e => setReason(e.target.value)}
              required
              minLength={4}
              rows={3}
              style={{ width: "100%", padding: 10, borderRadius: 6, border: "1px solid #cbd5e1", fontFamily: "inherit" }}
              placeholder="e.g. Investigating order failures reported by branch manager"
            />
            <div style={{ marginTop: 16, display: "flex", justifyContent: "flex-end", gap: 8 }}>
              <button type="button" onClick={() => setTarget(null)} disabled={submitting} style={btnGhost}>Cancel</button>
              <button type="submit" disabled={submitting || reason.trim().length < 4} style={btnPrimary}>
                {submitting ? "Starting…" : "Start impersonation"}
              </button>
            </div>
          </form>
        </div>
      )}
    </main>
  );
}

const th: React.CSSProperties = { textAlign: "left", padding: "10px 12px", fontSize: 12, textTransform: "uppercase", color: "#64748b", fontWeight: 600 };
const td: React.CSSProperties = { padding: "12px", fontSize: 14 };
const btn: React.CSSProperties = { background: "#0f172a", color: "white", border: "none", padding: "6px 12px", borderRadius: 6, cursor: "pointer", fontWeight: 600, fontSize: 13 };
const btnPrimary: React.CSSProperties = { ...btn, background: "#aa0014" };
const btnGhost: React.CSSProperties = { ...btn, background: "transparent", color: "#0f172a", border: "1px solid #cbd5e1" };
const modalBackdrop: React.CSSProperties = { position: "fixed", inset: 0, background: "rgba(15,23,42,0.55)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1100 };
const modal: React.CSSProperties = { background: "white", padding: 24, borderRadius: 12, width: 480, maxWidth: "90vw", boxShadow: "0 20px 50px rgba(0,0,0,0.25)" };
