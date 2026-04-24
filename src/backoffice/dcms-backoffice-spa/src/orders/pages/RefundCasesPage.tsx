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
  const [cases, setCases] = useState<RefundCase[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [apiLoading, setApiLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [exportBusy, setExportBusy] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);
  const [saveToast, setSaveToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [doModal, setDoModal] = useState<RefundCase | null>(null);
  const [remarkModal, setRemarkModal] = useState<RefundCase | null>(null);
  const [updateModal, setUpdateModal] = useState<RefundCase | null>(null);
  const [historyModal, setHistoryModal] = useState<RefundCase | null>(null);

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

  useEffect(() => {
    if (!saveToast) return;
    const t = setTimeout(() => setSaveToast(null), 3000);
    return () => clearTimeout(t);
  }, [saveToast]);

  const refresh = useCallback(async () => {
    if (!tenantId || !storeId) {
      setCases([]);
      setNextCursor(null);
      return;
    }
    setApiLoading(true);
    setApiError(null);
    try {
      const { cases: page, nextCursor: nc } = await fetchRefundCases(tenantId, storeId, { limit: 50 }, authToken);
      setCases(page);
      setNextCursor(nc);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to load refund cases";
      setApiError(msg);
      setCases([]);
      setNextCursor(null);
    } finally {
      setApiLoading(false);
    }
  }, [tenantId, storeId, authToken]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function loadMore() {
    if (!tenantId || !storeId || !nextCursor) return;
    setLoadingMore(true);
    setApiError(null);
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
      setApiError(msg);
    } finally {
      setLoadingMore(false);
    }
  }

  async function handleExport() {
    if (!tenantId || !storeId) return;
    setExportBusy(true);
    setApiError(null);
    try {
      const { cases: exportRows, limited } = await fetchAllRefundCasesForExport(tenantId, storeId, authToken, {
        limit: 5000,
        pageSize: 100,
      });
      exportRefundCasesToCsv(exportRows);
      if (limited) {
        setSaveToast({
          kind: "success",
          message: `Exported ${exportRows.length} refund case(s) (capped at 5000; refine filters if you need the rest).`,
        });
      } else {
        setSaveToast({ kind: "success", message: `Exported ${exportRows.length} refund case(s)` });
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Export failed";
      setSaveToast({ kind: "error", message: msg });
    } finally {
      setExportBusy(false);
    }
  }

  const handleSave = useCallback(
    async (refundNo: string, patch: { status: RefundStatus; remark: string }) => {
      const target = findCase(refundNo, cases);
      if (!tenantId || !storeId || !target?.orderId) {
        setSaveToast({ kind: "error", message: "Missing tenant/store/order context." });
        return;
      }
      try {
        await updateRefundCaseStatus(tenantId, storeId, target.orderId, patch, authToken);
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : "Failed to update refund case";
        setSaveToast({ kind: "error", message: msg });
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
      setSaveToast({ kind: "success", message: `Refund case ${refundNo} updated` });
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
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Refund Cases
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Track and process customer refund requests.
          </p>
        </div>
        <div className="flex gap-3">
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2 disabled:opacity-40"
            onClick={() => void handleExport()}
            disabled={!tenantId || !storeId || exportBusy}
          >
            <IconDownload className="h-4 w-4" />
            {exportBusy ? "Exporting…" : "Export"}
          </button>
        </div>
      </div>

      {!tenantId || !storeId ? (
        <div className="mt-6 rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-6 text-sm text-on-surface-variant">
          Select a tenant + store to view refund cases.
        </div>
      ) : (
        <>
          {apiError && (
            <div className="mt-6 rounded-xl border border-error/25 bg-error/5 px-5 py-4 text-sm text-error">
              {apiError}
            </div>
          )}

          <div className="mt-6">
            <DataTable
              columns={columns}
              data={cases}
              globalFilterPlaceholder="Search by refund no., customer, order…"
              columnLabels={REFUND_CASE_COLUMN_LABELS}
              getRowId={(row) => row.refundNo}
              loading={apiLoading || loadingMore}
              footerMode="loadMore"
              loadMore={{
                disabled: apiLoading || loadingMore || nextCursor === null,
                label: nextCursor ? (loadingMore ? "Loading…" : "Load more") : "No more",
                onClick: () => void loadMore(),
              }}
            />
          </div>
        </>
      )}

      {saveToast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-outline-variant/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <p className={`text-sm font-semibold ${saveToast.kind === "error" ? "text-error" : "text-on-surface"}`}>
            {saveToast.message}
          </p>
        </div>
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
