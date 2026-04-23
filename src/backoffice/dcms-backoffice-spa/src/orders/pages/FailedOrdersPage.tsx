import type { RowSelectionState } from "@tanstack/react-table";
import { useCallback, useEffect, useMemo, useState } from "react";
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
  resolveFailedOrder,
  retryFailedOrder,
  type OrderFilters,
} from "../api/ordersApi";
import type { FailedOrder, FailedOrderStatus, Order } from "../types";

const STATUS_ORDER: FailedOrderStatus[] = [...FAILURE_STATUSES];

type FilterId = "all" | FailedOrderStatus;

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
  const [toast, setToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [resolveConfirmOpen, setResolveConfirmOpen] = useState(false);
  const [retryHintOpen, setRetryHintOpen] = useState(false);
  const [tabCounts, setTabCounts] = useState<Record<string, number>>(() => ({ all: 0 }));
  const [countsKey, setCountsKey] = useState<string>("");

  const filters: OrderFilters = useMemo(() => {
    if (statusFilter === "all") return { status: FAILURE_STATUSES.join(",") };
    return { status: statusFilter };
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
  // Cached by tenant/store so switching tabs doesn't re-fetch counts.
  useEffect(() => {
    if (!tenantId || !storeId) {
      setTabCounts({ all: 0 });
      setCountsKey("");
      return;
    }

    const key = `${tenantId}::${storeId}`;
    if (countsKey === key) return;
    setCountsKey(key);

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
            { status: FAILURE_STATUSES.join(",") },
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
  }, [tenantId, storeId, authToken, countsKey]);

  const filteredRows = rows;

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
    setToast(
      failed === 0
        ? { kind: "success", message: `${action === "retry" ? "Retry queued" : "Resolved"} for ${succeeded} order(s)` }
        : { kind: "error", message: `${succeeded} succeeded, ${failed} failed` }
    );
    if (failed === 0) setRowSelection({});
    await refresh();
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

  async function doExport() {
    if (!tenantId || !storeId) {
      setToast({ kind: "error", message: "Missing tenantId / storeId for export." });
      return;
    }
    try {
      const { rows, limited, total } = await fetchAllOrdersForExport(tenantId, storeId, filters, authToken, {
        limit: 5000,
        pageSize: 200,
      });
      const failureRows = rows.filter((r) => isFailureStatus(r.status)).map(orderToFailedOrder);
      exportFailedOrdersToCsv(failureRows);
      if (limited) {
        setToast({ kind: "error", message: `Export limited to first 5000 rows (total ${total}). Refine filters.` });
      } else {
        setToast({ kind: "success", message: `Exported ${failureRows.length} row(s)` });
      }
    } catch (e) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Export failed" });
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
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Failed Orders
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Monitor and resolve order failures and payment issues.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            type="button"
            disabled={!tenantId || !storeId}
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2 disabled:opacity-40 disabled:cursor-not-allowed"
            onClick={doExport}
          >
            <IconDownload />
            Export
          </button>
        </div>
      </div>

      <div className="flex flex-wrap gap-1 border-b border-outline-variant/25">
        <button
          type="button"
          onClick={() => setStatusFilter("all")}
          aria-pressed={statusFilter === "all"}
          className={`px-3 py-2.5 text-xs font-bold uppercase tracking-wide border-b-2 -mb-px transition-colors ${
            statusFilter === "all"
              ? "border-primary text-primary"
              : "border-transparent text-on-surface-variant hover:text-on-surface"
          }`}
        >
          All{" "}
          <span className="ml-1 tabular-nums opacity-80">({tabCounts.all})</span>
        </button>
        {STATUS_ORDER.map((id) => {
          const active = statusFilter === id;
          return (
            <button
              key={id}
              type="button"
              onClick={() => setStatusFilter(id)}
              aria-pressed={active}
              className={`px-3 py-2.5 text-xs font-bold uppercase tracking-wide border-b-2 -mb-px transition-colors whitespace-nowrap ${
                active
                  ? "border-primary text-primary"
                  : "border-transparent text-on-surface-variant hover:text-on-surface"
              }`}
            >
              {id}{" "}
              <span className="ml-1 tabular-nums opacity-80">({tabCounts[id] ?? 0})</span>
            </button>
          );
        })}
      </div>

      {!tenantId || !storeId ? (
        <div className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest p-6 text-sm text-on-surface-variant">
          Select a tenant + store to view failed orders.
        </div>
      ) : (
        <div className="space-y-3">
          {loading && (
            <div className="text-xs text-on-surface-variant">Loading failed orders…</div>
          )}
          {error && (
            <div className="rounded-lg border border-error/30 bg-error-container/40 p-3 text-xs text-on-error-container">
              {error}
            </div>
          )}
          {toast && (
            <div
              className={`rounded-lg border p-3 text-xs ${
                toast.kind === "success"
                  ? "border-secondary/30 bg-secondary-container/30 text-on-secondary-container"
                  : "border-error/30 bg-error-container/40 text-on-error-container"
              }`}
              onClick={() => setToast(null)}
            >
              {toast.message}
            </div>
          )}

          <DataTable
            columns={columns}
            data={filteredRows}
            defaultHiddenColumns={["processedBy", "tags", "deliveryOption", "fulfilledDate", "failureReason"]}
            columnLabels={FAILED_ORDER_COLUMN_LABELS}
            globalFilterPlaceholder="Search by order, customer, status…"
            getRowId={(row) => row.orderId}
            rowSelection={rowSelection}
            onRowSelectionChange={setRowSelection}
            footerMode="loadMore"
            loading={loading || loadingMore}
            loadMore={{
              disabled: loading || loadingMore || nextCursor === null,
              label: nextCursor ? (loadingMore ? "Loading…" : "Load more") : "No more",
              onClick: () => void loadMore(),
            }}
          />

          {selectedCount > 0 && (
            <div className="sticky bottom-3 z-30 flex flex-wrap items-center gap-3 rounded-2xl border border-outline-variant/30 bg-surface-container-lowest px-4 py-3 shadow-lg ring-1 ring-black/5 transition-opacity duration-200">
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
        </div>
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
