import { useCallback, useEffect, useMemo, useState } from "react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../../orders/components/DataTable";
import {
  cancelBulkJob,
  downloadBulkExportUrl,
  listBulkJobs,
  retryBulkJob,
  startBrandImport,
  startCatalogImport,
  type BulkJobRow,
} from "../api/bulkJobsApi";
import {
  IconCancel,
  IconCheckCircle,
  IconCloudUpload,
  IconDownload,
  IconOpenInNew,
  IconRestartAlt,
  IconWarning,
} from "../../orders/icons";

type Props = {
  storeId: string;
};

const hangfirePath = "/umbraco/dcms/hangfire";

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const sectionHeading =
  "text-sm font-bold text-on-surface border-l-4 border-primary pl-3";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40";
const btnGhost =
  "flex items-center gap-2 rounded-md border border-outline-variant/40 px-4 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant";

type JobStatus = BulkJobRow["status"];

function StatusBadge({ status }: { status: JobStatus }) {
  const map: Record<string, string> = {
    succeeded: "bg-primary/10 text-primary",
    running: "bg-blue-500/10 text-blue-700",
    failed: "bg-error/10 text-error",
    queued: "bg-outline-variant/25 text-on-surface-variant",
    cancelled: "bg-outline-variant/15 text-on-surface-variant italic",
  };
  const cls = map[status] ?? "bg-outline-variant/25 text-on-surface-variant";
  return (
    <span
      className={`inline-flex shrink-0 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider ${cls}`}
    >
      {status}
    </span>
  );
}

function formatCreatedAt(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return iso;
  return d.toLocaleString();
}

export function BulkJobsPage({ storeId }: Props) {
  const [rows, setRows] = useState<BulkJobRow[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [brandFile, setBrandFile] = useState<File | null>(null);
  const [toast, setToast] = useState<{ message: string; visible: boolean; tone: "ok" | "error" }>({
    message: "",
    visible: false,
    tone: "ok",
  });

  const showToast = useCallback((message: string, tone: "ok" | "error" = "ok") => {
    setToast({ message, visible: true, tone });
  }, []);

  useEffect(() => {
    if (!toast.visible) return;
    const t = setTimeout(() => setToast((p) => ({ ...p, visible: false })), 3000);
    return () => clearTimeout(t);
  }, [toast.visible]);

  const refresh = useCallback(async () => {
    try {
      const r = await listBulkJobs(100);
      setRows(r);
    } catch (e) {
      showToast(e instanceof Error ? e.message : "Failed to load jobs.", "error");
    }
  }, [showToast]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => {
    const t = window.setInterval(() => void refresh(), 4000);
    return () => window.clearInterval(t);
  }, [refresh]);

  const handleStartImport = useCallback(async () => {
    if (!file) return;
    setBusy(true);
    try {
      await startCatalogImport(file, storeId);
      setFile(null);
      showToast("Catalog import queued.");
      await refresh();
    } catch (e) {
      showToast(e instanceof Error ? e.message : "Import failed", "error");
    } finally {
      setBusy(false);
    }
  }, [file, storeId, refresh, showToast]);

  const handleStartBrandImport = useCallback(async () => {
    if (!brandFile) return;
    setBusy(true);
    try {
      await startBrandImport(brandFile, storeId);
      setBrandFile(null);
      showToast("Brand import queued.");
      await refresh();
    } catch (e) {
      showToast(e instanceof Error ? e.message : "Brand import failed", "error");
    } finally {
      setBusy(false);
    }
  }, [brandFile, storeId, refresh, showToast]);

  const handleCancel = useCallback(
    async (id: string) => {
      setBusy(true);
      try {
        await cancelBulkJob(id);
        showToast("Job cancelled.");
        await refresh();
      } catch (e) {
        showToast(e instanceof Error ? e.message : "Cancel failed", "error");
      } finally {
        setBusy(false);
      }
    },
    [refresh, showToast],
  );

  const handleRetry = useCallback(
    async (id: string) => {
      setBusy(true);
      try {
        await retryBulkJob(id);
        showToast("Job re-queued.");
        await refresh();
      } catch (e) {
        showToast(e instanceof Error ? e.message : "Retry failed", "error");
      } finally {
        setBusy(false);
      }
    },
    [refresh, showToast],
  );

  const columns = useMemo<ColumnDef<BulkJobRow>[]>(
    () => [
      {
        id: "jobKind",
        header: "Kind",
        accessorKey: "jobKind",
        cell: (ctx) => (
          <span className="font-mono text-[11px] text-on-surface">
            {ctx.row.original.jobKind}
          </span>
        ),
      },
      {
        id: "status",
        header: "Status",
        accessorKey: "status",
        cell: (ctx) => <StatusBadge status={ctx.row.original.status} />,
      },
      {
        id: "progress",
        header: "Progress",
        cell: (ctx) => {
          const j = ctx.row.original;
          if (j.progressTotal <= 0) {
            return <span className="text-xs text-on-surface-variant">—</span>;
          }
          return (
            <div className="flex min-w-[140px] flex-col gap-1">
              <div className="flex items-center justify-between text-[11px] text-on-surface-variant">
                <span className="font-semibold tabular-nums text-on-surface">
                  {j.progressPercent}%
                </span>
                <span className="tabular-nums">
                  {j.progressProcessed}/{j.progressTotal}
                </span>
              </div>
              <div className="h-1 w-full overflow-hidden rounded-full bg-outline-variant/20">
                <div
                  className="h-full rounded-full bg-primary transition-all"
                  style={{ width: `${Math.min(100, j.progressPercent)}%` }}
                />
              </div>
            </div>
          );
        },
      },
      {
        id: "createdAt",
        header: "Created",
        accessorKey: "createdAt",
        cell: (ctx) => (
          <span className="text-xs text-on-surface-variant">
            {formatCreatedAt(ctx.row.original.createdAt)}
          </span>
        ),
      },
      {
        id: "errorMessage",
        header: "Error",
        accessorKey: "errorMessage",
        cell: (ctx) => {
          const msg = ctx.row.original.errorMessage;
          if (!msg) return <span className="text-xs text-on-surface-variant">—</span>;
          return (
            <span
              className="block max-w-xs truncate text-xs text-error"
              title={msg}
            >
              {msg}
            </span>
          );
        },
      },
      {
        id: "actions",
        header: "Actions",
        cell: (ctx) => {
          const j = ctx.row.original;
          const canDownload =
            j.status === "succeeded" && j.jobKind === "orders_export" && Boolean(j.outputBlobRef);
          const canCancel = j.status === "queued" || j.status === "running";
          const canRetry = j.status === "failed";
          return (
            <div className="flex items-center gap-1">
              {canDownload && (
                <a
                  href={downloadBulkExportUrl(j.id)}
                  title="Download CSV"
                  aria-label="Download CSV"
                  className="rounded p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container-high hover:text-primary"
                >
                  <IconDownload className="h-4 w-4" />
                </a>
              )}
              {canCancel && (
                <button
                  type="button"
                  title="Cancel job"
                  aria-label="Cancel job"
                  disabled={busy}
                  onClick={() => void handleCancel(j.id)}
                  className="rounded p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container-high hover:text-error disabled:opacity-40"
                >
                  <IconCancel className="h-4 w-4" />
                </button>
              )}
              {canRetry && (
                <button
                  type="button"
                  title="Retry job"
                  aria-label="Retry job"
                  disabled={busy}
                  onClick={() => void handleRetry(j.id)}
                  className="rounded p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container-high hover:text-primary disabled:opacity-40"
                >
                  <IconRestartAlt className="h-4 w-4" />
                </button>
              )}
              {!canDownload && !canCancel && !canRetry && (
                <span className="text-xs text-on-surface-variant">—</span>
              )}
            </div>
          );
        },
      },
    ],
    [busy, handleCancel, handleRetry],
  );

  return (
    <div
      className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low"
      aria-label="Bulk jobs management"
    >
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Bulk Jobs</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">
            Bulk Jobs
          </h1>
          <p className="text-sm text-on-surface-variant">
            Catalog and brand imports run in the background via Hangfire. Use the dashboard for full
            queue details, retries, and worker status.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <a
            href={hangfirePath}
            target="_blank"
            rel="noreferrer"
            className={btnGhost}
          >
            <IconOpenInNew className="h-4 w-4 shrink-0" />
            Open Hangfire Dashboard
          </a>
        </div>
      </header>

      <div className="flex-1 space-y-6 p-6">
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {/* ── Import catalog ─────────────────────────────────────────────── */}
          <section className="space-y-5 rounded-xl border border-outline-variant/15 bg-surface-container-lowest p-6 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary/10">
                <IconCloudUpload className="h-5 w-5 text-primary" />
              </div>
              <h2 className={sectionHeading}>Import Catalog (CSV / XLSX)</h2>
            </div>
            <p className="text-xs text-on-surface-variant">
              Format is auto-detected from the header row.
            </p>
            <ul className="list-disc space-y-1.5 pl-5 text-xs text-on-surface-variant">
              <li>
                <span className="font-semibold text-on-surface">Products:</span> columns{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  slug
                </code>
                ,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  categoryId
                </code>
                ,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  nameVi
                </code>
                , optional{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  descriptionVi
                </code>
                .
              </li>
              <li>
                <span className="font-semibold text-on-surface">Categories</span> (round-trips with
                the export file): columns{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  Code
                </code>
                ,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  Name
                </code>
                ,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  MetaTitle
                </code>
                , …. Hierarchy is derived from the{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  Code
                </code>{" "}
                prefix (e.g.{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  11-11-FASHION-MEN
                </code>{" "}
                nests under{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">
                  11-11-FASHION
                </code>
                ).
              </li>
            </ul>
            <div className="space-y-3">
              <div className="space-y-1.5">
                <label className={labelBase}>File</label>
                <input
                  type="file"
                  accept=".csv,text/csv,.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  className="block w-full text-xs text-on-surface file:mr-3 file:rounded-md file:border-0 file:bg-primary/10 file:px-3 file:py-2 file:text-xs file:font-semibold file:text-primary hover:file:bg-primary/15"
                />
                {file && (
                  <p className="text-[11px] text-on-surface-variant">
                    Selected:{" "}
                    <span className="font-semibold text-on-surface">{file.name}</span>{" "}
                    ({Math.round(file.size / 1024)} KB)
                  </p>
                )}
              </div>
              <button
                type="button"
                className={btnPrimary}
                disabled={busy || !file}
                onClick={() => void handleStartImport()}
              >
                <IconCloudUpload className="h-4 w-4" />
                Start Import
              </button>
            </div>
          </section>

          {/* ── Import brand ───────────────────────────────────────────────── */}
          <section className="space-y-5 rounded-xl border border-outline-variant/15 bg-surface-container-lowest p-6 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary/10">
                <IconCloudUpload className="h-5 w-5 text-primary" />
              </div>
              <h2 className={sectionHeading}>Import Brands (CSV / XLSX)</h2>
            </div>
            <p className="text-xs text-on-surface-variant">
              Round-trips with the brand export file. Existing brands (matched by{" "}
              <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Code</code>)
              are updated in place; unknown codes are created.
            </p>
            <ul className="list-disc space-y-1.5 pl-5 text-xs text-on-surface-variant">
              <li>
                <span className="font-semibold text-on-surface">Required:</span>{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Code</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Name</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Active</code>{" "}
                (<code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">True</code> /{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">False</code>).
              </li>
              <li>
                <span className="font-semibold text-on-surface">Content:</span>{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">DisplayName</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Description</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Categories</code>{" "}
                (pipe-separated),{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">BrandImage</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">MobileImage</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Video</code>.
              </li>
              <li>
                <span className="font-semibold text-on-surface">Contact &amp; SEO:</span>{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">ContactPerson</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">ContactPersonEmail</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">ContactPersonPhoneNo</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">OfficeNo1</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">OfficeNo2</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">MetaTitle</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">MetaKeywords</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">MetaDescription</code>.
              </li>
              <li>
                <span className="font-semibold text-on-surface">Schedule:</span>{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">PublishedFrom</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">PublishedTo</code>{" "}
                — accepts <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">dd/MM/yyyy HH:mm:ss</code>{" "}
                or ISO 8601.
              </li>
              <li>
                <span className="font-semibold text-on-surface">Y / N flags:</span>{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Available_Online</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Available_Offline_Store</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Different_Layout</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Gift_Wrap</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Gift_Message</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Exclude_Rebates_Redemption</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Enable_CRM_Membership</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">Disable_Ads</code>.
              </li>
              <li>
                <span className="font-semibold text-on-surface">Multi-language:</span>{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">CRM_Description</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">tangsPlazaOpenTime</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">tangsVivoOpenTime</code>,{" "}
                etc. — append{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">_ZH</code> /{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">_VN</code> /{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">_JA</code> for translations.
              </li>
              <li>
                <span className="font-semibold text-on-surface">Counter info</span> (per outlet):{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">tangsPlazaEnabled</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">tangsPlazaFloor</code>,{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">tangsPlazaCounterNumber</code>{" "}
                (same shape for{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">tangsVivo*</code>).
              </li>
              <li>
                Any unrecognised columns are stored in the brand&apos;s{" "}
                <code className="rounded bg-surface-container-high px-1 py-0.5 text-[10px]">AdditionalInfo</code>{" "}
                JSON — round-trip safe.
              </li>
            </ul>
            <div className="space-y-3">
              <div className="space-y-1.5">
                <label className={labelBase}>File</label>
                <input
                  type="file"
                  accept=".csv,text/csv,.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                  onChange={(e) => setBrandFile(e.target.files?.[0] ?? null)}
                  className="block w-full text-xs text-on-surface file:mr-3 file:rounded-md file:border-0 file:bg-primary/10 file:px-3 file:py-2 file:text-xs file:font-semibold file:text-primary hover:file:bg-primary/15"
                />
                {brandFile && (
                  <p className="text-[11px] text-on-surface-variant">
                    Selected:{" "}
                    <span className="font-semibold text-on-surface">{brandFile.name}</span>{" "}
                    ({Math.round(brandFile.size / 1024)} KB)
                  </p>
                )}
              </div>
              <button
                type="button"
                className={btnPrimary}
                disabled={busy || !brandFile}
                onClick={() => void handleStartBrandImport()}
              >
                <IconCloudUpload className="h-4 w-4" />
                Start Import
              </button>
            </div>
          </section>
        </div>

        {/* ── Recent jobs ────────────────────────────────────────────────── */}
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className={sectionHeading}>Recent Jobs</h2>
            <span className="text-[11px] text-on-surface-variant">
              Auto-refreshing every 4 s
            </span>
          </div>
          {rows == null ? (
            <div className="flex items-center justify-center gap-3 rounded-xl border border-outline-variant/15 bg-surface-container-lowest py-16 text-sm text-on-surface-variant shadow-sm">
              <svg
                className="h-5 w-5 animate-spin text-primary"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
              </svg>
              Loading jobs…
            </div>
          ) : (
            <DataTable
              columns={columns}
              data={rows}
              globalFilterPlaceholder="Search jobs by kind or status…"
            />
          )}
        </section>
      </div>

      {/* ── Toast ───────────────────────────────────────────────────────── */}
      <div
        aria-live="polite"
        className={`fixed bottom-6 left-1/2 z-[60] -translate-x-1/2 transition-all duration-300 ${
          toast.visible ? "translate-y-0 opacity-100" : "translate-y-4 opacity-0 pointer-events-none"
        }`}
      >
        <div className="flex items-center gap-3 rounded-xl bg-on-surface px-5 py-3 shadow-2xl">
          {toast.tone === "error" ? (
            <IconWarning className="h-4 w-4 shrink-0 text-error" />
          ) : (
            <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
          )}
          <span className="text-sm font-medium text-surface">{toast.message}</span>
          <button
            type="button"
            aria-label="Dismiss"
            className="ml-2 rounded p-0.5 text-surface/60 transition-colors hover:text-surface"
            onClick={() => setToast((p) => ({ ...p, visible: false }))}
          >
            ✕
          </button>
        </div>
      </div>
    </div>
  );
}
