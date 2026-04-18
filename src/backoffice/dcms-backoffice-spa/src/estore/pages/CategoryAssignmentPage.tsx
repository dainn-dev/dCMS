import { useEffect, useRef, useState } from "react";
import {
  IconAccountTree,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconCloudUpload,
  IconInfo,
  IconSearch,
} from "../../orders/icons";

// ── Style tokens ─────────────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95 disabled:pointer-events-none disabled:opacity-40";
const btnGhost =
  "text-xs font-bold uppercase tracking-widest text-on-surface-variant px-4 py-2.5 hover:bg-surface-container-high rounded-md transition-colors";

// ── Mock categories ───────────────────────────────────────────────────────────
const MOCK_CATEGORIES = [
  "@12%rebate",
  "Sub-category A",
  "Sub-category B",
  "1-12-REBATE",
  "Anniversary",
  "CGCategory",
  "Electronics",
  "Furniture",
  "Audio > Wireless",
  "Timepieces > Luxury",
  "Footwear > Athletics",
  "Cameras > Instant",
];

// ── Mock parsed rows (UPC/SKU file upload result) ─────────────────────────────
type AssignRow = {
  id: string;
  upc: string;
  sku: string;
  name: string;
  currentCategories: string[];
};

const MOCK_ASSIGN_ROWS: AssignRow[] = [
  { id: "a1", upc: "400234110", sku: "WT-550-B",  name: "Vantage Series 5 Watch",  currentCategories: ["Timepieces > Luxury"] },
  { id: "a2", upc: "400234111", sku: "AU-102-S",  name: "Echo-Noise Headphones",   currentCategories: ["Audio > Wireless"] },
  { id: "a3", upc: "400234115", sku: "FT-99-R",   name: "SwiftRun Pro Z",          currentCategories: ["Footwear > Athletics"] },
  { id: "a4", upc: "400234120", sku: "CM-2401-X", name: "InstaCam Retro X",        currentCategories: [] },
  { id: "a5", upc: "400234130", sku: "FN-700-B",  name: "NoiseFree Pro Buds",      currentCategories: ["Electronics"] },
];

// ── CategoryPicker ────────────────────────────────────────────────────────────
function CategoryPicker({
  selected,
  onChange,
}: {
  selected: string[];
  onChange: (next: string[]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  const filtered = MOCK_CATEGORIES.filter((c) =>
    c.toLowerCase().includes(search.toLowerCase())
  );

  function toggle(cat: string) {
    onChange(
      selected.includes(cat)
        ? selected.filter((c) => c !== cat)
        : [...selected, cat]
    );
  }

  return (
    <div ref={containerRef} className="relative space-y-1.5">
      {/* Selected chips */}
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((cat) => (
            <span
              key={cat}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
            >
              {cat}
              <button
                type="button"
                aria-label={`Remove ${cat}`}
                className="rounded p-0.5 hover:bg-primary/20 transition-colors"
                onClick={() => toggle(cat)}
              >
                <IconClose className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}

      {/* Trigger */}
      <button
        type="button"
        className="flex w-full items-center gap-2 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs transition-colors hover:border-primary/30 focus:outline-none focus:ring-1 focus:ring-primary"
        onClick={() => setOpen((o) => !o)}
      >
        <IconSearch className="h-3.5 w-3.5 shrink-0 text-on-surface-variant" />
        <span className={selected.length === 0 ? "flex-1 text-left text-on-surface-variant/60" : "flex-1 text-left text-on-surface"}>
          {selected.length === 0
            ? "Type to search categories…"
            : `${selected.length} categor${selected.length === 1 ? "y" : "ies"} selected`}
        </span>
        <IconChevronDown className={`h-3.5 w-3.5 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>

      {/* Dropdown */}
      {open && (
        <div className="absolute left-0 top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
          <div className="border-b border-outline-variant/10 p-2">
            <input
              autoFocus
              type="text"
              placeholder="Search…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full rounded bg-surface-container-low px-2.5 py-1.5 text-xs outline-none placeholder:text-on-surface-variant/50"
            />
          </div>
          <div className="max-h-48 overflow-y-auto">
            {filtered.length === 0 ? (
              <p className="px-3 py-3 text-center text-[10px] text-on-surface-variant">No categories found</p>
            ) : (
              filtered.map((cat) => (
                <label
                  key={cat}
                  className="flex cursor-pointer items-center gap-2.5 px-3 py-2 hover:bg-surface-container transition-colors select-none"
                >
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-primary"
                    checked={selected.includes(cat)}
                    onChange={() => toggle(cat)}
                  />
                  <span className="text-xs text-on-surface">{cat}</span>
                </label>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────
type Props = { onNavigateToProducts: () => void };

export function CategoryAssignmentPage({ onNavigateToProducts }: Props) {
  // ── Step 1 ────────────────────────────────────────────────────────────────
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadDone, setUploadDone] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── Step 2 ────────────────────────────────────────────────────────────────
  const [rows, setRows] = useState<AssignRow[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [targetCategories, setTargetCategories] = useState<string[]>([]);
  const [clearOther, setClearOther] = useState(false);

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
    setRows(MOCK_ASSIGN_ROWS);
    setSelectedIds(new Set(MOCK_ASSIGN_ROWS.map((r) => r.id)));
    setUploadDone(true);
  }

  function toggleRow(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  const allSelected = rows.length > 0 && rows.every((r) => selectedIds.has(r.id));
  const someSelected = rows.some((r) => selectedIds.has(r.id));

  function toggleAll(checked: boolean) {
    setSelectedIds(checked ? new Set(rows.map((r) => r.id)) : new Set());
  }

  function handleAssign() {
    const count = selectedIds.size;
    const cats = targetCategories.join(", ");
    setToast(
      `${count} product${count !== 1 ? "s" : ""} assigned to "${cats}" successfully.`
    );
  }

  function resetWizard() {
    setSelectedFile(null);
    setUploadDone(false);
    setRows([]);
    setSelectedIds(new Set());
    setTargetCategories([]);
    setClearOther(false);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  const canAssign = selectedIds.size > 0 && targetCategories.length > 0;

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-start md:justify-between">
        <div className="space-y-2 min-w-0">
          <nav className="mb-1 flex flex-wrap gap-x-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="text-on-surface-variant/50">/</span>
            <button type="button" className="text-primary hover:underline" onClick={onNavigateToProducts}>
              Products
            </button>
            <span className="text-on-surface-variant/50">/</span>
            <span className="text-primary">Category Assignment</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Category Assignment</h1>
          <p className="text-sm text-on-surface-variant max-w-2xl">
            Bulk-assign products to categories by uploading a UPC / SKU list.
          </p>
        </div>
      </header>

      {/* ── Body ────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* ── Step 1: Upload ───────────────────────────────────────────── */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${uploadDone ? "bg-secondary-container/30 text-secondary" : "bg-primary text-on-primary"}`}>
              {uploadDone ? <IconCheckCircle className="h-4 w-4" /> : "1"}
            </span>
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Upload UPC / SKU File</h3>
          </div>

          {/* Info banner */}
          <div className="mb-6 flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
            <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
            <div className="space-y-1 text-xs text-on-surface-variant leading-relaxed">
              <p className="font-semibold text-on-surface">File Requirements</p>
              <p>
                The data file must contain at least one of the following product identifiers per row:
              </p>
              <div className="flex gap-2">
                <code className="rounded bg-outline-variant/20 px-1.5 py-0.5 text-[10px]">UPC</code>
                <code className="rounded bg-outline-variant/20 px-1.5 py-0.5 text-[10px]">SKU</code>
              </div>
              <p>Accepted formats: <strong>.xlsx</strong>, <strong>.xls</strong>, <strong>.csv</strong></p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            {/* File picker */}
            <div className="space-y-2">
              <label className={labelBase}>Data File <span className="text-error">*</span></label>
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

        {/* ── Step 2: Assign ───────────────────────────────────────────── */}
        {uploadDone && (
          <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
            {/* Section header */}
            <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">2</span>
              <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Select Products &amp; Assign Categories</h3>
              <span className="ml-auto rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
                {rows.length} product{rows.length !== 1 ? "s" : ""} found
              </span>
            </div>

            {/* Category picker panel */}
            <div className="border-b border-outline-variant/10 bg-surface-container-low/50 px-6 py-5">
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                {/* Category dropdown */}
                <div className="space-y-2">
                  <label className={labelBase}>
                    Category <span className="text-error">*</span>
                  </label>
                  <p className="text-[10px] text-on-surface-variant -mt-1 mb-1">
                    Select one or more categories to assign to the selected products.
                  </p>
                  <CategoryPicker
                    selected={targetCategories}
                    onChange={setTargetCategories}
                  />
                </div>

                {/* Clear Other Categories checkbox */}
                <div className="flex flex-col justify-end gap-2">
                  <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                    <input
                      type="checkbox"
                      className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                      checked={clearOther}
                      onChange={(e) => setClearOther(e.target.checked)}
                    />
                    <div>
                      <p className="text-xs font-bold text-on-surface">Clear Other Categories</p>
                      <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                        Remove all existing category assignments from the selected products before applying the new ones.
                      </p>
                    </div>
                  </label>
                </div>
              </div>
            </div>

            {/* Product table */}
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
                        aria-label="Select all products"
                      />
                    </th>
                    <th className="px-4 py-3">UPC</th>
                    <th className="px-4 py-3">SKU</th>
                    <th className="px-4 py-3">Product Name</th>
                    <th className="px-4 py-3">Current Categories</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-outline-variant/10">
                  {rows.map((row) => {
                    const isSelected = selectedIds.has(row.id);
                    return (
                      <tr
                        key={row.id}
                        className={`cursor-pointer text-xs transition-colors ${isSelected ? "bg-primary/5" : "hover:bg-surface-container-low"}`}
                        onClick={() => toggleRow(row.id)}
                      >
                        <td className="px-5 py-3.5 text-center" onClick={(e) => e.stopPropagation()}>
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-primary"
                            checked={isSelected}
                            onChange={() => toggleRow(row.id)}
                          />
                        </td>
                        <td className="px-4 py-3.5 font-mono text-[10px] text-on-surface-variant">{row.upc}</td>
                        <td className="px-4 py-3.5 font-mono text-[10px] text-on-surface-variant">{row.sku}</td>
                        <td className="px-4 py-3.5 font-semibold text-on-surface">{row.name}</td>
                        <td className="px-4 py-3.5">
                          {row.currentCategories.length > 0 ? (
                            <div className="flex flex-wrap gap-1">
                              {row.currentCategories.map((c) => (
                                <span
                                  key={c}
                                  className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-medium text-on-surface-variant"
                                >
                                  {c}
                                </span>
                              ))}
                            </div>
                          ) : (
                            <span className="italic text-[10px] text-on-surface-variant/50">No categories</span>
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
                  {selectedIds.size} of {rows.length} product{rows.length !== 1 ? "s" : ""} selected
                </span>
              </div>

              <div className="flex items-center gap-3">
                <button type="button" className={btnGhost} onClick={resetWizard}>
                  Cancel
                </button>
                <button
                  type="button"
                  className={btnPrimary}
                  disabled={!canAssign}
                  onClick={handleAssign}
                >
                  <IconAccountTree className="h-4 w-4 shrink-0" />
                  Assign and Approve
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
