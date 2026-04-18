import type { RowSelectionState } from "@tanstack/react-table";
import { useMemo, useState } from "react";
import { DataTable } from "../components/DataTable";
import { exportFailedOrdersToCsv } from "../exportFailedOrders";
import { MOCK_FAILED_ORDERS } from "../failedOrdersMock";
import { FAILED_ORDER_COLUMN_LABELS, createFailedOrderColumns } from "../failed-orders-columns";
import { IconDownload } from "../icons";
import type { FailedOrderStatus } from "../types";

const STATUS_ORDER: FailedOrderStatus[] = [
  "Payment Failed",
  "Address Error",
  "Auth Failed",
  "Stock Error",
  "System Error",
];

type FilterId = "all" | FailedOrderStatus;

type Props = {
  onViewFailedOrder: (orderId: string) => void;
};

export function FailedOrdersPage({ onViewFailedOrder }: Props) {
  const [statusFilter, setStatusFilter] = useState<FilterId>("all");
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({});

  const tabCounts = useMemo(() => {
    const counts: Record<string, number> = { all: MOCK_FAILED_ORDERS.length };
    for (const s of STATUS_ORDER) {
      counts[s] = MOCK_FAILED_ORDERS.filter((o) => o.status === s).length;
    }
    return counts;
  }, []);

  const filteredRows = useMemo(() => {
    if (statusFilter === "all") return MOCK_FAILED_ORDERS;
    return MOCK_FAILED_ORDERS.filter((o) => o.status === statusFilter);
  }, [statusFilter]);

  const columns = useMemo(
    () => createFailedOrderColumns(onViewFailedOrder),
    [onViewFailedOrder]
  );

  const selectedOrderIds = useMemo(
    () => Object.keys(rowSelection).filter((id) => rowSelection[id]),
    [rowSelection]
  );
  const selectedCount = selectedOrderIds.length;

  return (
    <>
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Failed Orders</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Failed Orders
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Monitor and resolve order failures and payment issues.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2"
            onClick={() => exportFailedOrdersToCsv(MOCK_FAILED_ORDERS)}
          >
            <IconDownload />
            Export
          </button>
        </div>
      </div>

      <div className="flex flex-wrap gap-1 border-b border-outline-variant/25">
        <button
          type="button"
          onClick={() => setStatusFilter("all")}
          aria-pressed={statusFilter === "all"}
          className={`px-3 py-2.5 text-xs font-bold uppercase tracking-wide border-b-2 -mb-px transition-colors ${
            statusFilter === "all"
              ? "border-primary text-primary"
              : "border-transparent text-on-surface-variant hover:text-on-surface"
          }`}
        >
          All{" "}
          <span className="ml-1 tabular-nums opacity-80">({tabCounts.all})</span>
        </button>
        {STATUS_ORDER.map((id) => {
          const active = statusFilter === id;
          return (
            <button
              key={id}
              type="button"
              onClick={() => setStatusFilter(id)}
              aria-pressed={active}
              className={`px-3 py-2.5 text-xs font-bold uppercase tracking-wide border-b-2 -mb-px transition-colors whitespace-nowrap ${
                active
                  ? "border-primary text-primary"
                  : "border-transparent text-on-surface-variant hover:text-on-surface"
              }`}
            >
              {id}{" "}
              <span className="ml-1 tabular-nums opacity-80">({tabCounts[id] ?? 0})</span>
            </button>
          );
        })}
      </div>

      <div className="space-y-3">
        <DataTable
          columns={columns}
          data={filteredRows}
          defaultHiddenColumns={["processedBy", "tags", "deliveryOption", "fulfilledDate"]}
          columnLabels={FAILED_ORDER_COLUMN_LABELS}
          globalFilterPlaceholder="Search by order, customer, status…"
          getRowId={(row) => row.orderId}
          rowSelection={rowSelection}
          onRowSelectionChange={setRowSelection}
        />

        {selectedCount > 0 && (
          <div className="sticky bottom-3 z-30 flex flex-wrap items-center gap-3 rounded-2xl border border-outline-variant/30 bg-surface-container-lowest px-4 py-3 shadow-lg ring-1 ring-black/5 transition-opacity duration-200">
            <span className="text-xs font-medium text-on-surface">
              <span className="font-bold tabular-nums text-primary">{selectedCount}</span>
              {` order${selectedCount === 1 ? "" : "s"} selected`}
            </span>
            <button
              type="button"
              className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-outline-variant/40 text-lg leading-none text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface transition-colors"
              aria-label="Deselect all"
              title="Clear selection"
              onClick={() => setRowSelection({})}
            >
              ×
            </button>
            <span className="hidden h-6 w-px shrink-0 bg-outline-variant/40 sm:block" aria-hidden />
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="px-3 py-1.5 text-xs font-bold rounded-lg bg-white border border-outline-variant/40 text-on-surface hover:bg-surface-container-high transition-colors"
                onClick={() => console.info("[FailedOrders] Retry (no API)", selectedOrderIds)}
              >
                Retry
              </button>
              <button
                type="button"
                className="px-3 py-1.5 text-xs font-bold rounded-lg bg-primary text-on-primary hover:opacity-90 transition-opacity"
                onClick={() => {
                  console.info("[FailedOrders] Mark Resolved (no API)", selectedOrderIds);
                  setRowSelection({});
                }}
              >
                Mark Resolved
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  );
}
