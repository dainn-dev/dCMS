import { useEffect, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconAddCircle, IconBolt, IconFilterList, IconRestartAlt } from "../../orders/icons";
import { createCampaignColumns } from "../campaigns-columns";
import type { CampaignChannel, CampaignListRow, CampaignStatus } from "../campaigns-columns";

const ALL_STATUSES: CampaignStatus[] = ["draft", "scheduled", "active", "paused", "ended"];
const ALL_CHANNELS: CampaignChannel[] = ["Email", "SMS", "Push", "Web"];

const DEMO_CAMPAIGNS: CampaignListRow[] = [
  {
    id: "cmp-1",
    name: "Spring Launch 2026",
    code: "SPRING26",
    editorKind: "pwp-discount",
    status: "active",
    channel: "Email",
    startDate: "Apr 01, 2026",
    endDate: "Apr 30, 2026",
    audience: "Subscribers · VIP tier",
    budget: "$12,000",
    conversions: 842,
  },
  {
    id: "cmp-2",
    name: "Cart Abandonment — Week 15",
    code: "CART_ABN_W15",
    status: "scheduled",
    channel: "SMS",
    startDate: "Apr 18, 2026",
    endDate: "Apr 25, 2026",
    audience: "Checkout drop-off · last 7 days",
    budget: "$3,500",
    conversions: 0,
  },
  {
    id: "cmp-3",
    name: "Member Day Flash",
    code: "MEMBER_DAY",
    editorKind: "after-sales",
    status: "draft",
    channel: "Push",
    startDate: "—",
    endDate: "—",
    audience: "App users · logged-in",
    budget: "$8,000",
    conversions: 0,
  },
  {
    id: "cmp-4",
    name: "Homepage hero A/B",
    code: "HERO_AB",
    editorKind: "mix-match",
    status: "paused",
    channel: "Web",
    startDate: "Mar 01, 2026",
    endDate: "Mar 31, 2026",
    audience: "All storefront visitors",
    budget: "$2,000",
    conversions: 1204,
  },
  {
    id: "cmp-5",
    name: "Lunar New Year recap",
    code: "LNY_RECAP",
    status: "ended",
    channel: "Email",
    startDate: "Jan 15, 2026",
    endDate: "Feb 15, 2026",
    audience: "Purchasers · CNY collection",
    budget: "$5,000",
    conversions: 3102,
  },
  {
    id: "cmp-6",
    name: "Store opening — Orchard",
    code: "OPEN_ORCH",
    status: "active",
    channel: "SMS",
    startDate: "Apr 10, 2026",
    endDate: "May 10, 2026",
    audience: "Geo · 5km radius",
    budget: "$1,200",
    conversions: 56,
  },
];

const labelFilter =
  "text-xs font-bold text-on-surface-variant uppercase tracking-wider";
const selectFilter =
  "min-w-[140px] rounded-lg border border-outline-variant/20 bg-surface-container-lowest py-2 px-3 text-xs text-on-surface focus:ring-1 focus:ring-primary outline-none";

export type CampaignsPageProps = {
  onCreateCampaign?: () => void;
  onEditCampaign?: (row: CampaignListRow) => void;
  onViewCampaign?: (row: CampaignListRow) => void;
};

export function CampaignsPage({ onCreateCampaign, onEditCampaign, onViewCampaign }: CampaignsPageProps) {
  const [filterStatus, setFilterStatus] = useState<"all" | CampaignStatus>("all");
  const [filterChannel, setFilterChannel] = useState<"all" | CampaignChannel>("all");
  const [toast, setToast] = useState<string | null>(null);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 2400);
    return () => clearTimeout(t);
  }, [toast]);

  const filteredRows = useMemo(() => {
    return DEMO_CAMPAIGNS.filter((r) => {
      if (filterStatus !== "all" && r.status !== filterStatus) return false;
      if (filterChannel !== "all" && r.channel !== filterChannel) return false;
      return true;
    });
  }, [filterStatus, filterChannel]);

  const columns = useMemo(
    () =>
      createCampaignColumns(
        (id) => {
          const c = DEMO_CAMPAIGNS.find((x) => x.id === id);
          if (c && onViewCampaign) onViewCampaign(c);
          else if (c) setToast(`Opening campaign: ${c.name}`);
          else setToast("Campaign not found");
        },
        (id) => {
          const c = DEMO_CAMPAIGNS.find((x) => x.id === id);
          if (c && onEditCampaign) onEditCampaign(c);
          else if (c) setToast(`Edit campaign (demo): ${c.code}`);
          else setToast("Campaign not found");
        }
      ),
    [onEditCampaign, onViewCampaign]
  );

  const filterActive = filterStatus !== "all" || filterChannel !== "all";

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Campaign management">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-start md:justify-between">
        <div>
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Campaigns</span>
          </nav>
          <div className="flex items-center gap-2">
            <IconBolt className="h-7 w-7 shrink-0 text-primary" aria-hidden />
            <div>
              <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Campaign Management</h1>
              <p className="mt-0.5 text-sm text-on-surface-variant max-w-2xl">
                Browse campaigns; use the table search to filter by name, code, audience, and more. Combine status and channel filters below. Create or edit opens the campaign form (PWP, Mix and Match, Product Discount, After Sales).
              </p>
            </div>
          </div>
        </div>
        {onCreateCampaign && (
          <button
            type="button"
            onClick={onCreateCampaign}
            className="inline-flex shrink-0 items-center gap-2 self-start rounded-lg bg-primary px-4 py-2.5 text-xs font-bold text-on-primary shadow-md shadow-primary/20 transition-opacity hover:opacity-90"
          >
            <IconAddCircle className="h-4 w-4 shrink-0" aria-hidden />
            Create campaign
          </button>
        )}
      </header>

      <div className="w-full flex-1 space-y-4 p-6">
        <section className="rounded-xl border border-outline-variant/15 bg-surface-container-lowest p-4 shadow-sm">
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <IconFilterList className="h-4 w-4 text-primary shrink-0" aria-hidden />
            <h2 className="text-xs font-bold uppercase tracking-widest text-on-surface">Quick filters</h2>
          </div>
          <div className="flex flex-wrap items-end gap-4">
            <div>
              <label className={`${labelFilter} mb-1 block`}>Status</label>
              <select
                className={selectFilter}
                value={filterStatus}
                onChange={(e) => setFilterStatus(e.target.value as typeof filterStatus)}
              >
                <option value="all">All</option>
                {ALL_STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {s.charAt(0).toUpperCase() + s.slice(1)}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className={`${labelFilter} mb-1 block`}>Channel</label>
              <select
                className={selectFilter}
                value={filterChannel}
                onChange={(e) => setFilterChannel(e.target.value as typeof filterChannel)}
              >
                <option value="all">All</option>
                {ALL_CHANNELS.map((ch) => (
                  <option key={ch} value={ch}>
                    {ch}
                  </option>
                ))}
              </select>
            </div>
            <button
              type="button"
              disabled={!filterActive}
              className="inline-flex items-center gap-2 rounded-lg border border-outline-variant/30 px-3 py-2 text-xs font-bold text-on-surface-variant transition-colors hover:bg-surface-container-high disabled:pointer-events-none disabled:opacity-40"
              onClick={() => {
                setFilterStatus("all");
                setFilterChannel("all");
              }}
            >
              <IconRestartAlt className="h-4 w-4 shrink-0" />
              Clear filters
            </button>
            <p className="ml-auto text-xs text-on-surface-variant">
              Showing <strong className="text-on-surface">{filteredRows.length}</strong> / {DEMO_CAMPAIGNS.length} campaigns
            </p>
          </div>
        </section>

        <DataTable
          columns={columns}
          data={filteredRows}
          getRowId={(r) => r.id}
          globalFilterPlaceholder="Search by name, code, audience, budget…"
        />
      </div>

      {toast && (
        <div
          className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-secondary/20 bg-surface-container-lowest px-5 py-2.5 text-sm font-medium text-on-surface shadow-xl"
          role="status"
          aria-live="polite"
        >
          {toast}
        </div>
      )}
    </div>
  );
}
