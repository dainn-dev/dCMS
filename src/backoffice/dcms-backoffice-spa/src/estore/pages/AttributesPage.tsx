import { useEffect, useMemo, useRef, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import {
  IconAddCircle,
  IconAnalytics,
  IconChevronDown,
  IconCloudUpload,
  IconDelete,
  IconDownload,
  IconHistory,
  IconWarning,
} from "../../orders/icons";
import { createAttributeColumns } from "../attributes-columns";
import type { AttributeListRow } from "../attributes-columns";
import {
  downloadAttributeValuesImportTemplateXlsx,
  exportAttributesSchemaXlsx,
} from "../exportAttributeTemplates";
import { fetchAttributes, deleteAttribute } from "../api/attributesApi";

type AttributesPageProps = {
  onCreateAttribute?: () => void;
  onEditAttribute?: (row: AttributeListRow) => void;
  onImportValues?: () => void;
  tenantId?: string;
  authToken?: string;
};

const LS_KEY = "dcms.estore.attributesList.v1";

const INITIAL_ATTRIBUTE_ROWS: AttributeListRow[] = [
  { seq: "01", name: "Material Composition", code: "mat_composition", type: "TEXT", required: true },
  { seq: "02", name: "Primary Color", code: "color_primary", type: "COLOR", required: true },
  { seq: "03", name: "Product Lifestyle Image", code: "img_lifestyle", type: "IMAGE", required: false },
  { seq: "04", name: "Washing Instructions", code: "instruction_wash", type: "TEXT", required: false },
  { seq: "05", name: "Country of Origin", code: "geo_origin", type: "SELECT", required: true },
];

const ATTR_TYPES = new Set<AttributeListRow["type"]>(["TEXT", "COLOR", "IMAGE", "SELECT", "BOOLEAN"]);

function loadAttributeRows(): AttributeListRow[] {
  try {
    const raw = localStorage.getItem(LS_KEY);
    if (!raw) return [...INITIAL_ATTRIBUTE_ROWS];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [...INITIAL_ATTRIBUTE_ROWS];
    const out = parsed
      .filter((x): x is Record<string, unknown> => !!x && typeof x === "object")
      .map((r) => {
        const t = r.type as string;
        const type = ATTR_TYPES.has(t as AttributeListRow["type"]) ? (t as AttributeListRow["type"]) : "TEXT";
        return {
          seq: String(r.seq ?? ""),
          name: String(r.name ?? ""),
          code: String(r.code ?? ""),
          type,
          required: !!r.required,
        };
      })
      .filter((r) => r.code);
    return out.length > 0 ? out : [...INITIAL_ATTRIBUTE_ROWS];
  } catch {
    return [...INITIAL_ATTRIBUTE_ROWS];
  }
}

function persistAttributeRows(rows: AttributeListRow[]) {
  try {
    localStorage.setItem(LS_KEY, JSON.stringify(rows));
  } catch {
    /* ignore */
  }
}

export function AttributesPage({ onCreateAttribute, onEditAttribute, onImportValues, tenantId, authToken }: AttributesPageProps) {
  const [rows, setRows] = useState<AttributeListRow[]>(() => loadAttributeRows());
  const [deleteTargetCode, setDeleteTargetCode] = useState<string | null>(null);
  const [apiLoading, setApiLoading] = useState(false);

  // Fetch from API on mount when tenantId provided
  useEffect(() => {
    if (!tenantId) return;
    let cancelled = false;
    setApiLoading(true);
    fetchAttributes(tenantId, authToken)
      .then((apiRows) => {
        if (!cancelled) {
          setRows(apiRows.map((r, i) => ({ ...r, seq: String(i + 1).padStart(2, "0") })));
        }
      })
      .catch((err) => { if (!cancelled) console.error("[AttributesPage] fetch failed", err); })
      .finally(() => { if (!cancelled) setApiLoading(false); });
    return () => { cancelled = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId, authToken]);

  const columns = useMemo(
    () =>
      createAttributeColumns(
        (code) => {
          const row = rows.find((r) => r.code === code);
          if (row) onEditAttribute?.(row);
        },
        (code) => setDeleteTargetCode(code)
      ),
    [onEditAttribute, rows]
  );

  const deleteTarget = rows.find((r) => r.code === deleteTargetCode);

  async function confirmDelete() {
    if (!deleteTargetCode) return;
    const target = rows.find(r => r.code === deleteTargetCode);
    if (tenantId && target?.id) {
      try {
        await deleteAttribute(tenantId, target.id, authToken);
      } catch (err) {
        console.error("[AttributesPage] delete failed", err);
        setDeleteTargetCode(null);
        return;
      }
    }
    setRows((prev) => {
      const next = prev.filter((r) => r.code !== deleteTargetCode);
      if (!tenantId) persistAttributeRows(next); // only persist to localStorage when offline
      return next;
    });
    setDeleteTargetCode(null);
  }

  // ── Actions dropdown ──────────────────────────────────────────────────────
  const [actionsOpen, setActionsOpen] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function handler(e: MouseEvent) {
      const root = actionsRef.current;
      if (!root) return;
      const path = e.composedPath();
      if (path.includes(root)) return;
      setActionsOpen(false);
    }
    document.addEventListener("click", handler, true);
    return () => document.removeEventListener("click", handler, true);
  }, []);

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low relative" aria-label="Attributes">
      {apiLoading && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-surface/60 backdrop-blur-sm">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      )}
      <header className="relative z-20 flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
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
            onClick={() => void exportAttributesSchemaXlsx(rows)}
          >
            <IconDownload className="h-4 w-4 shrink-0" />
            Export Schema
          </button>

          <div className="relative" ref={actionsRef}>
            <button
              type="button"
              className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-4 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant"
              onClick={() => setActionsOpen((o) => !o)}
            >
              Actions
              <IconChevronDown
                className={`h-3.5 w-3.5 shrink-0 text-on-surface-variant transition-transform ${actionsOpen ? "rotate-180" : ""}`}
              />
            </button>
            {actionsOpen && (
              <div
                className="absolute left-0 top-full z-30 mt-1 w-52 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl"
                onMouseDown={(e) => e.stopPropagation()}
              >
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => {
                    setActionsOpen(false);
                    void downloadAttributeValuesImportTemplateXlsx();
                  }}
                >
                  <IconDownload className="h-4 w-4 shrink-0 text-primary" />
                  Import Template
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => {
                    setActionsOpen(false);
                    onImportValues?.();
                  }}
                >
                  <IconCloudUpload className="h-4 w-4 shrink-0 text-primary" />
                  Import Values
                </button>
              </div>
            )}
          </div>

          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:bg-primary-container"
            onClick={() => onCreateAttribute?.()}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            New Attribute
          </button>
        </div>
      </header>

      <div className="flex-1 space-y-6 p-6">
        <DataTable columns={columns} data={rows} globalFilterPlaceholder="Search by name, code, or type…" emptyMessage="No attributes found." itemNoun="attributes" />

        {apiLoading ? null : rows.length > 0 && (
        <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
          <div className="relative overflow-hidden rounded-xl bg-gradient-to-br from-primary to-primary-container p-6 text-on-primary shadow-lg shadow-primary/20 md:col-span-2">
            <div className="relative z-10">
              <h3 className="mb-1 text-lg font-bold">Schema Overview</h3>
              <p className="mb-4 text-sm text-primary-fixed/80">
                {rows.length} attribute{rows.length !== 1 ? "s" : ""} defined
                {rows.filter((r) => r.required).length > 0
                  ? ` · ${rows.filter((r) => r.required).length} marked required`
                  : ""}
                {rows.filter((r) => r.useAsSearchFilter).length > 0
                  ? ` · ${rows.filter((r) => r.useAsSearchFilter).length} search filter${rows.filter((r) => r.useAsSearchFilter).length !== 1 ? "s" : ""}`
                  : ""}
              </p>
              <p className="text-xs text-primary-fixed/70">
                Types:{" "}
                {(["TEXT", "COLOR", "IMAGE", "SELECT", "BOOLEAN"] as const)
                  .map((t) => {
                    const n = rows.filter((r) => r.type === t).length;
                    return n > 0 ? `${n} ${t}` : null;
                  })
                  .filter(Boolean)
                  .join(" · ") || "—"}
              </p>
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
              {(() => {
                const latest = [...rows]
                  .filter((r) => r.updatedAt)
                  .sort((a, b) => (b.updatedAt ?? "").localeCompare(a.updatedAt ?? ""))[0];
                return latest ? (
                  <p className="mt-1 text-xs text-on-surface-variant">
                    Attribute &quot;{latest.name}&quot; last updated{" "}
                    {new Date(latest.updatedAt!).toLocaleString()}
                  </p>
                ) : (
                  <p className="mt-1 text-xs text-on-surface-variant">No update timestamps yet.</p>
                );
              })()}
            </div>
          </div>
        </div>
        )}
      </div>

      {deleteTargetCode && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete attribute</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Delete <strong className="text-on-surface">{deleteTarget?.name ?? deleteTargetCode}</strong> (
                  <code className="text-[10px]">{deleteTargetCode}</code>)? This cannot be undone.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteTargetCode(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={confirmDelete}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
