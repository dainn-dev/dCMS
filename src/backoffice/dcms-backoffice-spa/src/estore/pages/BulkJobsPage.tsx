import { useCallback, useEffect, useState } from "react";
import {
  cancelBulkJob,
  downloadBulkExportUrl,
  listBulkJobs,
  retryBulkJob,
  startCatalogImport,
  startOrdersExport,
  type BulkJobRow,
} from "../api/bulkJobsApi";
import { IconDownload } from "../../orders/icons";

type Props = {
  storeId: string;
};

const hangfirePath = "/umbraco/dcms/hangfire";

export function BulkJobsPage({ storeId }: Props) {
  const [rows, setRows] = useState<BulkJobRow[] | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [dateFrom, setDateFrom] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() - 7);
    return d.toISOString().slice(0, 10);
  });
  const [dateTo, setDateTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [file, setFile] = useState<File | null>(null);

  const refresh = useCallback(async () => {
    try {
      setErr(null);
      const r = await listBulkJobs(100);
      setRows(r);
    } catch (e) {
      setErr(e instanceof Error ? e.message : "Failed to load jobs.");
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => {
    const t = window.setInterval(() => void refresh(), 4000);
    return () => window.clearInterval(t);
  }, [refresh]);

  return (
    <div className="dcms-bulk-jobs p-4 max-w-6xl">
      <h1 className="text-2xl font-semibold text-gray-900">Bulk jobs</h1>
      <p className="text-sm text-gray-600 mt-1">
        Catalog CSV import and order exports run in the background (Hangfire). Open the{" "}
        <a className="text-blue-600 underline" href={hangfirePath} target="_blank" rel="noreferrer">
          job dashboard
        </a>{" "}
        for full queue details and retries.
      </p>

      {err && <div className="mt-3 text-sm text-red-700 bg-red-50 border border-red-200 rounded p-2">{err}</div>}

      <section className="mt-6 space-y-3 border border-gray-200 rounded-lg p-4">
        <h2 className="text-lg font-medium">Import catalog (CSV)</h2>
        <p className="text-sm text-gray-600">
          Header line is skipped. Columns: <code>slug</code>, <code>categoryId</code>, <code>nameVi</code>, optional{" "}
          <code>descriptionVi</code>.
        </p>
        <div className="flex flex-wrap items-end gap-3">
          <label className="text-sm text-gray-700">
            <span className="block mb-1">File</span>
            <input
              type="file"
              accept=".csv,text/csv"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </label>
          <button
            type="button"
            className="px-3 py-1.5 rounded bg-blue-600 text-white text-sm disabled:opacity-50"
            disabled={busy || !file}
            onClick={async () => {
              if (!file) return;
              setBusy(true);
              setErr(null);
              try {
                await startCatalogImport(file, storeId);
                setFile(null);
                await refresh();
              } catch (e) {
                setErr(e instanceof Error ? e.message : "Import failed");
              } finally {
                setBusy(false);
              }
            }}
          >
            Start import
          </button>
        </div>
      </section>

      <section className="mt-6 space-y-3 border border-gray-200 rounded-lg p-4">
        <h2 className="text-lg font-medium">Export orders (CSV)</h2>
        <div className="flex flex-wrap gap-3 items-end">
          <label className="text-sm text-gray-700">
            <span className="block mb-1">From</span>
            <input
              className="border border-gray-300 rounded px-2 py-1"
              type="date"
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
            />
          </label>
          <label className="text-sm text-gray-700">
            <span className="block mb-1">To</span>
            <input
              className="border border-gray-300 rounded px-2 py-1"
              type="date"
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
            />
          </label>
          <button
            type="button"
            className="px-3 py-1.5 rounded bg-blue-600 text-white text-sm disabled:opacity-50"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              setErr(null);
              try {
                await startOrdersExport({ dateFrom, dateTo, storeId });
                await refresh();
              } catch (e) {
                setErr(e instanceof Error ? e.message : "Export failed");
              } finally {
                setBusy(false);
              }
            }}
          >
            Start export
          </button>
        </div>
      </section>

      <section className="mt-6">
        <h2 className="text-lg font-medium mb-2">Recent jobs</h2>
        {rows == null ? (
          <p className="text-sm text-gray-500">Loading…</p>
        ) : (
          <div className="overflow-x-auto border border-gray-200 rounded">
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left">
                <tr>
                  <th className="p-2">Kind</th>
                  <th className="p-2">Status</th>
                  <th className="p-2">Progress</th>
                  <th className="p-2">Created</th>
                  <th className="p-2">Error</th>
                  <th className="p-2">Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((j) => (
                  <tr key={j.id} className="border-t border-gray-100">
                    <td className="p-2 font-mono text-xs">{j.jobKind}</td>
                    <td className="p-2">{j.status}</td>
                    <td className="p-2">
                      {j.progressTotal > 0 ? `${j.progressPercent}% (${j.progressProcessed}/${j.progressTotal})` : "—"}
                    </td>
                    <td className="p-2 text-gray-600">{j.createdAt}</td>
                    <td className="p-2 text-red-700 max-w-xs truncate" title={j.errorMessage ?? ""}>
                      {j.errorMessage ?? ""}
                    </td>
                    <td className="p-2 space-x-2">
                      {j.status === "succeeded" && j.jobKind === "orders_export" && j.outputBlobRef && (
                        <a
                          className="text-blue-600 inline-flex items-center gap-0.5"
                          href={downloadBulkExportUrl(j.id)}
                        >
                          <IconDownload className="h-3 w-3" />
                          CSV
                        </a>
                      )}
                      {(j.status === "queued" || j.status === "running") && (
                        <button
                          type="button"
                          className="text-amber-700"
                          onClick={async () => {
                            setBusy(true);
                            try {
                              await cancelBulkJob(j.id);
                              await refresh();
                            } catch (e) {
                              setErr(e instanceof Error ? e.message : "Cancel failed");
                            } finally {
                              setBusy(false);
                            }
                          }}
                        >
                          Cancel
                        </button>
                      )}
                      {j.status === "failed" && (
                        <button
                          type="button"
                          className="text-blue-600"
                          onClick={async () => {
                            setBusy(true);
                            try {
                              await retryBulkJob(j.id);
                              await refresh();
                            } catch (e) {
                              setErr(e instanceof Error ? e.message : "Retry failed");
                            } finally {
                              setBusy(false);
                            }
                          }}
                        >
                          Retry
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {rows.length === 0 && <p className="text-sm text-gray-500 p-2">No jobs yet.</p>}
          </div>
        )}
      </section>
    </div>
  );
}
