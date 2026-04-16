import type { ColumnDef } from "@tanstack/react-table";
import type { ComponentType } from "react";
import { IconChevronDown, IconChevronUp, IconDelete, IconEdit, IconUnfoldMore } from "../orders/icons";

export type FulfillmentMethodRow = {
  id: string;
  name: string;
  type: string;
  baseCost: string;
  freeThreshold: string;
  deliveryTime: string;
  active: boolean;
  Icon: ComponentType<{ className?: string }>;
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

function StatusToggle({ on, onToggle }: { on: boolean; onToggle: () => void }) {
  return (
    <div className="flex justify-center">
      <button
        type="button"
        role="switch"
        aria-checked={on}
        className={`relative h-5 w-10 cursor-pointer rounded-full ${on ? "bg-primary/20" : "bg-outline-variant/30"}`}
        onClick={onToggle}
      >
        <span
          className={`absolute top-1 h-3 w-3 rounded-full transition-all ${on ? "right-1 bg-primary" : "left-1 bg-white"}`}
        />
      </button>
    </div>
  );
}

export function createFulfillmentColumns(
  onToggle?: (id: string) => void,
  onEdit?: (id: string) => void,
  onDelete?: (id: string) => void
): ColumnDef<FulfillmentMethodRow>[] {
  return [
    {
      id: "methodName",
      header: ({ column }) => (
        <SortHeader label="Method Name" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      accessorKey: "name",
      cell: ({ row }) => {
        const RowIcon = row.original.Icon;
        return (
          <div className="flex items-center gap-3">
            <div className="flex h-8 w-8 items-center justify-center rounded bg-surface-container-high text-primary shrink-0">
              <RowIcon className="h-5 w-5" />
            </div>
            <span className="text-sm font-bold text-on-surface">{row.original.name}</span>
          </div>
        );
      },
    },

    {
      accessorKey: "type",
      header: "Type",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("type")}</span>
      ),
    },

    {
      accessorKey: "baseCost",
      header: ({ column }) => (
        <SortHeader label="Base Cost" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-semibold text-on-surface">{row.getValue("baseCost")}</span>
      ),
    },

    {
      accessorKey: "freeThreshold",
      header: "Free Threshold",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("freeThreshold")}</span>
      ),
    },

    {
      accessorKey: "deliveryTime",
      header: ({ column }) => (
        <SortHeader label="Delivery Time" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs text-on-surface-variant">{row.getValue("deliveryTime")}</span>
      ),
    },

    {
      accessorKey: "active",
      header: "Status",
      cell: ({ row }) => (
        <StatusToggle
          on={row.getValue("active")}
          onToggle={() => onToggle?.(row.original.id)}
        />
      ),
    },

    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => (
        <div className="flex justify-end gap-2">
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container-highest hover:text-primary"
            aria-label="Edit"
            onClick={() => onEdit?.(row.original.id)}
          >
            <IconEdit className="h-4 w-4" />
          </button>
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container-highest hover:text-error"
            aria-label="Delete"
            onClick={() => onDelete?.(row.original.id)}
          >
            <IconDelete className="h-4 w-4" />
          </button>
        </div>
      ),
    },
  ];
}
