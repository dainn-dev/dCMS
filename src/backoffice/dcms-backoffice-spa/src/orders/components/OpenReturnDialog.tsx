import { useEffect, useMemo, useState } from "react";
import type { LineItemDto, ReturnReason } from "../api/ordersApi";

const REASONS: ReturnReason[] = [
  "WrongItem",
  "Defective",
  "NotAsDescribed",
  "ChangedMind",
  "DamagedInTransit",
  "Other",
];

const REASON_LABEL: Record<ReturnReason, string> = {
  WrongItem: "Wrong Item",
  Defective: "Defective",
  NotAsDescribed: "Not as Described",
  ChangedMind: "Changed Mind",
  DamagedInTransit: "Damaged in Transit",
  Other: "Other",
};

type Props = {
  open: boolean;
  orderId: string;
  /** Lines from order detail — the dialog filters to those Delivered/PickedUp. */
  lines: LineItemDto[];
  /** Optional pre-selected line for "Open RMA" from the per-item action menu. */
  initialLineId?: string | null;
  busy?: boolean;
  onClose: () => void;
  onSubmit: (payload: {
    idempotencyKey: string;
    reason: ReturnReason;
    notes: string | null;
    lines: { lineId: string; quantity: number; reason?: ReturnReason | null }[];
  }) => void;
};

type Row = { lineId: string; selected: boolean; quantity: number; reason: ReturnReason | "" };

export function OpenReturnDialog({ open, orderId, lines, initialLineId, busy, onClose, onSubmit }: Props) {
  const eligible = useMemo(
    () => lines.filter((l) => l.fulfillmentStatus === "delivered" || l.fulfillmentStatus === "picked_up"),
    [lines],
  );

  const [reason, setReason] = useState<ReturnReason>("Defective");
  const [notes, setNotes] = useState("");
  const [idempotencyKey, setIdempotencyKey] = useState<string>("");
  const [rows, setRows] = useState<Row[]>(() =>
    eligible.map((l) => ({
      lineId: l.lineId,
      selected: initialLineId ? l.lineId === initialLineId : false,
      quantity: Math.max(1, (l.quantity ?? 1) - (l.returnedQuantity ?? 0)),
      reason: "",
    })),
  );

  useEffect(() => {
    if (!open) return;
    setIdempotencyKey(globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`);
  }, [open]);

  if (!open) return null;

  const remainingFor = (lineId: string) => {
    const l = eligible.find((x) => x.lineId === lineId);
    if (!l) return 0;
    return Math.max(0, (l.quantity ?? 0) - (l.returnedQuantity ?? 0));
  };

  const toggle = (lineId: string) =>
    setRows((rs) => rs.map((r) => (r.lineId === lineId ? { ...r, selected: !r.selected } : r)));
  const setQty = (lineId: string, qty: number) =>
    setRows((rs) => rs.map((r) => (r.lineId === lineId ? { ...r, quantity: qty } : r)));
  const setLineReason = (lineId: string, v: ReturnReason | "") =>
    setRows((rs) => rs.map((r) => (r.lineId === lineId ? { ...r, reason: v } : r)));

  const selected = rows.filter((r) => r.selected && r.quantity > 0);
  const overQty = selected.find((r) => r.quantity > remainingFor(r.lineId));

  const canSubmit = selected.length > 0 && !overQty && !busy;

  function submit() {
    if (!canSubmit) return;
    onSubmit({
      idempotencyKey,
      reason,
      notes: notes.trim() ? notes.trim() : null,
      lines: selected.map((r) => ({
        lineId: r.lineId,
        quantity: r.quantity,
        reason: r.reason || null,
      })),
    });
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl mx-4 p-6 space-y-4">
        <h2 className="text-sm font-bold text-on-surface">Open Return / RMA</h2>
        <p className="text-xs text-on-surface-variant">
          Order <span className="font-mono font-semibold text-on-surface">#{orderId}</span> — only Delivered or
          Picked Up lines can be returned.
        </p>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
          <label className="space-y-1">
            <span className="block text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
              Header Reason
            </span>
            <select
              className="w-full bg-surface-container-low border border-outline-variant/20 rounded px-3 py-2 text-xs"
              value={reason}
              onChange={(e) => setReason(e.target.value as ReturnReason)}
            >
              {REASONS.map((r) => (
                <option key={r} value={r}>{REASON_LABEL[r]}</option>
              ))}
            </select>
          </label>
          <label className="space-y-1">
            <span className="block text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
              Notes (optional)
            </span>
            <input
              className="w-full bg-surface-container-low border border-outline-variant/20 rounded px-3 py-2 text-xs"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Customer comments / context"
            />
          </label>
        </div>

        <div className="rounded-lg border border-outline-variant/20 max-h-72 overflow-y-auto">
          <table className="w-full text-xs">
            <thead className="bg-surface-container-low">
              <tr>
                <th className="text-left px-3 py-2 font-bold text-on-surface-variant text-[10px] uppercase">Sel</th>
                <th className="text-left px-3 py-2 font-bold text-on-surface-variant text-[10px] uppercase">Line</th>
                <th className="text-right px-3 py-2 font-bold text-on-surface-variant text-[10px] uppercase">Remaining</th>
                <th className="text-right px-3 py-2 font-bold text-on-surface-variant text-[10px] uppercase">Qty</th>
                <th className="text-left px-3 py-2 font-bold text-on-surface-variant text-[10px] uppercase">Line Reason</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-outline-variant/10">
              {eligible.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-3 py-6 text-center italic text-on-surface-variant">
                    No returnable lines — none are Delivered or Picked Up.
                  </td>
                </tr>
              )}
              {eligible.map((l) => {
                const row = rows.find((r) => r.lineId === l.lineId);
                if (!row) return null;
                const remaining = remainingFor(l.lineId);
                const over = row.selected && row.quantity > remaining;
                return (
                  <tr key={l.lineId} className={over ? "bg-error/5" : ""}>
                    <td className="px-3 py-2">
                      <input type="checkbox" checked={row.selected} onChange={() => toggle(l.lineId)} />
                    </td>
                    <td className="px-3 py-2">
                      <div className="font-semibold">{l.productNameSnapshot ?? l.productId}</div>
                      <div className="text-[10px] font-mono text-on-surface-variant">{l.variantId ?? l.productId}</div>
                    </td>
                    <td className="px-3 py-2 text-right">{remaining}</td>
                    <td className="px-3 py-2 text-right">
                      <input
                        type="number"
                        min={1}
                        max={remaining}
                        value={row.quantity}
                        disabled={!row.selected}
                        onChange={(e) => setQty(l.lineId, Math.max(0, Number(e.target.value) || 0))}
                        className="w-16 text-right bg-surface-container-low border border-outline-variant/20 rounded px-2 py-1 text-xs disabled:opacity-50"
                      />
                    </td>
                    <td className="px-3 py-2">
                      <select
                        className="w-full bg-surface-container-low border border-outline-variant/20 rounded px-2 py-1 text-[11px] disabled:opacity-50"
                        value={row.reason}
                        disabled={!row.selected}
                        onChange={(e) => setLineReason(l.lineId, e.target.value as ReturnReason | "")}
                      >
                        <option value="">— Use Header Reason —</option>
                        {REASONS.map((r) => (
                          <option key={r} value={r}>{REASON_LABEL[r]}</option>
                        ))}
                      </select>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {overQty && (
          <p className="text-xs text-error">
            Quantity exceeds remaining returnable units on at least one line.
          </p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <button
            type="button"
            className="px-4 py-2 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-low rounded-lg"
            onClick={onClose}
            disabled={busy}
          >
            Close
          </button>
          <button
            type="button"
            className="px-4 py-2 text-xs font-bold bg-primary text-white rounded-lg hover:bg-primary-container disabled:opacity-40"
            onClick={submit}
            disabled={!canSubmit}
          >
            Open Return
          </button>
        </div>
      </div>
    </div>
  );
}
