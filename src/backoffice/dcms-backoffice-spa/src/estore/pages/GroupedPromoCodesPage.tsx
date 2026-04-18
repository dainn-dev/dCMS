import { useEffect, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconDelete,
  IconDownload,
  IconEdit,
  IconFormatListBulleted,
  IconWarning,
} from "../../orders/icons";
import type { PromoListRow } from "../promotions-columns";
import { exportGroupedPromoCodesToXlsx } from "../exportPromoCodesXlsx";

// ── Mock child rows ───────────────────────────────────────────────────────────
type ChildPromoRow = {
  id: string;
  code: string;
  status: "approved" | "pending" | "expired";
  startDate: string;
  endDate: string;
  used: number;
  limit: number;
};

function makeMockChildren(parentId: string): ChildPromoRow[] {
  return [
    { id: `${parentId}-c1`, code: `${parentId}XJKL7`, status: "approved", startDate: "May 12, 10:00", endDate: "May 14, 23:59", used: 1, limit: 1 },
    { id: `${parentId}-c2`, code: `${parentId}MNOP2`, status: "approved", startDate: "May 12, 10:00", endDate: "May 14, 23:59", used: 0, limit: 1 },
    { id: `${parentId}-c3`, code: `${parentId}QRST5`, status: "pending", startDate: "May 12, 10:00", endDate: "May 14, 23:59", used: 0, limit: 1 },
    { id: `${parentId}-c4`, code: `${parentId}UVWX9`, status: "approved", startDate: "May 12, 10:00", endDate: "May 14, 23:59", used: 0, limit: 1 },
    { id: `${parentId}-c5`, code: `${parentId}YZAB3`, status: "expired", startDate: "Jan 01, 00:00", endDate: "Jan 31, 23:59", used: 1, limit: 1 },
  ];
}

function statusPillClass(status: ChildPromoRow["status"]) {
  switch (status) {
    case "approved": return "bg-tertiary-container text-on-tertiary-container";
    case "pending": return "bg-secondary-fixed text-on-secondary-fixed-variant";
    case "expired": return "bg-error-container text-on-error-container";
    default: return "bg-outline-variant/20 text-on-surface-variant";
  }
}

// ── Types ────────────────────────────────────────────────────────────────────
type Props = {
  parentPromo: PromoListRow;
  onBack: () => void;
  onEditChild: (child: PromoListRow) => void;
};

// ── Component ────────────────────────────────────────────────────────────────
export function GroupedPromoCodesPage({ parentPromo, onBack, onEditChild }: Props) {
  const [rows, setRows] = useState<ChildPromoRow[]>(() => makeMockChildren(parentPromo.code));

  // ── Delete confirmation ───────────────────────────────────────────────────
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);

  function confirmDelete(id: string) { setDeleteTarget(id); }
  function handleDeleteConfirm() {
    if (!deleteTarget) return;
    setRows((prev) => prev.filter((r) => r.id !== deleteTarget));
    setDeleteTarget(null);
    setToast("Promo code deleted.");
  }

  // ── Toast ─────────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Edit child: map to PromoListRow shape ─────────────────────────────────
  function handleEditChild(child: ChildPromoRow) {
    const row: PromoListRow = {
      id: child.id,
      promoType: parentPromo.promoType,
      discount: parentPromo.discount,
      value: parentPromo.value,
      minSpend: parentPromo.minSpend,
      code: child.code,
      scheduleStart: child.startDate,
      scheduleEnd: `to ${child.endDate}`,
      activeDot: child.status === "approved" ? "live" : child.status === "pending" ? "warning" : "off",
      status: child.status,
      usedPct: child.limit > 0 ? Math.round((child.used / child.limit) * 100) : 0,
    };
    onEditChild(row);
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 items-center gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div className="flex-1">
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Promo Codes
          </button>
          <div className="flex items-center gap-3">
            <IconFormatListBulleted className="h-6 w-6 shrink-0 text-primary" />
            <div>
              <h2 className="text-2xl font-bold tracking-tight text-on-surface">View Grouped Codes</h2>
              <p className="mt-0.5 text-sm text-on-surface-variant">
                Parent: <code className="rounded bg-surface-container-high px-1.5 py-0.5 text-xs font-mono font-bold">{parentPromo.code}</code>
                <span className="ml-2 rounded-full bg-surface-container-high px-2 py-0.5 text-[10px] text-on-surface-variant">{parentPromo.promoType}</span>
              </p>
            </div>
          </div>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-3 py-2 text-xs font-semibold text-on-surface transition-colors hover:bg-surface-container-high"
            onClick={() =>
              void exportGroupedPromoCodesToXlsx(
                parentPromo.code,
                rows.map((r) => ({
                  code: r.code,
                  status: r.status,
                  startDate: r.startDate,
                  endDate: r.endDate,
                  used: r.used,
                  limit: r.limit,
                }))
              )
            }
          >
            <IconDownload className="h-4 w-4 shrink-0 text-primary" />
            Export Excel
          </button>
          <div className="rounded-full bg-surface-container-high px-4 py-1.5 text-xs font-bold text-on-surface-variant">
            {rows.length} code{rows.length !== 1 ? "s" : ""}
          </div>
        </div>
      </div>

      {/* ── Body ─────────────────────────────────────────────────────────── */}
      <div className="flex-1 p-6">
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">Code</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Start</th>
                  <th className="px-4 py-3">End</th>
                  <th className="px-4 py-3 text-right">Used / Limit</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.id} className="text-xs hover:bg-surface-container-low transition-colors">
                    <td className="px-5 py-3.5">
                      <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">
                        {row.code}
                      </code>
                    </td>
                    <td className="px-4 py-3.5">
                      <span className={`inline-block rounded-full px-3 py-0.5 text-[9px] font-extrabold uppercase tracking-widest ${statusPillClass(row.status)}`}>
                        {row.status}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-on-surface-variant">{row.startDate}</td>
                    <td className="px-4 py-3.5 text-on-surface-variant">{row.endDate}</td>
                    <td className="px-4 py-3.5 text-right font-mono text-on-surface">
                      {row.used} / {row.limit}
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          title="Edit"
                          className="rounded p-2 text-on-surface-variant transition-all hover:bg-primary/10 hover:text-primary"
                          onClick={() => handleEditChild(row)}
                        >
                          <IconEdit className="h-[18px] w-[18px]" />
                        </button>
                        <button
                          type="button"
                          title="Delete"
                          className="rounded p-2 text-on-surface-variant transition-all hover:bg-error-container hover:text-error"
                          onClick={() => confirmDelete(row.id)}
                        >
                          <IconDelete className="h-[18px] w-[18px]" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-6 py-12 text-center text-xs text-on-surface-variant italic">
                      No codes found under this group.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {/* ── Delete confirmation modal ────────────────────────────────────── */}
      {deleteTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete Promo Code</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Delete <strong className="text-on-surface font-mono">{rows.find((r) => r.id === deleteTarget)?.code}</strong>? This cannot be undone.
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
                onClick={handleDeleteConfirm}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Toast ────────────────────────────────────────────────────────── */}
      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
