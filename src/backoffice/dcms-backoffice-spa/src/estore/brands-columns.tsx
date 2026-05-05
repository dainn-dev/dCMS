import type { ColumnDef } from "@tanstack/react-table";
import {
  IconChevronDown,
  IconChevronUp,
  IconDelete,
  IconEdit,
  IconUnfoldMore,
} from "../orders/icons";

export type BrandListRow = {
  code: string;
  name: string;
  imageSrc: string;
  imageAlt: string;
  active: boolean;
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

export function createBrandColumns(
  onEditBrand?: (row: BrandListRow) => void,
  onDelete?: (code: string) => void
): ColumnDef<BrandListRow>[] {
  return [
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

    {
      accessorKey: "code",
      header: ({ column }) => (
        <SortHeader label="Brand Code" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-semibold text-on-surface">{row.getValue("code")}</span>
      ),
    },

    {
      id: "image",
      header: "Image",
      enableSorting: false,
      cell: ({ row }) => (
        <div className="h-8 w-8 rounded bg-surface-container border border-outline-variant/20 overflow-hidden flex items-center justify-center">
          <img className="h-full w-full object-cover" src={row.original.imageSrc} alt={row.original.imageAlt} />
        </div>
      ),
    },

    {
      accessorKey: "name",
      header: ({ column }) => (
        <SortHeader label="Brand Name" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-medium text-on-surface-variant">{row.getValue("name")}</span>
      ),
    },

    {
      accessorKey: "active",
      header: "Active",
      cell: ({ row }) =>
        row.getValue("active") ? (
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[0.6rem] font-bold bg-[#fbdbd8] text-[#aa0014] uppercase tracking-tighter">
            Yes
          </span>
        ) : (
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[0.6rem] font-bold bg-outline-variant/20 text-on-surface-variant uppercase tracking-tighter">
            No
          </span>
        ),
    },

    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => (
        <div className="flex items-center justify-end gap-0.5">
          <button
            type="button"
            className="p-1.5 hover:bg-surface-variant rounded transition-colors text-on-surface-variant hover:text-primary"
            aria-label="Edit brand"
            onClick={() => onEditBrand?.(row.original)}
          >
            <IconEdit className="text-lg" />
          </button>
          <button
            type="button"
            className="p-1.5 hover:bg-error-container/20 rounded transition-colors text-on-surface-variant hover:text-error"
            aria-label="Delete brand"
            onClick={() => onDelete?.(row.original.code)}
          >
            <IconDelete className="h-4 w-4" />
          </button>
        </div>
      ),
    },
  ];
}
