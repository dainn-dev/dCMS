import { useEffect, useMemo, useRef, useState } from "react";
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

type PromoType = "standard" | "shareable" | "account-bound";

type PromotionsPageProps = {
  onCreatePromo?: (type: PromoType) => void;
  onEditPromo?: (row: PromoListRow) => void;
  onViewCodes?: (row: PromoListRow) => void;
  onOpenExclusionList?: () => void;
};

const LS_KEY = "dcms.estore.promoCodesList.v1";

const INITIAL_PROMO_ROWS: PromoListRow[] = [
  {
    id: "1",
    promoType: "Flash Sale",
    discount: "Percentage",
    value: "25%",
    minSpend: "$120.00",
    code: "FLASH25OFF",
    scheduleStart: "May 12, 10:00",
    scheduleEnd: "to May 14, 23:59",
    activeDot: "live",
    status: "approved",
    usedPct: 64,
  },
  {
    id: "2",
    promoType: "Seasonal",
    discount: "Fixed",
    value: "$50.00",
    minSpend: "$250.00",
    code: "SUMMER50",
    scheduleStart: "Jun 01, 00:00",
    scheduleEnd: "to Aug 31, 23:59",
    activeDot: "warning",
    status: "pending",
    usedPct: 0,
  },
  {
    id: "3",
    promoType: "Loyalty",
    discount: "Fixed",
    value: "$15.00",
    minSpend: "$50.00",
    code: "WELCOME15",
    scheduleStart: "Jan 01, 00:00",
    scheduleEnd: "Permanent",
    activeDot: "live",
    status: "approved",
    usedPct: 92,
  },
  {
    id: "4",
    promoType: "Special Event",
    discount: "Shipping",
    value: "Free",
    minSpend: "$30.00",
    code: "FREESHIP",
    scheduleStart: "Apr 01, 10:00",
    scheduleEnd: "to Apr 15, 23:59",
    activeDot: "off",
    status: "expired",
    usedPct: 100,
  },
];

const ACTIVE_DOTS = new Set<PromoListRow["activeDot"]>(["live", "warning", "off"]);
const STATUSES = new Set<PromoListRow["status"]>(["approved", "pending", "expired"]);

function loadPromoRows(): PromoListRow[] {
  try {
    const raw = localStorage.getItem(LS_KEY);
    if (!raw) return [...INITIAL_PROMO_ROWS];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [...INITIAL_PROMO_ROWS];
    const out = parsed
      .filter((x): x is Record<string, unknown> => !!x && typeof x === "object")
      .map((r) => {
        const ad = r.activeDot as string;
        const st = r.status as string;
        return {
          id: String(r.id ?? ""),
          promoType: String(r.promoType ?? ""),
          discount: String(r.discount ?? ""),
          value: String(r.value ?? ""),
          minSpend: String(r.minSpend ?? ""),
          code: String(r.code ?? ""),
          scheduleStart: String(r.scheduleStart ?? ""),
          scheduleEnd: String(r.scheduleEnd ?? ""),
          activeDot: ACTIVE_DOTS.has(ad as PromoListRow["activeDot"]) ? (ad as PromoListRow["activeDot"]) : "off",
          status: STATUSES.has(st as PromoListRow["status"]) ? (st as PromoListRow["status"]) : "pending",
          usedPct: typeof r.usedPct === "number" && !Number.isNaN(r.usedPct) ? r.usedPct : 0,
        };
      })
      .filter((r) => r.id && r.code);
    return out.length > 0 ? out : [...INITIAL_PROMO_ROWS];
  } catch {
    return [...INITIAL_PROMO_ROWS];
  }
}

function persistPromoRows(rows: PromoListRow[]) {
  try {
    localStorage.setItem(LS_KEY, JSON.stringify(rows));
  } catch {
    /* ignore */
  }
}

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

export function PromotionsPage({ onCreatePromo, onEditPromo, onViewCodes, onOpenExclusionList }: PromotionsPageProps) {
  const [rows, setRows] = useState<PromoListRow[]>(() => loadPromoRows());
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);

  const columns = useMemo(
    () =>
      createPromotionColumns(
        (id) => {
          const row = rows.find((r) => r.id === id);
          if (row) onEditPromo?.(row);
        },
        (id) => setDeleteTargetId(id),
        (id) => {
          const row = rows.find((r) => r.id === id);
          if (row) onViewCodes?.(row);
        }
      ),
    [onEditPromo, onViewCodes, rows]
  );

  const deleteTarget = rows.find((r) => r.id === deleteTargetId);

  function confirmDeletePromo() {
    if (!deleteTargetId) return;
    setRows((prev) => {
      const next = prev.filter((r) => r.id !== deleteTargetId);
      persistPromoRows(next);
      return next;
    });
    setDeleteTargetId(null);
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
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Promotion code">
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
        </div>

        <div className="relative" ref={actionsRef}>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:opacity-90"
            onClick={() => setActionsOpen((o) => !o)}
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
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={() => {
                  setActionsOpen(false);
                  void exportPromoCodesToXlsx(rows);
                }}
              >
                <IconDownload className="h-4 w-4 shrink-0 text-secondary" />
                Export to Excel
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

      {deleteTargetId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete promo code</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Delete <strong className="text-on-surface">{deleteTarget?.code ?? deleteTargetId}</strong> (
                  {deleteTarget?.promoType ?? "promotion"})? This cannot be undone. The list is stored in the browser for
                  this demo.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteTargetId(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={confirmDeletePromo}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
