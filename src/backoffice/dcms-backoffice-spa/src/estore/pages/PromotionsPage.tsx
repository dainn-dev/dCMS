import { useEffect, useMemo, useRef, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconAddCircle, IconChevronDown, IconDownload, IconFilterList } from "../../orders/icons";
import { createPromotionColumns } from "../promotions-columns";
import type { PromoListRow } from "../promotions-columns";

type PromoType = "standard" | "shareable" | "account-bound";

type PromotionsPageProps = {
  onCreatePromo?: (type: PromoType) => void;
  onEditPromo?: (row: PromoListRow) => void;
  onViewCodes?: (row: PromoListRow) => void;
  onOpenExclusionList?: () => void;
};

function exportPromoCodesToCSV(rows: PromoListRow[]) {
  const bom = "\uFEFF";
  const headers = [
    "Promo Type", "Promo Value", "Minimum Spend", "Code",
    "Start Date / Time", "End Date / Time", "Redemption Limit", "Used", "% Used",
  ];
  const dataRows = rows.map((r) => {
    const redemptionLimit = 500;
    const used = Math.round((r.usedPct / 100) * redemptionLimit);
    return [
      r.promoType,
      r.value,
      r.minSpend,
      r.code,
      r.scheduleStart,
      r.scheduleEnd.replace(/^to\s*/, ""),
      String(redemptionLimit),
      String(used),
      `${r.usedPct}%`,
    ].map((c) => `"${String(c).replace(/"/g, '""')}"`).join(",");
  });
  const csv = bom + [headers.join(","), ...dataRows].join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "promo-codes-export.csv";
  a.click();
  URL.revokeObjectURL(url);
}

const PROMO_ROWS: PromoListRow[] = [
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
  const columns = useMemo(
    () => createPromotionColumns(
      (id) => onEditPromo?.(PROMO_ROWS.find((r) => r.id === id)!),
      (id) => console.info("[Promotions] Delete", id),
      (id) => onViewCodes?.(PROMO_ROWS.find((r) => r.id === id)!)
    ),
    [onEditPromo, onViewCodes]
  );

  // ── Actions dropdown ──────────────────────────────────────────────────────
  const [actionsOpen, setActionsOpen] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node)) setActionsOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
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
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
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

        {/* Actions dropdown */}
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
            <div className="absolute right-0 top-full z-20 mt-1 w-56 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
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
                onClick={() => { setActionsOpen(false); exportPromoCodesToCSV(PROMO_ROWS); }}
              >
                <IconDownload className="h-4 w-4 shrink-0 text-secondary" />
                Export to Excel
              </button>
              <div className="my-1 border-t border-outline-variant/10" />
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={() => { setActionsOpen(false); onOpenExclusionList?.(); }}
              >
                <IconFilterList className="h-4 w-4 shrink-0 text-on-surface-variant" />
                Promotions Exclusion List
              </button>
            </div>
          )}
        </div>
      </header>

      <div className="mx-auto w-full max-w-[1600px] flex-1 p-6">
        <DataTable
          columns={columns}
          data={PROMO_ROWS}
          globalFilterPlaceholder="Search by promo type, code, or status…"
          columnLabels={{ schedule: "Schedule", activeDot: "Active", usedPct: "% Used" }}
        />
      </div>

      {/* ── Type-selection modal ─────────────────────────────────────────── */}
      {typeModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[480px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            {/* Header */}
            <div className="border-b border-outline-variant/10 px-6 py-5">
              <h3 className="text-base font-bold text-on-surface">Add New Promotion Code</h3>
              <p className="mt-1 text-xs text-on-surface-variant">
                Select the type of promotion code to create.
              </p>
            </div>

            {/* Radio options */}
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
                    <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">{pt.description}</p>
                  </div>
                </label>
              ))}
            </div>

            {/* Footer */}
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
    </div>
  );
}
