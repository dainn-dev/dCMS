import { useEffect, useState } from "react";
import { IconClose, IconWarning } from "../../orders/icons";

const inputBase =
  "w-full rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs text-on-surface focus:ring-1 focus:ring-primary outline-none";

type Props = {
  open: boolean;
  title?: string;
  /** e.g. "Reject3 products?" */
  subtitle?: string;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
};

export function RejectionReasonModal({
  open,
  title = "Reason of rejection",
  subtitle,
  onConfirm,
  onCancel,
}: Props) {
  const [reason, setReason] = useState("");

  useEffect(() => {
    if (open) setReason("");
  }, [open]);

  if (!open) return null;

  const canSubmit = reason.trim().length > 0;

  return (
    <div
      className="fixed inset-0 z-[70] flex items-center justify-center bg-black/45 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="reject-reason-title"
    >
      <div className="w-full max-w-md rounded-2xl border border-outline-variant/20 bg-surface-container-lowest p-6 shadow-2xl">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
              <IconWarning className="h-5 w-5 text-error" />
            </div>
            <div>
              <h2 id="reject-reason-title" className="text-sm font-bold text-on-surface">
                {title}
              </h2>
              {subtitle && <p className="mt-1 text-xs text-on-surface-variant">{subtitle}</p>}
              <label className="mt-3 block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant">
                Reason <span className="text-error">*</span>
              </label>
              <textarea
                className={`${inputBase} mt-1 min-h-[100px] resize-y`}
                placeholder="Explain why this submission cannot be approved…"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                aria-required
              />
            </div>
          </div>
          <button
            type="button"
            className="shrink-0 rounded-lg p-1.5 text-on-surface-variant hover:bg-surface-container-high"
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
            disabled={!canSubmit}
            className="rounded-lg bg-error px-4 py-2 text-xs font-bold text-on-error shadow-md hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
            onClick={() => onConfirm(reason.trim())}
          >
            Ok
          </button>
        </div>
      </div>
    </div>
  );
}
