import { useMemo } from "react";
import { DataTable } from "../../orders/components/DataTable";
import {
  IconAdd,
  IconAnalytics,
  IconBolt,
  IconElectricMoped,
  IconFilterList,
  IconShipping,
  IconStore,
} from "../../orders/icons";
import { createFulfillmentColumns } from "../fulfillment-columns";
import type { FulfillmentMethodRow } from "../fulfillment-columns";

const METHOD_ROWS: FulfillmentMethodRow[] = [
  {
    id: "1",
    name: "Standard Shipping",
    type: "Ground",
    baseCost: "$5.99",
    freeThreshold: "$49.00",
    deliveryTime: "3-5 Business Days",
    active: true,
    Icon: IconShipping,
  },
  {
    id: "2",
    name: "Next Day Delivery",
    type: "Air Express",
    baseCost: "$19.99",
    freeThreshold: "N/A",
    deliveryTime: "24 Hours",
    active: true,
    Icon: IconBolt,
  },
  {
    id: "3",
    name: "Express Courier",
    type: "Last Mile",
    baseCost: "$12.50",
    freeThreshold: "$150.00",
    deliveryTime: "2-4 Hours",
    active: true,
    Icon: IconElectricMoped,
  },
  {
    id: "4",
    name: "Store Pickup",
    type: "In-Store",
    baseCost: "Free",
    freeThreshold: "N/A",
    deliveryTime: "Instant (Same Day)",
    active: false,
    Icon: IconStore,
  },
];

export function FulfillmentOptionsPage() {
  const columns = useMemo(
    () => createFulfillmentColumns(
      (id) => console.info("[Fulfillment] Toggle", id),
      (id) => console.info("[Fulfillment] Edit", id),
      (id) => console.info("[Fulfillment] Delete", id)
    ),
    []
  );

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Fulfillment options">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Fulfillment Options</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Fulfillment Options</h1>
          <p className="text-sm text-on-surface-variant">
            Manage logistics, delivery timeframes, and global shipping costs.
          </p>
        </div>
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-4 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant"
            onClick={() => console.info("[Fulfillment] Advanced filters (placeholder)")}
          >
            <IconFilterList className="h-4 w-4 shrink-0" />
            Advanced Filters
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:bg-primary-container"
            onClick={() => console.info("[Fulfillment] Add option (placeholder)")}
          >
            <IconAdd className="h-4 w-4 shrink-0" />
            Add Fulfillment Option
          </button>
        </div>
      </header>

      <div className="flex-1 p-6">
        <div className="mx-auto max-w-[1400px] space-y-8">
          <DataTable
            columns={columns}
            data={METHOD_ROWS}
            globalFilterPlaceholder="Search by method name or type…"
            columnLabels={{ methodName: "Method Name", activeDot: "Status" }}
          />

          {/* Bottom cards */}
          <div className="grid grid-cols-1 gap-8 md:grid-cols-2">
            <div className="group relative flex min-h-[200px] flex-col justify-end overflow-hidden rounded-xl p-8">
              <img
                alt=""
                className="absolute inset-0 h-full w-full object-cover transition-transform duration-700 group-hover:scale-110"
                src="https://lh3.googleusercontent.com/aida-public/AB6AXuDUxyDv8p7QrnrzBsLCJpbkdKda7h8KP30NHrODWWOQ3zC62pUrWRIuiaDmOVvg__b41TPZl2Lhn7qImELsX41Roi8q1V56ja4pho5_F3Ve1nq93goISFVA09v9VFW4MmPyqKed5LiyQwBNC6hA1ZOMa-J35wh3nb9SaY01ZoVZgls_h8zdGSWUu-8KhU6alU523sSGNOrYlKM_YajLfTMWtIaxadFQNPeHr6iC7uYKV9SthUHfer6aOQPeXR2R71iOY7f4ekartyE"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-[#281716] via-[#281716]/40 to-transparent" />
              <div className="relative">
                <span className="text-xs font-bold uppercase tracking-widest text-primary-fixed">
                  Network Optimization
                </span>
                <h3 className="mt-2 text-xl font-bold text-white">Global Routing Rules</h3>
                <p className="mt-2 max-w-sm text-sm text-white/70">
                  Automatically route orders based on warehouse proximity and shipping costs.
                </p>
                <button
                  type="button"
                  className="mt-4 rounded border border-white/20 px-4 py-2 text-xs font-bold uppercase tracking-widest text-white transition-all hover:bg-white hover:text-on-surface"
                  onClick={() => console.info("[Fulfillment] Configure engine (placeholder)")}
                >
                  Configure Engine
                </button>
              </div>
            </div>
            <div className="flex flex-col justify-center rounded-xl border border-outline-variant/10 bg-surface-container-low p-8">
              <h4 className="flex items-center gap-2 text-sm font-bold uppercase tracking-widest text-on-surface">
                <IconAnalytics className="h-4 w-4 shrink-0 text-primary" aria-hidden />
                Fulfillment Insights
              </h4>
              <div className="mt-6 space-y-4">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-on-surface-variant">Top Performer</span>
                  <span className="font-bold text-on-surface">Standard Shipping</span>
                </div>
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-outline-variant/20">
                  <div className="h-full w-[65%] bg-primary" />
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-on-surface-variant">Fastest Method</span>
                  <span className="font-bold text-on-surface">Express Courier</span>
                </div>
                <div className="h-1.5 w-full overflow-hidden rounded-full bg-outline-variant/20">
                  <div className="h-full w-[20%] bg-primary-container" />
                </div>
              </div>
              <p className="mt-6 text-[10px] leading-relaxed text-on-surface-variant">
                Data is based on the last 30 days of shipment tracking. Efficiency score is calculated by the ratio of
                on-time delivery vs. method base cost.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
