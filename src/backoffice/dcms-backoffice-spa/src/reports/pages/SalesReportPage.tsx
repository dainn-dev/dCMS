import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import { fetchSalesGrouped, type SalesGroupBy, type SalesGroupRow } from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";
import { ReportFilterField, inputClass } from "../shared/ReportFilterPanel";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = {
  id: string;
  key: string;
  orders: number | null;
  gross: string;
};

const GROUP_BY_OPTIONS: { value: SalesGroupBy; label: string }[] = [
  { value: "store", label: "Store" },
  { value: "product", label: "Product" },
  { value: "category", label: "Category" },
];

function money(n: number): string {
  return n.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export function SalesReportPage({ tenantId, storeId, authToken }: Props) {
  const [groupBy, setGroupBy] = useState<SalesGroupBy>("store");

  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "key",
        header: groupBy === "store" ? "Store" : groupBy === "product" ? "Product" : "Category",
        cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("key")}</span>,
      },
      {
        accessorKey: "orders",
        header: "Orders",
        cell: ({ row }) => <span className="tabular-nums text-xs">{(row.getValue("orders") as number | null) ?? "—"}</span>,
      },
      {
        accessorKey: "gross",
        header: "Gross",
        cell: ({ row }) => <span className="tabular-nums text-xs font-semibold">{row.getValue("gross")}</span>,
      },
    ],
    [groupBy],
  );

  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) return [];
      const apiRows: SalesGroupRow[] = await fetchSalesGrouped(
        tenantId,
        {
          dateFrom: filters.dateFrom,
          dateTo: filters.dateTo,
          storeId: filters.storeScope !== "all" ? filters.storeScope : storeId ?? "all",
        },
        groupBy,
        authToken,
      );
      return apiRows.map((r, i) => ({
        id: `${r.key}-${i}`,
        key: r.key,
        orders: r.orders ?? null,
        gross: money(r.gross),
      }));
    },
    [tenantId, storeId, authToken, groupBy],
  );

  const headerExtras = (
    <select
      className={inputClass}
      value={groupBy}
      onChange={(e) => setGroupBy(e.target.value as SalesGroupBy)}
      aria-label="Group by"
    >
      {GROUP_BY_OPTIONS.map((o) => (
        <option key={o.value} value={o.value}>
          Group by: {o.label}
        </option>
      ))}
    </select>
  );

  return (
    <ReportView<UiRow>
      breadcrumb="Sales"
      title="Sales report"
      description="Aggregated sales by store / product / category. Backed by analytics DB (DAI-685)."
      exportSheetName="Sales"
      exportFilename={`sales-${groupBy}.xlsx`}
      columns={columns}
      exportHeaders={[GROUP_BY_OPTIONS.find((o) => o.value === groupBy)?.label ?? "Key", "Orders", "Gross"]}
      toExportRow={(r) => [r.key, r.orders === null ? "" : String(r.orders), r.gross]}
      loadData={loadData}
      headerExtras={headerExtras}
    />
  );
}

// Re-export so legacy callers don't break (file used to export ReportFilterField as a value).
export { ReportFilterField };
