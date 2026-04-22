import type { ColumnDef } from "@tanstack/react-table";
import {
  IconCancel,
  IconCheckCircle,
  IconChevronDown,
  IconChevronUp,
  IconDelete,
  IconEdit,
  IconUnfoldMore,
} from "../orders/icons";

export type AttributeType = "TEXT" | "COLOR" | "IMAGE" | "SELECT" | "BOOLEAN";

export type AttributeListRow = {
  seq: string;
  name: string;
  code: string;
  type: AttributeType;
  required: boolean;
  /** Numeric DB id — present when loaded from API, absent for localStorage-only rows. */
  id?: number;
};

function typePillClasses(type: AttributeType) {
  switch (type) {
    case "COLOR":
      return "border-tertiary-container/20 bg-tertiary-container/10 text-on-tertiary-container";
    case "IMAGE":
      return "border-primary-container/20 bg-primary-container/10 text-on-primary-fixed-variant";
    default:
      return "border-secondary-container/20 bg-secondary-container/10 text-on-secondary-container";
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

export function createAttributeColumns(
  onEdit?: (code: string) => void,
  onDelete?: (code: string) => void
): ColumnDef<AttributeListRow>[] {
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
      accessorKey: "seq",
      header: ({ column }) => (
        <SortHeader label="Seq." isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-medium text-on-surface-variant">{row.getValue("seq")}</span>
      ),
    },

    {
      accessorKey: "name",
      header: ({ column }) => (
        <SortHeader label="Name" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-bold text-on-surface">{row.getValue("name")}</span>
      ),
    },

    {
      accessorKey: "code",
      header: "Code",
      cell: ({ row }) => (
        <span className="font-mono text-xs text-on-surface-variant">{row.getValue("code")}</span>
      ),
    },

    {
      accessorKey: "type",
      header: "Type",
      cell: ({ row }) => {
        const type = row.getValue("type") as AttributeType;
        return (
          <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-[0.625rem] font-bold ${typePillClasses(type)}`}>
            {type}
          </span>
        );
      },
    },

    {
      accessorKey: "required",
      header: "Required",
      cell: ({ row }) =>
        row.getValue("required") ? (
          <IconCheckCircle className="h-[18px] w-[18px] text-green-600" aria-label="Required" />
        ) : (
          <IconCancel className="h-[18px] w-[18px] text-on-surface-variant" aria-label="Optional" />
        ),
    },

    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => (
        <div className="flex justify-end gap-1">
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
            aria-label={`Edit ${row.original.name}`}
            onClick={() => onEdit?.(row.original.code)}
          >
            <IconEdit className="h-[18px] w-[18px]" />
          </button>
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-error"
            aria-label={`Delete ${row.original.name}`}
            onClick={() => onDelete?.(row.original.code)}
          >
            <IconDelete className="h-[18px] w-[18px]" />
          </button>
        </div>
      ),
    },
  ];
}
