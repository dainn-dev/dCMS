import type { ColumnDef } from "@tanstack/react-table";
import { IconBolt, IconChevronDown, IconChevronUp, IconUnfoldMore, IconVisibility } from "./icons";
import type { Order, OrderStatus } from "./types";
import { useEffect, useMemo, useRef, useState } from "react";

const STATUS_STYLES: Record<OrderStatus, string> = {
  "Open Order": "bg-secondary-container/20 text-on-secondary-container",
  "Processing": "bg-blue-100 text-blue-700",
  "Ready for Delivery": "bg-cyan-100 text-cyan-700",
  "Ready for Pickup": "bg-cyan-100 text-cyan-700",
  "Out for Delivery": "bg-indigo-100 text-indigo-700",
  "Picked Up": "bg-teal-100 text-teal-700",
  "Delivered": "bg-green-100 text-green-700",
  "Returned": "bg-orange-100 text-orange-700",
  "Admin Cancelled": "bg-red-100 text-red-700",
  "Pending Cancellation": "bg-orange-100 text-orange-800",
  "User Cancelled": "bg-red-100 text-red-700",
  "Partially Fulfilled": "bg-yellow-100 text-yellow-700",
  // DAI-637 failure states — shown on Order Processing when mixed with active orders
  "Payment Failed": "bg-red-100 text-red-800",
  "Auth Failed": "bg-sky-100 text-sky-800",
  "Address Error": "bg-orange-100 text-orange-800",
  "Stock Error": "bg-amber-100 text-amber-800",
  "System Error": "bg-purple-100 text-purple-800",
};

/** Sortable column header button */
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

export function createOrderColumns(
  onViewOrder?: (orderId: string) => void,
  actions?: {
    onCancel: (orderId: string) => void;
    onShip: (orderId: string) => void;
    onDeliver: (orderId: string) => void;
  }
): ColumnDef<Order>[] {
  return [
    // ── Row selection ───────────────────────────────────────────────────────
    {
      id: "select",
      header: ({ table }) => (
        <input
          type="checkbox"
          className="h-3.5 w-3.5 rounded border-outline-variant accent-primary cursor-pointer"
          checked={table.getIsAllPageRowsSelected()}
          ref={(el) => {
            if (el) el.indeterminate = table.getIsSomePageRowsSelected();
          }}
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
        <SortHeader
          label="DO Number"
          isSorted={column.getIsSorted()}
          onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")}
        />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-bold text-on-surface">{row.getValue("doNumber")}</span>
      ),
    },

    // ── Order ID ────────────────────────────────────────────────────────────
    {
      accessorKey: "orderId",
      header: "Order ID",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant font-mono">{row.getValue("orderId")}</span>
      ),
    },

    // ── Order Date ──────────────────────────────────────────────────────────
    {
      accessorKey: "orderDate",
      header: ({ column }) => (
        <SortHeader
          label="Order Date"
          isSorted={column.getIsSorted()}
          onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")}
        />
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
        <SortHeader
          label="Customer"
          isSorted={column.getIsSorted()}
          onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")}
        />
      ),
      cell: ({ row }) => (
        <div className="flex flex-col">
          <span className="text-xs font-bold text-on-surface">{row.getValue("customerName")}</span>
          <span className="text-[10px] text-on-surface-variant">{row.original.customerEmail}</span>
        </div>
      ),
    },

    // ── Status ──────────────────────────────────────────────────────────────
    {
      accessorKey: "status",
      header: "Status",
      cell: ({ row }) => {
        const status = row.getValue("status") as OrderStatus;
        return (
          <span
            className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[status]}`}
          >
            <span className="w-1.5 h-1.5 rounded-full bg-current opacity-60 shrink-0" />
            {status}
          </span>
        );
      },
    },

    // ── Fulfilled ───────────────────────────────────────────────────────────
    {
      accessorKey: "fulfilledDate",
      header: ({ column }) => (
        <SortHeader
          label="Fulfilled"
          isSorted={column.getIsSorted()}
          onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")}
        />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">
          {row.getValue("fulfilledDate") ?? "—"}
        </span>
      ),
    },

    // ── Store ───────────────────────────────────────────────────────────────
    {
      accessorKey: "store",
      header: "Store",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("store")}</span>
      ),
    },

    // ── Optional: Distribution Centre ───────────────────────────────────────
    {
      accessorKey: "distributionCentre",
      header: "Distribution Centre",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">
          {row.getValue("distributionCentre") ?? "—"}
        </span>
      ),
    },

    // ── Optional: Payment Method ────────────────────────────────────────────
    {
      accessorKey: "paymentMethod",
      header: "Payment Method",
      cell: ({ row }) => {
        const method = row.getValue("paymentMethod") as string | undefined;
        if (!method) return <span className="text-xs text-on-surface-variant">—</span>;
        if (method === "Multi Payment") {
          return (
            <span className="inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase bg-primary-container/20 text-primary">
              Multi Payment
            </span>
          );
        }
        return <span className="text-xs text-on-surface-variant">{method}</span>;
      },
    },

    // ── Optional: Payment Date ──────────────────────────────────────────────
    {
      accessorKey: "paymentDate",
      header: ({ column }) => (
        <SortHeader
          label="Payment Date"
          isSorted={column.getIsSorted()}
          onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")}
        />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">
          {row.getValue("paymentDate") ?? "—"}
        </span>
      ),
    },

    // ── Actions ─────────────────────────────────────────────────────────────
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => {
        const id = row.original.orderId;
        const status = row.original.status;
        return (
          <div className="flex items-center justify-center gap-1">
            <button
              type="button"
              className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all"
              aria-label="View Details"
              title="View Details"
              onClick={() => onViewOrder?.(id)}
            >
              <IconVisibility />
            </button>
            <TakeActionMenu
              orderId={id}
              status={status}
              onCancel={() => actions?.onCancel(id)}
              onShip={() => actions?.onShip(id)}
              onDeliver={() => actions?.onDeliver(id)}
              disabled={!actions}
            />
          </div>
        );
      },
    },
  ];
}

function TakeActionMenu({
  orderId,
  status,
  onCancel,
  onShip,
  onDeliver,
  disabled,
}: {
  orderId: string;
  status: Order["status"];
  onCancel: () => void;
  onShip: () => void;
  onDeliver: () => void;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const canCancel = status === "Open Order" || status === "Processing";
  const canShip = status === "Ready for Delivery";
  const canDeliver = status === "Out for Delivery";

  useEffect(() => {
    function handler(e: MouseEvent) {
      const root = ref.current;
      if (!root) return;
      if (root.contains(e.target as Node)) return;
      setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const title = useMemo(() => `Take Action — ${orderId}`, [orderId]);

  return (
    <div className="relative" ref={ref}>
      <button
        type="button"
        className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-amber-500 transition-all disabled:opacity-40"
        aria-label={title}
        title={title}
        disabled={disabled}
        onClick={() => setOpen((o) => !o)}
      >
        <IconBolt />
      </button>
      {open && (
        <div className="absolute right-0 top-full z-30 mt-1 w-56 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
          <div className="px-3 py-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant/60">
            Actions
          </div>
          <button
            type="button"
            className="flex w-full items-center justify-between gap-2 px-3 py-2.5 text-left text-xs font-semibold text-on-surface hover:bg-surface-container-high transition-colors"
            onClick={() => {
              setOpen(false);
              onCancel();
            }}
            disabled={!canCancel}
          >
            Cancel Order
            {!canCancel && <span className="text-[10px] text-on-surface-variant">Not allowed</span>}
          </button>
          <button
            type="button"
            className="flex w-full items-center justify-between gap-2 px-3 py-2.5 text-left text-xs font-semibold text-on-surface hover:bg-surface-container-high transition-colors"
            onClick={() => {
              setOpen(false);
              onShip();
            }}
            disabled={!canShip}
          >
            Mark as Shipped
            {!canShip && <span className="text-[10px] text-on-surface-variant">Not allowed</span>}
          </button>
          <button
            type="button"
            className="flex w-full items-center justify-between gap-2 px-3 py-2.5 text-left text-xs font-semibold text-on-surface hover:bg-surface-container-high transition-colors"
            onClick={() => {
              setOpen(false);
              onDeliver();
            }}
            disabled={!canDeliver}
          >
            Mark as Delivered
            {!canDeliver && <span className="text-[10px] text-on-surface-variant">Not allowed</span>}
          </button>
        </div>
      )}
    </div>
  );
}

/** Column id → display label for the visibility dropdown */
export const COLUMN_LABELS: Record<string, string> = {
  doNumber: "DO Number",
  orderId: "Order ID",
  orderDate: "Order Date",
  type: "Type",
  customerName: "Customer",
  status: "Status",
  fulfilledDate: "Fulfilled",
  store: "Store",
  distributionCentre: "Distribution Centre",
  paymentMethod: "Payment Method",
  paymentDate: "Payment Date",
};
