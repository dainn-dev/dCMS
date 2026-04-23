type Props = {
  open: boolean;
  count: number;
  busy?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
};

export function ConfirmResolveFailedOrdersDialog({ open, count, busy, onCancel, onConfirm }: Props) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
      <div className="w-[460px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
        <div className="p-6">
          <h3 className="text-sm font-bold text-on-surface">Resolve failed orders</h3>
          <p className="mt-2 text-xs text-on-surface-variant leading-relaxed">
            Resolve <strong className="tabular-nums text-on-surface">{count}</strong> failed order{count === 1 ? "" : "s"}? They will be
            marked as <strong className="text-on-surface">Cancelled</strong> — this cannot be undone.
          </p>
        </div>
        <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
          <button
            type="button"
            className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high disabled:opacity-40"
            onClick={onCancel}
            disabled={busy}
          >
            Cancel
          </button>
          <button
            type="button"
            className="rounded-md bg-primary px-5 py-2.5 text-xs font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
            onClick={onConfirm}
            disabled={busy}
          >
            Confirm
          </button>
        </div>
      </div>
    </div>
  );
}

