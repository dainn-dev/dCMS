import type { ColumnDef } from "@tanstack/react-table";
import {
  IconCancel,
  IconCheckCircle,
  IconOpenInNew,
  IconUnfoldMore,
  IconVisibility,
} from "../orders/icons";

export type PromoCodeApprovalStatus = "pending" | "approved" | "rejected";

export type PromoCodeApprovalRow = {
  id: string;
  promoCode: string;
  promoName: string;
  discountType: string;
  discountValue: string;
  submittedBy: string;
  submittedDate: string;
  status: PromoCodeApprovalStatus;
};

function statusPill(s: PromoCodeApprovalStatus) {
  if (s === "pending") return "bg-amber-100 text-amber-900";
  if (s === "approved") return "bg-tertiary-container text-on-tertiary-container";
  return "bg-error-container text-on-error-container";
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
    <button type="button" className="flex items-center gap-1 hover:text-primary transition-colors group" onClick={onToggle}>
      {label}
      <IconUnfoldMore className="h-3 w-3 text-outline group-hover:text-primary" />
    </button>
  );
}

export function createPromoCodeApprovalColumns(handlers: {
  onViewPage: (row: PromoCodeApprovalRow) => void;
  onPreview: (row: PromoCodeApprovalRow) => void;
  onApprove: (row: PromoCodeApprovalRow) => void;
  onReject: (row: PromoCodeApprovalRow) => void;
}): ColumnDef<PromoCodeApprovalRow>[] {
  return [
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
    },
    {
      accessorKey: "promoCode",
      header: ({ column }) => (
        <SortHeader label="Code" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="font-mono text-xs font-bold text-on-surface">{row.getValue("promoCode")}</span>
      ),
    },
    {
      accessorKey: "promoName",
      header: "Name",
      cell: ({ row }) => <span className="text-xs font-bold text-on-surface">{row.getValue("promoName")}</span>,
    },
    {
      accessorKey: "discountType",
      header: "Discount type",
      cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("discountType")}</span>,
    },
    {
      accessorKey: "discountValue",
      header: "Value",
      cell: ({ row }) => (
        <span className="text-[11px] tabular-nums text-on-surface-variant">{row.getValue("discountValue")}</span>
      ),
    },
    {
      accessorKey: "submittedBy",
      header: "Submitted by",
      cell: ({ row }) => <span className="text-xs text-on-surface">{row.getValue("submittedBy")}</span>,
    },
    {
      accessorKey: "submittedDate",
      header: ({ column }) => (
        <SortHeader label="Submitted" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("submittedDate")}</span>,
    },
    {
      accessorKey: "status",
      header: "Status",
      cell: ({ row }) => {
        const s = row.getValue("status") as PromoCodeApprovalStatus;
        return (
          <span
            className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase ${statusPill(s)}`}
          >
            <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-current opacity-60" />
            {s}
          </span>
        );
      },
    },
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      cell: ({ row }) => {
        const r = row.original;
        return (
          <div className="flex flex-wrap items-center justify-end gap-1">
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
              title="View page"
              aria-label="View promo on storefront"
              onClick={() => handlers.onViewPage(r)}
            >
              <IconOpenInNew className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
              title="Preview promo"
              aria-label="Preview promo code"
              onClick={() => handlers.onPreview(r)}
            >
              <IconVisibility className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
              title="Approve"
              aria-label="Approve promo code"
              onClick={() => handlers.onApprove(r)}
            >
              <IconCheckCircle className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-error"
              title="Reject"
              aria-label="Reject promo code"
              onClick={() => handlers.onReject(r)}
            >
              <IconCancel className="h-[18px] w-[18px]" />
            </button>
          </div>
        );
      },
    },
  ];
}
