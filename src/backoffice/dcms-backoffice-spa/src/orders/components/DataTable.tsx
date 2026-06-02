import {
  type ColumnDef,
  type ColumnFiltersState,
  type RowSelectionState,
  type SortingState,
  type Updater,
  type VisibilityState,
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { useState } from "react";
import {
  IconChevronDown,
  IconChevronLeft,
  IconChevronRight,
  IconFirstPage,
  IconLastPage,
  IconSearch,
} from "../icons";

interface DataTableProps<TData, TValue> {
  columns: ColumnDef<TData, TValue>[];
  data: TData[];
  /** Optional columns hidden by default */
  defaultHiddenColumns?: string[];
  globalFilterPlaceholder?: string;
  /** Human-readable label overrides for the column visibility dropdown */
  columnLabels?: Record<string, string>;
  /** Stable row id for selection (defaults to row index). */
  getRowId?: (row: TData) => string;
  /** Controlled row selection — pass together with onRowSelectionChange (e.g. bulk bar outside table). */
  rowSelection?: RowSelectionState;
  onRowSelectionChange?: (updater: Updater<RowSelectionState>) => void;
  /** When true and data empty, show a loading row and disable footer controls. */
  loading?: boolean;
  /** Footer mode: default is page-based pagination. */
  footerMode?: "pagination" | "loadMore" | "none";
  /** Cursor pagination: show a load more button in footer. */
  loadMore?: { onClick: () => void; disabled?: boolean; label?: string };
  /** Message shown when there are no rows. */
  emptyMessage?: string;
  /** Plural noun for the footer count, e.g. "brands". */
  itemNoun?: string;
}

const PAGE_SIZES = [10, 25, 50, 100];

export function DataTable<TData, TValue>({
  columns,
  data,
  defaultHiddenColumns = [],
  globalFilterPlaceholder = "Search…",
  columnLabels = {},
  getRowId,
  rowSelection: rowSelectionProp,
  onRowSelectionChange: onRowSelectionChangeProp,
  loading = false,
  footerMode = "pagination",
  loadMore,
  emptyMessage = "No results found.",
  itemNoun = "results",
}: DataTableProps<TData, TValue>) {
  const [sorting, setSorting] = useState<SortingState>([]);
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [columnVisibility, setColumnVisibility] = useState<VisibilityState>(
    Object.fromEntries(defaultHiddenColumns.map((id) => [id, false]))
  );
  const [internalRowSelection, setInternalRowSelection] = useState<RowSelectionState>({});
  const [viewOptionsOpen, setViewOptionsOpen] = useState(false);

  const selectionControlled =
    rowSelectionProp !== undefined && onRowSelectionChangeProp !== undefined;
  const rowSelection = selectionControlled ? rowSelectionProp : internalRowSelection;
  const onRowSelectionChange = selectionControlled
    ? onRowSelectionChangeProp
    : setInternalRowSelection;

  const table = useReactTable({
    data,
    columns,
    state: { sorting, columnFilters, globalFilter, columnVisibility, rowSelection },
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    onGlobalFilterChange: setGlobalFilter,
    onColumnVisibilityChange: setColumnVisibility,
    onRowSelectionChange,
    getRowId: getRowId ? (row) => getRowId(row) : undefined,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 10 } },
  });

  const selectedCount = table.getFilteredSelectedRowModel().rows.length;
  const totalFiltered = table.getFilteredRowModel().rows.length;
  const { pageIndex, pageSize } = table.getState().pagination;
  const firstRow = pageIndex * pageSize + 1;
  const lastRow = Math.min((pageIndex + 1) * pageSize, totalFiltered);

  return (
    <div className="bg-surface-container-lowest rounded-xl shadow-sm border border-outline-variant/20 overflow-hidden flex flex-col">

      {/* ── Toolbar ───────────────────────────────────────────────────────── */}
      <div className="px-5 py-3 flex flex-wrap items-center gap-3 border-b border-outline-variant/10 bg-surface-container-low/30">
        {/* Global search */}
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <IconSearch className="absolute left-3 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-on-surface-variant pointer-events-none" />
          <input
            type="text"
            placeholder={globalFilterPlaceholder}
            value={globalFilter}
            onChange={(e) => setGlobalFilter(e.target.value)}
            className="w-full pl-8 pr-3 py-1.5 text-xs bg-surface-container-low border border-outline-variant/20 rounded-lg focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary"
          />
        </div>

        <div className="flex items-center gap-2 ml-auto">
          {/* Selection count */}
          {selectedCount > 0 && (
            <span className="text-[11px] text-on-surface-variant font-medium">
              {selectedCount} of {totalFiltered} selected
            </span>
          )}

          {/* Column visibility */}
          <div className="relative">
            <button
              type="button"
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold border border-outline-variant/30 rounded-lg bg-white hover:bg-surface-container-low transition-all text-on-surface"
              onClick={() => setViewOptionsOpen((v) => !v)}
            >
              Columns
              <IconChevronDown className="h-3 w-3" />
            </button>
            {viewOptionsOpen && (
              <>
                <div
                  className="fixed inset-0 z-10"
                  onClick={() => setViewOptionsOpen(false)}
                />
                <div className="absolute right-0 top-full mt-1 w-52 bg-white rounded-lg shadow-lg border border-outline-variant/20 z-20 py-2">
                  <p className="px-3 py-1 text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
                    Toggle Columns
                  </p>
                  {table
                    .getAllColumns()
                    .filter((col) => col.getCanHide())
                    .map((col) => (
                      <label
                        key={col.id}
                        className="flex items-center gap-2.5 px-3 py-1.5 text-xs text-on-surface hover:bg-surface-container-low cursor-pointer transition-colors"
                      >
                        <input
                          type="checkbox"
                          className="h-3.5 w-3.5 rounded border-outline-variant accent-primary"
                          checked={col.getIsVisible()}
                          onChange={(e) => col.toggleVisibility(e.target.checked)}
                        />
                        {columnLabels[col.id] ??
                        (typeof col.columnDef.header === "string" ? col.columnDef.header : col.id)}
                      </label>
                    ))}
                </div>
              </>
            )}
          </div>

          {/* Page size */}
          <div className="flex items-center gap-1.5">
            <span className="text-[11px] font-semibold text-on-surface-variant uppercase">Show</span>
            <select
              className="text-xs bg-transparent border border-outline-variant/20 rounded px-2 py-1 focus:ring-0 cursor-pointer font-bold text-on-surface"
              value={pageSize}
              onChange={(e) => table.setPageSize(Number(e.target.value))}
            >
              {PAGE_SIZES.map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* ── Table ─────────────────────────────────────────────────────────── */}
      <div className="overflow-x-auto custom-scrollbar">
        <table className="w-full text-left border-collapse">
          <thead>
            {table.getHeaderGroups().map((hg) => (
              <tr key={hg.id} className="bg-surface-container-high">
                {hg.headers.map((header) => (
                  <th
                    key={header.id}
                    className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap"
                  >
                    {header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.header, header.getContext())}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody className="divide-y divide-outline-variant/10">
            {table.getRowModel().rows.length > 0 ? (
              table.getRowModel().rows.map((row) => (
                <tr
                  key={row.id}
                  className={`hover:bg-surface-container-low transition-colors ${
                    row.getIsSelected() ? "bg-primary/5" : ""
                  }`}
                  data-state={row.getIsSelected() ? "selected" : undefined}
                >
                  {row.getVisibleCells().map((cell) => (
                    <td key={cell.id} className="px-4 py-2">
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </td>
                  ))}
                </tr>
              ))
            ) : loading ? (
              <tr>
                <td
                  colSpan={columns.length}
                  className="h-32 text-center text-sm text-on-surface-variant"
                >
                  Loading…
                </td>
              </tr>
            ) : (
              <tr>
                <td
                  colSpan={columns.length}
                  className="h-32 text-center text-sm text-on-surface-variant"
                >
                  {emptyMessage}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* ── Pagination footer ─────────────────────────────────────────────── */}
      {footerMode !== "none" && (
        <div className="px-5 py-3 flex flex-col sm:flex-row items-center justify-between gap-3 border-t border-outline-variant/10 bg-surface-container-low/20">
          <span className="text-xs text-on-surface-variant">
            {totalFiltered === 0 ? (
              "No results"
            ) : (
              <>
                Showing <span className="font-bold text-on-surface">{firstRow}</span>–
                <span className="font-bold text-on-surface">{lastRow}</span> of{" "}
                <span className="font-bold text-on-surface">{totalFiltered}</span> {itemNoun}
              </>
            )}
          </span>

          {footerMode === "loadMore" ? (
            <button
              type="button"
              className="rounded-lg bg-primary px-4 py-2 text-xs font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
              disabled={loading || loadMore?.disabled}
              onClick={loadMore?.onClick}
            >
              {loadMore?.label ?? "Load more"}
            </button>
          ) : (
            <div className="flex items-center gap-1">
              <PageButton
                onClick={() => table.setPageIndex(0)}
                disabled={loading || !table.getCanPreviousPage()}
                aria-label="First page"
              >
                <IconFirstPage />
              </PageButton>
              <PageButton
                onClick={() => table.previousPage()}
                disabled={loading || !table.getCanPreviousPage()}
                aria-label="Previous page"
              >
                <IconChevronLeft />
              </PageButton>

              {/* Page number pills */}
              {buildPageRange(table.getPageCount(), pageIndex).map((p, i) =>
                p === "…" ? (
                  <span key={`ellipsis-${i}`} className="px-2 text-outline text-xs">
                    …
                  </span>
                ) : (
                  <button
                    key={p}
                    type="button"
                    className={`w-8 h-8 flex items-center justify-center rounded-lg text-xs font-bold transition-colors ${
                      p === pageIndex
                        ? "bg-primary text-on-primary"
                        : "hover:bg-surface-container-high text-on-surface-variant"
                    }`}
                    onClick={() => table.setPageIndex(p as number)}
                    disabled={loading}
                  >
                    {(p as number) + 1}
                  </button>
                )
              )}

              <PageButton
                onClick={() => table.nextPage()}
                disabled={loading || !table.getCanNextPage()}
                aria-label="Next page"
              >
                <IconChevronRight />
              </PageButton>
              <PageButton
                onClick={() => table.setPageIndex(table.getPageCount() - 1)}
                disabled={loading || !table.getCanNextPage()}
                aria-label="Last page"
              >
                <IconLastPage />
              </PageButton>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function PageButton({
  children,
  disabled,
  onClick,
  ...rest
}: React.ButtonHTMLAttributes<HTMLButtonElement> & { disabled?: boolean }) {
  return (
    <button
      type="button"
      className={`p-1.5 rounded hover:bg-surface-container-high transition-colors ${
        disabled ? "text-outline/40 cursor-not-allowed" : "text-on-surface-variant"
      }`}
      disabled={disabled}
      onClick={onClick}
      {...rest}
    >
      {children}
    </button>
  );
}

/** Returns a compact page range with at most 5 visible page pills + ellipsis. */
function buildPageRange(pageCount: number, current: number): (number | "…")[] {
  if (pageCount <= 7) return Array.from({ length: pageCount }, (_, i) => i);

  const pages: (number | "…")[] = [];
  const addPage = (p: number) => {
    if (!pages.includes(p)) pages.push(p);
  };

  addPage(0);
  if (current > 2) pages.push("…");
  for (let p = Math.max(1, current - 1); p <= Math.min(pageCount - 2, current + 1); p++) addPage(p);
  if (current < pageCount - 3) pages.push("…");
  addPage(pageCount - 1);

  return pages;
}
