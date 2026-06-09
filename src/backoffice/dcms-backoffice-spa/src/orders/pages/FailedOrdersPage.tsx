import type { RowSelectionState } from "@tanstack/react-table";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { DataTable } from "../components/DataTable";
import { ConfirmResolveFailedOrdersDialog } from "../components/ConfirmResolveFailedOrdersDialog";
import { exportFailedOrdersToCsv } from "../exportFailedOrders";
import { FAILED_ORDER_COLUMN_LABELS, createFailedOrderColumns } from "../failed-orders-columns";
import { IconDownload } from "../icons";
import {
  FAILURE_STATUSES,
  fetchAllOrdersForExport,
  fetchOrders,
  isFailureStatus,
  mapUiStatusToApiFilter,
  resolveFailedOrder,
  retryFailedOrder,
  type OrderFilters,
} from "../api/ordersApi";
import type { FailedOrder, FailedOrderStatus, Order } from "../types";

const STATUS_ORDER: FailedOrderStatus[] = [...FAILURE_STATUSES];
const FAILURE_STATUS_API_FILTER = FAILURE_STATUSES.map(mapUiStatusToApiFilter).join(",");

type FilterId = "all" | FailedOrderStatus;

const STATUS_OPTIONS: Array<{ value: FilterId; label: string }> = [
  { value: "all", label: "All statuses" },
  ...STATUS_ORDER.map((s) => ({ value: s as FilterId, label: s })),
];

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
  onViewFailedOrder: (orderId: string) => void;
};

/**
 * DAI-637 / DAI-634 — A "failed order" is just an `Order` whose status is one
 * of the failure states. Map the `Order` rows from `fetchOrders` into the
 * `FailedOrder` shape that the existing columns expect. Failure-detail fields
 * (failureReason, failureLog, lineItems) live on the detail page (TODO once
 * backend exposes them — list table doesn't render them).
 */
function orderToFailedOrder(o: Order): FailedOrder {
  const status: FailedOrderStatus = isFailureStatus(o.status)
    ? (o.status as FailedOrderStatus)
    : "System Error";
  return {
    doNumber: o.doNumber,
    orderId: o.orderId,
    orderDate: o.orderDate,
    type: o.type,
    customerName: o.customerName,
    customerEmail: o.customerEmail,
    customerContactNo: o.customerContactNo,
    deliveryDate: o.deliveryDate,
    deliveryOption: o.deliveryOption,
    fulfilledDate: o.fulfilledDate,
    shippingStatus: o.shippingStatus,
    status,
    processedBy: o.processingOfficer,
    tags: o.tags,
    paymentMethod: o.paymentMethod ?? null,
    paymentGateway: o.paymentGateway,
    failureReason: String((o as unknown as { failureReason?: unknown }).failureReason ?? ""),
    failureErrorCode: ((o as unknown as { failureErrorCode?: unknown }).failureErrorCode as string | null | undefined) ?? null,
    failureAt: ((o as unknown as { failedAt?: unknown }).failedAt as string | null | undefined) ??
      ((o as unknown as { failureAt?: unknown }).failureAt as string | null | undefined) ??
      null,
    failureRetryCount: Number((o as unknown as { retryCount?: unknown }).retryCount ?? (o as unknown as { failureRetryCount?: unknown }).failureRetryCount ?? 0) || 0,
    failureLog: [],
    lineItems: [],
    store: o.store,
    storeAutoId: o.storeAutoId,
    shippingAddressLine1: o.shippingAddressLine1,
    shippingAddressLine2: o.shippingAddressLine2,
    shippingPostalCode: o.shippingPostalCode,
    shippingCountry: o.shippingCountry,
    billingAddressLine1: o.billingAddressLine1,
    billingAddressLine2: o.billingAddressLine2,
    billingPostalCode: o.billingPostalCode,
    billingCountry: o.billingCountry,
  };
}

export function FailedOrdersPage({ tenantId, storeId, authToken, onViewFailedOrder }: Props) {
  const [statusFilter, setStatusFilter] = useState<FilterId>("all");
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({});
  const [rows, setRows] = useState<FailedOrder[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [actionToast, setActionToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [resolveConfirmOpen, setResolveConfirmOpen] = useState(false);
  const [retryHintOpen, setRetryHintOpen] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [tabCounts, setTabCounts] = useState<Record<string, number>>(() => ({ all: 0 }));
  // Bumped after retry/resolve so the tab counts re-scan; the ref dedupes per (tenant,store,tick)
  // WITHOUT a state write inside the effect (a state write there would self-cancel the in-flight scan).
  const [countsTick, setCountsTick] = useState(0);
  const countsKeyRef = useRef<string>("");

  // Auto-dismiss toast after 3s (matches OrderProcessingPage pattern).
  useEffect(() => {
    if (!actionToast) return;
    const t = setTimeout(() => setActionToast(null), 3000);
    return () => clearTimeout(t);
  }, [actionToast]);

  const filters: OrderFilters = useMemo(() => {
    if (statusFilter === "all") return { status: FAILURE_STATUS_API_FILTER };
    return { status: mapUiStatusToApiFilter(statusFilter) };
  }, [statusFilter]);

  const refresh = useCallback(async () => {
    if (!tenantId || !storeId) {
      setRows([]);
      setNextCursor(null);
      setLoading(false);
      setError(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const out = await fetchOrders(tenantId, storeId, filters, { cursor: undefined, limit: 50 }, authToken);
      setRows(out.rows.filter((r) => isFailureStatus(r.status)).map(orderToFailedOrder));
      setNextCursor(out.nextCursor);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load failed orders");
      setRows([]);
      setNextCursor(null);
    } finally {
      setLoading(false);
    }
  }, [tenantId, storeId, authToken, filters]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  // Accurate tab counts: scan ALL failure statuses using cursor pagination (limit=100).
  // Cached by tenant/store so switching filters doesn't re-fetch counts.
  useEffect(() => {
    if (!tenantId || !storeId) {
      setTabCounts({ all: 0 });
      setCountsKey("");
      return;
    }

    const key = `${tenantId}::${storeId}::${countsTick}`;
    if (countsKeyRef.current === key) return;
    countsKeyRef.current = key;

    let cancelled = false;
    (async () => {
      try {
        const counts: Record<string, number> = { all: 0 };
        for (const s of STATUS_ORDER) counts[s] = 0;

        let cursor: string | null | undefined = undefined;
        for (;;) {
          const out = await fetchOrders(
            tenantId,
            storeId,
            { status: FAILURE_STATUS_API_FILTER },
            { cursor, limit: 100 },
            authToken
          );
          const failure = out.rows.filter((r) => isFailureStatus(r.status));
          for (const o of failure) {
            counts.all += 1;
            if (counts[o.status] !== undefined) counts[o.status] += 1;
          }
          if (!out.nextCursor || failure.length === 0 || out.rows.length === 0) break;
          cursor = out.nextCursor;
        }

        if (!cancelled) setTabCounts(counts);
      } catch {
        // If counts fail, keep existing counts (don't block page).
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken, countsTick]);

  const columns = useMemo(
    () => createFailedOrderColumns(onViewFailedOrder),
    [onViewFailedOrder]
  );

  const selectedOrderIds = useMemo(
    () => Object.keys(rowSelection).filter((id) => rowSelection[id]),
    [rowSelection]
  );
  const selectedCount = selectedOrderIds.length;

  async function runBulk(action: "retry" | "resolve", ids: string[]) {
    if (!tenantId || !storeId || ids.length === 0) return;
    setActionBusy(true);
    let succeeded = 0;
    let failed = 0;
    for (const id of ids) {
      try {
        if (action === "retry") {
          await retryFailedOrder(tenantId, storeId, id, authToken);
        } else {
          await resolveFailedOrder(tenantId, storeId, id, null, authToken);
        }
        succeeded += 1;
      } catch {
        failed += 1;
      }
    }
    setActionBusy(false);
    setActionToast(
      failed === 0
        ? { kind: "success", message: `${action === "retry" ? "Retry queued" : "Resolved"} for ${succeeded} order(s)` }
        : { kind: "error", message: `${succeeded} succeeded, ${failed} failed` }
    );
    if (failed === 0) setRowSelection({});
    await refresh();
    setCountsTick((t) => t + 1); // re-scan tab counts now that some orders left the failure set
  }

  async function loadMore() {
    if (!tenantId || !storeId) return;
    if (!nextCursor) return;
    setLoadingMore(true);
    setError(null);
    try {
      const out = await fetchOrders(tenantId, storeId, filters, { cursor: nextCursor, limit: 50 }, authToken);
      const more = out.rows.filter((r) => isFailureStatus(r.status)).map(orderToFailedOrder);
      setRows((prev) => [...prev, ...more]);
      setNextCursor(out.nextCursor);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load more failed orders");
    } finally {
      setLoadingMore(false);
    }
  }

  async function runExport() {
    if (!tenantId || !storeId) {
      setActionToast({ kind: "error", message: "Missing tenantId / storeId for export." });
      return;
    }
    setExporting(true);
    setError(null);
    try {
      const { rows, limited, total } = await fetchAllOrdersForExport(tenantId, storeId, filters, authToken, {
        limit: 5000,
        pageSize: 200,
      });
      const failureRows = rows.filter((r) => isFailureStatus(r.status)).map(orderToFailedOrder);
      exportFailedOrdersToCsv(failureRows);
      if (limited) {
        setError(`Export limited to first 5000 rows (total ${total}). Refine filters.`);
      } else {
        setActionToast({ kind: "success", message: `Exported ${failureRows.length} row(s)` });
      }
    } catch (e) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Export failed" });
    } finally {
      setExporting(false);
    }
  }

  return (
    <>
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Failed Orders</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Failed Orders</h1>
          <p className="text-sm text-on-surface-variant mt-1">Monitor and resolve order failures and payment issues</p>
        </div>

        <div className="flex items-center gap-3">
          <select
            className="h-10 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 text-sm text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as FilterId)}
            disabled={!tenantId || !storeId}
            aria-label="Status filter"
          >
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label} ({tabCounts[o.value] ?? 0})
              </option>
            ))}
          </select>
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2 disabled:opacity-40"
            onClick={() => void runExport()}
            disabled={!tenantId || !storeId || exporting}
          >
            <IconDownload />
            {exporting ? "Exporting…" : "Export"}
          </button>
        </div>
      </div>

      {actionToast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-outline-variant/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <p className={`text-sm font-semibold ${actionToast.kind === "error" ? "text-error" : "text-on-surface"}`}>
            {actionToast.message}
          </p>
        </div>
      )}

      {!tenantId || !storeId ? (
        <div className="mt-6 rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-6 text-sm text-on-surface-variant">
          Select a tenant + store to view failed orders.
        </div>
      ) : (
        <>
          {error && (
            <div className="mt-6 rounded-xl border border-error/25 bg-error/5 px-5 py-4 text-sm text-error">
              {error}
            </div>
          )}

          <div className="mt-6">
            <DataTable
              columns={columns}
              data={rows}
              defaultHiddenColumns={["processedBy", "tags", "deliveryOption", "fulfilledDate", "failureReason"]}
              columnLabels={FAILED_ORDER_COLUMN_LABELS}
              globalFilterPlaceholder="Search by order, customer, status…"
              getRowId={(row) => row.orderId}
              rowSelection={rowSelection}
              onRowSelectionChange={setRowSelection}
              emptyMessage="No failed orders found."
              itemNoun="orders"
              loading={(loading || loadingMore) && rows.length === 0}
              footerMode="loadMore"
              loadMore={{
                onClick: () => void loadMore(),
                disabled: loading || loadingMore || nextCursor === null,
                label: nextCursor ? (loadingMore ? "Loading…" : "Load more") : "No more",
              }}
            />
          </div>

          {selectedCount > 0 && (
            <div className="sticky bottom-3 z-30 mt-4 flex flex-wrap items-center gap-3 rounded-2xl border border-outline-variant/30 bg-surface-container-lowest px-4 py-3 shadow-lg ring-1 ring-black/5 transition-opacity duration-200">
              <span className="text-xs font-medium text-on-surface">
                <span className="font-bold tabular-nums text-primary">{selectedCount}</span>
                {` order${selectedCount === 1 ? "" : "s"} selected`}
              </span>
              <button
                type="button"
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-outline-variant/40 text-lg leading-none text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface transition-colors"
                aria-label="Deselect all"
                title="Clear selection"
                onClick={() => setRowSelection({})}
              >
                ×
              </button>
              <span className="hidden h-6 w-px shrink-0 bg-outline-variant/40 sm:block" aria-hidden />
              <div className="flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  disabled={actionBusy}
                  className="px-3 py-1.5 text-xs font-bold rounded-lg bg-white border border-outline-variant/40 text-on-surface hover:bg-surface-container-high transition-colors disabled:opacity-40"
                  onMouseEnter={() => setRetryHintOpen(true)}
                  onMouseLeave={() => setRetryHintOpen(false)}
                  onClick={() => runBulk("retry", selectedOrderIds)}
                >
                  {actionBusy ? "Working…" : "Retry"}
                </button>
                <button
                  type="button"
                  disabled={actionBusy}
                  className="px-3 py-1.5 text-xs font-bold rounded-lg bg-primary text-on-primary hover:opacity-90 transition-opacity disabled:opacity-40"
                  onClick={() => setResolveConfirmOpen(true)}
                >
                  {actionBusy ? "Working…" : "Mark Resolved"}
                </button>
              </div>
            </div>
          )}
        </>
      )}

      <ConfirmResolveFailedOrdersDialog
        open={resolveConfirmOpen}
        count={selectedCount}
        busy={actionBusy}
        onCancel={() => setResolveConfirmOpen(false)}
        onConfirm={() => {
          setResolveConfirmOpen(false);
          void runBulk("resolve", selectedOrderIds);
        }}
      />

      {retryHintOpen && selectedCount > 0 && (
        <div className="fixed bottom-20 left-1/2 z-50 -translate-x-1/2 w-[520px] max-w-[calc(100vw-2rem)] rounded-xl border border-outline-variant/20 bg-surface-container-lowest px-4 py-3 shadow-2xl">
          <p className="text-[11px] font-bold uppercase tracking-widest text-on-surface-variant mb-1">Retry hint</p>
          <p className="text-xs text-on-surface-variant leading-relaxed">
            {statusFilter === "Payment Failed"
              ? "Retry will re-attempt capture on same payment method."
              : statusFilter === "Address Error"
              ? "Retry without changes may fail again — edit address first."
              : statusFilter === "Auth Failed"
              ? "Retry will issue a new 3DS challenge."
              : statusFilter === "Stock Error"
              ? "Retry will re-check inventory — may still fail if stock is empty."
              : statusFilter === "System Error"
              ? "Retry will re-run the saga — contact engineering if it persists."
              : "Retry behavior depends on the failure type (payment/auth/address/stock/system)."}
          </p>
        </div>
      )}
    </>
  );
}
