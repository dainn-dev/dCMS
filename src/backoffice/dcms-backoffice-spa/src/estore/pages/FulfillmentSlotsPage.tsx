import { useEffect, useMemo, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconDelete,
  IconEdit,
  IconFormatListBulleted,
  IconWarning,
} from "../../orders/icons";
import type { FulfillmentGrouping, FulfillmentSlot } from "../EStoreApp";

type Props = {
  grouping: FulfillmentGrouping;
  slots: FulfillmentSlot[];
  onBack: () => void;
  onEdit: (slotId: string) => void;
  onDelete: (slotId: string) => void;
  onCreate: () => void;
};

function statusFromDates(startingDate: string, endingDate: string) {
  if (!startingDate && !endingDate) return "active";
  const now = new Date();
  const start = startingDate ? new Date(startingDate) : null;
  const end = endingDate ? new Date(endingDate) : null;
  if (start && now < start) return "scheduled";
  if (end && now > end) return "expired";
  return "active";
}

function statusChip(status: string) {
  if (status === "expired") return "bg-error-container text-on-error-container";
  if (status === "scheduled") return "bg-amber-50 text-amber-700";
  return "bg-secondary-container/20 text-on-secondary-container";
}

export function FulfillmentSlotsPage({ grouping, slots, onBack, onEdit, onDelete, onCreate }: Props) {
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  const sorted = useMemo(() => {
    return [...slots].sort((a, b) => (a.updatedAt < b.updatedAt ? 1 : -1));
  }, [slots]);

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" /> Back to Fulfillment Options
          </button>
          <div className="flex items-center gap-3">
            <IconFormatListBulleted className="h-6 w-6 shrink-0 text-primary" />
            <div>
              <h2 className="text-2xl font-bold tracking-tight text-on-surface">View Delivery Slots</h2>
              <p className="mt-0.5 text-sm text-on-surface-variant">
                Group:{" "}
                <code className="rounded bg-surface-container-high px-1.5 py-0.5 text-xs font-mono font-bold">
                  {grouping.code}
                </code>{" "}
                · {grouping.groupName}
              </p>
            </div>
          </div>
        </div>
        <button
          type="button"
          className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
          onClick={onCreate}
        >
          <IconCheckCircle className="h-4 w-4 shrink-0" />
          Add Delivery Slot
        </button>
      </div>

      <div className="flex-1 p-6 pb-24">
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Delivery Slots</h3>
            <span className="ml-auto rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
              {sorted.length} slot{sorted.length !== 1 ? "s" : ""}
            </span>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">Name</th>
                  <th className="px-4 py-3">Code</th>
                  <th className="px-4 py-3">Mode</th>
                  <th className="px-4 py-3">Schedule</th>
                  <th className="px-4 py-3 text-right">Price</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {sorted.map((s) => {
                  const status = statusFromDates(s.startingDate, s.endingDate);
                  return (
                    <tr key={s.id} className="text-xs hover:bg-surface-container-low transition-colors">
                      <td className="px-5 py-3.5 font-semibold text-on-surface">{s.name}</td>
                      <td className="px-4 py-3.5">
                        <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">
                          {s.code}
                        </code>
                      </td>
                      <td className="px-4 py-3.5 text-on-surface-variant">{s.mode}</td>
                      <td className="px-4 py-3.5">
                        <div className="flex flex-col text-[11px]">
                          <span className="font-medium text-on-surface">{s.startingDate ? s.startingDate.replace("T", " ") : "—"}</span>
                          <span className="text-on-surface-variant opacity-60">{s.endingDate ? `to ${s.endingDate.replace("T", " ")}` : "Permanent"}</span>
                        </div>
                      </td>
                      <td className="px-4 py-3.5 text-right font-mono font-bold text-on-surface">${s.price || "0.00"}</td>
                      <td className="px-4 py-3.5">
                        <span className={`inline-block rounded-full px-3 py-0.5 text-[9px] font-extrabold uppercase tracking-widest ${statusChip(status)}`}>
                          {status}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            type="button"
                            title="Edit"
                            className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                            onClick={() => onEdit(s.id)}
                          >
                            <IconEdit className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            title="Delete Delivery Slot"
                            className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                            onClick={() => setDeleteTarget(s.id)}
                          >
                            <IconDelete className="h-4 w-4" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
                {sorted.length === 0 && (
                  <tr>
                    <td colSpan={7} className="px-6 py-12 text-center text-xs text-on-surface-variant italic">
                      No delivery slots found. Click “Add Delivery Slot” to create one.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {deleteTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete Delivery Slot</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Click Ok to delete the delivery time slot.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteTarget(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={() => {
                  onDelete(deleteTarget);
                  setDeleteTarget(null);
                  setToast("Delivery slot deleted.");
                }}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Ok
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}

