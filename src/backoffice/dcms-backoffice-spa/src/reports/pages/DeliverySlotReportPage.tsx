import type { ColumnDef } from "@tanstack/react-table";
import { useMemo } from "react";
import { DataTable } from "../../orders/components/DataTable";

// DAI-711: delivery slot utilization endpoint is stubbed server-side for now.
type Props = { tenantId?: string; storeId?: string; authToken?: string };

type Row = { slotId: string; date: string; booked: number; capacity: number };

export function DeliverySlotReportPage(_: Props) {
  const columns: ColumnDef<Row>[] = useMemo(
    () => [
      { accessorKey: "date", header: "Date" },
      { accessorKey: "slotId", header: "Slot" },
      { accessorKey: "booked", header: "Booked", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("booked")}</span> },
      { accessorKey: "capacity", header: "Capacity", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("capacity")}</span> },
    ],
    [],
  );

  return (
    <section className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Delivery slots</h1>
        <p className="text-xs text-on-surface-variant">Utilization report (coming soon).</p>
      </div>

      <div className="rounded-xl border border-outline-variant/30 bg-surface">
        <DataTable columns={columns} data={[]} loading={false} />
      </div>
    </section>
  );
}

