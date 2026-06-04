import type { ColumnDef } from "@tanstack/react-table";
import {
  IconChevronDown,
  IconChevronUp,
  IconEdit,
  IconHistory,
  IconUnfoldMore,
} from "./icons";
import type { RefundCase, RefundStatus } from "./types";

const STATUS_STYLES: Record<RefundStatus, string> = {
  Pending: "bg-amber-100 text-amber-800",
  Processing: "bg-blue-100 text-blue-800",
  Success: "bg-green-100 text-green-800",
  Rejected: "bg-red-100 text-red-800",
};

const REMARK_PREVIEW = 52;

export function canRefundCaseBeEdited(c: RefundCase, hasUpdatePermission: boolean): boolean {
  if (!hasUpdatePermission) return false;
  if (c.isPaymentGatewayCase) {
    return c.status === "Pending" || c.status === "Rejected";
  }
  return true;
}

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency,
      maximumFractionDigits: currency === "JPY" ? 0 : 2,
    }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}

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

export type RefundCaseColumnHandlers = {
  onDoClick: (refundNo: string) => void;
  onRemarkDetails: (refundNo: string) => void;
  onEdit: (refundNo: string) => void;
  onHistory: (refundNo: string) => void;
  canUpdateRefundStatus: boolean;
};

export function createRefundCaseColumns({
  onDoClick,
  onRemarkDetails,
  onEdit,
  onHistory,
  canUpdateRefundStatus,
}: RefundCaseColumnHandlers): ColumnDef<RefundCase>[] {
  return [
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

    {
      accessorKey: "doNumber",
      header: ({ column }) => (
        <SortHeader label="DO Number" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <button
          type="button"
          className="text-left text-xs font-bold text-primary underline-offset-2 hover:underline"
          onClick={() => onDoClick(row.original.refundNo)}
        >
          {row.getValue("doNumber")}
        </button>
      ),
    },

    {
      accessorKey: "orderId",
      header: "Order Number",
      cell: ({ row }) => (
        <span className="text-xs font-mono text-on-surface-variant">{row.getValue("orderId")}</span>
      ),
    },

    {
      accessorKey: "customerEmail",
      header: ({ column }) => (
        <SortHeader label="Customer Email" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-primary break-all max-w-[160px] inline-block">{row.getValue("customerEmail")}</span>
      ),
    },

    {
      accessorKey: "customerName",
      header: ({ column }) => (
        <SortHeader label="Customer Name" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-bold text-on-surface">{row.getValue("customerName")}</span>
      ),
    },

    {
      accessorKey: "amount",
      header: ({ column }) => (
        <div className="text-right">
          <SortHeader label="Refund Amount" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
        </div>
      ),
      cell: ({ row }) => (
        <div className="text-right text-sm font-black text-on-surface tabular-nums">
          {formatMoney(row.original.amount, row.original.currency)}
        </div>
      ),
    },

    {
      accessorKey: "paymentMethod",
      header: "Payment Method",
      cell: ({ row }) => <span className="text-xs text-on-surface">{row.getValue("paymentMethod")}</span>,
    },

    {
      accessorKey: "paymentReferenceNo",
      header: "Payment Ref. No.",
      cell: ({ row }) => (
        <span className="text-xs font-mono text-on-surface-variant break-all max-w-[140px] inline-block">
          {row.getValue("paymentReferenceNo")}
        </span>
      ),
    },

    {
      accessorKey: "requestDate",
      header: ({ column }) => (
        <SortHeader label="Request Date" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("requestDate")}</span>
      ),
    },

    {
      accessorKey: "refundDate",
      header: ({ column }) => (
        <SortHeader label="Refund Date" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => {
        const date = row.getValue("refundDate") as string | null;
        if (date) {
          return <span className="text-xs text-green-800 font-medium">{date}</span>;
        }
        return <span className="text-xs text-on-surface-variant">—</span>;
      },
    },

    {
      accessorKey: "status",
      header: "Refund Status",
      cell: ({ row }) => {
        const status = row.getValue("status") as RefundStatus;
        return (
          <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[status]}`}>
            {status}
          </span>
        );
      },
    },

    {
      accessorKey: "remark",
      header: "Remark",
      enableSorting: false,
      cell: ({ row }) => {
        const full = row.original.remark as string;
        const short = full.length > REMARK_PREVIEW ? `${full.slice(0, REMARK_PREVIEW)}…` : full;
        return (
          <div className="max-w-[200px]">
            <p className="text-[10px] text-on-surface-variant italic line-clamp-2">{short}</p>
            <button
              type="button"
              className="mt-1 text-[10px] font-bold uppercase text-primary hover:underline"
              onClick={() => onRemarkDetails(row.original.refundNo)}
            >
              Details
            </button>
          </div>
        );
      },
    },

    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => {
        const c = row.original;
        const editable = canRefundCaseBeEdited(c, canUpdateRefundStatus);
        return (
          <div className="flex items-center justify-center gap-1">
            <button
              type="button"
              className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all"
              aria-label="Change history"
              title="Change history"
              onClick={() => onHistory(c.refundNo)}
            >
              <IconHistory className="h-4 w-4" />
            </button>
            {canUpdateRefundStatus ? (
              <button
                type="button"
                disabled={!editable}
                className={`p-1.5 rounded transition-all ${
                  editable
                    ? "hover:bg-white text-on-surface-variant hover:text-primary"
                    : "text-outline/50 cursor-not-allowed"
                }`}
                aria-label="Update status"
                title={
                  editable
                    ? "Update refund status"
                    : c.isPaymentGatewayCase
                      ? "Gateway refund: only Pending or Rejected can be edited"
                      : "Cannot edit"
                }
                onClick={() => editable && onEdit(c.refundNo)}
              >
                <IconEdit className="h-4 w-4" />
              </button>
            ) : null}
          </div>
        );
      },
    },
  ];
}

export const REFUND_CASE_COLUMN_LABELS: Record<string, string> = {
  refundNo: "Refund No.",
  doNumber: "DO Number",
  orderId: "Order Number",
  customerEmail: "Customer Email",
  customerName: "Customer Name",
  amount: "Refund Amount",
  paymentMethod: "Payment Method",
  paymentReferenceNo: "Payment Ref. No.",
  requestDate: "Request Date",
  refundDate: "Refund Date",
  status: "Refund Status",
  remark: "Remark",
};
