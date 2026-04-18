import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconDownload } from "../../orders/icons";
import { exportReportRowsToXlsx } from "./exportReportRowsToXlsx";
import { ReportFilterField, ReportFilterPanel, inputClass } from "./ReportFilterPanel";
import { useReportExportState } from "./useReportExport";

export type ReportFilters = {
  dateFrom: string;
  dateTo: string;
  storeScope: string;
};

type Props<T extends { id: string }> = {
  breadcrumb: string;
  title: string;
  description: string;
  exportSheetName: string;
  exportFilename: string;
  columns: ColumnDef<T, unknown>[];
  exportHeaders: string[];
  toExportRow: (row: T) => string[];
  /** Demo fetch — replace with API later. */
  loadData: (filters: ReportFilters) => Promise<T[]>;
  emptyMessage?: string;
};

const STORE_OPTIONS = [
  { value: "all", label: "All stores" },
  { value: "sg-flagship", label: "SG — Flagship" },
  { value: "my-central", label: "MY — Central" },
];

export function ReportView<T extends { id: string }>({
  breadcrumb,
  title,
  description,
  exportSheetName,
  exportFilename,
  columns,
  exportHeaders,
  toExportRow,
  loadData,
  emptyMessage = "No rows match the current filters. Adjust filters and click Search.",
}: Props<T>) {
  const [dateFrom, setDateFrom] = useState("2026-04-01");
  const [dateTo, setDateTo] = useState("2026-04-18");
  const [storeScope, setStoreScope] = useState("all");
  const [rows, setRows] = useState<T[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  const { exportDisabled } = useReportExportState(loading, rows.length);

  const filters = useMemo(
    (): ReportFilters => ({ dateFrom, dateTo, storeScope }),
    [dateFrom, dateTo, storeScope]
  );

  const handleSearch = useCallback(async () => {
    setLoading(true);
    setHasSearched(true);
    try {
      const next = await loadData(filters);
      setRows(next);
    } finally {
      setLoading(false);
    }
  }, [filters, loadData]);

  const handleReset = useCallback(() => {
    setDateFrom("2026-04-01");
    setDateTo("2026-04-18");
    setStoreScope("all");
    setRows([]);
    setHasSearched(false);
  }, []);

  const handleExport = useCallback(async () => {
    if (rows.length === 0) return;
    const body = rows.map((r) => toExportRow(r));
    await exportReportRowsToXlsx(exportSheetName, exportFilename, exportHeaders, body);
  }, [exportFilename, exportHeaders, exportSheetName, rows, toExportRow]);

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label={title}>
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>Reports</span>
            <span className="mx-2">/</span>
            <span>Reports</span>
            <span className="mx-2">/</span>
            <span className="text-primary">{breadcrumb}</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">{title}</h1>
          <p className="max-w-2xl text-sm text-on-surface-variant">{description}</p>
        </div>
        <button
          type="button"
          disabled={exportDisabled}
          className="flex items-center gap-2 self-start rounded-lg border border-outline-variant/30 bg-white px-4 py-2 text-xs font-bold text-on-surface shadow-sm hover:bg-surface-container-high disabled:pointer-events-none disabled:opacity-40 md:self-center"
          onClick={() => void handleExport()}
        >
          <IconDownload className="h-4 w-4 shrink-0 text-secondary" />
          Export to Excel
        </button>
      </header>

      <div className="w-full flex-1 space-y-6 p-6">
        <ReportFilterPanel onSearch={() => void handleSearch()} onReset={handleReset} searchDisabled={loading}>
          <ReportFilterField label="From" htmlFor="report-from">
            <input
              id="report-from"
              type="date"
              className={inputClass}
              value={dateFrom}
              onChange={(e) => setDateFrom(e.target.value)}
            />
          </ReportFilterField>
          <ReportFilterField label="To" htmlFor="report-to">
            <input
              id="report-to"
              type="date"
              className={inputClass}
              value={dateTo}
              onChange={(e) => setDateTo(e.target.value)}
            />
          </ReportFilterField>
          <ReportFilterField label="Store scope" htmlFor="report-store">
            <select
              id="report-store"
              className={inputClass}
              value={storeScope}
              onChange={(e) => setStoreScope(e.target.value)}
            >
              {STORE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
        </ReportFilterPanel>

        {loading && (
          <div
            className="flex items-center justify-center rounded-xl border border-outline-variant/20 bg-surface-container-lowest py-16 text-sm font-medium text-on-surface-variant"
            role="status"
            aria-live="polite"
          >
            Loading report…
          </div>
        )}

        {!loading && hasSearched && rows.length === 0 && (
          <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-outline-variant/30 bg-surface-container-lowest py-16 px-6 text-center">
            <p className="text-sm font-semibold text-on-surface">No results</p>
            <p className="mt-2 max-w-md text-xs text-on-surface-variant">{emptyMessage}</p>
          </div>
        )}

        {!loading && rows.length > 0 && (
          <DataTable columns={columns} data={rows} getRowId={(row) => row.id} globalFilterPlaceholder="Search in results…" />
        )}

        {!loading && !hasSearched && (
          <p className="rounded-xl border border-outline-variant/15 bg-surface-container-low/50 px-4 py-3 text-center text-xs text-on-surface-variant">
            Choose filters and click <span className="font-bold text-on-surface">Search</span> to load the report.
          </p>
        )}
      </div>
    </div>
  );
}
