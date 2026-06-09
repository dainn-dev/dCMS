import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import { fetchDeliverySlots, type DeliverySlotRow } from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";
import { ReportFilterField, inputClass } from "../shared/ReportFilterPanel";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = DeliverySlotRow & { id: string };

// BRD §2.2 — delivery modes (default "All modes"). Values are the literal fulfillment DeliveryMode
// strings (FulfillmentGroupingRow.ValidDeliveryModes — note the spaces), so the API can match directly.
const DELIVERY_MODE_OPTIONS = [
  { value: "all", label: "All modes" },
  { value: "Local Delivery", label: "Local Delivery" },
  { value: "Store Collection", label: "Store Collection" },
  { value: "Overseas Delivery", label: "Overseas Delivery" },
];

const num = (v: unknown) => Number(v ?? 0).toLocaleString("en-US");
const day = (v: unknown) => String(v ?? "").slice(0, 10);

/**
 * DAI-711: Delivery slot utilization report (BRD §1–§8).
 * Reads GET /reports/delivery-slots (analytics projection of fulfillment slots + bookings).
 * Rows are grouped by Slot Name + First Day Of The Week + Delivery Mode (BR03).
 */
export function DeliverySlotReportPage({ tenantId, authToken }: Props) {
  // Extra filter (BRD §2.2) — lives outside ReportView's built-in date filters.
  const [deliveryMode, setDeliveryMode] = useState("all");

  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "slotName",
        header: "Delivery Slot Name",
        cell: ({ row }) => <span className="text-xs font-semibold text-on-surface">{row.getValue("slotName")}</span>,
      },
      {
        accessorKey: "firstDayOfWeek",
        header: "First Day Of The Week",
        cell: ({ row }) => <span className="text-xs tabular-nums">{day(row.getValue("firstDayOfWeek"))}</span>,
      },
      {
        accessorKey: "noOfSlots",
        header: "No of Slots",
        cell: ({ row }) => <span className="text-xs tabular-nums">{num(row.getValue("noOfSlots"))}</span>,
      },
      {
        accessorKey: "noOfDeliveries",
        header: "No of Deliveries",
        cell: ({ row }) => <span className="text-xs tabular-nums">{num(row.getValue("noOfDeliveries"))}</span>,
      },
      {
        accessorKey: "deliveryMode",
        header: "Delivery Mode",
        cell: ({ row }) => <span className="text-xs">{row.getValue("deliveryMode")}</span>,
      },
    ],
    [],
  );

  // deliveryMode is in the deps so changing it (or clicking Search) re-runs the fetch.
  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) return [];
      const rows = await fetchDeliverySlots(
        tenantId,
        { dateFrom: filters.dateFrom, dateTo: filters.dateTo },
        deliveryMode,
        authToken,
      );
      return rows.map((r, i) => ({ ...r, id: `${r.slotName}|${r.firstDayOfWeek}|${r.deliveryMode}|${i}` }));
    },
    [tenantId, authToken, deliveryMode],
  );

  return (
    <ReportView<UiRow>
      breadcrumb="Delivery slots"
      title="Delivery slots"
      description="Utilization per delivery slot — slots configured vs. orders booked, by delivery mode (BRD §1)."
      exportSheetName="DeliverySlots"
      exportFilename="delivery-slots.xlsx"
      columns={columns}
      exportHeaders={["Delivery Slot Name", "First Day Of The Week", "No of Slots", "No of Deliveries", "Delivery Mode"]}
      toExportRow={(r) => [
        r.slotName,
        day(r.firstDayOfWeek),
        String(r.noOfSlots),
        String(r.noOfDeliveries),
        r.deliveryMode,
      ]}
      loadData={loadData}
      showStoreFilter={false}
      onReset={() => setDeliveryMode("all")}
      emptyMessage="No delivery-slot bookings for the selected period (the slot-booking analytics projection is still being populated)."
      extraFilters={
        <ReportFilterField label="Delivery mode" htmlFor="delivery-mode">
          <select
            id="delivery-mode"
            className={inputClass}
            value={deliveryMode}
            onChange={(e) => setDeliveryMode(e.target.value)}
          >
            {DELIVERY_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </ReportFilterField>
      }
    />
  );
}
