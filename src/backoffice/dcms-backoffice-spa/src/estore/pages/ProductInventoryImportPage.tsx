import { useEffect, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconCloudUpload,
  IconInfo,
} from "../../orders/icons";
import { uploadImport } from "../api/importsApi";
import { ImportProgressPanel } from "../components/ImportProgressPanel";

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95";
const btnGhost =
  "text-xs font-bold uppercase tracking-widest text-on-surface-variant px-4 py-2.5 hover:bg-surface-container-high rounded-md transition-colors";

type Props = { tenantId?: string; onBack: () => void };

export function ProductInventoryImportPage({ tenantId, onBack }: Props) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [jobId, setJobId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    setSelectedFile(e.target.files?.[0] ?? null);
  }

  async function handleUpload() {
    if (!selectedFile || !tenantId) return;
    setUploading(true);
    try {
      const r = await uploadImport(tenantId, "inventory", selectedFile);
      setJobId(r.jobId);
      setToast(`Job ${r.jobId} queued. Processing in background.`);
    } catch (e: any) {
      setToast(`Upload failed: ${e?.message ?? "unknown error"}`);
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
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
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">Inventory Import</h2>
          <p className="mt-1 text-sm text-on-surface-variant">
            Bulk update on-hand stock per (sku, store). Files are processed in the background.
          </p>
        </div>
      </div>

      <div className="flex-1 space-y-6 p-6 pb-24">
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${jobId ? "bg-secondary-container/30 text-secondary" : "bg-primary text-on-primary"}`}>
              {jobId ? <IconCheckCircle className="h-4 w-4" /> : "1"}
            </span>
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Upload File</h3>
          </div>

          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
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

            <div className="flex flex-col justify-end gap-4">
              <button
                type="button"
                className={`${btnPrimary} self-start ${(!selectedFile || !tenantId || uploading || !!jobId) ? "pointer-events-none opacity-40" : ""}`}
                onClick={handleUpload}
                disabled={!selectedFile || !tenantId || uploading || !!jobId}
              >
                <IconCloudUpload className="h-4 w-4 shrink-0" />
                {uploading ? "Uploading…" : jobId ? "Uploaded" : "Upload File"}
              </button>
              {!tenantId && (
                <p className="text-[10px] text-error">Tenant context required to upload.</p>
              )}
            </div>
          </div>

          <div className="mt-5 flex items-start gap-3 rounded-md border border-outline-variant/10 bg-surface-container-low p-3">
            <IconInfo className="h-4 w-4 shrink-0 text-primary mt-0.5" />
            <p className="text-[11px] text-on-surface-variant leading-relaxed">
              Required columns: <code className="rounded bg-outline-variant/20 px-1">sku</code>, <code className="rounded bg-outline-variant/20 px-1">store_id</code>, <code className="rounded bg-outline-variant/20 px-1">qty</code>.
              Quantities below the existing reserved amount are rejected as row errors.
            </p>
          </div>
        </section>

        {jobId && tenantId && (
          <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
            <div className="flex items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">2</span>
              <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Background Import</h3>
            </div>
            <div className="p-6">
              <ImportProgressPanel tenantId={tenantId} jobId={jobId} />
              <div className="mt-4 flex justify-end">
                <button type="button" className={btnGhost} onClick={onBack}>
                  Done
                </button>
              </div>
            </div>
          </section>
        )}
      </div>

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
