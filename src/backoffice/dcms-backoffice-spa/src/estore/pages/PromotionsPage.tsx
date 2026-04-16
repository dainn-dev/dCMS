import { useMemo } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconAddCircle } from "../../orders/icons";
import { createPromotionColumns } from "../promotions-columns";
import type { PromoListRow } from "../promotions-columns";

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

export function PromotionsPage() {
  const columns = useMemo(
    () => createPromotionColumns(
      (id) => console.info("[Promotions] Edit", id),
      (id) => console.info("[Promotions] Delete", id)
    ),
    []
  );

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
        <button
          type="button"
          className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:bg-primary-container"
          onClick={() => console.info("[Promotions] Create new promo (placeholder)")}
        >
          <IconAddCircle className="h-4 w-4 shrink-0" />
          Create New Promo
        </button>
      </header>

      <div className="mx-auto w-full max-w-[1600px] flex-1 p-6">
        <DataTable
          columns={columns}
          data={PROMO_ROWS}
          globalFilterPlaceholder="Search by promo type, code, or status…"
          columnLabels={{ schedule: "Schedule", activeDot: "Active", usedPct: "% Used" }}
        />
      </div>
    </div>
  );
}
