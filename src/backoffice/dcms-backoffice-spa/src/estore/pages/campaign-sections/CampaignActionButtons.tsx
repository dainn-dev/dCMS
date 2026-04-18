import { useEffect, useRef, useState } from "react";
import { IconCheckCircle, IconChevronDown, IconSearch } from "../../../orders/icons";

type Props = {
  onSaveAndApprove: () => void;
  onSaveAndSendForApproval: () => void;
  onSaveAndArchive: () => void;
  onDeactivate: () => void;
  onReject: () => void;
  /** US-7 / DAI-456: opens change history panel */
  onShowChangeHistory: () => void;
};

export function CampaignActionButtons({
  onSaveAndApprove,
  onSaveAndSendForApproval,
  onSaveAndArchive,
  onDeactivate,
  onReject,
  onShowChangeHistory,
}: Props) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  function run(fn: () => void) {
    setOpen(false);
    fn();
  }

  return (
    <div className="relative shrink-0" ref={ref}>
      <button
        type="button"
        className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90"
        onClick={() => setOpen((o) => !o)}
      >
        Actions
        <IconChevronDown className={`h-3.5 w-3.5 shrink-0 transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute right-0 top-full z-20 mt-1 w-64 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
          <button
            type="button"
            className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
            onClick={() => run(onSaveAndApprove)}
          >
            <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
            Save and Approve
          </button>
          <button
            type="button"
            className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
            onClick={() => run(onSaveAndSendForApproval)}
          >
            <IconCheckCircle className="h-4 w-4 shrink-0 text-secondary" />
            Save and Send for Approval
          </button>
          <button
            type="button"
            className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
            onClick={() => run(onSaveAndArchive)}
          >
            Save and Archive
          </button>
          <div className="my-1 border-t border-outline-variant/10" />
          <button
            type="button"
            className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
            onClick={() => run(onDeactivate)}
          >
            Deactivate
          </button>
          <button
            type="button"
            className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
            onClick={() => run(onReject)}
          >
            Reject
          </button>
          <div className="my-1 border-t border-outline-variant/10" />
          <button
            type="button"
            className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
            onClick={() => run(onShowChangeHistory)}
          >
            <IconSearch className="h-4 w-4 shrink-0 text-on-surface-variant" />
            Show Change History
          </button>
        </div>
      )}
    </div>
  );
}
