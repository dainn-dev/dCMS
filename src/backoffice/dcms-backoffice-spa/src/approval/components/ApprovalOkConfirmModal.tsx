import { IconCheckCircle, IconClose } from "../../orders/icons";

type Props = {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  onConfirm: () => void;
  onCancel: () => void;
};

export function ApprovalOkConfirmModal({
  open,
  title,
  message,
  confirmLabel = "Ok",
  onConfirm,
  onCancel,
}: Props) {
  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-[70] flex items-center justify-center bg-black/45 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="approval-confirm-title"
    >
      <div className="w-full max-w-md rounded-2xl border border-outline-variant/20 bg-surface-container-lowest p-6 shadow-2xl">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary/15">
              <IconCheckCircle className="h-5 w-5 text-primary" />
            </div>
            <div>
              <h2 id="approval-confirm-title" className="text-sm font-bold text-on-surface">
                {title}
              </h2>
              <p className="mt-2 text-xs leading-relaxed text-on-surface-variant">{message}</p>
            </div>
          </div>
          <button
            type="button"
            className="rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-container-high"
            onClick={onCancel}
            aria-label="Close"
          >
            <IconClose className="h-4 w-4" />
          </button>
        </div>
        <div className="mt-6 flex justify-end gap-2 border-t border-outline-variant/10 pt-4">
          <button
            type="button"
            className="rounded-lg border border-outline-variant/30 px-4 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="button"
            className="rounded-lg bg-primary px-4 py-2 text-xs font-bold text-on-primary shadow-md hover:opacity-90"
            onClick={onConfirm}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
