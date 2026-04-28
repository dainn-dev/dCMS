import type { ColumnDef } from "@tanstack/react-table";
import { useEffect, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { fetchAbandonCart, type AbandonCartRow } from "../api/reportsApi";
import { ReportFilterPanel, type ReportFilterField } from "../shared/ReportFilterPanel";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = { cartId: string; createdAt: string };

export function AbandonCartReportPage({ tenantId, storeId, authToken }: Props) {
  const [dateFrom, setDateFrom] = useState(() => new Date(Date.now() - 7 * 86400_000).toISOString().slice(0, 10));
  const [dateTo, setDateTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [rows, setRows] = useState<UiRow[]>([]);
  const [loading, setLoading] = useState(false);

  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      { accessorKey: "cartId", header: "Cart ID", cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("cartId")}</span> },
      { accessorKey: "createdAt", header: "Created at", cell: ({ row }) => <span className="text-xs tabular-nums">{row.getValue("createdAt")}</span> },
    ],
    [],
  );

  useEffect(() => {
    let cancelled = false;
    async function run() {
      if (!tenantId) return;
      setLoading(true);
      try {
        const apiRows: AbandonCartRow[] = await fetchAbandonCart(
          tenantId,
          { dateFrom, dateTo, storeId: storeId ?? "all" },
          authToken,
        );
        if (cancelled) return;
        setRows(apiRows.map((r) => ({ cartId: r.cartId, createdAt: r.createdAt })));
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    run();
    return () => {
      cancelled = true;
    };
  }, [tenantId, authToken, storeId, dateFrom, dateTo]);

  const filterFields: ReportFilterField[] = [
    { id: "dateFrom", label: "Date from", kind: "date", value: dateFrom, onChange: setDateFrom },
    { id: "dateTo", label: "Date to", kind: "date", value: dateTo, onChange: setDateTo },
  ];

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Abandon cart</h1>
        <p className="text-xs text-on-surface-variant">Carts created but not confirmed within 24h.</p>
      </div>

      <ReportFilterPanel fields={filterFields} />

      <div className="rounded-xl border border-outline-variant/30 bg-surface">
        <DataTable columns={columns} data={rows} loading={loading} />
      </div>
    </section>
  );
}

