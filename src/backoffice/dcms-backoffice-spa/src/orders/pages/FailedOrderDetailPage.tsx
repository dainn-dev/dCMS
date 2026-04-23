import { useEffect, useState } from "react";
import { getOrderDetail, mapApiStatusToUiLabel, resolveFailedOrder, retryFailedOrder } from "../api/ordersApi";
import { IconArrowBack, IconCheckCircle, IconChevronDown, IconPrint, IconWarning } from "../icons";
import type { FailedOrderStatus } from "../types";

const STATUS_STYLES: Record<FailedOrderStatus, string> = {
  "Payment Failed": "bg-red-100 text-red-800",
  "Address Error": "bg-orange-100 text-orange-800",
  "Auth Failed": "bg-sky-100 text-sky-800",
  "Stock Error": "bg-amber-100 text-amber-800",
  "System Error": "bg-purple-100 text-purple-800",
};

type Props = {
  orderId: string;
  tenantId?: string;
  storeId?: string;
  authToken?: string;
  onBack: () => void;
};

function addrBlock(
  line1: string | null,
  line2: string | null,
  postal: string | null,
  country: string | null
): string {
  const parts = [line1, line2, [postal, country].filter(Boolean).join(" ")].filter(Boolean);
  return parts.length ? parts.join("\n") : "—";
}

function shippingBlock(dto: Awaited<ReturnType<typeof getOrderDetail>>): string {
  const s = dto.shippingAddress;
  if (!s) return "—";
  const parts = [s.line1, s.line2, s.city, s.region, s.postalCode, s.countryCode]
    .filter((x): x is string => Boolean((x ?? "").trim()))
    .map((x) => String(x));
  return parts.length ? parts.join("\n") : "—";
}

export function FailedOrderDetailPage({ orderId, tenantId, storeId, authToken, onBack }: Props) {
  const [printOpen, setPrintOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [detail, setDetail] = useState<Awaited<ReturnType<typeof getOrderDetail>> | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [resolveConfirmOpen, setResolveConfirmOpen] = useState(false);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  useEffect(() => {
    if (!tenantId || !storeId) return;
    let cancelled = false;
    setLoading(true);
    setLoadError(null);
    setNotFound(false);
    getOrderDetail(tenantId, storeId, orderId, authToken)
      .then((dto) => {
        if (cancelled) return;
        setDetail(dto);
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        const msg = e instanceof Error ? e.message : "Failed to load order";
        if (/not found/i.test(msg) || /404/.test(msg)) setNotFound(true);
        else setLoadError(msg);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, orderId, authToken]);

  const uiStatus = mapApiStatusToUiLabel(detail?.status ?? "SystemError") as FailedOrderStatus;

  function runPrint(kind: "delivery" | "packing") {
    setPrintOpen(false);
    if (!detail) return;
    const prevTitle = document.title;
    document.title = `${kind === "delivery" ? "Delivery Receipt" : "Packing Slip"} — ${detail.orderId}`;
    window.print();
    document.title = prevTitle;
  }

  async function doRetry() {
    if (!tenantId || !storeId) return;
    setActionBusy(true);
    try {
      await retryFailedOrder(tenantId, storeId, orderId, authToken);
      setToast({ kind: "success", message: "Retry triggered." });
      // refetch
      const dto = await getOrderDetail(tenantId, storeId, orderId, authToken);
      setDetail(dto);
    } catch (e: unknown) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Retry failed" });
    } finally {
      setActionBusy(false);
    }
  }

  async function doResolve() {
    if (!tenantId || !storeId) return;
    setActionBusy(true);
    try {
      await resolveFailedOrder(tenantId, storeId, orderId, null, authToken);
      setToast({ kind: "success", message: "Marked as resolved." });
      const dto = await getOrderDetail(tenantId, storeId, orderId, authToken);
      setDetail(dto);
    } catch (e: unknown) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Resolve failed" });
    } finally {
      setActionBusy(false);
    }
  }

  if (!tenantId || !storeId) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-bold text-primary hover:underline"
        >
          <IconArrowBack className="h-4 w-4" />
          Back to Failed Orders
        </button>
        <div className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-8 text-center text-sm text-on-surface-variant">
          Missing <span className="font-mono font-bold text-on-surface">tenantId</span> /{" "}
          <span className="font-mono font-bold text-on-surface">storeId</span>.
        </div>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-bold text-primary hover:underline"
        >
          <IconArrowBack className="h-4 w-4" />
          Back to Failed Orders
        </button>
        <div className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-8 text-center text-sm text-on-surface-variant">
          Loading failed order…
        </div>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-bold text-primary hover:underline"
        >
          <IconArrowBack className="h-4 w-4" />
          Back to Failed Orders
        </button>
        <div className="rounded-xl border border-error/25 bg-error/5 p-6 text-sm text-error">
          {loadError}
        </div>
      </div>
    );
  }

  if (notFound || !detail) {
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={onBack}
          className="inline-flex items-center gap-2 text-xs font-bold text-primary hover:underline"
        >
          <IconArrowBack className="h-4 w-4" />
          Back to Failed Orders
        </button>
        <div className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-8 text-center text-sm text-on-surface-variant">
          No failed order found for <span className="font-mono font-bold text-on-surface">{orderId}</span>.
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6 print:shadow-none">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between print:hidden">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="inline-flex items-center gap-2 text-xs font-bold text-primary hover:underline mb-3"
          >
            <IconArrowBack className="h-4 w-4" />
            Back to Failed Orders
          </button>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-on-surface-variant">Failed Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Detail</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Failed order {detail.orderId}
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Investigate failure (API-backed).
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <span
            className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[uiStatus]}`}
          >
            {uiStatus}
          </span>
          <button
            type="button"
            className="inline-flex items-center gap-2 rounded-lg bg-primary px-3 py-2 text-xs font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
            disabled={actionBusy}
            onClick={() => void doRetry()}
          >
            <IconCheckCircle className="h-4 w-4" />
            Retry
          </button>
          <button
            type="button"
            className="inline-flex items-center gap-2 rounded-lg bg-error px-3 py-2 text-xs font-bold text-on-error hover:opacity-90 disabled:opacity-40"
            disabled={actionBusy}
            onClick={() => setResolveConfirmOpen(true)}
          >
            <IconWarning className="h-4 w-4" />
            Mark Resolved
          </button>
          <div className="relative">
            <button
              type="button"
              onClick={() => setPrintOpen((o) => !o)}
              className="inline-flex items-center gap-2 px-3 py-2 text-xs font-bold rounded-lg border border-outline-variant/30 bg-white text-on-surface hover:bg-surface-container-low transition-colors"
            >
              <IconPrint className="h-4 w-4" />
              Print
              <IconChevronDown className="h-3 w-3 text-on-surface-variant" />
            </button>
            {printOpen && (
              <>
                <button
                  type="button"
                  className="fixed inset-0 z-10 cursor-default bg-transparent"
                  aria-label="Close menu"
                  onClick={() => setPrintOpen(false)}
                />
                <div className="absolute right-0 top-full z-20 mt-1 w-52 rounded-lg border border-outline-variant/20 bg-white py-1 shadow-lg">
                  <button
                    type="button"
                    className="block w-full px-3 py-2 text-left text-xs font-semibold text-on-surface hover:bg-surface-container-low"
                    onClick={() => runPrint("delivery")}
                  >
                    Delivery Receipt
                  </button>
                  <button
                    type="button"
                    className="block w-full px-3 py-2 text-left text-xs font-semibold text-on-surface hover:bg-surface-container-low"
                    onClick={() => runPrint("packing")}
                  >
                    Packing Slip
                  </button>
                </div>
              </>
            )}
          </div>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className="rounded-xl border border-red-200/60 bg-red-50/50 p-5 space-y-4">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-red-900">Failure summary</h2>
          <dl className="grid grid-cols-1 gap-3 text-xs sm:grid-cols-2">
            <div>
              <dt className="text-on-surface-variant font-semibold">Error code</dt>
              <dd className="font-mono font-bold text-red-950 mt-0.5">{detail.failureErrorCode ?? "—"}</dd>
            </div>
            <div>
              <dt className="text-on-surface-variant font-semibold">Failure time</dt>
              <dd className="text-red-950 mt-0.5">{detail.failedAt ? new Date(detail.failedAt).toLocaleString() : "—"}</dd>
            </div>
            <div>
              <dt className="text-on-surface-variant font-semibold">Retry count</dt>
              <dd className="tabular-nums text-red-950 mt-0.5">{detail.retryCount ?? 0}</dd>
            </div>
            <div>
              <dt className="text-on-surface-variant font-semibold">Processed by</dt>
              <dd className="font-mono text-red-950 mt-0.5">—</dd>
            </div>
          </dl>
          <div>
            <h3 className="text-[10px] font-bold uppercase text-red-900/90 mb-1">Error message</h3>
            <p className="text-sm text-red-950 leading-relaxed">{detail.failureReason ?? "—"}</p>
          </div>
        </section>

        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-3">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Order information</h2>
          <dl className="space-y-2 text-xs">
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">DO number</dt>
              <dd className="font-bold text-on-surface">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Order number</dt>
              <dd className="font-mono text-on-surface">{detail.orderId}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Order date</dt>
              <dd className="text-on-surface">{detail.createdAt}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Type</dt>
              <dd className="text-on-surface">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Store</dt>
              <dd className="text-on-surface text-right">{detail.storeId}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Store Auto ID</dt>
              <dd className="font-mono text-on-surface">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Tags</dt>
              <dd className="text-on-surface text-right">—</dd>
            </div>
          </dl>
        </section>
      </div>

      <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-3">
        <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Delivery order details</h2>
        <dl className="grid gap-3 text-xs sm:grid-cols-2">
          <div className="flex justify-between gap-4 sm:col-span-2">
            <dt className="text-on-surface-variant">Delivery order</dt>
            <dd className="font-bold text-on-surface">—</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Shipping status</dt>
            <dd className="text-on-surface">{detail.shipment?.status ?? "—"}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Fulfilled date</dt>
            <dd className="text-on-surface">—</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Delivery date</dt>
            <dd className="text-on-surface">—</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Delivery option</dt>
            <dd className="text-on-surface">—</dd>
          </div>
        </dl>
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-3">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Customer and payment</h2>
          <dl className="space-y-2 text-xs">
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Name</dt>
              <dd className="font-bold text-on-surface text-right">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Email</dt>
              <dd className="text-on-surface break-all text-right">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Contact</dt>
              <dd className="text-on-surface">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Payment method</dt>
              <dd className="text-on-surface text-right">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Payment gateway</dt>
              <dd className="text-on-surface">—</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Customer ID</dt>
              <dd className="font-mono text-on-surface text-right">{detail.customerId}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Payment intent</dt>
              <dd className="font-mono text-on-surface text-right">{detail.paymentIntentId ?? "—"}</dd>
            </div>
          </dl>
        </section>

        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-2">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Shipping address</h2>
          <p className="text-xs text-on-surface whitespace-pre-line">
            {shippingBlock(detail)}
          </p>
        </section>

        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-2 lg:col-span-2">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Billing address</h2>
          <p className="text-xs text-on-surface whitespace-pre-line">
            —
          </p>
        </section>
      </div>

      <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest overflow-hidden">
        <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary px-5 pt-5 pb-2">
          Order line items
        </h2>
        <div className="border-t border-outline-variant/10 px-5 py-6 text-xs text-on-surface-variant">
          Line items are not included in the current Order Detail API response.
        </div>
      </section>

      <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5">
        <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary mb-4">Failure log</h2>
        <div className="text-xs text-on-surface-variant">
          Failure log is not included in the current Order Detail API response.
        </div>
      </section>

      {resolveConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6">
              <h3 className="text-sm font-bold text-on-surface">Mark resolved</h3>
              <p className="mt-2 text-xs text-on-surface-variant">
                Mark this failed order as resolved? This will move it to a cancelled/resolved state in the backend.
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setResolveConfirmOpen(false)}
                disabled={actionBusy}
              >
                Cancel
              </button>
              <button
                type="button"
                className="rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 disabled:opacity-40"
                onClick={() => {
                  setResolveConfirmOpen(false);
                  void doResolve();
                }}
                disabled={actionBusy}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-outline-variant/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <p className={`text-sm font-semibold ${toast.kind === "error" ? "text-error" : "text-on-surface"}`}>{toast.message}</p>
        </div>
      )}
    </div>
  );
}
