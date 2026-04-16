import { useMemo } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconAddCircle, IconAnalytics, IconArrowForward, IconDownload, IconHistory } from "../../orders/icons";
import { createAttributeColumns } from "../attributes-columns";
import type { AttributeListRow } from "../attributes-columns";

const ATTRIBUTE_ROWS: AttributeListRow[] = [
  { seq: "01", name: "Material Composition", code: "mat_composition", type: "TEXT",    required: true  },
  { seq: "02", name: "Primary Color",         code: "color_primary",  type: "COLOR",   required: true  },
  { seq: "03", name: "Product Lifestyle Image", code: "img_lifestyle", type: "IMAGE",  required: false },
  { seq: "04", name: "Washing Instructions",  code: "instruction_wash", type: "TEXT",  required: false },
  { seq: "05", name: "Country of Origin",     code: "geo_origin",    type: "SELECT",   required: true  },
];

export function AttributesPage() {
  const columns = useMemo(
    () => createAttributeColumns(
      (code) => console.info("[Attributes] Edit", code),
      (code) => console.info("[Attributes] Delete", code)
    ),
    []
  );

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Attributes">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Attributes</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Attributes Management</h1>
          <p className="text-sm text-on-surface-variant">
            Define and organize product metadata for global consistency.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-4 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant"
            onClick={() => console.info("[Attributes] Export schema (placeholder)")}
          >
            <IconDownload className="h-4 w-4 shrink-0" />
            Export Schema
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:bg-primary-container"
            onClick={() => console.info("[Attributes] New attribute (placeholder)")}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            New Attribute
          </button>
        </div>
      </header>

      <div className="flex-1 space-y-6 p-6">
        <DataTable
          columns={columns}
          data={ATTRIBUTE_ROWS}
          globalFilterPlaceholder="Search by name, code, or type…"
        />

        {/* Bottom cards */}
        <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <div className="relative overflow-hidden rounded-xl bg-gradient-to-br from-primary to-primary-container p-6 text-on-primary shadow-lg shadow-primary/20 md:col-span-2">
            <div className="relative z-10">
              <h3 className="mb-1 text-lg font-bold">Global Schema Health</h3>
              <p className="mb-4 text-sm text-primary-fixed/80">
                You have 12 attributes currently in draft mode across 3 attribute sets.
              </p>
              <button
                type="button"
                className="rounded bg-white px-4 py-2 text-xs font-bold uppercase tracking-wider text-primary transition-colors hover:bg-surface-bright"
                onClick={() => console.info("[Attributes] Review drafts (placeholder)")}
              >
                Review Drafts
              </button>
            </div>
            <IconAnalytics
              className="pointer-events-none absolute -bottom-4 -right-4 h-[120px] w-[120px] rotate-12 opacity-10"
              aria-hidden
            />
          </div>
          <div className="flex flex-col justify-between rounded-xl border border-outline-variant/20 bg-surface-container-highest p-6">
            <div>
              <IconHistory className="mb-2 h-8 w-8 text-primary" aria-hidden />
              <h3 className="font-bold text-on-surface">Recent Activity</h3>
              <p className="mt-1 text-xs text-on-surface-variant">
                Attribute &quot;Material Composition&quot; updated by Sarah M.
              </p>
            </div>
            <button
              type="button"
              className="mt-4 flex items-center gap-1 text-left text-[0.75rem] font-bold text-primary hover:underline"
              onClick={() => console.info("[Attributes] Audit log (placeholder)")}
            >
              View Audit Log
              <IconArrowForward className="h-3.5 w-3.5 shrink-0" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
