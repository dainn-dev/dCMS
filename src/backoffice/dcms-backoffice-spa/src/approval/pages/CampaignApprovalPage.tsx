import type { RowSelectionState } from "@tanstack/react-table";
import { useCallback, useEffect, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconBolt, IconCheckCircle } from "../../orders/icons";
import { approveCampaign, fetchPendingCampaigns, rejectCampaign } from "../api/approvalApi";
import { ApprovalOkConfirmModal } from "../components/ApprovalOkConfirmModal";
import { RejectionReasonModal } from "../components/RejectionReasonModal";
import {
  type CampaignApprovalRow,
  createCampaignApprovalColumns,
} from "../campaign-approval-columns";

type Props = {
  tenantId?: string;
  authToken?: string;
  onPendingCountChange?: (count: number) => void;
};

export function CampaignApprovalPage({ tenantId, authToken, onPendingCountChange }: Props) {
  const [rows, setRows] = useState<CampaignApprovalRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({});
  const [toast, setToast] = useState<string | null>(null);
  const [confirmApprove, setConfirmApprove] = useState<string[] | null>(null);
  const [rejectTargetIds, setRejectTargetIds] = useState<string[] | null>(null);

  const pendingRows = useMemo(() => rows.filter((r) => r.status === "pending"), [rows]);

  const loadList = useCallback(async () => {
    if (!tenantId) {
      setRows([]);
      onPendingCountChange?.(0);
      return;
    }
    setLoading(true);
    setLoadError(null);
    try {
      const { items, total } = await fetchPendingCampaigns(tenantId, { page: 1, pageSize: 100 }, authToken);
      setRows(items);
      onPendingCountChange?.(total);
    } catch (e: unknown) {
      setRows([]);
      setLoadError(e instanceof Error ? e.message : "Failed to load pending campaigns");
      onPendingCountChange?.(0);
    } finally {
      setLoading(false);
    }
  }, [tenantId, authToken, onPendingCountChange]);

  useEffect(() => {
    void loadList();
  }, [loadList]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 3200);
    return () => window.clearTimeout(t);
  }, [toast]);

  const clearSelectionFor = useCallback((ids: string[]) => {
    setRowSelection((prev) => {
      const next = { ...prev };
      for (const id of ids) delete next[id];
      return next;
    });
  }, []);

  const applyApprove = useCallback(
    async (ids: string[]) => {
      if (!tenantId || ids.length === 0) return;
      const prev = rows;
      setRows((r) => r.filter((x) => !ids.includes(x.id)));
      clearSelectionFor(ids);
      setConfirmApprove(null);
      try {
        await Promise.all(ids.map((id) => approveCampaign(tenantId, id, authToken)));
        setToast(ids.length > 1 ? `Approved ${ids.length} campaigns.` : "Approved.");
        await loadList();
      } catch (e: unknown) {
        setRows(prev);
        setToast(e instanceof Error ? e.message : "Approve failed");
        await loadList();
      }
    },
    [tenantId, authToken, rows, clearSelectionFor, loadList]
  );

  const applyReject = useCallback(
    async (ids: string[], reason: string) => {
      if (!tenantId || ids.length === 0) return;
      const prev = rows;
      const shortReason = reason.length > 80 ? `${reason.slice(0, 80)}…` : reason;
      setRows((r) => r.filter((x) => !ids.includes(x.id)));
      clearSelectionFor(ids);
      setRejectTargetIds(null);
      try {
        await Promise.all(ids.map((id) => rejectCampaign(tenantId, id, reason, authToken)));
        setToast(`Rejected: ${shortReason}`);
        await loadList();
      } catch (e: unknown) {
        setRows(prev);
        setToast(e instanceof Error ? e.message : "Reject failed");
        await loadList();
      }
    },
    [tenantId, authToken, rows, clearSelectionFor, loadList]
  );

  const onViewPage = useCallback((row: CampaignApprovalRow) => {
    setToast(`Opening storefront view for “${row.campaignName}”.`);
  }, []);

  const onPreview = useCallback((row: CampaignApprovalRow) => {
    window.open(`about:blank#campaign-preview=${encodeURIComponent(row.id)}`, "_blank", "noopener,noreferrer");
    setToast(`Campaign preview opened in a new tab — ${row.campaignName}.`);
  }, []);

  const onApproveRow = useCallback((row: CampaignApprovalRow) => {
    setConfirmApprove([row.id]);
  }, []);

  const onRejectRow = useCallback((row: CampaignApprovalRow) => {
    setRejectTargetIds([row.id]);
  }, []);

  const columns = useMemo(
    () =>
      createCampaignApprovalColumns({
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

  const approveModalTitle = "Approve campaign?";
  const approveModalMessage =
    confirmApprove && confirmApprove.length > 1
      ? `Approve ${confirmApprove.length} campaigns?`
      : "Approve this campaign?";

  const rejectSubtitle =
    rejectTargetIds && rejectTargetIds.length > 1
      ? `Reject ${rejectTargetIds.length} campaigns?`
      : rejectTargetIds
        ? "Reject this campaign?"
        : undefined;

  const missingTenant = !tenantId;

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Campaign approval">
      <header className="flex shrink-0 flex-col gap-2 border-b border-outline-variant/15 bg-surface px-6 py-4">
        <nav className="flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
          <span>Approval</span>
          <span className="mx-2">/</span>
          <span className="text-primary">Campaigns</span>
        </nav>
        <div className="flex items-center gap-2">
          <IconBolt className="h-7 w-7 shrink-0 text-primary" aria-hidden />
          <div>
            <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Campaign approval</h1>
            <p className="mt-0.5 max-w-2xl text-sm text-on-surface-variant">
              Review pending campaigns (spec 6.3). Approve or reject with a required reason.
            </p>
          </div>
        </div>
      </header>

      <div className="w-full flex-1 space-y-4 p-6">
        {missingTenant && (
          <div className="rounded-xl border border-error/25 bg-error/5 px-4 py-3 text-sm text-error">
            Missing tenantId — configure <code className="text-xs">Dcms:Estore</code> in Umbraco appsettings or pass mount options.
          </div>
        )}
        {loadError && (
          <div className="rounded-xl border border-error/25 bg-error/5 px-4 py-3 text-sm text-error">{loadError}</div>
        )}
        {loading && (
          <div className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest px-4 py-6 text-center text-sm text-on-surface-variant">
            Loading pending campaigns…
          </div>
        )}
        {!loading && !missingTenant && (
          <DataTable
            columns={columns}
            data={pendingRows}
            globalFilterPlaceholder="Search by name, code, type, submitter…"
            getRowId={(row) => row.id}
            rowSelection={rowSelection}
            onRowSelectionChange={setRowSelection}
          />
        )}

        {selectedCount > 0 && (
          <div className="sticky bottom-3 z-30 flex flex-wrap items-center gap-3 rounded-2xl border border-outline-variant/30 bg-surface-container-lowest px-4 py-3 shadow-lg ring-1 ring-black/5">
            <span className="text-xs font-medium text-on-surface">
              <span className="font-bold tabular-nums text-primary">{selectedCount}</span>
              {` campaign${selectedCount === 1 ? "" : "s"} selected`}
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
        onConfirm={() => confirmApprove && void applyApprove(confirmApprove)}
      />

      <RejectionReasonModal
        open={Boolean(rejectTargetIds?.length)}
        subtitle={rejectSubtitle}
        onCancel={() => setRejectTargetIds(null)}
        onConfirm={(reason) => rejectTargetIds && void applyReject(rejectTargetIds, reason)}
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
