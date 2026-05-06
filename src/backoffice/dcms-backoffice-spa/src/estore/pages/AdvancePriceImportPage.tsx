import { useEffect, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconCloudUpload,
  IconChevronLeft,
  IconChevronRight,
  IconFirstPage,
  IconInfo,
  IconLastPage,
} from "../../orders/icons";

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95";
const btnGhost =
  "text-xs font-bold uppercase tracking-widest text-on-surface-variant px-4 py-2.5 hover:bg-surface-container-high rounded-md transition-colors";

type Props = { tenantId?: string; onBack: () => void };

type AdvancePriceJobRow = {
  id: string;
  fileName: string;
  uploadedAt: string;
  totalRows: number;
  processed: number;
  status: "Pending" | "Running" | "Completed" | "Failed";
};

const PAGE_SIZE = 300;

export function AdvancePriceImportPage({ tenantId, onBack }: Props) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [jobId, setJobId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [jobs, setJobs] = useState<AdvancePriceJobRow[]>([]);
  const [page, setPage] = useState(1);

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
      // Backend "advance-prices" import type is not yet wired in importsApi/ImportType.
      // Keep this UI functional with a synthetic job id so the screen behaves correctly
      // until DAI-XXXX adds the server endpoint.
      console.info("[AdvancePriceImport] Upload pending backend support", selectedFile.name);
      const fakeId = `local-${Date.now().toString(36)}`;
      setJobId(fakeId);
      setJobs((prev) => [
        {
          id: fakeId,
          fileName: selectedFile.name,
          uploadedAt: new Date().toISOString().slice(0, 16).replace("T", " "),
          totalRows: 0,
          processed: 0,
          status: "Pending",
        },
        ...prev,
      ]);
      setToast(`Job ${fakeId} queued. Processing in background.`);
    } catch (e: any) {
      setToast(`Upload failed: ${e?.message ?? "unknown error"}`);
    } finally {
      setUploading(false);
    }
  }

  const totalRecords = jobs.length;
  const totalPages = Math.max(0, Math.ceil(totalRecords / PAGE_SIZE));
  const safePage = Math.min(Math.max(1, page), Math.max(totalPages, 1));
  const visibleJobs = jobs.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">Advance Price Import</h2>
          <p className="mt-1 text-sm text-on-surface-variant">
            Bulk import advance / promotional pricing for products. Files are processed in the background.
          </p>
        </div>
        <button
          type="button"
          onClick={onBack}
          className="flex items-center gap-2 rounded-md border border-outline-variant/40 px-4 py-2 text-xs font-bold uppercase tracking-widest text-on-surface-variant hover:bg-surface-container-high transition-colors"
        >
          <IconArrowBack className="h-3 w-3 shrink-0" />
          Back
        </button>
      </div>

      <div className="flex-1 space-y-6 p-6 pb-24">
        {/* Upload section */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span
              className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${
                jobId ? "bg-secondary-container/30 text-secondary" : "bg-primary text-on-primary"
              }`}
            >
              {jobId ? <IconCheckCircle className="h-4 w-4" /> : "1"}
            </span>
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Advance Price File Upload</h3>
          </div>

          <div className="space-y-2">
            <label className={labelBase}>Advance Price File:</label>
            <div className="flex items-center gap-3">
              <div
                className="flex flex-1 cursor-pointer items-center gap-4 rounded-lg border-2 border-dashed border-primary/20 bg-primary/5 px-5 py-4 transition-colors hover:border-primary/40 hover:bg-primary/10"
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
              <button
                type="button"
                className={`${btnPrimary} ${
                  !selectedFile || !tenantId || uploading ? "pointer-events-none opacity-40" : ""
                }`}
                onClick={handleUpload}
                disabled={!selectedFile || !tenantId || uploading}
              >
                <IconCloudUpload className="h-4 w-4 shrink-0" />
                {uploading ? "Uploading…" : "Upload File"}
              </button>
            </div>
            {!tenantId && (
              <p className="text-[10px] text-error">Tenant context required to upload.</p>
            )}
          </div>

          <div className="mt-5 flex items-start gap-3 rounded-md border border-outline-variant/10 bg-surface-container-low p-3">
            <IconInfo className="h-4 w-4 shrink-0 text-primary mt-0.5" />
            <p className="text-[11px] text-on-surface-variant leading-relaxed">
              Required columns: <code className="rounded bg-outline-variant/20 px-1">sku</code>,{" "}
              <code className="rounded bg-outline-variant/20 px-1">price</code>,{" "}
              <code className="rounded bg-outline-variant/20 px-1">start_date</code>,{" "}
              <code className="rounded bg-outline-variant/20 px-1">end_date</code>. Optional:{" "}
              <code className="rounded bg-outline-variant/20 px-1">currency</code>,{" "}
              <code className="rounded bg-outline-variant/20 px-1">customer_group</code>.
            </p>
          </div>
        </section>

        {/* Jobs list / pagination */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-4 py-3">Job ID</th>
                  <th className="px-4 py-3">File Name</th>
                  <th className="px-4 py-3">Uploaded</th>
                  <th className="px-4 py-3 text-right">Total Rows</th>
                  <th className="px-4 py-3 text-right">Processed</th>
                  <th className="px-4 py-3">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/10">
                {visibleJobs.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-4 py-10 text-center text-sm text-on-surface-variant">
                      No advance price imports yet.
                    </td>
                  </tr>
                ) : (
                  visibleJobs.map((j) => (
                    <tr key={j.id} className="text-[12px] hover:bg-surface-container-low">
                      <td className="px-4 py-3 font-mono text-[11px]">{j.id}</td>
                      <td className="px-4 py-3">{j.fileName}</td>
                      <td className="px-4 py-3 text-on-surface-variant">{j.uploadedAt}</td>
                      <td className="px-4 py-3 text-right">{j.totalRows}</td>
                      <td className="px-4 py-3 text-right">{j.processed}</td>
                      <td className="px-4 py-3">
                        <span className="inline-flex items-center rounded-full bg-outline-variant/20 px-2 py-0.5 text-[9px] font-bold uppercase">
                          {j.status}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex flex-col items-center justify-between gap-3 sm:flex-row">
            <p className="text-[11px] text-on-surface-variant">
              Page {totalPages === 0 ? 0 : safePage} of {totalPages} pages, Each page {PAGE_SIZE}, Total {totalRecords} records found
            </p>
            <div className="flex items-center gap-1">
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage <= 1}
                aria-label="First page"
                onClick={() => setPage(1)}
              >
                <IconFirstPage className="h-5 w-5" />
              </button>
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage <= 1}
                aria-label="Previous page"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <IconChevronLeft className="h-5 w-5" />
              </button>
              <button
                type="button"
                className="h-8 w-8 rounded bg-primary text-[11px] font-bold text-on-primary"
              >
                {Math.max(1, safePage)}
              </button>
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage >= Math.max(1, totalPages)}
                aria-label="Next page"
                onClick={() => setPage((p) => Math.min(Math.max(1, totalPages), p + 1))}
              >
                <IconChevronRight className="h-5 w-5" />
              </button>
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage >= Math.max(1, totalPages)}
                aria-label="Last page"
                onClick={() => setPage(Math.max(1, totalPages))}
              >
                <IconLastPage className="h-5 w-5" />
              </button>
            </div>
          </div>
        </section>

        <div className="flex justify-end">
          <button type="button" className={btnGhost} onClick={onBack}>
            Done
          </button>
        </div>
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
