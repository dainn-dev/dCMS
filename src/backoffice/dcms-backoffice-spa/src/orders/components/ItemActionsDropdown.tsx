import { useState } from "react";
import { ITEM_NEXT_STATUSES, ITEM_STATUS_LABEL, type ItemFulfillmentStatus } from "../types";

export type ItemAction =
  | { kind: "transition"; to: ItemFulfillmentStatus }
  | { kind: "issue_pin" }
  | { kind: "confirm_pickup" }
  | { kind: "open_return" };

type Props = {
  current: ItemFulfillmentStatus;
  /** Variant snapshot delivery method (e.g. "pickup") so we know to offer PIN actions. */
  isPickup?: boolean;
  /** Disable everything (e.g. busy state). */
  disabled?: boolean;
  onAction: (action: ItemAction) => void;
};

export function ItemActionsDropdown({ current, isPickup, disabled, onAction }: Props) {
  const [open, setOpen] = useState(false);
  const next = ITEM_NEXT_STATUSES[current] ?? [];

  // Hide PIN-related transitions when not pickup; pickup confirms via dedicated handler.
  const transitions = next.filter((s) => !(isPickup && s === "picked_up"));
  const showPinActions = isPickup && current === "ready_for_delivery";
  const showOpenReturn = current === "delivered" || current === "picked_up";

  const empty = transitions.length === 0 && !showPinActions && !showOpenReturn;

  return (
    <div className="relative inline-block">
      <button
        type="button"
        className="text-[10px] font-bold border border-outline-variant/30 rounded px-2 py-1 hover:bg-surface-container-low disabled:opacity-40"
        onClick={() => setOpen((v) => !v)}
        disabled={disabled || empty}
        aria-haspopup="menu"
        aria-expanded={open}
      >
        Actions ▾
      </button>
      {open && (
        <div
          role="menu"
          className="absolute right-0 top-full mt-1 z-20 w-52 bg-white border border-outline-variant/20 rounded-lg shadow-lg py-1"
        >
          {transitions.length > 0 && (
            <>
              <p className="px-3 pt-2 pb-1 text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
                Move to
              </p>
              {transitions.map((s) => (
                <button
                  key={s}
                  type="button"
                  role="menuitem"
                  className="w-full text-left px-3 py-1.5 text-xs hover:bg-surface-container-low text-on-surface"
                  onClick={() => { setOpen(false); onAction({ kind: "transition", to: s }); }}
                >
                  {ITEM_STATUS_LABEL[s] ?? s}
                </button>
              ))}
            </>
          )}

          {showPinActions && (
            <div className="border-t border-outline-variant/10 mt-1 pt-1">
              <button
                type="button"
                role="menuitem"
                className="w-full text-left px-3 py-1.5 text-xs hover:bg-surface-container-low text-on-surface"
                onClick={() => { setOpen(false); onAction({ kind: "issue_pin" }); }}
              >
                Issue Pickup PIN
              </button>
              <button
                type="button"
                role="menuitem"
                className="w-full text-left px-3 py-1.5 text-xs hover:bg-surface-container-low text-on-surface"
                onClick={() => { setOpen(false); onAction({ kind: "confirm_pickup" }); }}
              >
                Confirm Pickup…
              </button>
            </div>
          )}

          {showOpenReturn && (
            <div className="border-t border-outline-variant/10 mt-1 pt-1">
              <button
                type="button"
                role="menuitem"
                className="w-full text-left px-3 py-1.5 text-xs hover:bg-surface-container-low text-on-surface"
                onClick={() => { setOpen(false); onAction({ kind: "open_return" }); }}
              >
                Open RMA / Return…
              </button>
            </div>
          )}

          {empty && (
            <p className="px-3 py-2 text-[11px] italic text-on-surface-variant">No actions available</p>
          )}
        </div>
      )}
    </div>
  );
}
