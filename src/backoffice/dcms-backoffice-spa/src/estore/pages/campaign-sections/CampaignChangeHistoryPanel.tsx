import { IconClose } from "../../../orders/icons";

export type CampaignChangeHistoryEntry = {
  id: string;
  /** ISO timestamp */
  at: string;
  user: string;
  field: string;
  oldValue: string;
  newValue: string;
};

function formatAt(iso: string) {
  try {
    const d = new Date(iso);
    return d.toLocaleString(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    });
  } catch {
    return iso;
  }
}

/** Demo rows for US-7 change history UI. */
export function buildSeedChangeHistory(campaignCode: string, campaignName: string): CampaignChangeHistoryEntry[] {
  return [
    {
      id: "seed-1",
      at: "2026-04-16T09:12:00.000Z",
      user: "brand.manager@example.com",
      field: "Name",
      oldValue: "(empty)",
      newValue: campaignName,
    },
    {
      id: "seed-2",
      at: "2026-04-16T09:14:22.000Z",
      user: "brand.manager@example.com",
      field: "Code",
      oldValue: "(empty)",
      newValue: campaignCode,
    },
    {
      id: "seed-3",
      at: "2026-04-16T10:01:00.000Z",
      user: "admin@dainn.dev",
      field: "Workflow",
      oldValue: "Draft",
      newValue: "Pending approval",
    },
    {
      id: "seed-4",
      at: "2026-04-16T15:40:00.000Z",
      user: "admin@dainn.dev",
      field: "Apply on eStore",
      oldValue: "Off",
      newValue: "On",
    },
  ];
}

type Props = {
  open: boolean;
  onClose: () => void;
  campaignCode: string;
  campaignName?: string;
  entries: CampaignChangeHistoryEntry[];
};

export function CampaignChangeHistoryPanel({ open, onClose, campaignCode, campaignName, entries }: Props) {
  if (!open) return null;

  const sorted = [...entries].sort((a, b) => (a.at < b.at ? 1 : a.at > b.at ? -1 : 0));

  return (
    <div
      className="fixed inset-0 z-[60] flex items-center justify-center bg-black/45 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="campaign-history-title"
    >
      <div className="flex max-h-[min(85vh,720px)] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
        <div className="flex shrink-0 items-start justify-between gap-4 border-b border-outline-variant/15 px-5 py-4">
          <div>
            <h2 id="campaign-history-title" className="text-base font-bold text-on-surface">
              Change history
            </h2>
            <p className="mt-0.5 text-xs text-on-surface-variant">
              <span className="font-mono font-semibold text-on-surface">{campaignCode}</span>
              {campaignName ? <span> · {campaignName}</span> : null}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-2 text-on-surface-variant transition-colors hover:bg-surface-container-high hover:text-on-surface"
            aria-label="Close change history"
          >
            <IconClose className="h-5 w-5" />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-auto p-4">
          {sorted.length === 0 ? (
            <p className="py-8 text-center text-sm text-on-surface-variant">No changes recorded yet.</p>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-outline-variant/15">
              <table className="w-full min-w-[640px] border-collapse text-left text-xs">
                <thead>
                  <tr className="border-b border-outline-variant/15 bg-surface-container-high/80">
                    <th className="px-3 py-2.5 font-bold uppercase tracking-wider text-on-surface-variant">When</th>
                    <th className="px-3 py-2.5 font-bold uppercase tracking-wider text-on-surface-variant">User</th>
                    <th className="px-3 py-2.5 font-bold uppercase tracking-wider text-on-surface-variant">Field / action</th>
                    <th className="px-3 py-2.5 font-bold uppercase tracking-wider text-on-surface-variant">Old → new</th>
                  </tr>
                </thead>
                <tbody>
                  {sorted.map((row) => (
                    <tr key={row.id} className="border-b border-outline-variant/10 last:border-0 hover:bg-surface-container-low/80">
                      <td className="whitespace-nowrap px-3 py-2.5 font-mono text-on-surface-variant">{formatAt(row.at)}</td>
                      <td className="px-3 py-2.5 text-on-surface">{row.user}</td>
                      <td className="px-3 py-2.5 font-semibold text-on-surface">{row.field}</td>
                      <td className="px-3 py-2.5 text-on-surface-variant">
                        <span className="line-through opacity-70">{row.oldValue}</span>
                        <span className="mx-1.5 text-primary">→</span>
                        <span className="font-medium text-on-surface">{row.newValue}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
