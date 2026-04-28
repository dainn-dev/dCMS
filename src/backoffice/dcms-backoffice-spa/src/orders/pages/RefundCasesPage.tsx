import { useCallback, useEffect, useMemo, useState } from "react";
import {
  RefundChangeHistoryDialog,
  RefundDoDetailsDialog,
  RefundRemarkDetailsDialog,
  RefundUpdateStatusDialog,
} from "../components/RefundCaseDialogs";
import { DataTable } from "../components/DataTable";
import { exportRefundCasesToCsv } from "../exportRefundCases";
import { IconDownload } from "../icons";
import { REFUND_CASE_COLUMN_LABELS, createRefundCaseColumns } from "../refund-cases-columns";
import { fetchAllRefundCasesForExport, fetchRefundCases, updateRefundCaseStatus } from "../api/refundCasesApi";
import type { RefundCase, RefundCaseHistoryEntry, RefundStatus } from "../types";

type StatusFilter = "all" | RefundStatus;

const STATUS_OPTIONS: Array<{ value: StatusFilter; label: string }> = [
  { value: "all", label: "All statuses" },
  { value: "Pending Refund", label: "Pending Refund" },
  { value: "Success", label: "Success" },
  { value: "Failed", label: "Failed" },
];

function findCase(refundNo: string, list: RefundCase[]): RefundCase | null {
  return list.find((c) => c.refundNo === refundNo) ?? null;
}

export function RefundCasesPage({
  tenantId,
  storeId,
  authToken,
}: {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
}) {
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [cases, setCases] = useState<RefundCase[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionToast, setActionToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [doModal, setDoModal] = useState<RefundCase | null>(null);
  const [remarkModal, setRemarkModal] = useState<RefundCase | null>(null);
  const [updateModal, setUpdateModal] = useState<RefundCase | null>(null);
  const [historyModal, setHistoryModal] = useState<RefundCase | null>(null);

  // Auto-dismiss toast after 3s (matches OrderProcessingPage pattern).
  useEffect(() => {
    if (!actionToast) return;
    const t = setTimeout(() => setActionToast(null), 3000);
    return () => clearTimeout(t);
  }, [actionToast]);

  // Only users authenticated through the Order API (Store/Chain Manager, Customer Support, SuperAdmin)
  // can edit refund case status; backend enforces this via OrderAccess + OrderFailureManage policies.
  // The button visibility is a UX hint only — a 403 from the API is the source of truth.
  const canUpdateRefundStatus = Boolean(tenantId && storeId);

  const openDo = useCallback((id: string) => setDoModal(findCase(id, cases)), [cases]);
  const openRemark = useCallback((id: string) => setRemarkModal(findCase(id, cases)), [cases]);
  const openEdit = useCallback((id: string) => setUpdateModal(findCase(id, cases)), [cases]);
  const openHistory = useCallback((id: string) => setHistoryModal(findCase(id, cases)), [cases]);

  const columns = useMemo(
    () =>
      createRefundCaseColumns({
        onDoClick: openDo,
        onRemarkDetails: openRemark,
        onEdit: openEdit,
        onHistory: openHistory,
        canUpdateRefundStatus,
      }),
    [openDo, openRemark, openEdit, openHistory, canUpdateRefundStatus]
  );

  // Client-side status filter — refundCasesApi list endpoint doesn't accept status,
  // so we filter the loaded page rather than re-fetch. (Keeps loadMore semantics simple.)
  const filteredCases = useMemo(
    () => (statusFilter === "all" ? cases : cases.filter((c) => c.status === statusFilter)),
    [cases, statusFilter]
  );

  const refresh = useCallback(async () => {
    if (!tenantId || !storeId) {
      setCases([]);
      setNextCursor(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const { cases: page, nextCursor: nc } = await fetchRefundCases(tenantId, storeId, { limit: 50 }, authToken);
      setCases(page);
      setNextCursor(nc);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to load refund cases";
      setError(msg);
      setCases([]);
      setNextCursor(null);
    } finally {
      setLoading(false);
    }
  }, [tenantId, storeId, authToken]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function loadMore() {
    if (!tenantId || !storeId || !nextCursor) return;
    setLoadingMore(true);
    setError(null);
    try {
      const { cases: more, nextCursor: nc } = await fetchRefundCases(
        tenantId,
        storeId,
        { cursor: nextCursor ?? undefined, limit: 50 },
        authToken
      );
      setCases((prev) => [...prev, ...more]);
      setNextCursor(nc);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to load more refund cases";
      setError(msg);
    } finally {
      setLoadingMore(false);
    }
  }

  async function runExport() {
    if (!tenantId || !storeId) return;
    setExporting(true);
    setError(null);
    try {
      const { cases: exportRows, limited } = await fetchAllRefundCasesForExport(tenantId, storeId, authToken, {
        limit: 5000,
        pageSize: 100,
      });
      exportRefundCasesToCsv(exportRows);
      if (limited) {
        setError("Export limited to first 5000 rows. Please refine filters.");
      } else {
        setActionToast({ kind: "success", message: `Exported ${exportRows.length} refund case(s)` });
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Export failed";
      setActionToast({ kind: "error", message: msg });
    } finally {
      setExporting(false);
    }
  }

  const handleSave = useCallback(
    async (refundNo: string, patch: { status: RefundStatus; remark: string }) => {
      const target = findCase(refundNo, cases);
      if (!tenantId || !storeId || !target?.orderId) {
        setActionToast({ kind: "error", message: "Missing tenant/store/order context." });
        return;
      }
      try {
        await updateRefundCaseStatus(tenantId, storeId, target.orderId, patch, authToken);
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : "Failed to update refund case";
        setActionToast({ kind: "error", message: msg });
        return;
      }

      const ts = new Date().toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
      const successDate = new Date().toLocaleDateString("en-US", {
        month: "short",
        day: "numeric",
        year: "numeric",
      });
      const entry: RefundCaseHistoryEntry = {
        at: ts,
        actor: "Current User",
        status: patch.status,
        remark: patch.remark,
      };
      setCases((prev) =>
        prev.map((c) => {
          if (c.refundNo !== refundNo) return c;
          const refundDate = patch.status === "Success" ? c.refundDate ?? successDate : c.refundDate;
          return {
            ...c,
            status: patch.status,
            remark: patch.remark,
            refundDate,
            changeHistory: [entry, ...c.changeHistory],
          };
        })
      );
      setActionToast({ kind: "success", message: `Refund case ${refundNo} updated` });
    },
    [authToken, cases, storeId, tenantId]
  );

  return (
    <>
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Refund Cases</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Refund Cases</h1>
          <p className="text-sm text-on-surface-variant mt-1">Track and process customer refund requests</p>
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

      {!tenantId || !storeId ? (
        <div className="mt-6 rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-6 text-sm text-on-surface-variant">
          Select a tenant + store to view refund cases.
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
              data={filteredCases}
              globalFilterPlaceholder="Search by refund no., customer, order…"
              columnLabels={REFUND_CASE_COLUMN_LABELS}
              getRowId={(row) => row.refundNo}
              loading={(loading || loadingMore) && cases.length === 0}
              footerMode="loadMore"
              loadMore={{
                disabled: loading || loadingMore || nextCursor === null,
                label: nextCursor ? (loadingMore ? "Loading…" : "Load more") : "No more",
                onClick: () => void loadMore(),
              }}
            />
          </div>
        </>
      )}

      {doModal && <RefundDoDetailsDialog c={doModal} onClose={() => setDoModal(null)} />}
      {remarkModal && <RefundRemarkDetailsDialog c={remarkModal} onClose={() => setRemarkModal(null)} />}
      {updateModal && (
        <RefundUpdateStatusDialog c={updateModal} onClose={() => setUpdateModal(null)} onSave={handleSave} />
      )}
      {historyModal && <RefundChangeHistoryDialog c={historyModal} onClose={() => setHistoryModal(null)} />}
    </>
  );
}
