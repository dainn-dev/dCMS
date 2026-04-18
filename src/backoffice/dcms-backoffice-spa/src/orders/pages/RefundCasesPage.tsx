import { useCallback, useMemo, useState } from "react";
import {
  RefundChangeHistoryDialog,
  RefundDoDetailsDialog,
  RefundRemarkDetailsDialog,
  RefundUpdateStatusDialog,
} from "../components/RefundCaseDialogs";
import { DataTable } from "../components/DataTable";
import { exportRefundCasesToCsv } from "../exportRefundCases";
import { IconDownload } from "../icons";
import { MOCK_REFUND_CASES } from "../refundCasesMock";
import { REFUND_CASE_COLUMN_LABELS, createRefundCaseColumns } from "../refund-cases-columns";
import type { RefundCase, RefundCaseHistoryEntry, RefundStatus } from "../types";

/** Replace with real RBAC (e.g. auth context) when API is wired — DAI-388. */
const MOCK_CAN_UPDATE_REFUND_STATUS = true;

function findCase(refundNo: string, list: RefundCase[]): RefundCase | null {
  return list.find((c) => c.refundNo === refundNo) ?? null;
}

export function RefundCasesPage() {
  const [cases, setCases] = useState<RefundCase[]>(() => MOCK_REFUND_CASES.map((c) => ({ ...c, changeHistory: [...c.changeHistory] })));
  const [doModal, setDoModal] = useState<RefundCase | null>(null);
  const [remarkModal, setRemarkModal] = useState<RefundCase | null>(null);
  const [updateModal, setUpdateModal] = useState<RefundCase | null>(null);
  const [historyModal, setHistoryModal] = useState<RefundCase | null>(null);

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
        canUpdateRefundStatus: MOCK_CAN_UPDATE_REFUND_STATUS,
      }),
    [openDo, openRemark, openEdit, openHistory]
  );

  const handleSave = useCallback((refundNo: string, patch: { status: RefundStatus; remark: string }) => {
    const ts = new Date().toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
    const successDate = new Date().toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
    });
    const entry: RefundCaseHistoryEntry = {
      at: ts,
      actor: "Store Staff (mock)",
      status: patch.status,
      remark: patch.remark,
    };
    setCases((prev) =>
      prev.map((c) => {
        if (c.refundNo !== refundNo) return c;
        const refundDate =
          patch.status === "Success" ? c.refundDate ?? successDate : null;
        return {
          ...c,
          status: patch.status,
          remark: patch.remark,
          refundDate,
          changeHistory: [entry, ...c.changeHistory],
        };
      })
    );
  }, []);

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
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2"
            onClick={() => exportRefundCasesToCsv(cases)}
          >
            <IconDownload className="h-4 w-4" />
            Export
          </button>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={cases}
        globalFilterPlaceholder="Search by refund no., customer, order…"
        columnLabels={REFUND_CASE_COLUMN_LABELS}
      />

      {doModal && <RefundDoDetailsDialog c={doModal} onClose={() => setDoModal(null)} />}
      {remarkModal && <RefundRemarkDetailsDialog c={remarkModal} onClose={() => setRemarkModal(null)} />}
      {updateModal && (
        <RefundUpdateStatusDialog c={updateModal} onClose={() => setUpdateModal(null)} onSave={handleSave} />
      )}
      {historyModal && <RefundChangeHistoryDialog c={historyModal} onClose={() => setHistoryModal(null)} />}
    </>
  );
}
