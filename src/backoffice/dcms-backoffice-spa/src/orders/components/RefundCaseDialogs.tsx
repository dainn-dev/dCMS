import { useEffect, useState, type ReactNode } from "react";
import { IconClose } from "../icons";
import { REFUND_STATUS_OPTIONS } from "../refundCasesMock";
import type { RefundCase, RefundCaseHistoryEntry, RefundStatus } from "../types";

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency,
      maximumFractionDigits: currency === "JPY" ? 0 : 2,
    }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}

function ModalChrome({
  title,
  children,
  onClose,
  wide,
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  wide?: boolean;
}) {
  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center bg-black/45 p-4"
      role="presentation"
      onClick={onClose}
    >
      <div
        className={`relative flex max-h-[90vh] w-full flex-col overflow-hidden rounded-2xl border border-outline-variant/30 bg-surface-container-lowest shadow-2xl ${
          wide ? "max-w-3xl" : "max-w-lg"
        }`}
        role="dialog"
        aria-modal="true"
        aria-labelledby="refund-modal-title"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-outline-variant/15 px-5 py-3 shrink-0">
          <h2 id="refund-modal-title" className="text-sm font-bold text-on-surface font-headline">
            {title}
          </h2>
          <button
            type="button"
            className="rounded-full p-1.5 text-on-surface-variant hover:bg-surface-container-high"
            aria-label="Close"
            onClick={onClose}
          >
            <IconClose className="h-4 w-4" />
          </button>
        </div>
        <div className="overflow-y-auto custom-scrollbar p-5 flex-1 min-h-0">{children}</div>
      </div>
    </div>
  );
}

export function RefundDoDetailsDialog({ c, onClose }: { c: RefundCase; onClose: () => void }) {
  return (
    <ModalChrome title={`Delivery order ${c.doNumber}`} onClose={onClose} wide>
      <p className="text-xs text-on-surface-variant mb-4">
        Cancelled line items and refund breakdown for <span className="font-mono font-bold text-on-surface">{c.refundNo}</span>
        <span className="block text-[10px] mt-1">Return <span className="font-mono">{c.returnNo}</span> · Order{" "}
          <span className="font-mono">{c.orderId}</span>
        </span>
      </p>
      <h3 className="text-[10px] font-bold uppercase tracking-wider text-primary mb-2">Cancelled items</h3>
      <div className="overflow-x-auto rounded-lg border border-outline-variant/15 mb-6">
        <table className="w-full text-left text-xs">
          <thead className="bg-surface-container-high/80">
            <tr>
              <th className="px-3 py-2 font-bold text-primary uppercase">SKU</th>
              <th className="px-3 py-2 font-bold text-primary uppercase">Product</th>
              <th className="px-3 py-2 font-bold text-primary uppercase text-right">Qty</th>
              <th className="px-3 py-2 font-bold text-primary uppercase text-right">Line total</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-outline-variant/10">
            {c.doCancelledItems.map((line) => (
              <tr key={line.sku}>
                <td className="px-3 py-2 font-mono">{line.sku}</td>
                <td className="px-3 py-2">{line.name}</td>
                <td className="px-3 py-2 text-right tabular-nums">{line.qty}</td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {formatMoney(line.lineTotal, c.currency)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <h3 className="text-[10px] font-bold uppercase tracking-wider text-primary mb-2">Refund amount breakdown</h3>
      <ul className="space-y-2 text-xs">
        {c.refundBreakdown.map((b) => (
          <li key={b.label} className="flex justify-between gap-4 border-b border-outline-variant/10 border-dashed pb-2">
            <span className="text-on-surface-variant">{b.label}</span>
            <span className="font-bold tabular-nums text-on-surface">{formatMoney(b.amount, c.currency)}</span>
          </li>
        ))}
      </ul>
      <p className="mt-4 text-xs font-bold text-on-surface">
        Case total: {formatMoney(c.amount, c.currency)}
      </p>
    </ModalChrome>
  );
}

export function RefundRemarkDetailsDialog({ c, onClose }: { c: RefundCase; onClose: () => void }) {
  return (
    <ModalChrome title="Remark / gateway details" onClose={onClose}>
      <p className="text-[10px] font-bold uppercase text-on-surface-variant mb-2">Refund</p>
      <p className="font-mono text-xs font-bold text-on-surface mb-4">{c.refundNo}</p>
      <p className="text-[10px] font-bold uppercase text-on-surface-variant mb-1">Short remark</p>
      <p className="text-sm text-on-surface mb-4">{c.remark}</p>
      <p className="text-[10px] font-bold uppercase text-on-surface-variant mb-1">Full payment gateway message</p>
      <p className="text-sm text-on-surface leading-relaxed whitespace-pre-wrap">{c.paymentGatewayMessage}</p>
    </ModalChrome>
  );
}

export function RefundUpdateStatusDialog({
  c,
  onClose,
  onSave,
}: {
  c: RefundCase;
  onClose: () => void;
  onSave: (refundNo: string, patch: { status: RefundStatus; remark: string }) => void;
}) {
  const [status, setStatus] = useState<RefundStatus>(c.status);
  const [remark, setRemark] = useState(c.remark);

  useEffect(() => {
    setStatus(c.status);
    setRemark(c.remark);
  }, [c.refundNo, c.status, c.remark]);

  return (
    <ModalChrome title="Update Refund Case Status" onClose={onClose}>
      <p className="text-xs text-on-surface-variant mb-4">
        Refund <span className="font-mono font-bold text-on-surface">{c.refundNo}</span>
        {c.isPaymentGatewayCase ? (
          <span className="block mt-1 text-[10px]">
            Payment gateway case — status can only be changed while in <strong>Pending Refund</strong> or{" "}
            <strong>Failed</strong> (mock RBAC applies).
          </span>
        ) : (
          <span className="block mt-1 text-[10px]">Non–payment-gateway case — always editable (mock).</span>
        )}
      </p>
      <label className="block text-[10px] font-bold uppercase text-on-surface-variant mb-1">Refund status</label>
      <select
        className="w-full mb-4 rounded-lg border border-outline-variant/30 bg-white px-3 py-2 text-xs font-medium text-on-surface"
        value={status}
        onChange={(e) => setStatus(e.target.value as RefundStatus)}
      >
        {REFUND_STATUS_OPTIONS.map((s) => (
          <option key={s} value={s}>
            {s}
          </option>
        ))}
      </select>
      <label className="block text-[10px] font-bold uppercase text-on-surface-variant mb-1">Remarks</label>
      <textarea
        className="w-full min-h-[100px] rounded-lg border border-outline-variant/30 bg-white px-3 py-2 text-xs text-on-surface"
        value={remark}
        onChange={(e) => setRemark(e.target.value)}
      />
      <div className="mt-5 flex justify-end gap-2">
        <button
          type="button"
          className="px-4 py-2 text-xs font-bold rounded-lg border border-outline-variant/40 text-on-surface hover:bg-surface-container-high"
          onClick={onClose}
        >
          Cancel
        </button>
        <button
          type="button"
          className="px-4 py-2 text-xs font-bold rounded-lg bg-primary text-on-primary hover:opacity-90"
          onClick={() => {
            onSave(c.refundNo, { status, remark });
            onClose();
          }}
        >
          Save
        </button>
      </div>
    </ModalChrome>
  );
}

export function RefundChangeHistoryDialog({ c, onClose }: { c: RefundCase; onClose: () => void }) {
  const rows: RefundCaseHistoryEntry[] = [...c.changeHistory].reverse();
  return (
    <ModalChrome title="Change history" onClose={onClose} wide>
      <p className="text-xs text-on-surface-variant mb-4">
        Status and remark changes for <span className="font-mono font-bold text-on-surface">{c.refundNo}</span>
      </p>
      <ol className="relative border-l border-outline-variant/40 ml-2 space-y-4 pl-6">
        {rows.map((entry, i) => (
          <li key={`${entry.at}-${i}`} className="relative">
            <span className="absolute -left-[calc(0.5rem+5px)] top-1.5 h-2.5 w-2.5 rounded-full bg-primary ring-4 ring-surface-container-lowest" />
            <p className="text-[10px] font-mono text-on-surface-variant">{entry.at}</p>
            <p className="text-[10px] font-bold text-on-surface mt-0.5">Actor: {entry.actor}</p>
            <p className="mt-1 inline-flex rounded-full bg-secondary-container/20 px-2 py-0.5 text-[10px] font-bold uppercase text-secondary">
              {entry.status}
            </p>
            <p className="text-xs text-on-surface mt-2 leading-relaxed">{entry.remark}</p>
          </li>
        ))}
      </ol>
    </ModalChrome>
  );
}
