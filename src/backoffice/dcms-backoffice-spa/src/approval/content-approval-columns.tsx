import type { ColumnDef } from "@tanstack/react-table";
import {
  IconCancel,
  IconCheckCircle,
  IconOpenInNew,
  IconUnfoldMore,
  IconVisibility,
} from "../orders/icons";

export type ContentApprovalStatus = "pending" | "approved" | "rejected";

export type ContentApprovalRow = {
  id: string;
  contentTitle: string;
  contentType: string;
  pagePath: string;
  submittedBy: string;
  submittedDate: string;
  status: ContentApprovalStatus;
};

function statusPill(s: ContentApprovalStatus) {
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

export function createContentApprovalColumns(handlers: {
  onViewPage: (row: ContentApprovalRow) => void;
  onPreview: (row: ContentApprovalRow) => void;
  onApprove: (row: ContentApprovalRow) => void;
  onReject: (row: ContentApprovalRow) => void;
}): ColumnDef<ContentApprovalRow>[] {
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
      accessorKey: "contentTitle",
      header: ({ column }) => (
        <SortHeader label="Content title" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => <span className="text-xs font-bold text-on-surface">{row.getValue("contentTitle")}</span>,
    },
    {
      accessorKey: "contentType",
      header: "Type",
      cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("contentType")}</span>,
    },
    {
      accessorKey: "pagePath",
      header: "Page / path",
      cell: ({ row }) => (
        <span className="line-clamp-2 min-w-0 font-mono text-xs text-on-surface-variant" title={String(row.getValue("pagePath"))}>
          {row.getValue("pagePath")}
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
        const s = row.getValue("status") as ContentApprovalStatus;
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
              aria-label="View live page"
              onClick={() => handlers.onViewPage(r)}
            >
              <IconOpenInNew className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
              title="Preview content"
              aria-label="Preview content"
              onClick={() => handlers.onPreview(r)}
            >
              <IconVisibility className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
              title="Approve"
              aria-label="Approve content"
              onClick={() => handlers.onApprove(r)}
            >
              <IconCheckCircle className="h-[18px] w-[18px]" />
            </button>
            <button
              type="button"
              className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-error"
              title="Reject"
              aria-label="Reject content"
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
