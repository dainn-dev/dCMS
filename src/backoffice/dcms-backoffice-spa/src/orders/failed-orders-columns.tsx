import type { ColumnDef } from "@tanstack/react-table";
import { IconChevronDown, IconChevronUp, IconUnfoldMore, IconVisibility } from "./icons";
import type { FailedOrder, FailedOrderStatus } from "./types";

const STATUS_STYLES: Record<FailedOrderStatus, string> = {
  "Payment Failed": "bg-red-100 text-red-800",
  "Address Error": "bg-orange-100 text-orange-800",
  "Auth Failed": "bg-sky-100 text-sky-800",
  "Stock Error": "bg-amber-100 text-amber-800",
  "System Error": "bg-purple-100 text-purple-800",
};

function SortHeader({
  label,
  isSorted,
  onToggle,
}: {
  label: string;
  isSorted: false | "asc" | "desc";
  onToggle: () => void;
}) {
  return (
    <button
      type="button"
      className="flex items-center gap-1 hover:text-primary transition-colors group"
      onClick={onToggle}
    >
      {label}
      <span className="text-outline group-hover:text-primary transition-colors">
        {isSorted === "asc" ? (
          <IconChevronUp className="h-3 w-3" />
        ) : isSorted === "desc" ? (
          <IconChevronDown className="h-3 w-3" />
        ) : (
          <IconUnfoldMore className="h-3 w-3" />
        )}
      </span>
    </button>
  );
}

export function createFailedOrderColumns(
  onView?: (orderId: string) => void
): ColumnDef<FailedOrder>[] {
  return [
    // ── Row selection ───────────────────────────────────────────────────────
    {
      id: "select",
      header: ({ table }) => (
        <input
          type="checkbox"
          className="h-3.5 w-3.5 rounded border-outline-variant accent-primary cursor-pointer"
          checked={table.getIsAllPageRowsSelected()}
          ref={(el) => { if (el) el.indeterminate = table.getIsSomePageRowsSelected(); }}
          onChange={(e) => table.toggleAllPageRowsSelected(e.target.checked)}
          aria-label="Select all"
        />
      ),
      cell: ({ row }) => (
        <input
          type="checkbox"
          className="h-3.5 w-3.5 rounded border-outline-variant accent-primary cursor-pointer"
          checked={row.getIsSelected()}
          onChange={(e) => row.toggleSelected(e.target.checked)}
          aria-label="Select row"
        />
      ),
      enableSorting: false,
      enableHiding: false,
    },

    // ── DO Number ───────────────────────────────────────────────────────────
    {
      accessorKey: "doNumber",
      header: ({ column }) => (
        <SortHeader label="DO Number" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-bold text-on-surface">{row.getValue("doNumber")}</span>
      ),
    },

    // ── Order Number ────────────────────────────────────────────────────────
    {
      accessorKey: "orderId",
      header: "Order Number",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant font-mono">{row.getValue("orderId")}</span>
      ),
    },

    // ── Order Date ──────────────────────────────────────────────────────────
    {
      accessorKey: "orderDate",
      header: ({ column }) => (
        <SortHeader label="Order Date" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("orderDate")}</span>
      ),
    },

    // ── Type ────────────────────────────────────────────────────────────────
    {
      accessorKey: "type",
      header: "Type",
      cell: ({ row }) => (
        <span className="inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase bg-tertiary-container/10 text-tertiary">
          {row.getValue("type")}
        </span>
      ),
    },

    // ── Customer ────────────────────────────────────────────────────────────
    {
      accessorKey: "customerName",
      header: ({ column }) => (
        <SortHeader label="Customer" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <div className="flex flex-col">
          <span className="text-xs font-bold text-on-surface">{row.getValue("customerName")}</span>
          <span className="text-[10px] text-on-surface-variant">{row.original.customerContactNo ?? "—"}</span>
          <span className="text-[10px] text-on-surface-variant">{row.original.customerEmail}</span>
        </div>
      ),
    },

    // ── Delivery Date ───────────────────────────────────────────────────────
    {
      accessorKey: "deliveryDate",
      header: ({ column }) => (
        <SortHeader label="Delivery Date" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("deliveryDate") ?? "—"}</span>
      ),
    },

    // ── Delivery Option ─────────────────────────────────────────────────────
    {
      accessorKey: "deliveryOption",
      header: "Delivery Option",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("deliveryOption") ?? "—"}</span>
      ),
    },

    // ── Fulfilled ───────────────────────────────────────────────────────────
    {
      accessorKey: "fulfilledDate",
      header: ({ column }) => (
        <SortHeader label="Fulfilled Date" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">
          {row.getValue("fulfilledDate") ?? "N/A"}
        </span>
      ),
    },

    // ── Shipping status ─────────────────────────────────────────────────────
    {
      accessorKey: "shippingStatus",
      header: "Shipping",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("shippingStatus") ?? "—"}</span>
      ),
    },

    // ── Status ──────────────────────────────────────────────────────────────
    {
      accessorKey: "status",
      header: "Status",
      cell: ({ row }) => {
        const status = row.getValue("status") as FailedOrderStatus;
        return (
          <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[status]}`}>
            {status}
          </span>
        );
      },
    },

    // ── Processed By ────────────────────────────────────────────────────────
    {
      accessorKey: "processedBy",
      header: "Processed By",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant font-mono">{row.getValue("processedBy") ?? "—"}</span>
      ),
    },

    // ── Tags ────────────────────────────────────────────────────────────────
    {
      accessorKey: "tags",
      header: "Tags",
      cell: ({ row }) => (
        <span className="inline-flex px-2 py-0.5 rounded-full text-[10px] font-medium italic bg-secondary-container/20 text-secondary">
          {row.getValue("tags") ?? "—"}
        </span>
      ),
    },

    // ── Payment Method ──────────────────────────────────────────────────────
    {
      accessorKey: "paymentMethod",
      header: "Payment Method",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("paymentMethod") ?? "—"}</span>
      ),
    },

    // ── Failure Reason (optional, hidden by default) ─────────────────────────
    {
      accessorKey: "failureReason",
      header: "Failure Reason",
      enableHiding: true,
      cell: ({ row }) => {
        const raw = String(row.getValue("failureReason") ?? "");
        const text = raw.trim();
        const short = text.length > 60 ? `${text.slice(0, 60)}…` : text || "—";
        return (
          <span className="text-xs text-on-surface-variant" title={text || undefined}>
            {short}
          </span>
        );
      },
    },

    // ── Actions ─────────────────────────────────────────────────────────────
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => (
        <div className="flex items-center justify-center">
          <button
            type="button"
            className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all"
            aria-label="View"
            title="View"
            onClick={() => onView?.(row.original.orderId)}
          >
            <IconVisibility />
          </button>
        </div>
      ),
    },
  ];
}

export const FAILED_ORDER_COLUMN_LABELS: Record<string, string> = {
  doNumber: "DO Number",
  orderId: "Order Number",
  orderDate: "Order Date",
  type: "Type",
  customerName: "Customer",
  deliveryDate: "Delivery Date",
  deliveryOption: "Delivery Option",
  fulfilledDate: "Fulfilled Date",
  shippingStatus: "Shipping",
  status: "Status",
  processedBy: "Processed By",
  tags: "Tags",
  paymentMethod: "Payment Method",
  failureReason: "Failure Reason",
};
