import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import {
  IconAddCircle,
  IconChevronDown,
  IconDelete,
  IconDownload,
  IconFilterList,
  IconWarning,
} from "../../orders/icons";
import { createPromotionColumns } from "../promotions-columns";
import type { PromoListRow } from "../promotions-columns";
import { exportPromoCodesToXlsx } from "../exportPromoCodesXlsx";
import { archivePromoCode, fetchAllPromoCodesForExport, fetchPromoCodes } from "../api/promoCodesApi";

type PromoType = "standard" | "shareable" | "account-bound";

type PromotionsPageProps = {
  onCreatePromo?: (type: PromoType) => void;
  onEditPromo?: (row: PromoListRow) => void;
  onViewCodes?: (row: PromoListRow) => void;
  onOpenExclusionList?: () => void;
  tenantId?: string;
  authToken?: string;
};

const PROMO_TYPES: { value: PromoType; label: string; description: string }[] = [
  {
    value: "standard",
    label: "Standard Promo Code",
    description: "A single code redeemable by any customer.",
  },
  {
    value: "shareable",
    label: "Multiple Shareable Promo Codes",
    description: "A batch of unique codes that can be shared freely.",
  },
  {
    value: "account-bound",
    label: "Multiple Account Bound Promo Codes",
    description: "Unique codes each bound to a specific customer account.",
  },
];

export function PromotionsPage({
  onCreatePromo,
  onEditPromo,
  onViewCodes,
  onOpenExclusionList,
  tenantId,
  authToken,
}: PromotionsPageProps) {
  const [rows, setRows] = useState<PromoListRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [archiveTargetId, setArchiveTargetId] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const refetch = useCallback(() => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    fetchPromoCodes(tenantId, { page: 1, pageSize: 200 }, authToken)
      .then(({ rows: next }) => setRows(next))
      .catch((err: unknown) => {
        setRows([]);
        setError(err instanceof Error ? err.message : "Failed to load promo codes");
      })
      .finally(() => setLoading(false));
  }, [tenantId, authToken]);

  useEffect(() => {
    if (!tenantId) {
      setRows([]);
      setError("Missing tenantId for Promotions API.");
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    fetchPromoCodes(tenantId, { page: 1, pageSize: 200 }, authToken)
      .then(({ rows: next }) => {
        if (!cancelled) setRows(next);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setRows([]);
          setError(err instanceof Error ? err.message : "Failed to load promo codes");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, authToken]);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3200);
    return () => clearTimeout(t);
  }, [toast]);

  const columns = useMemo(
    () =>
      createPromotionColumns(
        (id) => {
          const row = rows.find((r) => r.id === id);
          if (row) onEditPromo?.(row);
        },
        (id) => setArchiveTargetId(id),
        (id) => {
          const row = rows.find((r) => r.id === id);
          if (row) onViewCodes?.(row);
        }
      ),
    [onEditPromo, onViewCodes, rows]
  );

  const archiveTarget = rows.find((r) => r.id === archiveTargetId);

  async function confirmArchivePromo() {
    if (!tenantId || !archiveTargetId) return;
    try {
      await archivePromoCode(tenantId, archiveTargetId, authToken);
      setArchiveTargetId(null);
      setToast("Promo code archived.");
      refetch();
    } catch (e: unknown) {
      setToast(e instanceof Error ? e.message : "Archive failed");
    }
  }

  async function handleExport() {
    if (!tenantId) {
      setToast("Missing tenantId for export.");
      return;
    }
    setExporting(true);
    try {
      const { rows: exportRows, limited } = await fetchAllPromoCodesForExport(tenantId, {}, authToken);
      await exportPromoCodesToXlsx(exportRows);
      if (limited) {
        setToast("Export includes up to 1000 rows. Download again after filters are available if you need more.");
      } else {
        setToast("Export started.");
      }
    } catch (e: unknown) {
      setToast(e instanceof Error ? e.message : "Export failed");
    } finally {
      setExporting(false);
    }
  }

  // ── Actions dropdown ──────────────────────────────────────────────────────
  const [actionsOpen, setActionsOpen] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function handler(e: MouseEvent) {
      const root = actionsRef.current;
      if (!root) return;
      const path = e.composedPath();
      if (path.includes(root)) return;
      setActionsOpen(false);
    }
    document.addEventListener("click", handler, true);
    return () => document.removeEventListener("click", handler, true);
  }, []);

  // ── Type-selection modal ──────────────────────────────────────────────────
  const [typeModalOpen, setTypeModalOpen] = useState(false);
  const [selectedType, setSelectedType] = useState<PromoType>("standard");

  function openTypeModal() {
    setActionsOpen(false);
    setSelectedType("standard");
    setTypeModalOpen(true);
  }

  function handleCreate() {
    setTypeModalOpen(false);
    onCreatePromo?.(selectedType);
  }

  return (
    <div className="-m-6 relative flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Promotion code">
      {loading && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-surface/60 backdrop-blur-sm">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      )}
      <header className="relative z-20 flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Promo Codes</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Promotion Code Manager</h1>
          <p className="text-sm text-on-surface-variant">
            Create, schedule, and audit promotion codes across your storefront campaigns.
          </p>
          {error && (
            <p className="rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-xs font-medium text-error" role="alert">
              {error}
            </p>
          )}
        </div>

        <div className="relative" ref={actionsRef}>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:opacity-90 disabled:opacity-50"
            onClick={() => setActionsOpen((o) => !o)}
            disabled={!tenantId}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Actions
            <IconChevronDown className={`h-3.5 w-3.5 shrink-0 transition-transform ${actionsOpen ? "rotate-180" : ""}`} />
          </button>
          {actionsOpen && (
            <div
              className="absolute right-0 top-full z-30 mt-1 w-56 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl"
              onMouseDown={(e) => e.stopPropagation()}
            >
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={openTypeModal}
              >
                <IconAddCircle className="h-4 w-4 shrink-0 text-primary" />
                Add New Promo Code
              </button>
              <div className="my-1 border-t border-outline-variant/10" />
              <button
                type="button"
                disabled={exporting || !tenantId}
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors disabled:opacity-50"
                onClick={() => {
                  setActionsOpen(false);
                  void handleExport();
                }}
              >
                <IconDownload className="h-4 w-4 shrink-0 text-secondary" />
                {exporting ? "Exporting…" : "Export to Excel"}
              </button>
              <div className="my-1 border-t border-outline-variant/10" />
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={() => {
                  setActionsOpen(false);
                  onOpenExclusionList?.();
                }}
              >
                <IconFilterList className="h-4 w-4 shrink-0 text-on-surface-variant" />
                Promotions Exclusion List
              </button>
            </div>
          )}
        </div>
      </header>

      <div className="w-full flex-1 p-6">
        <DataTable
          columns={columns}
          data={rows}
          globalFilterPlaceholder="Search by promo type, code, or status…"
          columnLabels={{ schedule: "Schedule", activeDot: "Active", usedPct: "% Used" }}
        />
      </div>

      {typeModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[480px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="border-b border-outline-variant/10 px-6 py-5">
              <h3 className="text-base font-bold text-on-surface">Add New Promotion Code</h3>
              <p className="mt-1 text-xs text-on-surface-variant">Select the type of promotion code to create.</p>
            </div>

            <div className="space-y-2 px-6 py-6">
              {PROMO_TYPES.map((pt) => (
                <label
                  key={pt.value}
                  className={`flex cursor-pointer items-start gap-3 rounded-xl border p-4 transition-colors select-none ${
                    selectedType === pt.value
                      ? "border-primary/40 bg-primary/5"
                      : "border-outline-variant/20 hover:border-primary/20 hover:bg-surface-container-low"
                  }`}
                >
                  <input
                    type="radio"
                    name="promoType"
                    value={pt.value}
                    checked={selectedType === pt.value}
                    onChange={() => setSelectedType(pt.value)}
                    className="mt-0.5 accent-primary"
                  />
                  <div>
                    <p className="text-xs font-bold text-on-surface">{pt.label}</p>
                    <p className="mt-0.5 text-xs text-on-surface-variant leading-relaxed">{pt.description}</p>
                  </div>
                </label>
              ))}
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setTypeModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
                onClick={handleCreate}
              >
                Create Promotional Code
              </button>
            </div>
          </div>
        </div>
      )}

      {archiveTargetId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Archive promo code</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Archive <strong className="text-on-surface">{archiveTarget?.code ?? archiveTargetId}</strong> (
                  {archiveTarget?.promoType ?? "promotion"})? Archived codes are kept for audit but no longer apply at
                  checkout.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setArchiveTargetId(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={() => void confirmArchivePromo()}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Archive
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
