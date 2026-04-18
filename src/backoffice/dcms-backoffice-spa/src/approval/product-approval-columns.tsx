import type { ColumnDef } from "@tanstack/react-table";
import {
  IconCancel,
  IconCheckCircle,
  IconOpenInNew,
  IconUnfoldMore,
  IconVisibility,
} from "../orders/icons";

export type ProductApprovalStatus = "pending" | "approved" | "rejected";

export type ProductApprovalRow = {
  id: string;
  productName: string;
  brand: string;
  category: string;
  submittedBy: string;
  submittedDate: string;
  status: ProductApprovalStatus;
};

function statusPill(s: ProductApprovalStatus) {
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

export function createProductApprovalColumns(handlers: {
  onViewPage: (row: ProductApprovalRow) => void;
  onPreview: (row: ProductApprovalRow) => void;
  onApprove: (row: ProductApprovalRow) => void;
  onReject: (row: ProductApprovalRow) => void;
}): ColumnDef<ProductApprovalRow>[] {
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
      accessorKey: "productName",
      header: ({ column }) => (
        <SortHeader label="Product name" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => <span className="text-xs font-bold text-on-surface">{row.getValue("productName")}</span>,
    },
    {
      accessorKey: "brand",
      header: "Brand",
      cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("brand")}</span>,
    },
    {
      accessorKey: "category",
      header: "Category",
      cell: ({ row }) => (
        <span className="line-clamp-2 min-w-0 text-xs text-on-surface-variant" title={String(row.getValue("category"))}>
          {row.getValue("category")}
        </span>
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
        const s = row.getValue("status") as ProductApprovalStatus;
        return (
          <span className={`inline-block rounded-full px-2 py-0.5 text-[9px] font-extrabold uppercase tracking-widest ${statusPill(s)}`}>
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
              aria-label="View page on eStore"
              onClick={() => handlers.onViewPage(r)}
            >
              <IconOpenInNew className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
              title="Preview product"
              aria-label="Preview product"
              onClick={() => handlers.onPreview(r)}
            >
              <IconVisibility className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
              title="Approve"
              aria-label="Approve product"
              onClick={() => handlers.onApprove(r)}
            >
              <IconCheckCircle className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-error"
              title="Reject"
              aria-label="Reject product"
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
