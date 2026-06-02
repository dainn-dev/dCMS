import type { ColumnDef } from "@tanstack/react-table";
import { useEffect, useMemo, useState } from "react";
import { DataTable } from "../components/DataTable";
import { IconDownload } from "../icons";
import {
  approveReturn,
  completeReturn,
  listReturns,
  rejectReturn,
  type ReturnDto,
  type ReturnStatus,
} from "../api/ordersApi";

type StatusFilter = "all" | ReturnStatus;

const STATUS_OPTIONS: Array<{ value: StatusFilter; label: string }> = [
  { value: "all", label: "All statuses" },
  { value: "Pending", label: "Pending" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
  { value: "Completed", label: "Completed" },
];

const STATUS_STYLE: Record<ReturnStatus, string> = {
  Pending: "bg-amber-100 text-amber-700",
  Approved: "bg-blue-100 text-blue-700",
  Rejected: "bg-red-100 text-red-700",
  Completed: "bg-green-100 text-green-700",
};

const COLUMN_LABELS: Record<string, string> = {
  returnId: "Return ID",
  orderId: "Order",
  status: "Status",
  reason: "Reason",
  itemCount: "Lines",
  createdAt: "Created",
  actions: "Actions",
};

function escapeCsv(v: unknown): string {
  if (v === null || v === undefined) return "";
  const s = String(v);
  if (/[",\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

function exportReturnsToCsv(rows: ReturnDto[]) {
  const header = ["Return ID", "Order ID", "Status", "Reason", "Lines", "Notes", "Refund Case", "Created", "Approved By", "Approved At", "Completed At"];
  const lines = [header.join(",")];
  for (const r of rows) {
    lines.push([
      escapeCsv(r.returnId),
      escapeCsv(r.orderId),
      escapeCsv(r.status),
      escapeCsv(r.reason),
      escapeCsv(r.items.length),
      escapeCsv(r.notes ?? ""),
      escapeCsv(r.refundCaseId ?? ""),
      escapeCsv(r.createdAt),
      escapeCsv(r.approvedBy ?? ""),
      escapeCsv(r.approvedAt ?? ""),
      escapeCsv(r.completedAt ?? ""),
    ].join(","));
  }
  const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `returns-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

export function ReturnsPage({ tenantId, storeId, authToken }: Props) {
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("Pending");
  const [rows, setRows] = useState<ReturnDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [actionBusy, setActionBusy] = useState(false);
  const [actionToast, setActionToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [completeTarget, setCompleteTarget] = useState<{ id: string; refundCaseId: string } | null>(null);
  const [approveTarget, setApproveTarget] = useState<string | null>(null);
  const [rejectTarget, setRejectTarget] = useState<string | null>(null);

  // Auto-dismiss toast after 3s (matches OrderProcessingPage pattern).
  useEffect(() => {
    if (!actionToast) return;
    const t = setTimeout(() => setActionToast(null), 3000);
    return () => clearTimeout(t);
  }, [actionToast]);

  async function refresh() {
    if (!tenantId || !storeId) {
      setRows([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const list = await listReturns(
        tenantId,
        storeId,
        statusFilter === "all" ? undefined : { status: statusFilter, limit: 100 },
        authToken,
      );
      setRows(list);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load returns");
      setRows([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId, storeId, authToken, statusFilter]);

  async function runApprove() {
    if (!tenantId || !storeId || !approveTarget) return;
    setActionBusy(true);
    try {
      await approveReturn(tenantId, storeId, approveTarget, null, authToken);
      setActionToast({ kind: "success", message: `Return ${approveTarget} approved (lines restocked)` });
      setApproveTarget(null);
      await refresh();
    } catch (e: unknown) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Approve failed" });
    } finally {
      setActionBusy(false);
    }
  }

  async function runReject() {
    if (!tenantId || !storeId || !rejectTarget) return;
    setActionBusy(true);
    try {
      await rejectReturn(tenantId, storeId, rejectTarget, null, authToken);
      setActionToast({ kind: "success", message: `Return ${rejectTarget} rejected` });
      setRejectTarget(null);
      await refresh();
    } catch (e: unknown) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Reject failed" });
    } finally {
      setActionBusy(false);
    }
  }

  async function runComplete() {
    if (!tenantId || !storeId || !completeTarget) return;
    setActionBusy(true);
    try {
      await completeReturn(
        tenantId,
        storeId,
        completeTarget.id,
        completeTarget.refundCaseId.trim() || null,
        authToken,
      );
      setActionToast({ kind: "success", message: `Return ${completeTarget.id} completed` });
      setCompleteTarget(null);
      await refresh();
    } catch (e: unknown) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Complete failed" });
    } finally {
      setActionBusy(false);
    }
  }

  async function runExport() {
    if (!tenantId || !storeId) return;
    setExporting(true);
    setError(null);
    try {
      // Returns API doesn't expose cursor pagination — use the same filter as the
      // currently visible page; bump limit to capture more rows in the export.
      const list = await listReturns(
        tenantId,
        storeId,
        statusFilter === "all" ? { limit: 5000 } : { status: statusFilter, limit: 5000 },
        authToken,
      );
      exportReturnsToCsv(list);
      setActionToast({ kind: "success", message: `Exported ${list.length} return(s)` });
    } catch (e: unknown) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Export failed" });
    } finally {
      setExporting(false);
    }
  }

  const columns = useMemo<ColumnDef<ReturnDto>[]>(() => [
    {
      id: "returnId",
      accessorKey: "returnId",
      header: "Return ID",
      cell: ({ getValue }) => (
        <span className="font-mono text-[11px]">{String(getValue())}</span>
      ),
    },
    {
      id: "orderId",
      accessorKey: "orderId",
      header: "Order",
      cell: ({ getValue }) => (
        <span className="font-mono text-[11px]">{String(getValue())}</span>
      ),
    },
    {
      id: "status",
      accessorKey: "status",
      header: "Status",
      cell: ({ getValue }) => {
        const s = getValue() as ReturnStatus;
        return (
          <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLE[s]}`}>
            {s}
          </span>
        );
      },
    },
    {
      id: "reason",
      accessorKey: "reason",
      header: "Reason",
    },
    {
      id: "itemCount",
      header: "Lines",
      accessorFn: (r) => r.items.length,
      cell: ({ getValue }) => (
        <div className="text-right tabular-nums">{Number(getValue())}</div>
      ),
    },
    {
      id: "createdAt",
      accessorKey: "createdAt",
      header: "Created",
      cell: ({ getValue }) => {
        const v = getValue();
        return v ? new Date(String(v)).toLocaleString() : "";
      },
    },
    {
      id: "actions",
      header: "Actions",
      enableSorting: false,
      enableGlobalFilter: false,
      cell: ({ row }) => {
        const r = row.original;
        if (r.status === "Pending") {
          return (
            <div className="flex justify-center gap-1">
              <button
                type="button"
                className="px-2 py-1 rounded text-[10px] font-bold bg-primary text-white disabled:opacity-40"
                disabled={actionBusy}
                onClick={() => setApproveTarget(r.returnId)}
              >
                Approve
              </button>
              <button
                type="button"
                className="px-2 py-1 rounded text-[10px] font-bold border border-outline-variant/30 disabled:opacity-40"
                disabled={actionBusy}
                onClick={() => setRejectTarget(r.returnId)}
              >
                Reject
              </button>
            </div>
          );
        }
        if (r.status === "Approved") {
          return (
            <div className="flex justify-center">
              <button
                type="button"
                className="px-2 py-1 rounded text-[10px] font-bold bg-green-600 text-white disabled:opacity-40"
                disabled={actionBusy}
                onClick={() => setCompleteTarget({ id: r.returnId, refundCaseId: r.refundCaseId ?? "" })}
              >
                Complete…
              </button>
            </div>
          );
        }
        return (
          <div className="text-center">
            <span className="text-[10px] text-on-surface-variant italic">—</span>
          </div>
        );
      },
    },
  ], [actionBusy]);

  return (
    <>
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Returns / RMA</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Returns / RMA</h1>
          <p className="text-sm text-on-surface-variant mt-1">Process customer return requests and track restocking</p>
        </div>

        <div className="flex items-center gap-3">
          <select
            className="h-10 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 text-sm text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
            disabled={!tenantId || !storeId}
            aria-label="Status filter"
          >
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2 disabled:opacity-40"
            onClick={() => void runExport()}
            disabled={!tenantId || !storeId || exporting}
          >
            <IconDownload />
            {exporting ? "Exporting…" : "Export"}
          </button>
        </div>
      </div>

      {actionToast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-outline-variant/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <p className={`text-sm font-semibold ${actionToast.kind === "error" ? "text-error" : "text-on-surface"}`}>
            {actionToast.message}
          </p>
        </div>
      )}

      {approveTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6">
              <h3 className="text-sm font-bold text-on-surface">Approve return</h3>
              <p className="mt-2 text-xs text-on-surface-variant">
                Approve <strong>{approveTarget}</strong>? Lines will be restocked.
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setApproveTarget(null)}
                disabled={actionBusy}
              >
                Close
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2.5 text-xs font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
                onClick={() => void runApprove()}
                disabled={actionBusy}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {rejectTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6">
              <h3 className="text-sm font-bold text-on-surface">Reject return</h3>
              <p className="mt-2 text-xs text-on-surface-variant">
                Reject <strong>{rejectTarget}</strong>?
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setRejectTarget(null)}
                disabled={actionBusy}
              >
                Close
              </button>
              <button
                type="button"
                className="rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 disabled:opacity-40"
                onClick={() => void runReject()}
                disabled={actionBusy}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {completeTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[460px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6 space-y-4">
              <div>
                <h3 className="text-sm font-bold text-on-surface">Complete return</h3>
                <p className="mt-1 text-xs text-on-surface-variant">
                  Mark <strong>{completeTarget.id}</strong> as completed. Optionally link a refund case.
                </p>
              </div>
              <div>
                <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                  Refund Case ID (optional UUID)
                </label>
                <input
                  className="w-full bg-surface-container-lowest border border-outline-variant/20 rounded-lg px-3 py-2 text-xs font-mono focus:ring-1 focus:ring-primary outline-none"
                  value={completeTarget.refundCaseId}
                  onChange={(e) =>
                    setCompleteTarget((prev) => (prev ? { ...prev, refundCaseId: e.target.value } : prev))
                  }
                  placeholder="00000000-0000-0000-0000-000000000000"
                />
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setCompleteTarget(null)}
                disabled={actionBusy}
              >
                Close
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2.5 text-xs font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
                onClick={() => void runComplete()}
                disabled={actionBusy}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {!tenantId || !storeId ? (
        <div className="mt-6 rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-6 text-sm text-on-surface-variant">
          Select a tenant + store to view returns.
        </div>
      ) : (
        <>
          {error && (
            <div className="mt-6 rounded-xl border border-error/25 bg-error/5 px-5 py-4 text-sm text-error">
              {error}
            </div>
          )}

          <div className="mt-6">
            <DataTable
              columns={columns}
              data={rows}
              columnLabels={COLUMN_LABELS}
              globalFilterPlaceholder="Search by return id, order, reason…"
              getRowId={(row) => row.returnId}
              loading={loading && rows.length === 0}
              emptyMessage="No returns found."
              itemNoun="returns"
            />
          </div>
        </>
      )}
    </>
  );
}
