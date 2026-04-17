import { useEffect, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconCloudUpload,
  IconImage,
  IconInfo,
} from "../../orders/icons";

// ── Style tokens ─────────────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95";
const btnGhost =
  "text-xs font-bold uppercase tracking-widest text-on-surface-variant px-4 py-2.5 hover:bg-surface-container-high rounded-md transition-colors";

// ── Types ─────────────────────────────────────────────────────────────────────
type ImportRow = {
  id: string;
  sku: string;
  name: string;
  category: string;
  price: string;
  qty: number;
  hasImage: boolean;
  status: "ready" | "error";
  errorMsg?: string;
};

// ── Mock parsed rows (simulates spreadsheet parse result) ─────────────────────
const MOCK_IMPORT_ROWS: ImportRow[] = [
  { id: "r1", sku: "WT-550-B",  name: "Vantage Series 5 Watch",  category: "Timepieces > Luxury",     price: "$549.00", qty: 50,  hasImage: false, status: "ready"  },
  { id: "r2", sku: "AU-102-S",  name: "Echo-Noise Headphones",   category: "Audio > Wireless",        price: "$299.00", qty: 30,  hasImage: false, status: "ready"  },
  { id: "r3", sku: "FT-99-R",   name: "SwiftRun Pro Z",          category: "Footwear > Athletics",    price: "$120.00", qty: 100, hasImage: false, status: "ready"  },
  { id: "r4", sku: "CM-???",    name: "InstaCam Retro X",        category: "",                        price: "",        qty: 0,   hasImage: false, status: "error", errorMsg: "Category is required" },
];

type Props = { onBack: () => void };

// ── Component ─────────────────────────────────────────────────────────────────
export function ProductImportPage({ onBack }: Props) {
  // ── Step 1 state ──────────────────────────────────────────────────────────
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [stripHtml, setStripHtml] = useState(false);
  const [uploadDone, setUploadDone] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── Step 2 state ──────────────────────────────────────────────────────────
  const [rows, setRows] = useState<ImportRow[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // ── Toast ─────────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3500);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Handlers ─────────────────────────────────────────────────────────────
  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    setSelectedFile(e.target.files?.[0] ?? null);
  }

  function handleUpload() {
    if (!selectedFile) return;
    // Mock parse — in production this would send to API and return parsed rows
    setRows(MOCK_IMPORT_ROWS);
    setSelectedIds(
      new Set(MOCK_IMPORT_ROWS.filter((r) => r.status === "ready").map((r) => r.id))
    );
    setUploadDone(true);
  }

  function toggleRow(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  function toggleAll(checked: boolean) {
    setSelectedIds(checked ? new Set(rows.filter((r) => r.status === "ready").map((r) => r.id)) : new Set());
  }

  const readyRows = rows.filter((r) => r.status === "ready");
  const allSelected = readyRows.length > 0 && readyRows.every((r) => selectedIds.has(r.id));
  const someSelected = readyRows.some((r) => selectedIds.has(r.id));

  function handleImportAndApprove() {
    setToast(`${selectedIds.size} product${selectedIds.size !== 1 ? "s" : ""} imported and approved successfully.`);
    setTimeout(() => onBack(), 2200);
  }

  function handleRowImageDrop(rowId: string) {
    setRows((prev) => prev.map((r) => (r.id === rowId ? { ...r, hasImage: true } : r)));
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Products
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">Product Import</h2>
          <p className="mt-1 text-sm text-on-surface-variant">
            Bulk add or update products from a spreadsheet file.
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

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            {/* File picker */}
            <div className="space-y-2">
              <label className={labelBase}>Import File <span className="text-error">*</span></label>
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

            {/* Options + Upload button */}
            <div className="flex flex-col justify-between gap-4">
              <div>
                <label className={`${labelBase} mb-3`}>Options</label>
                <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface p-3 select-none hover:bg-surface-container-low transition-colors">
                  <input
                    type="checkbox"
                    className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                    checked={stripHtml}
                    onChange={(e) => setStripHtml(e.target.checked)}
                  />
                  <div>
                    <p className="text-xs font-semibold text-on-surface">Strip HTML</p>
                    <p className="text-[10px] text-on-surface-variant">Remove HTML tags from imported values</p>
                  </div>
                </label>
              </div>

              <button
                type="button"
                className={`${btnPrimary} self-start ${!selectedFile ? "pointer-events-none opacity-40" : ""}`}
                onClick={handleUpload}
                disabled={!selectedFile}
              >
                <IconCloudUpload className="h-4 w-4 shrink-0" />
                Upload File
              </button>
            </div>
          </div>

          {/* Template hint */}
          <div className="mt-5 flex items-start gap-3 rounded-md border border-outline-variant/10 bg-surface-container-low p-3">
            <IconInfo className="h-4 w-4 shrink-0 text-primary mt-0.5" />
            <p className="text-[11px] text-on-surface-variant leading-relaxed">
              Don't have a template? Use <strong>Export → Product Import Template</strong> from the Products page to download the spreadsheet template.
              Attribute values can be specified in separate columns (e.g. <code className="rounded bg-outline-variant/20 px-1">ATTRIB_COLOR</code>, <code className="rounded bg-outline-variant/20 px-1">ATTRIB_SIZE</code>) or as a single field separated by semicolons (<code className="rounded bg-outline-variant/20 px-1">;</code>).
            </p>
          </div>
        </section>

        {/* ── Step 2: Preview table ────────────────────────────────────── */}
        {uploadDone && (
          <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
            {/* Section header */}
            <div className="flex items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">2</span>
              <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Review & Import</h3>
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
                    <th className="px-4 py-3">SKU</th>
                    <th className="px-4 py-3">Product Name</th>
                    <th className="px-4 py-3">Category</th>
                    <th className="px-4 py-3 text-right">Price</th>
                    <th className="px-4 py-3 text-center">Qty</th>
                    <th className="px-4 py-3 text-center">Product Image</th>
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
                        className={`text-xs transition-colors ${
                          isSelected ? "bg-primary/5" : "hover:bg-surface-container-low"
                        } ${isError ? "opacity-60" : ""}`}
                      >
                        <td className="px-5 py-3 text-center">
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-primary"
                            checked={isSelected}
                            disabled={isError}
                            onChange={() => toggleRow(row.id)}
                          />
                        </td>
                        <td className="px-4 py-3 font-mono text-[10px] text-on-surface-variant">{row.sku}</td>
                        <td className="px-4 py-3 font-semibold text-on-surface">{row.name}</td>
                        <td className="px-4 py-3 text-on-surface-variant">{row.category || <span className="italic text-error/70">—</span>}</td>
                        <td className="px-4 py-3 text-right font-bold text-on-surface">{row.price || <span className="italic text-on-surface-variant">—</span>}</td>
                        <td className="px-4 py-3 text-center text-on-surface-variant">{row.qty}</td>
                        <td className="px-4 py-3 text-center">
                          {row.hasImage ? (
                            <div className="mx-auto flex h-10 w-10 items-center justify-center rounded border border-outline-variant/20 bg-secondary-container/20">
                              <IconImage className="h-5 w-5 text-secondary" />
                            </div>
                          ) : (
                            <div
                              className="mx-auto flex h-10 w-10 cursor-pointer items-center justify-center rounded border-2 border-dashed border-primary/20 bg-primary/5 transition-colors hover:border-primary/40 hover:bg-primary/10"
                              title="Drag & drop or click to upload image"
                              onClick={() => !isError && handleRowImageDrop(row.id)}
                              onDragOver={(e) => e.preventDefault()}
                              onDrop={(e) => { e.preventDefault(); if (!isError) handleRowImageDrop(row.id); }}
                            >
                              <IconCloudUpload className="h-4 w-4 text-primary/40" />
                            </div>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          {isError ? (
                            <span className="inline-flex flex-col gap-0.5">
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
                  className={`${btnPrimary} ${selectedIds.size === 0 ? "pointer-events-none opacity-40" : ""}`}
                  disabled={selectedIds.size === 0}
                  onClick={handleImportAndApprove}
                >
                  <IconCheckCircle className="h-4 w-4 shrink-0" />
                  Import and Approve
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
