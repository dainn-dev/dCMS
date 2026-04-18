import type { ColumnDef } from "@tanstack/react-table";
import {
  IconChevronDown,
  IconChevronUp,
  IconEdit,
  IconUnfoldMore,
  IconVisibility,
} from "../orders/icons";

export type CampaignStatus = "draft" | "scheduled" | "active" | "paused" | "ended";
export type CampaignChannel = "Email" | "SMS" | "Push" | "Web";

/** Campaign manager form type (US-7 prefill). */
export type CampaignEditorKind =
  | "pwp-item"
  | "pwp-discount"
  | "mix-match"
  | "product-discount"
  | "after-sales";

/** Approval / lifecycle state for backoffice (US-7). */
export type CampaignWorkflowState =
  | "draft"
  | "pending_approval"
  | "approved"
  | "active"
  | "deactivated"
  | "archived"
  | "rejected";

export type CampaignListRow = {
  id: string;
  name: string;
  code: string;
  status: CampaignStatus;
  channel: CampaignChannel;
  startDate: string;
  endDate: string;
  audience: string;
  budget: string;
  conversions: number;
  /** When set, editor opens this campaign type. Otherwise defaults to PWP-Item. */
  editorKind?: CampaignEditorKind;
  /** When set, overrides status→workflow mapping for the editor. */
  workflowState?: CampaignWorkflowState;
};

/** Maps list row status to workflow when `workflowState` is not set explicitly. */
export function deriveCampaignWorkflow(row?: CampaignListRow | null): CampaignWorkflowState {
  if (row?.workflowState) return row.workflowState;
  if (!row) return "draft";
  switch (row.status) {
    case "draft":
      return "draft";
    case "scheduled":
      return "pending_approval";
    case "active":
      return "active";
    case "paused":
      return "deactivated";
    case "ended":
      return "archived";
    default:
      return "draft";
  }
}

function statusPillClass(s: CampaignStatus) {
  switch (s) {
    case "active":
      return "bg-tertiary-container text-on-tertiary-container";
    case "scheduled":
      return "bg-secondary-fixed text-on-secondary-fixed-variant";
    case "draft":
      return "bg-surface-container-high text-on-surface-variant";
    case "paused":
      return "bg-amber-100 text-amber-900";
    case "ended":
      return "bg-error-container text-on-error-container";
    default:
      return "bg-outline-variant/20 text-on-surface-variant";
  }
}

function channelPillClass(ch: CampaignChannel) {
  switch (ch) {
    case "Email":
      return "border-primary/30 bg-primary/5 text-primary";
    case "SMS":
      return "border-secondary/30 bg-secondary-container/20 text-on-secondary-container";
    case "Push":
      return "border-tertiary/30 bg-tertiary-container/15 text-on-tertiary-container";
    case "Web":
      return "border-outline-variant/40 bg-surface-container-high text-on-surface-variant";
    default:
      return "border-outline-variant/30 bg-surface-container-high text-on-surface-variant";
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
    <button type="button" className="flex items-center gap-1 hover:text-primary transition-colors group" onClick={onToggle}>
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

export function createCampaignColumns(
  onView?: (id: string) => void,
  onEdit?: (id: string) => void
): ColumnDef<CampaignListRow>[] {
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
      enableHiding: false,
    },
    {
      accessorKey: "name",
      header: ({ column }) => (
        <SortHeader label="Campaign" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => (
        <div>
          <div className="text-xs font-bold text-on-surface">{row.getValue("name")}</div>
          <code className="mt-0.5 inline-block rounded bg-surface-container-high px-1.5 py-0.5 font-mono text-xs text-on-surface-variant">
            {row.original.code}
          </code>
        </div>
      ),
    },
    {
      accessorKey: "status",
      header: "Status",
      cell: ({ row }) => {
        const s = row.getValue("status") as CampaignStatus;
        return (
          <span
            className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[10px] font-bold uppercase ${statusPillClass(s)}`}
          >
            <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-current opacity-60" />
            {s}
          </span>
        );
      },
    },
    {
      accessorKey: "channel",
      header: "Channel",
      cell: ({ row }) => {
        const ch = row.getValue("channel") as CampaignChannel;
        return (
          <span className={`inline-block rounded-full border px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide ${channelPillClass(ch)}`}>
            {ch}
          </span>
        );
      },
    },
    {
      accessorKey: "startDate",
      header: ({ column }) => (
        <SortHeader label="Start" isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("startDate")}</span>,
    },
    {
      accessorKey: "endDate",
      header: "End",
      cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("endDate")}</span>,
    },
    {
      accessorKey: "audience",
      header: "Audience",
      cell: ({ row }) => (
        <span className="line-clamp-2 min-w-0 text-xs text-on-surface-variant" title={String(row.getValue("audience"))}>
          {row.getValue("audience")}
        </span>
      ),
    },
    {
      accessorKey: "budget",
      header: "Budget",
      cell: ({ row }) => <span className="text-xs font-semibold text-on-surface">{row.getValue("budget")}</span>,
    },
    {
      accessorKey: "conversions",
      header: ({ column }) => (
        <SortHeader label="Conv." isSorted={column.getIsSorted()} onToggle={() => column.toggleSorting(column.getIsSorted() === "asc")} />
      ),
      cell: ({ row }) => <span className="font-mono text-xs text-on-surface">{row.getValue("conversions")}</span>,
    },
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      enableSorting: false,
      cell: ({ row }) => (
        <div className="flex items-center justify-end gap-1">
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
            title="View"
            aria-label="View campaign"
            onClick={() => onView?.(row.original.id)}
          >
            <IconVisibility className="h-[18px] w-[18px]" />
          </button>
          <button
            type="button"
            className="rounded p-1.5 text-on-surface-variant transition-all hover:bg-white hover:text-primary"
            title="Edit"
            aria-label="Edit campaign"
            onClick={() => onEdit?.(row.original.id)}
          >
            <IconEdit className="h-[18px] w-[18px]" />
          </button>
        </div>
      ),
    },
  ];
}
