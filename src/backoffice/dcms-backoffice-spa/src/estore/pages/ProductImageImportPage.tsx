import { useEffect, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconCloudUpload,
  IconDelete,
  IconImage,
  IconInfo,
} from "../../orders/icons";

// ── Style tokens ─────────────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-5 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95 disabled:pointer-events-none disabled:opacity-40";
const btnSecondary =
  "flex items-center gap-2 rounded-md border border-outline-variant/30 bg-surface-container-lowest px-5 py-2.5 text-xs font-bold uppercase tracking-widest text-on-surface-variant transition-colors hover:bg-surface-container-high disabled:pointer-events-none disabled:opacity-40";
const btnDanger =
  "flex items-center gap-2 rounded-md border border-error/30 px-5 py-2.5 text-xs font-bold uppercase tracking-widest text-error transition-colors hover:bg-error/10 disabled:pointer-events-none disabled:opacity-40";

// ── Types ─────────────────────────────────────────────────────────────────────
type MatchField = "UPC" | "SKU" | "PID1" | "PID2";

type PreviewImage = {
  id: string;
  filename: string;
  identifier: string;
  productName: string;
  color: string;
  status: "matched" | "unmatched";
};

// ── Mock preview data ─────────────────────────────────────────────────────────
function buildMockImages(field: MatchField): PreviewImage[] {
  return [
    { id: "pi1", filename: "TEST001-1.jpg", identifier: field === "UPC" ? "400234110" : field === "SKU" ? "WT-550-B"  : "PID-001", productName: "Vantage Series 5 Watch", color: "#c7d2fe", status: "matched"   },
    { id: "pi2", filename: "TEST001-2.jpg", identifier: field === "UPC" ? "400234110" : field === "SKU" ? "WT-550-B"  : "PID-001", productName: "Vantage Series 5 Watch", color: "#c7d2fe", status: "matched"   },
    { id: "pi3", filename: "TEST002-1.jpg", identifier: field === "UPC" ? "400234111" : field === "SKU" ? "AU-102-S"  : "PID-002", productName: "Echo-Noise Headphones",  color: "#bbf7d0", status: "matched"   },
    { id: "pi4", filename: "TEST003-1.jpg", identifier: field === "UPC" ? "400234115" : field === "SKU" ? "FT-99-R"   : "PID-003", productName: "SwiftRun Pro Z",         color: "#fde68a", status: "matched"   },
    { id: "pi5", filename: "UNKNOWN-1.jpg", identifier: "???",                                                                      productName: "—",                     color: "#fecaca", status: "unmatched" },
  ];
}

type Props = { onBack: () => void };

// ── Component ─────────────────────────────────────────────────────────────────
export function ProductImageImportPage({ onBack }: Props) {
  // ── Step 1 state ──────────────────────────────────────────────────────────
  const [matchField, setMatchField] = useState<MatchField>("UPC");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── Confirmation dialog ───────────────────────────────────────────────────
  const [showConfirm, setShowConfirm] = useState(false);

  // ── Step 2 state ──────────────────────────────────────────────────────────
  const [images, setImages] = useState<PreviewImage[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [uploadDone, setUploadDone] = useState(false);

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

  function handleUploadClick() {
    if (!selectedFile) return;
    setShowConfirm(true);
  }

  function handleConfirmOk() {
    const parsed = buildMockImages(matchField);
    setImages(parsed);
    setSelectedIds(new Set(parsed.filter((i) => i.status === "matched").map((i) => i.id)));
    setUploadDone(true);
    setShowConfirm(false);
  }

  function toggleImage(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  function toggleAll(checked: boolean) {
    setSelectedIds(checked ? new Set(images.map((i) => i.id)) : new Set());
  }

  const allSelected = images.length > 0 && images.every((i) => selectedIds.has(i.id));
  const someSelected = images.some((i) => selectedIds.has(i.id));

  function doImport(ids: Set<string>) {
    const count = ids.size;
    setImages((prev) => prev.filter((i) => !ids.has(i.id)));
    setSelectedIds(new Set());
    setToast(`${count} image${count !== 1 ? "s" : ""} imported and approved successfully.`);
    setTimeout(() => onBack(), 2200);
  }

  function handleImportAll() {
    doImport(new Set(images.map((i) => i.id)));
  }

  function handleImportSelected() {
    if (selectedIds.size === 0) return;
    doImport(new Set(selectedIds));
  }

  function handleRemoveSelected() {
    setImages((prev) => prev.filter((i) => !selectedIds.has(i.id)));
    setSelectedIds(new Set());
  }

  function handleClear() {
    setImages([]);
    setSelectedIds(new Set());
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
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">Product Image Import</h2>
          <p className="mt-1 text-sm text-on-surface-variant">
            Bulk upload product images from a compressed ZIP file.
          </p>
        </div>
      </div>

      {/* ── Body ────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* ── Step 1: Setup & Upload ──────────────────────────────────── */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${uploadDone ? "bg-secondary-container/30 text-secondary" : "bg-primary text-on-primary"}`}>
              {uploadDone ? <IconCheckCircle className="h-4 w-4" /> : "1"}
            </span>
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Setup & Upload</h3>
          </div>

          {/* File naming instructions */}
          <div className="mb-6 flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
            <IconInfo className="h-5 w-5 shrink-0 text-primary mt-0.5" />
            <div className="space-y-1.5 text-xs text-on-surface-variant leading-relaxed">
              <p className="font-semibold text-on-surface">Image File Naming Convention</p>
              <p>Rename each image file as <code className="rounded bg-outline-variant/20 px-1.5 py-0.5 text-[10px]">&lt;identifier&gt;-&lt;priority&gt;</code> before uploading.</p>
              <ul className="ml-4 list-disc space-y-0.5">
                <li>First image: <code className="rounded bg-outline-variant/20 px-1 text-[10px]">TEST001-1</code></li>
                <li>Second image: <code className="rounded bg-outline-variant/20 px-1 text-[10px]">TEST001-2</code></li>
                <li>Third image: <code className="rounded bg-outline-variant/20 px-1 text-[10px]">TEST001-3</code></li>
              </ul>
              <p>Compress all image files into a single <strong>.zip</strong> file before uploading. Maximum file size: <strong>10 MB</strong> per image.</p>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            {/* Match field selector */}
            <div className="space-y-3">
              <label className={labelBase}>Match images by</label>
              <div className="grid grid-cols-2 gap-2">
                {(["UPC", "SKU", "PID1", "PID2"] as MatchField[]).map((field) => (
                  <label
                    key={field}
                    className={`flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors select-none ${
                      matchField === field
                        ? "border-primary/40 bg-primary/5 font-semibold text-primary"
                        : "border-outline-variant/20 hover:bg-surface-container-low text-on-surface"
                    }`}
                  >
                    <input
                      type="radio"
                      name="matchField"
                      className="h-4 w-4 accent-primary shrink-0"
                      checked={matchField === field}
                      onChange={() => setMatchField(field)}
                    />
                    <span className="text-xs font-semibold">{field}</span>
                  </label>
                ))}
              </div>
              <p className="text-[10px] text-on-surface-variant">
                {matchField === "PID1" || matchField === "PID2"
                  ? "Use PID to import a single image across multiple products (e.g. shoes in different sizes)."
                  : `Images will be matched to products by their ${matchField}.`}
              </p>
            </div>

            {/* File picker + upload */}
            <div className="flex flex-col justify-between gap-4">
              <div className="space-y-2">
                <label className={labelBase}>ZIP File <span className="text-error">*</span></label>
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
                        <p className="text-[10px] text-on-surface-variant">Accepts .zip only</p>
                      </>
                    )}
                  </div>
                </div>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".zip"
                  className="hidden"
                  onChange={handleFileChange}
                />
              </div>

              <button
                type="button"
                className={btnPrimary}
                disabled={!selectedFile}
                onClick={handleUploadClick}
              >
                <IconCloudUpload className="h-4 w-4 shrink-0" />
                Upload File
              </button>
            </div>
          </div>
        </section>

        {/* ── Step 2: Preview & Actions ────────────────────────────────── */}
        {uploadDone && (
          <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
            {/* Section header */}
            <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">2</span>
              <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Preview & Import</h3>
              <span className="ml-auto rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
                {images.length} image{images.length !== 1 ? "s" : ""} found
              </span>
            </div>

            {images.length === 0 ? (
              <div className="flex flex-col items-center justify-center gap-3 py-16 text-on-surface-variant">
                <IconImage className="h-12 w-12 opacity-30" />
                <p className="text-sm font-semibold">No images remaining</p>
              </div>
            ) : (
              <>
                {/* Image grid */}
                <div className="p-6">
                  <div className="mb-4 flex items-center gap-3">
                    <label className="flex cursor-pointer items-center gap-2 text-xs font-semibold text-on-surface-variant select-none">
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-primary"
                        checked={allSelected}
                        ref={(el) => { if (el) el.indeterminate = !allSelected && someSelected; }}
                        onChange={(e) => toggleAll(e.target.checked)}
                      />
                      Select All
                    </label>
                    <span className="text-[11px] text-on-surface-variant">
                      {selectedIds.size} of {images.length} selected
                    </span>
                  </div>

                  <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
                    {images.map((img) => {
                      const isSelected = selectedIds.has(img.id);
                      return (
                        <div
                          key={img.id}
                          className={`group relative overflow-hidden rounded-xl border transition-all ${
                            isSelected
                              ? "border-primary/40 ring-2 ring-primary/20"
                              : "border-outline-variant/20"
                          }`}
                        >
                          {/* Checkbox overlay */}
                          <label className="absolute left-2 top-2 z-10 cursor-pointer">
                            <input
                              type="checkbox"
                              className="h-4 w-4 accent-primary"
                              checked={isSelected}
                              onChange={() => toggleImage(img.id)}
                            />
                          </label>

                          {/* Thumbnail */}
                          <div
                            className="flex h-28 items-center justify-center"
                            style={{ backgroundColor: img.color }}
                          >
                            <IconImage className="h-10 w-10 text-white/60" />
                          </div>

                          {/* Info */}
                          <div className="bg-surface-container-lowest p-2.5 space-y-1">
                            <p className="truncate text-[10px] font-bold text-on-surface" title={img.filename}>
                              {img.filename}
                            </p>
                            <p className="truncate text-[9px] text-on-surface-variant" title={img.productName}>
                              {img.productName}
                            </p>
                            <div className="flex items-center justify-between">
                              <span className="text-[9px] font-mono text-on-surface-variant">{img.identifier}</span>
                              <span className={`rounded-full px-1.5 py-0.5 text-[8px] font-bold uppercase ${
                                img.status === "matched"
                                  ? "bg-secondary-container/20 text-on-secondary-container"
                                  : "bg-error-container text-on-error-container"
                              }`}>
                                {img.status}
                              </span>
                            </div>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>

                {/* Action bar */}
                <div className="flex flex-wrap items-center gap-3 border-t border-outline-variant/10 px-6 py-4">
                  <button
                    type="button"
                    className={btnPrimary}
                    onClick={handleImportAll}
                  >
                    <IconCheckCircle className="h-4 w-4 shrink-0" />
                    Import All
                  </button>
                  <button
                    type="button"
                    className={btnSecondary}
                    disabled={selectedIds.size === 0}
                    onClick={handleImportSelected}
                  >
                    <IconCheckCircle className="h-4 w-4 shrink-0" />
                    Import Selected
                  </button>
                  <button
                    type="button"
                    className={btnDanger}
                    disabled={selectedIds.size === 0}
                    onClick={handleRemoveSelected}
                  >
                    <IconDelete className="h-4 w-4 shrink-0" />
                    Remove Selected
                  </button>
                  <button
                    type="button"
                    className={`${btnDanger} ml-auto`}
                    onClick={handleClear}
                  >
                    <IconDelete className="h-4 w-4 shrink-0" />
                    Clear
                  </button>
                </div>
              </>
            )}
          </section>
        )}
      </div>

      {/* ── Confirmation dialog ──────────────────────────────────────────── */}
      {showConfirm && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setShowConfirm(false)}
        >
          <div
            className="mx-4 w-full max-w-sm rounded-2xl bg-surface-container-lowest p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-4 flex items-start gap-3">
              <IconCloudUpload className="h-6 w-6 shrink-0 text-primary mt-0.5" />
              <div>
                <h3 className="text-base font-bold text-on-surface">Proceed with image import?</h3>
                <p className="mt-1 text-sm text-on-surface-variant">
                  Images will be matched to products by <strong>{matchField}</strong>. Ensure the file names match the product's {matchField}.
                </p>
              </div>
            </div>
            <div className="mt-2 flex items-start gap-2 rounded-md border border-amber-200/60 bg-amber-50/60 px-3 py-2.5">
              <IconInfo className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
              <p className="text-[11px] text-amber-800">
                If images do not match the product {matchField}, they will be flagged as unmatched. Verify the file names and product records before importing.
              </p>
            </div>
            <div className="mt-5 flex justify-end gap-3">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2 text-xs font-bold uppercase tracking-widest text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setShowConfirm(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2 text-xs font-bold uppercase tracking-widest text-on-primary hover:opacity-90 transition-opacity"
                onClick={handleConfirmOk}
              >
                OK
              </button>
            </div>
          </div>
        </div>
      )}

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
