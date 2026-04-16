import type { ColumnDef } from "@tanstack/react-table";
import { IconCheckCircle, IconChevronDown, IconChevronUp, IconEdit, IconUnfoldMore, IconVisibility } from "./icons";
import type { RefundCase, RefundStatus } from "./types";

const STATUS_STYLES: Record<RefundStatus, string> = {
  Completed: "bg-green-100 text-green-700",
  Pending: "bg-amber-100 text-amber-700",
  Rejected: "bg-red-100 text-red-700",
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

const currencyFormatter = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
});

export function createRefundCaseColumns(
  onView?: (refundNo: string) => void,
  onEdit?: (refundNo: string) => void
): ColumnDef<RefundCase>[] {
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

    // ── Refund No ───────────────────────────────────────────────────────────
    {
      accessorKey: "refundNo",
      header: ({ column }) => (
        <SortHeader label="Refund No." isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <div className="flex flex-col">
          <span className="text-xs font-bold text-on-surface">{row.getValue("refundNo")}</span>
          <span className="text-[10px] text-on-surface-variant font-mono">{row.original.referenceHash}</span>
        </div>
      ),
    },

    // ── Return / DO / Order ─────────────────────────────────────────────────
    {
      id: "references",
      header: "Return / DO / Order",
      enableSorting: false,
      cell: ({ row }) => (
        <div className="text-xs space-y-0.5">
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-black text-on-surface-variant w-6 uppercase">Ret</span>
            <span className="text-on-surface-variant">{row.original.returnNo}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-black text-on-surface-variant w-6 uppercase">DO</span>
            <span className="text-on-surface-variant">{row.original.doNumber}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="text-[9px] font-black text-on-surface-variant w-6 uppercase">Ord</span>
            <span className="text-on-surface-variant">{row.original.orderId}</span>
          </div>
        </div>
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
          <span className="text-[10px] text-on-surface-variant">{row.original.customerPhone}</span>
          <span className="text-[10px] text-primary truncate max-w-[128px]">{row.original.customerEmail}</span>
        </div>
      ),
    },

    // ── Amount ──────────────────────────────────────────────────────────────
    {
      accessorKey: "amount",
      header: ({ column }) => (
        <div className="text-right">
          <SortHeader label="Amount" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
        </div>
      ),
      cell: ({ row }) => (
        <div className="text-right">
          <div className="text-sm font-black text-on-surface">
            {currencyFormatter.format(row.getValue("amount"))}
          </div>
          <div className="text-[9px] text-on-surface-variant font-bold uppercase tracking-tight">
            {row.original.currency}
          </div>
        </div>
      ),
    },

    // ── Payment ─────────────────────────────────────────────────────────────
    {
      accessorKey: "paymentMethod",
      header: "Payment",
      cell: ({ row }) => (
        <div className="flex flex-col">
          <span className="text-xs font-medium text-on-surface">{row.getValue("paymentMethod")}</span>
          <span className="text-[10px] text-on-surface-variant">{row.original.paymentDetail}</span>
        </div>
      ),
    },

    // ── Request Date ────────────────────────────────────────────────────────
    {
      accessorKey: "requestDate",
      header: ({ column }) => (
        <SortHeader label="Request Date" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("requestDate")}</span>
      ),
    },

    // ── Refund Date ─────────────────────────────────────────────────────────
    {
      accessorKey: "refundDate",
      header: ({ column }) => (
        <SortHeader label="Refund Date" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => {
        const date = row.getValue("refundDate") as string | null;
        const status = row.original.status;
        if (date) {
          return (
            <span className="flex items-center gap-1 text-xs text-green-700">
              <IconCheckCircle className="h-3 w-3 shrink-0" />
              {date}
            </span>
          );
        }
        if (status === "Rejected") {
          return <span className="text-xs text-red-600 italic">Rejected</span>;
        }
        return <span className="text-xs text-on-surface-variant italic">Processing</span>;
      },
    },

    // ── Status ──────────────────────────────────────────────────────────────
    {
      accessorKey: "status",
      header: "Status",
      cell: ({ row }) => {
        const status = row.getValue("status") as RefundStatus;
        return (
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[status]}`}>
            {status}
          </span>
        );
      },
    },

    // ── Remark ──────────────────────────────────────────────────────────────
    {
      accessorKey: "remark",
      header: "Remark",
      enableSorting: false,
      cell: ({ row }) => (
        <p className="text-[10px] text-on-surface-variant italic line-clamp-2 max-w-[160px]">
          {row.getValue("remark")}
        </p>
      ),
    },

    // ── Actions ─────────────────────────────────────────────────────────────
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => (
        <div className="flex items-center justify-center gap-1">
          <button
            type="button"
            className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all"
            aria-label="View"
            title="View"
            onClick={() => onView?.(row.original.refundNo)}
          >
            <IconVisibility />
          </button>
          <button
            type="button"
            className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all"
            aria-label="Edit"
            title="Edit"
            onClick={() => onEdit?.(row.original.refundNo)}
          >
            <IconEdit />
          </button>
        </div>
      ),
    },
  ];
}
