import type { ColumnDef } from "@tanstack/react-table";
import { useEffect, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconDownload } from "../../orders/icons";
import { fetchSalesGrouped, type SalesGroupBy, type SalesGroupRow } from "../api/reportsApi";
import { exportReportRowsToXlsx } from "../shared/exportReportRowsToXlsx";
import { ReportFilterPanel, inputClass, type ReportFilterField } from "../shared/ReportFilterPanel";
import { useReportExportState } from "../shared/useReportExport";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = { key: string; orders: number | null; gross: string };

const GROUP_BY_OPTIONS: { value: SalesGroupBy; label: string }[] = [
  { value: "store", label: "Store" },
  { value: "product", label: "Product" },
  { value: "category", label: "Category" },
];

function money(n: number) {
  return n.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

export function SalesReportPage({ tenantId, storeId, authToken }: Props) {
  const [groupBy, setGroupBy] = useState<SalesGroupBy>("store");
  const [dateFrom, setDateFrom] = useState(() => new Date(Date.now() - 7 * 86400_000).toISOString().slice(0, 10));
  const [dateTo, setDateTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [rows, setRows] = useState<UiRow[]>([]);
  const [loading, setLoading] = useState(false);
  const exportState = useReportExportState("sales");

  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "key",
        header: groupBy === "store" ? "Store" : groupBy === "product" ? "Product" : "Category",
        cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("key")}</span>,
      },
      { accessorKey: "orders", header: "Orders", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("orders") ?? "-"}</span> },
      { accessorKey: "gross", header: "Gross", cell: ({ row }) => <span className="tabular-nums text-xs font-semibold">{row.getValue("gross")}</span> },
    ],
    [groupBy],
  );

  useEffect(() => {
    let cancelled = false;
    async function run() {
      if (!tenantId) return;
      setLoading(true);
      try {
        const apiRows: SalesGroupRow[] = await fetchSalesGrouped(
          tenantId,
          { dateFrom, dateTo, storeId: storeId ?? "all" },
          groupBy,
          authToken,
        );
        if (cancelled) return;
        setRows(apiRows.map((r) => ({ key: r.key, orders: r.orders ?? null, gross: money(r.gross) })));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    run();
    return () => {
      cancelled = true;
    };
  }, [tenantId, authToken, storeId, dateFrom, dateTo, groupBy]);

  const filterFields: ReportFilterField[] = [
    { id: "dateFrom", label: "Date from", kind: "date", value: dateFrom, onChange: setDateFrom },
    { id: "dateTo", label: "Date to", kind: "date", value: dateTo, onChange: setDateTo },
  ];

  return (
    <section className="space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Sales report</h1>
          <p className="text-xs text-on-surface-variant">Backed by analytics DB (DAI-685).</p>
        </div>
        <div className="flex items-center gap-2">
          <select className={inputClass} value={groupBy} onChange={(e) => setGroupBy(e.target.value as SalesGroupBy)}>
            {GROUP_BY_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                Group by: {o.label}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="inline-flex items-center gap-2 rounded-md bg-primary px-3 py-2 text-xs font-semibold text-on-primary hover:bg-primary/90 disabled:opacity-50"
            disabled={exportState.exporting || rows.length === 0}
            onClick={async () => {
              await exportReportRowsToXlsx(`sales-${groupBy}`, rows);
            }}
          >
            <IconDownload className="h-4 w-4" />
            Export
          </button>
        </div>
      </div>

      <ReportFilterPanel fields={filterFields} />

      <div className="rounded-xl border border-outline-variant/30 bg-surface">
        <DataTable columns={columns} data={rows} loading={loading} />
      </div>
    </section>
  );
}

