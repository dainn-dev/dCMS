import { useState } from "react";
import { getFailedOrderByOrderId } from "../failedOrdersMock";
import { IconArrowBack, IconChevronDown, IconPrint } from "../icons";
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

export function FailedOrderDetailPage({ orderId, onBack }: Props) {
  const [printOpen, setPrintOpen] = useState(false);
  const order = getFailedOrderByOrderId(orderId);

  if (!order) {
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

  function runPrint(kind: "delivery" | "packing") {
    setPrintOpen(false);
    const prevTitle = document.title;
    document.title = `${kind === "delivery" ? "Delivery Receipt" : "Packing Slip"} — ${order.orderId}`;
    window.print();
    document.title = prevTitle;
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
            Failed order {order.orderId}
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            View-only — investigate failure (no edits or backend actions in this build).
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <span
            className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[order.status]}`}
          >
            {order.status}
          </span>
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
              <dd className="font-mono font-bold text-red-950 mt-0.5">{order.failureErrorCode ?? "—"}</dd>
            </div>
            <div>
              <dt className="text-on-surface-variant font-semibold">Failure time</dt>
              <dd className="text-red-950 mt-0.5">{order.failureAt ?? "—"}</dd>
            </div>
            <div>
              <dt className="text-on-surface-variant font-semibold">Retry count</dt>
              <dd className="tabular-nums text-red-950 mt-0.5">{order.failureRetryCount}</dd>
            </div>
            <div>
              <dt className="text-on-surface-variant font-semibold">Processed by</dt>
              <dd className="font-mono text-red-950 mt-0.5">{order.processedBy ?? "—"}</dd>
            </div>
          </dl>
          <div>
            <h3 className="text-[10px] font-bold uppercase text-red-900/90 mb-1">Error message</h3>
            <p className="text-sm text-red-950 leading-relaxed">{order.failureReason}</p>
          </div>
        </section>

        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-3">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Order information</h2>
          <dl className="space-y-2 text-xs">
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">DO number</dt>
              <dd className="font-bold text-on-surface">{order.doNumber}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Order number</dt>
              <dd className="font-mono text-on-surface">{order.orderId}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Order date</dt>
              <dd className="text-on-surface">{order.orderDate}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Type</dt>
              <dd className="text-on-surface">{order.type}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Store</dt>
              <dd className="text-on-surface text-right">{order.store}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Store Auto ID</dt>
              <dd className="font-mono text-on-surface">{order.storeAutoId ?? "—"}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Tags</dt>
              <dd className="text-on-surface text-right">{order.tags ?? "—"}</dd>
            </div>
          </dl>
        </section>
      </div>

      <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-3">
        <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Delivery order details</h2>
        <dl className="grid gap-3 text-xs sm:grid-cols-2">
          <div className="flex justify-between gap-4 sm:col-span-2">
            <dt className="text-on-surface-variant">Delivery order</dt>
            <dd className="font-bold text-on-surface">{order.doNumber}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Shipping status</dt>
            <dd className="text-on-surface">{order.shippingStatus ?? "—"}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Fulfilled date</dt>
            <dd className="text-on-surface">{order.fulfilledDate ?? "—"}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Delivery date</dt>
            <dd className="text-on-surface">{order.deliveryDate ?? "—"}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-on-surface-variant">Delivery option</dt>
            <dd className="text-on-surface">{order.deliveryOption ?? "—"}</dd>
          </div>
        </dl>
      </section>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-3">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Customer and payment</h2>
          <dl className="space-y-2 text-xs">
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Name</dt>
              <dd className="font-bold text-on-surface text-right">{order.customerName}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Email</dt>
              <dd className="text-on-surface break-all text-right">{order.customerEmail}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Contact</dt>
              <dd className="text-on-surface">{order.customerContactNo ?? "—"}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Payment method</dt>
              <dd className="text-on-surface text-right">{order.paymentMethod ?? "—"}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-on-surface-variant">Payment gateway</dt>
              <dd className="text-on-surface">{order.paymentGateway ?? "—"}</dd>
            </div>
          </dl>
        </section>

        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-2">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Shipping address</h2>
          <p className="text-xs text-on-surface whitespace-pre-line">
            {addrBlock(
              order.shippingAddressLine1,
              order.shippingAddressLine2,
              order.shippingPostalCode,
              order.shippingCountry
            )}
          </p>
        </section>

        <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 space-y-2 lg:col-span-2">
          <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary">Billing address</h2>
          <p className="text-xs text-on-surface whitespace-pre-line">
            {addrBlock(
              order.billingAddressLine1,
              order.billingAddressLine2,
              order.billingPostalCode,
              order.billingCountry
            )}
          </p>
        </section>
      </div>

      <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest overflow-hidden">
        <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary px-5 pt-5 pb-2">
          Order line items
        </h2>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs border-t border-outline-variant/10">
            <thead className="bg-surface-container-high/80">
              <tr>
                <th className="px-4 py-2 font-bold text-primary uppercase">Line</th>
                <th className="px-4 py-2 font-bold text-primary uppercase">SKU</th>
                <th className="px-4 py-2 font-bold text-primary uppercase">Product</th>
                <th className="px-4 py-2 font-bold text-primary uppercase text-right">Qty</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-outline-variant/10">
              {order.lineItems.map((line) => (
                <tr key={line.lineId} className="hover:bg-surface-container-low/50">
                  <td className="px-4 py-2 font-mono text-on-surface-variant">{line.lineId}</td>
                  <td className="px-4 py-2 font-mono">{line.sku}</td>
                  <td className="px-4 py-2 text-on-surface">{line.name}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{line.qty}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5">
        <h2 className="text-[11px] font-bold uppercase tracking-wider text-primary mb-4">Failure log</h2>
        <ol className="relative border-l border-outline-variant/40 ml-2 space-y-4 pl-6">
          {order.failureLog.map((entry, i) => (
            <li key={`${entry.at}-${i}`} className="relative">
              <span className="absolute -left-[calc(0.5rem+5px)] top-1.5 h-2.5 w-2.5 rounded-full bg-primary ring-4 ring-surface-container-lowest" />
              <p className="text-[10px] font-mono text-on-surface-variant">{entry.at}</p>
              <p className="text-xs text-on-surface mt-0.5 leading-relaxed">{entry.event}</p>
            </li>
          ))}
        </ol>
      </section>
    </div>
  );
}
