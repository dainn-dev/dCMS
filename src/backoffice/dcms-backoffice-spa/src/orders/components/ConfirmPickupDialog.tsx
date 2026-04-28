import { useState } from "react";

type Props = {
  open: boolean;
  orderId: string;
  lineId: string;
  busy?: boolean;
  onClose: () => void;
  onSubmit: (pin: string, pickedUpBy: string | null) => void;
};

export function ConfirmPickupDialog({ open, orderId, lineId, busy, onClose, onSubmit }: Props) {
  const [pin, setPin] = useState("");
  const [pickedUpBy, setPickedUpBy] = useState("");

  if (!open) return null;
  const canSubmit = pin.trim().length >= 4 && !busy;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md mx-4 p-6 space-y-4">
        <h2 className="text-sm font-bold text-on-surface">Confirm Pickup</h2>
        <p className="text-xs text-on-surface-variant">
          Verify the customer's pickup PIN for line{" "}
          <span className="font-mono font-semibold text-on-surface">{lineId}</span> on order{" "}
          <span className="font-mono font-semibold text-on-surface">#{orderId}</span>.
        </p>
        <div className="space-y-3">
          <label className="space-y-1 block">
            <span className="block text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
              Pickup PIN
            </span>
            <input
              className="w-full bg-surface-container-low border border-outline-variant/20 rounded-lg px-3 py-2 text-xs font-mono tracking-widest"
              value={pin}
              onChange={(e) => setPin(e.target.value.replace(/\D/g, ""))}
              inputMode="numeric"
              autoComplete="one-time-code"
              placeholder="••••••"
            />
          </label>
          <label className="space-y-1 block">
            <span className="block text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
              Picked Up By (optional)
            </span>
            <input
              className="w-full bg-surface-container-low border border-outline-variant/20 rounded-lg px-3 py-2 text-xs"
              value={pickedUpBy}
              onChange={(e) => setPickedUpBy(e.target.value)}
              placeholder="Staff name / ID"
            />
          </label>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <button
            type="button"
            className="px-4 py-2 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-low rounded-lg"
            onClick={onClose}
            disabled={busy}
          >
            Cancel
          </button>
          <button
            type="button"
            className="px-4 py-2 text-xs font-bold bg-primary text-white rounded-lg hover:bg-primary-container disabled:opacity-40"
            onClick={() => onSubmit(pin.trim(), pickedUpBy.trim() || null)}
            disabled={!canSubmit}
          >
            Confirm Pickup
          </button>
        </div>
      </div>
    </div>
  );
}
