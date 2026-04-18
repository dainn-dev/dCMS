import type { ColumnDef } from "@tanstack/react-table";
import { IconChevronDown, IconChevronUp, IconDelete, IconEdit, IconFormatListBulleted, IconUnfoldMore } from "../orders/icons";

export type PromoListRow = {
  id: string;
  promoType: string;
  discount: string;
  value: string;
  minSpend: string;
  code: string;
  scheduleStart: string;
  scheduleEnd: string;
  activeDot: "live" | "warning" | "off";
  status: "approved" | "pending" | "expired";
  usedPct: number;
};

function activeDotClass(variant: PromoListRow["activeDot"]) {
  switch (variant) {
    case "live":    return "bg-green-500 shadow-[0_0_8px_rgba(34,197,94,0.4)]";
    case "warning": return "bg-amber-400 shadow-[0_0_8px_rgba(251,191,36,0.4)]";
    case "off":     return "bg-red-500 shadow-[0_0_8px_rgba(239,68,68,0.4)]";
    default:        return "bg-outline-variant";
  }
}

function statusPillClass(status: PromoListRow["status"]) {
  switch (status) {
    case "approved": return "bg-tertiary-container text-on-tertiary-container";
    case "pending":  return "bg-secondary-fixed text-on-secondary-fixed-variant";
    case "expired":  return "bg-error-container text-on-error-container";
    default:         return "bg-outline-variant/20 text-on-surface-variant";
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

export function createPromotionColumns(
  onEdit?: (id: string) => void,
  onDelete?: (id: string) => void,
  onViewCodes?: (id: string) => void
): ColumnDef<PromoListRow>[] {
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
      accessorKey: "promoType",
      header: ({ column }) => (
        <SortHeader label="Promo Type" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-semibold text-on-surface">{row.getValue("promoType")}</span>
      ),
    },

    {
      accessorKey: "discount",
      header: "Discount",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface">{row.getValue("discount")}</span>
      ),
    },

    {
      accessorKey: "value",
      header: ({ column }) => (
        <SortHeader label="Value" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <span className="text-xs font-bold text-primary">{row.getValue("value")}</span>
      ),
    },

    {
      accessorKey: "minSpend",
      header: "Min Spend",
      cell: ({ row }) => (
        <span className="text-xs text-on-surface">{row.getValue("minSpend")}</span>
      ),
    },

    {
      accessorKey: "code",
      header: "Code",
      cell: ({ row }) => (
        <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-xs font-bold text-on-surface-variant">
          {row.getValue("code")}
        </code>
      ),
    },

    {
      id: "schedule",
      header: "Schedule",
      enableSorting: false,
      cell: ({ row }) => (
        <div className="flex flex-col text-xs">
          <span className="font-medium text-on-surface">{row.original.scheduleStart}</span>
          <span className="text-on-surface-variant opacity-60">{row.original.scheduleEnd}</span>
        </div>
      ),
    },

    {
      accessorKey: "activeDot",
      header: "Active",
      cell: ({ row }) => (
        <div className="flex justify-center">
          <div
            className={`h-2 w-2 rounded-full ${activeDotClass(row.getValue("activeDot"))}`}
            aria-hidden
          />
        </div>
      ),
    },

    {
      accessorKey: "status",
      header: "Status",
      cell: ({ row }) => {
        const status = row.getValue("status") as PromoListRow["status"];
        return (
          <span
            className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[10px] font-bold uppercase ${statusPillClass(status)}`}
          >
            <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-current opacity-60" />
            {status}
          </span>
        );
      },
    },

    {
      accessorKey: "usedPct",
      header: ({ column }) => (
        <SortHeader label="% Used" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => {
        const pct = row.getValue("usedPct") as number;
        return (
          <div className="w-full min-w-0 max-w-[140px]">
            <div className="mb-1 text-xs font-bold text-on-surface">{pct}%</div>
            <div className="h-1.5 w-full overflow-hidden rounded-full bg-surface-container-high">
              <div className="h-full bg-primary" style={{ width: `${pct}%` }} />
            </div>
          </div>
        );
      },
    },

    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      enableHiding: false,
      cell: ({ row }) => (
        <div className="flex items-center justify-end gap-1">
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
            aria-label="Edit"
            title="Edit promo code"
            onClick={() => onEdit?.(row.original.id)}
          >
            <IconEdit className="h-[18px] w-[18px]" />
          </button>
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-secondary"
            aria-label="View codes"
            title="View grouped codes"
            onClick={() => onViewCodes?.(row.original.id)}
          >
            <IconFormatListBulleted className="h-[18px] w-[18px]" />
          </button>
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-error"
            aria-label="Delete"
            title="Delete promo code"
            onClick={() => onDelete?.(row.original.id)}
          >
            <IconDelete className="h-[18px] w-[18px]" />
          </button>
        </div>
      ),
    },
  ];
}
