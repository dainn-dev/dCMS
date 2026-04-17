import { useEffect, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconCloudUpload,
  IconInfo,
} from "../../orders/icons";

// ── Style tokens ─────────────────────────────────────────────────────────────
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95 disabled:pointer-events-none disabled:opacity-40";
const btnGhost =
  "text-xs font-bold uppercase tracking-widest text-on-surface-variant px-4 py-2.5 hover:bg-surface-container-high rounded-md transition-colors";

// ── Types ─────────────────────────────────────────────────────────────────────
type ImportAction = "Replace" | "Merge";

type AttrImportRow = {
  id: string;
  attributeName: string;
  attributeCode: string;
  values: string[];
  action: ImportAction;
  status: "ready" | "error";
  errorMsg?: string;
};

// ── Mock parsed data ──────────────────────────────────────────────────────────
const MOCK_IMPORT_ROWS: AttrImportRow[] = [
  {
    id: "ai1",
    attributeName: "Material Composition",
    attributeCode: "mat_composition",
    values: ["Cotton", "Polyester", "Silk", "Wool", "Linen"],
    action: "Replace",
    status: "ready",
  },
  {
    id: "ai2",
    attributeName: "Primary Color",
    attributeCode: "color_primary",
    values: ["Red", "Blue", "Green", "Black", "White", "Yellow"],
    action: "Merge",
    status: "ready",
  },
  {
    id: "ai3",
    attributeName: "Country of Origin",
    attributeCode: "geo_origin",
    values: ["Malaysia", "Singapore", "Thailand", "Indonesia", "Vietnam"],
    action: "Replace",
    status: "ready",
  },
  {
    id: "ai4",
    attributeName: "Washing Instructions",
    attributeCode: "instruction_wash",
    values: ["Hand Wash Only", "Machine Wash Cold", "Dry Clean Only"],
    action: "Merge",
    status: "ready",
  },
  {
    id: "ai5",
    attributeName: "Primary Color (Duplicate)",
    attributeCode: "color_primary",
    values: ["Magenta", "Cyan"],
    action: "Merge",
    status: "error",
    errorMsg: "Duplicate attribute code in this file",
  },
];

// ── Component ─────────────────────────────────────────────────────────────────
type Props = { onBack: () => void };

export function AttributeImportPage({ onBack }: Props) {
  // ── Step 1 ────────────────────────────────────────────────────────────────
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadDone, setUploadDone] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── Step 2 ────────────────────────────────────────────────────────────────
  const [rows, setRows] = useState<AttrImportRow[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // ── Toast ─────────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Handlers ─────────────────────────────────────────────────────────────
  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    setSelectedFile(e.target.files?.[0] ?? null);
  }

  function handleUpload() {
    if (!selectedFile) return;
    const readyRows = MOCK_IMPORT_ROWS.filter((r) => r.status === "ready");
    setRows(MOCK_IMPORT_ROWS);
    setSelectedIds(new Set(readyRows.map((r) => r.id)));
    setUploadDone(true);
  }

  function toggleRow(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  const readyRows = rows.filter((r) => r.status === "ready");
  const allSelected = readyRows.length > 0 && readyRows.every((r) => selectedIds.has(r.id));
  const someSelected = readyRows.some((r) => selectedIds.has(r.id));

  function toggleAll(checked: boolean) {
    setSelectedIds(checked ? new Set(readyRows.map((r) => r.id)) : new Set());
  }

  function handleImport() {
    const count = selectedIds.size;
    setToast(`${count} attribute value set${count !== 1 ? "s" : ""} imported successfully.`);
    setTimeout(() => onBack(), 2200);
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 items-center gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Attributes
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">Attribute Values Import</h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Bulk-import or update attribute values from a spreadsheet.
          </p>
        </div>
      </div>

      {/* ── Body ────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* ── Step 1: File Upload ──────────────────────────────────────── */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${uploadDone ? "bg-secondary-container/30 text-secondary" : "bg-primary text-on-primary"}`}>
              {uploadDone ? <IconCheckCircle className="h-4 w-4" /> : "1"}
            </span>
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Upload File</h3>
          </div>

          {/* Info banner */}
          <div className="mb-6 flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
            <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
            <div className="space-y-2 text-xs text-on-surface-variant leading-relaxed">
              <p className="font-semibold text-on-surface">File Requirements</p>
              <p>
                The spreadsheet must include the following column headers in the first row:
              </p>
              <div className="flex flex-wrap gap-1.5">
                {["Attribute Name", "Attribute Code", "Values (semicolon-separated)"].map((h) => (
                  <code key={h} className="rounded bg-outline-variant/20 px-1.5 py-0.5 text-[10px]">{h}</code>
                ))}
              </div>
              <p>
                <strong>Note:</strong> Multiple values per attribute must be separated by{" "}
                <code className="rounded bg-outline-variant/20 px-1.5 py-0.5">;</code>
              </p>
              <p>
                Don't have a template? Use{" "}
                <strong>Generate Forms → Import Template</strong>{" "}
                on the Attributes page to download it.
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            {/* File picker */}
            <div className="space-y-2">
              <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                Import File <span className="text-error">*</span>
              </label>
              <div
                className="flex cursor-pointer items-center gap-4 rounded-lg border-2 border-dashed border-primary/20 bg-primary/5 px-5 py-4 transition-colors hover:border-primary/40 hover:bg-primary/10"
                onClick={() => fileInputRef.current?.click()}
              >
                <IconCloudUpload className="h-8 w-8 shrink-0 text-primary/50" />
                <div className="min-w-0">
                  {selectedFile ? (
                    <>
                      <p className="truncate text-sm font-semibold text-on-surface">{selectedFile.name}</p>
                      <p className="text-[10px] text-on-surface-variant">
                        {(selectedFile.size / 1024).toFixed(1)} KB · Click to change
                      </p>
                    </>
                  ) : (
                    <>
                      <p className="text-sm font-semibold text-primary/80">Choose File</p>
                      <p className="text-[10px] text-on-surface-variant">Accepts .xlsx, .xls, .csv</p>
                    </>
                  )}
                </div>
              </div>
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,.xls,.csv"
                className="hidden"
                onChange={handleFileChange}
              />
            </div>

            {/* Upload button */}
            <div className="flex items-end">
              <button
                type="button"
                className={btnPrimary}
                disabled={!selectedFile}
                onClick={handleUpload}
              >
                <IconCloudUpload className="h-4 w-4 shrink-0" />
                Upload File
              </button>
            </div>
          </div>
        </section>

        {/* ── Step 2: Review & Import ──────────────────────────────────── */}
        {uploadDone && (
          <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
            {/* Section header */}
            <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">2</span>
              <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Review Attribute Values</h3>
              <span className="ml-auto rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
                {rows.length} row{rows.length !== 1 ? "s" : ""} parsed
              </span>
            </div>

            {/* Table */}
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left">
                <thead>
                  <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                    <th className="w-10 px-5 py-3 text-center">
                      <input
                        type="checkbox"
                        className="h-3.5 w-3.5 accent-primary"
                        checked={allSelected}
                        ref={(el) => { if (el) el.indeterminate = !allSelected && someSelected; }}
                        onChange={(e) => toggleAll(e.target.checked)}
                        aria-label="Select all ready rows"
                      />
                    </th>
                    <th className="px-4 py-3">Attribute Name</th>
                    <th className="px-4 py-3">Attribute Code</th>
                    <th className="px-4 py-3">Values</th>
                    <th className="px-4 py-3">Action</th>
                    <th className="px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-outline-variant/10">
                  {rows.map((row) => {
                    const isSelected = selectedIds.has(row.id);
                    const isError = row.status === "error";
                    return (
                      <tr
                        key={row.id}
                        className={`text-xs transition-colors ${isSelected ? "bg-primary/5" : "hover:bg-surface-container-low"} ${isError ? "opacity-60" : ""}`}
                      >
                        <td className="px-5 py-3.5 text-center">
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-primary"
                            checked={isSelected}
                            disabled={isError}
                            onChange={() => toggleRow(row.id)}
                          />
                        </td>
                        <td className="px-4 py-3.5 font-semibold text-on-surface">{row.attributeName}</td>
                        <td className="px-4 py-3.5 font-mono text-[10px] text-on-surface-variant">{row.attributeCode}</td>
                        <td className="px-4 py-3.5">
                          <div className="flex flex-wrap gap-1">
                            {row.values.map((v) => (
                              <span
                                key={v}
                                className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-medium text-on-surface-variant"
                              >
                                {v}
                              </span>
                            ))}
                          </div>
                        </td>
                        <td className="px-4 py-3.5">
                          <span
                            className={`rounded-full px-2 py-0.5 text-[9px] font-bold ${
                              row.action === "Replace"
                                ? "bg-amber-50 text-amber-700"
                                : "bg-blue-50 text-blue-700"
                            }`}
                          >
                            {row.action}
                          </span>
                        </td>
                        <td className="px-4 py-3.5">
                          {isError ? (
                            <span className="flex flex-col gap-0.5">
                              <span className="rounded-full bg-error-container px-2 py-0.5 text-[9px] font-bold uppercase text-on-error-container">
                                Error
                              </span>
                              {row.errorMsg && (
                                <span className="text-[9px] text-error">{row.errorMsg}</span>
                              )}
                            </span>
                          ) : (
                            <span className="rounded-full bg-secondary-container/20 px-2 py-0.5 text-[9px] font-bold uppercase text-on-secondary-container">
                              Ready
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Footer action bar */}
            <div className="flex flex-col items-start justify-between gap-4 border-t border-outline-variant/10 px-6 py-4 sm:flex-row sm:items-center">
              <div className="flex items-center gap-3 text-sm text-on-surface-variant">
                <label className="flex cursor-pointer items-center gap-2 select-none">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-primary"
                    checked={allSelected}
                    ref={(el) => { if (el) el.indeterminate = !allSelected && someSelected; }}
                    onChange={(e) => toggleAll(e.target.checked)}
                  />
                  <span className="text-xs font-semibold">Select All</span>
                </label>
                <span className="text-[11px]">
                  {selectedIds.size} of {readyRows.length} row{readyRows.length !== 1 ? "s" : ""} selected
                </span>
              </div>

              <div className="flex items-center gap-3">
                <button type="button" className={btnGhost} onClick={onBack}>
                  Cancel
                </button>
                <button
                  type="button"
                  className={btnPrimary}
                  disabled={selectedIds.size === 0}
                  onClick={handleImport}
                >
                  <IconCheckCircle className="h-4 w-4 shrink-0" />
                  Import Values
                </button>
              </div>
            </div>
          </section>
        )}
      </div>

      {/* ── Toast ───────────────────────────────────────────────────────── */}
      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
