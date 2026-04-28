import type { ColumnDef } from "@tanstack/react-table";
import { useEffect, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { fetchRestockSubscriptions, type RestockSubscriptionAggRow } from "../api/reportsApi";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

export function RestockSubscriptionsReportPage({ tenantId, storeId, authToken }: Props) {
  const [rows, setRows] = useState<RestockSubscriptionAggRow[]>([]);
  const [loading, setLoading] = useState(false);

  const columns: ColumnDef<RestockSubscriptionAggRow>[] = useMemo(
    () => [
      { accessorKey: "productId", header: "Product ID", cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("productId")}</span> },
      { accessorKey: "subscriptions", header: "Subscriptions", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("subscriptions")}</span> },
      { accessorKey: "fulfilled", header: "Fulfilled", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("fulfilled")}</span> },
    ],
    [],
  );

  useEffect(() => {
    let cancelled = false;
    async function run() {
      if (!tenantId) return;
      setLoading(true);
      try {
        const apiRows = await fetchRestockSubscriptions(tenantId, { storeId: storeId ?? "all" }, authToken);
        if (cancelled) return;
        setRows(apiRows);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    run();
    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken]);

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Restock subscriptions</h1>
        <p className="text-xs text-on-surface-variant">Aggregated from analytics DB.</p>
      </div>

      <div className="rounded-xl border border-outline-variant/30 bg-surface">
        <DataTable columns={columns} data={rows} loading={loading} />
      </div>
    </section>
  );
}

