import { useEffect, useState } from "react";
import {
  approveReturn,
  completeReturn,
  listReturns,
  rejectReturn,
  type ReturnDto,
  type ReturnStatus,
} from "../api/ordersApi";

const STATUSES: (ReturnStatus | "all")[] = ["all", "Pending", "Approved", "Rejected", "Completed"];

const STATUS_STYLE: Record<ReturnStatus, string> = {
  Pending: "bg-amber-100 text-amber-700",
  Approved: "bg-blue-100 text-blue-700",
  Rejected: "bg-red-100 text-red-700",
  Completed: "bg-green-100 text-green-700",
};

export function ReturnsPage({ tenantId, storeId, authToken }: {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
}) {
  const [filter, setFilter] = useState<ReturnStatus | "all">("Pending");
  const [rows, setRows] = useState<ReturnDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [completing, setCompleting] = useState<{ open: boolean; id: string }>({ open: false, id: "" });
  const [refundCaseId, setRefundCaseId] = useState("");

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 2500);
    return () => clearTimeout(t);
  }, [toast]);

  async function refresh() {
    if (!tenantId || !storeId) return;
    setLoading(true);
    setError(null);
    try {
      const list = await listReturns(
        tenantId,
        storeId,
        filter === "all" ? undefined : { status: filter, limit: 100 },
        authToken,
      );
      setRows(list);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load returns");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId, storeId, authToken, filter]);

  async function runApprove(id: string) {
    if (!tenantId || !storeId) return;
    setBusy(true);
    try {
      await approveReturn(tenantId, storeId, id, null, authToken);
      setToast({ kind: "success", message: `Return ${id} approved (lines restocked)` });
      await refresh();
    } catch (e: unknown) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Approve failed" });
    } finally {
      setBusy(false);
    }
  }

  async function runReject(id: string) {
    if (!tenantId || !storeId) return;
    setBusy(true);
    try {
      await rejectReturn(tenantId, storeId, id, null, authToken);
      setToast({ kind: "success", message: `Return ${id} rejected` });
      await refresh();
    } catch (e: unknown) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Reject failed" });
    } finally {
      setBusy(false);
    }
  }

  async function runComplete() {
    if (!tenantId || !storeId || !completing.id) return;
    setBusy(true);
    try {
      await completeReturn(tenantId, storeId, completing.id, refundCaseId.trim() || null, authToken);
      setToast({ kind: "success", message: `Return ${completing.id} completed` });
      setCompleting({ open: false, id: "" });
      setRefundCaseId("");
      await refresh();
    } catch (e: unknown) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Complete failed" });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-4 max-w-7xl">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Returns / RMA</h1>
        <div className="flex items-center gap-2">
          {STATUSES.map((s) => (
            <button
              key={s}
              type="button"
              className={`px-3 py-1 text-[11px] font-bold rounded border ${
                filter === s
                  ? "bg-primary text-white border-primary"
                  : "border-outline-variant/30 text-on-surface hover:bg-surface-container-low"
              }`}
              onClick={() => setFilter(s)}
            >
              {s === "all" ? "All" : s}
            </button>
          ))}
          <button
            type="button"
            className="px-3 py-1 text-[11px] font-bold rounded border border-outline-variant/30 hover:bg-surface-container-low"
            onClick={() => void refresh()}
          >
            Refresh
          </button>
        </div>
      </div>

      {!tenantId || !storeId ? (
        <div className="rounded-xl border border-error/25 bg-error/5 px-5 py-4 text-sm text-error">
          Missing tenantId / storeId for Orders API.
        </div>
      ) : null}
      {error && <div className="rounded-xl border border-error/25 bg-error/5 px-5 py-4 text-sm text-error">{error}</div>}

      <div className="overflow-x-auto bg-surface-container-lowest rounded-xl border border-outline-variant/10">
        <table className="w-full text-xs">
          <thead className="bg-surface-container-high">
            <tr>
              <th className="text-left px-4 py-3 text-[11px] font-bold text-primary uppercase">Return ID</th>
              <th className="text-left px-4 py-3 text-[11px] font-bold text-primary uppercase">Order</th>
              <th className="text-left px-4 py-3 text-[11px] font-bold text-primary uppercase">Status</th>
              <th className="text-left px-4 py-3 text-[11px] font-bold text-primary uppercase">Reason</th>
              <th className="text-right px-4 py-3 text-[11px] font-bold text-primary uppercase">Lines</th>
              <th className="text-left px-4 py-3 text-[11px] font-bold text-primary uppercase">Created</th>
              <th className="text-center px-4 py-3 text-[11px] font-bold text-primary uppercase">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-outline-variant/10">
            {loading && (
              <tr>
                <td colSpan={7} className="px-4 py-6 text-center text-on-surface-variant italic">
                  Loading…
                </td>
              </tr>
            )}
            {!loading && rows.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-10 text-center text-on-surface-variant italic">
                  No returns in this filter.
                </td>
              </tr>
            )}
            {rows.map((r) => (
              <tr key={r.returnId} className="hover:bg-surface-container-low">
                <td className="px-4 py-3 font-mono text-[11px]">{r.returnId}</td>
                <td className="px-4 py-3 font-mono text-[11px]">{r.orderId}</td>
                <td className="px-4 py-3">
                  <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLE[r.status]}`}>
                    {r.status}
                  </span>
                </td>
                <td className="px-4 py-3">{r.reason}</td>
                <td className="px-4 py-3 text-right">{r.items.length}</td>
                <td className="px-4 py-3">{new Date(r.createdAt).toLocaleString()}</td>
                <td className="px-4 py-3 text-center">
                  <div className="flex justify-center gap-1">
                    {r.status === "Pending" && (
                      <>
                        <button
                          type="button"
                          className="px-2 py-1 rounded text-[10px] font-bold bg-primary text-white disabled:opacity-40"
                          disabled={busy}
                          onClick={() => void runApprove(r.returnId)}
                        >
                          Approve
                        </button>
                        <button
                          type="button"
                          className="px-2 py-1 rounded text-[10px] font-bold border border-outline-variant/30 disabled:opacity-40"
                          disabled={busy}
                          onClick={() => void runReject(r.returnId)}
                        >
                          Reject
                        </button>
                      </>
                    )}
                    {r.status === "Approved" && (
                      <button
                        type="button"
                        className="px-2 py-1 rounded text-[10px] font-bold bg-green-600 text-white disabled:opacity-40"
                        disabled={busy}
                        onClick={() => { setCompleting({ open: true, id: r.returnId }); setRefundCaseId(r.refundCaseId ?? ""); }}
                      >
                        Complete…
                      </button>
                    )}
                    {(r.status === "Completed" || r.status === "Rejected") && (
                      <span className="text-[10px] text-on-surface-variant italic">—</span>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {completing.open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm mx-4 p-6 space-y-4">
            <h2 className="text-sm font-bold text-on-surface">Complete return</h2>
            <p className="text-xs text-on-surface-variant">
              Mark <span className="font-mono">{completing.id}</span> as completed. Optionally link a refund case.
            </p>
            <label className="space-y-1 block">
              <span className="block text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
                Refund Case ID (optional UUID)
              </span>
              <input
                className="w-full bg-surface-container-low border border-outline-variant/20 rounded-lg px-3 py-2 text-xs font-mono"
                value={refundCaseId}
                onChange={(e) => setRefundCaseId(e.target.value)}
                placeholder="00000000-0000-0000-0000-000000000000"
              />
            </label>
            <div className="flex justify-end gap-2">
              <button
                type="button"
                className="px-4 py-2 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-low rounded-lg"
                onClick={() => { setCompleting({ open: false, id: "" }); setRefundCaseId(""); }}
                disabled={busy}
              >
                Close
              </button>
              <button
                type="button"
                className="px-4 py-2 text-xs font-bold bg-primary text-white rounded-lg disabled:opacity-40"
                onClick={() => void runComplete()}
                disabled={busy}
              >
                Complete
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-outline-variant/20 bg-white px-6 py-3 shadow-2xl">
          <p className={`text-sm font-semibold ${toast.kind === "error" ? "text-error" : "text-on-surface"}`}>
            {toast.message}
          </p>
        </div>
      )}
    </div>
  );
}
