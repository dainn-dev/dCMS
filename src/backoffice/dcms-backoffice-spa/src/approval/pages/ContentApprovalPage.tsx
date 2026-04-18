import type { RowSelectionState } from "@tanstack/react-table";
import { useCallback, useEffect, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconCheckCircle, IconEditNote } from "../../orders/icons";
import { ApprovalOkConfirmModal } from "../components/ApprovalOkConfirmModal";
import { RejectionReasonModal } from "../components/RejectionReasonModal";
import {
  type ContentApprovalRow,
  createContentApprovalColumns,
} from "../content-approval-columns";

const MOCK_PENDING: ContentApprovalRow[] = [
  {
    id: "ca-1",
    contentTitle: "Homepage hero — spring campaign",
    contentType: "Rich text block",
    pagePath: "/",
    submittedBy: "editor@brand.example",
    submittedDate: "2026-04-12",
    status: "pending",
  },
  {
    id: "ca-2",
    contentTitle: "FAQ — delivery & returns",
    contentType: "Structured content",
    pagePath: "/help/delivery-returns",
    submittedBy: "content@ops.example",
    submittedDate: "2026-04-16",
    status: "pending",
  },
];

type Props = {
  onPendingCountChange?: (count: number) => void;
};

export function ContentApprovalPage({ onPendingCountChange }: Props) {
  const [rows, setRows] = useState<ContentApprovalRow[]>(() => [...MOCK_PENDING]);
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({});
  const [toast, setToast] = useState<string | null>(null);
  const [confirmApprove, setConfirmApprove] = useState<string[] | null>(null);
  const [rejectTargetIds, setRejectTargetIds] = useState<string[] | null>(null);

  const pendingRows = useMemo(() => rows.filter((r) => r.status === "pending"), [rows]);

  useEffect(() => {
    onPendingCountChange?.(pendingRows.length);
  }, [pendingRows.length, onPendingCountChange]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(t);
  }, [toast]);

  const showDemoEmailToast = useCallback((action: "approved" | "rejected", detail?: string) => {
    const base =
      action === "approved"
        ? "Content approved. Notification email sent to requestor(s) (demo)."
        : `Content rejected${detail ? `: ${detail}` : ""}. Notification email sent to requestor(s) (demo).`;
    setToast(base);
  }, []);

  const clearSelectionFor = useCallback((ids: string[]) => {
    setRowSelection((prev) => {
      const next = { ...prev };
      for (const id of ids) delete next[id];
      return next;
    });
  }, []);

  const applyApprove = useCallback(
    (ids: string[]) => {
      if (ids.length === 0) return;
      setRows((prev) =>
        prev.map((r) => (ids.includes(r.id) ? { ...r, status: "approved" as const } : r))
      );
      clearSelectionFor(ids);
      setConfirmApprove(null);
      showDemoEmailToast("approved");
    },
    [clearSelectionFor, showDemoEmailToast]
  );

  const applyReject = useCallback(
    (ids: string[], reason: string) => {
      if (ids.length === 0) return;
      setRows((prev) =>
        prev.map((r) => (ids.includes(r.id) ? { ...r, status: "rejected" as const } : r))
      );
      clearSelectionFor(ids);
      setRejectTargetIds(null);
      const shortReason = reason.length > 80 ? `${reason.slice(0, 80)}…` : reason;
      showDemoEmailToast("rejected", shortReason);
    },
    [clearSelectionFor, showDemoEmailToast]
  );

  const onViewPage = useCallback((row: ContentApprovalRow) => {
    setToast(`Opening live page for “${row.contentTitle}” (demo).`);
  }, []);

  const onPreview = useCallback((row: ContentApprovalRow) => {
    window.open(`about:blank#content-preview=${encodeURIComponent(row.id)}`, "_blank", "noopener,noreferrer");
    setToast(`Content preview opened in a new tab (demo) — ${row.contentTitle}.`);
  }, []);

  const onApproveRow = useCallback((row: ContentApprovalRow) => {
    setConfirmApprove([row.id]);
  }, []);

  const onRejectRow = useCallback((row: ContentApprovalRow) => {
    setRejectTargetIds([row.id]);
  }, []);

  const columns = useMemo(
    () =>
      createContentApprovalColumns({
        onViewPage,
        onPreview,
        onApprove: onApproveRow,
        onReject: onRejectRow,
      }),
    [onApproveRow, onPreview, onRejectRow, onViewPage]
  );

  const selectedIds = useMemo(
    () => Object.keys(rowSelection).filter((id) => rowSelection[id]),
    [rowSelection]
  );
  const selectedCount = selectedIds.length;

  const approveModalTitle = "Approve content?";
  const approveModalMessage =
    confirmApprove && confirmApprove.length > 1
      ? `Approve ${confirmApprove.length} content items?`
      : "Approve this content?";

  const rejectSubtitle =
    rejectTargetIds && rejectTargetIds.length > 1
      ? `Reject ${rejectTargetIds.length} content items?`
      : rejectTargetIds
        ? "Reject this content?"
        : undefined;

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Content approval">
      <header className="flex shrink-0 flex-col gap-2 border-b border-outline-variant/15 bg-surface px-6 py-4">
        <nav className="flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
          <span>Approval</span>
          <span className="mx-2">/</span>
          <span className="text-primary">Content</span>
        </nav>
        <div className="flex items-center gap-2">
          <IconEditNote className="h-7 w-7 shrink-0 text-primary" aria-hidden />
          <div>
            <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Content approval</h1>
            <p className="mt-0.5 max-w-2xl text-sm text-on-surface-variant">
              Review pending content changes (spec 6.2). Approve or reject with a required reason; requestors receive a notification (demo toast until the API is available).
            </p>
          </div>
        </div>
      </header>

      <div className="w-full flex-1 space-y-4 p-6">
        <DataTable
          columns={columns}
          data={pendingRows}
          globalFilterPlaceholder="Search by title, type, path, submitter…"
          getRowId={(row) => row.id}
          rowSelection={rowSelection}
          onRowSelectionChange={setRowSelection}
        />

        {selectedCount > 0 && (
          <div className="sticky bottom-3 z-30 flex flex-wrap items-center gap-3 rounded-2xl border border-outline-variant/30 bg-surface-container-lowest px-4 py-3 shadow-lg ring-1 ring-black/5">
            <span className="text-xs font-medium text-on-surface">
              <span className="font-bold tabular-nums text-primary">{selectedCount}</span>
              {` item${selectedCount === 1 ? "" : "s"} selected`}
            </span>
            <span className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Actions</span>
            <button
              type="button"
              className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-outline-variant/40 text-lg leading-none text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface transition-colors"
              aria-label="Clear selection"
              title="Clear selection"
              onClick={() => setRowSelection({})}
            >
              ×
            </button>
            <span className="hidden h-6 w-px shrink-0 bg-outline-variant/40 sm:block" aria-hidden />
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="rounded-lg border border-outline-variant/40 bg-white px-3 py-1.5 text-xs font-bold text-primary hover:bg-tertiary-container/20 transition-colors"
                onClick={() => setConfirmApprove([...selectedIds])}
              >
                Approve
              </button>
              <button
                type="button"
                className="rounded-lg border border-error/30 bg-error/10 px-3 py-1.5 text-xs font-bold text-error hover:bg-error/20 transition-colors"
                onClick={() => setRejectTargetIds([...selectedIds])}
              >
                Reject
              </button>
            </div>
          </div>
        )}
      </div>

      <ApprovalOkConfirmModal
        open={Boolean(confirmApprove?.length)}
        title={approveModalTitle}
        message={approveModalMessage}
        confirmLabel="Ok"
        onCancel={() => setConfirmApprove(null)}
        onConfirm={() => confirmApprove && applyApprove(confirmApprove)}
      />

      <RejectionReasonModal
        open={Boolean(rejectTargetIds?.length)}
        subtitle={rejectSubtitle}
        onCancel={() => setRejectTargetIds(null)}
        onConfirm={(reason) => rejectTargetIds && applyReject(rejectTargetIds, reason)}
      />

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-[80] flex -translate-x-1/2 items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="max-w-md text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
