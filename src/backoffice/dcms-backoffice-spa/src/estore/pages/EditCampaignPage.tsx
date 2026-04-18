import { useEffect, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconSearch,
} from "../../orders/icons";
import {
  deriveCampaignWorkflow,
  type CampaignEditorKind as CampaignKind,
  type CampaignListRow,
  type CampaignWorkflowState,
} from "../campaigns-columns";
import { CampaignActionButtons } from "./campaign-sections/CampaignActionButtons";
import {
  buildSeedChangeHistory,
  CampaignChangeHistoryPanel,
  type CampaignChangeHistoryEntry,
} from "./campaign-sections/CampaignChangeHistoryPanel";
import { defaultMixMatchMechanicsValue, MixMatchMechanicsSection, type MixMatchMechanicsValue } from "./campaign-sections/MixMatchMechanicsSection";
import {
  defaultAfterSalesMechanicsValue,
  AfterSalesMechanicsSection,
  type AfterSalesMechanicsValue,
} from "./campaign-sections/AfterSalesMechanicsSection";
import {
  defaultProductDiscountMechanicsValue,
  ProductDiscountMechanicsSection,
  type ProductDiscountMechanicsValue,
} from "./campaign-sections/ProductDiscountMechanicsSection";
import { PwpDiscountMechanicsSection, type PwpDiscountMechanicsValue } from "./campaign-sections/PwpDiscountMechanicsSection";
import { PwpItemMechanicsSection, type PwpItemMechanicsValue } from "./campaign-sections/PwpItemMechanicsSection";

const sectionCard = "rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm";
const sectionTitle = "mb-5 text-sm font-bold uppercase tracking-widest text-on-surface";
const labelBase = "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-xs text-on-surface-variant";

const MOCK_BRANDS = ["Cronos Ltd.", "Sonic Bloom", "Apex Footwear", "NovaCam", "FurniCraft"];
const MOCK_CATEGORIES = ["Electronics", "Furniture", "Timepieces > Luxury", "Audio > Wireless", "Footwear > Athletics"];
const MOCK_PID1 = ["Department A", "Department B", "Core Range", "Seasonal"];
const MOCK_PID2 = ["Line 01", "Line 02", "Limited", "Standard"];
const MOCK_PRODUCTS = ["Vantage Series 5 Watch (WT-550-B)", "Echo-Noise Headphones (AU-102-S)", "SwiftRun Pro Z (FT-99-R)"];
const MOCK_MEMBERSHIP_TYPES = ["Standard", "Premium", "Corporate"];
const MOCK_MEMBERSHIP_TIERS = ["Bronze", "Silver", "Gold", "Platinum"];

const CAMPAIGN_KIND_LABELS: Record<CampaignKind, string> = {
  "pwp-item": "Purchase with Purchase - Item",
  "pwp-discount": "Purchase with Purchase - Discount",
  "mix-match": "Mix and Match",
  "product-discount": "Product Discount",
  "after-sales": "After Sales Promotion",
};

const WORKFLOW_LABEL: Record<CampaignWorkflowState, string> = {
  draft: "Draft",
  pending_approval: "Pending approval",
  approved: "Approved",
  active: "Active on eStore",
  deactivated: "Deactivated",
  archived: "Archived",
  rejected: "Rejected",
};

const WORKFLOW_BADGE: Record<CampaignWorkflowState, string> = {
  draft: "bg-surface-container-high text-on-surface-variant",
  pending_approval: "bg-secondary-container/30 text-on-secondary-container",
  approved: "bg-primary/15 text-primary",
  active: "bg-tertiary-container text-on-tertiary-container",
  deactivated: "bg-amber-100 text-amber-900",
  archived: "bg-outline-variant/25 text-on-surface-variant",
  rejected: "bg-error-container text-on-error-container",
};

function newHistoryId() {
  return typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `h-${Date.now()}`;
}

function SearchablePicker({
  label,
  options,
  selected,
  onChange,
  placeholder,
}: {
  label: string;
  options: string[];
  selected: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const filtered = options.filter((o) => o.toLowerCase().includes(search.toLowerCase()));

  function toggle(opt: string) {
    onChange(selected.includes(opt) ? selected.filter((s) => s !== opt) : [...selected, opt]);
  }

  return (
    <div ref={ref} className="relative space-y-1.5">
      <label className={labelBase}>{label}</label>
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((s) => (
            <span
              key={s}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-bold uppercase text-primary"
            >
              {s}
              <button type="button" onClick={() => toggle(s)} className="rounded p-0.5 hover:bg-primary/20 transition-colors" aria-label={`Remove ${s}`}>
                <IconClose className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center gap-2 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs transition-colors hover:border-primary/30 focus:outline-none focus:ring-1 focus:ring-primary"
      >
        <IconSearch className="h-3.5 w-3.5 shrink-0 text-on-surface-variant" />
        <span className={selected.length === 0 ? "flex-1 text-left text-on-surface-variant/60" : "flex-1 text-left text-on-surface"}>
          {selected.length === 0 ? (placeholder ?? `Search ${label.toLowerCase()}…`) : `${selected.length} selected`}
        </span>
        <IconChevronDown className={`h-3.5 w-3.5 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute left-0 top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
          <div className="border-b border-outline-variant/10 p-2">
            <input
              autoFocus
              type="text"
              placeholder="Search…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full rounded bg-surface-container-low px-2.5 py-1.5 text-xs outline-none placeholder:text-on-surface-variant/50"
            />
          </div>
          <div className="max-h-44 overflow-y-auto">
            {filtered.length === 0 ? (
              <p className="px-3 py-3 text-center text-xs text-on-surface-variant">No results</p>
            ) : (
              filtered.map((opt) => (
                <label key={opt} className="flex cursor-pointer items-center gap-2.5 px-3 py-2 hover:bg-surface-container transition-colors select-none">
                  <input type="checkbox" className="h-3.5 w-3.5 accent-primary shrink-0" checked={selected.includes(opt)} onChange={() => toggle(opt)} />
                  <span className="text-xs text-on-surface">{opt}</span>
                </label>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}

const DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"] as const;
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"] as const;

function toggleIn<T extends string>(list: T[], item: T): T[] {
  return list.includes(item) ? list.filter((x) => x !== item) : [...list, item];
}

type Props = {
  mode: "add" | "edit";
  campaign?: CampaignListRow;
  onBack: () => void;
};

export function EditCampaignPage({ mode, campaign, onBack }: Props) {
  const isAdd = mode === "add";

  const [campaignKind, setCampaignKind] = useState<CampaignKind>(() => campaign?.editorKind ?? "pwp-item");
  const [workflow, setWorkflow] = useState<CampaignWorkflowState>(() => deriveCampaignWorkflow(campaign));
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyEntries, setHistoryEntries] = useState<CampaignChangeHistoryEntry[]>([]);
  const [code, setCode] = useState(campaign?.code ?? "");
  const [name, setName] = useState(campaign?.name ?? "");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [activeDays, setActiveDays] = useState<typeof DAYS[number][]>([]);
  const [activeMonths, setActiveMonths] = useState<typeof MONTHS[number][]>([]);

  const [inclusionEnabled, setInclusionEnabled] = useState(true);
  const [incBrands, setIncBrands] = useState<string[]>([]);
  const [incCategories, setIncCategories] = useState<string[]>([]);
  const [incPid1, setIncPid1] = useState<string[]>([]);
  const [incPid2, setIncPid2] = useState<string[]>([]);
  const [incProducts, setIncProducts] = useState<string[]>([]);
  const [cartAllQualifiers, setCartAllQualifiers] = useState(false);
  const [minSpend, setMinSpend] = useState("");
  const [minSpendNet, setMinSpendNet] = useState(false);

  const [exclusionEnabled, setExclusionEnabled] = useState(false);
  const [excBrands, setExcBrands] = useState<string[]>([]);
  const [excCategories, setExcCategories] = useState<string[]>([]);
  const [excPid1, setExcPid1] = useState<string[]>([]);
  const [excPid2, setExcPid2] = useState<string[]>([]);
  const [excProducts, setExcProducts] = useState<string[]>([]);
  const [excMembershipTypes, setExcMembershipTypes] = useState<string[]>([]);
  const [excMembershipTiers, setExcMembershipTiers] = useState<string[]>([]);

  const [itemMechanics, setItemMechanics] = useState<PwpItemMechanicsValue>({
    promotionProducts: [],
    qualifyingProductsPerSet: "",
    maxPromotionalProductsPerUser: "",
  });

  const [discountMechanics, setDiscountMechanics] = useState<PwpDiscountMechanicsValue>({
    promotionProducts: [],
    discountType: "fixed",
    discountMode: "add",
    discountValue: "",
    qualifyingProductsPerSet: "",
    maxPromotionalProductsPerUser: "",
    promotionMessageOnProducts: "",
  });

  const [mixMatchMechanics, setMixMatchMechanics] = useState<MixMatchMechanicsValue>(() => defaultMixMatchMechanicsValue());

  const [productDiscountMechanics, setProductDiscountMechanics] = useState<ProductDiscountMechanicsValue>(() =>
    defaultProductDiscountMechanicsValue()
  );

  const [afterSalesMechanics, setAfterSalesMechanics] = useState<AfterSalesMechanicsValue>(() => defaultAfterSalesMechanicsValue());

  const [orderDetailMessage, setOrderDetailMessage] = useState("");
  const [promotionDetails, setPromotionDetails] = useState("");
  const [showPromoOnProductPage, setShowPromoOnProductPage] = useState(false);
  const [promotionMessagePriority, setPromotionMessagePriority] = useState("");

  const [campaignPriority, setCampaignPriority] = useState("");
  const [blockOtherPromos, setBlockOtherPromos] = useState(false);
  const [blockSamePriority, setBlockSamePriority] = useState(false);
  const [activationCodeEnabled, setActivationCodeEnabled] = useState(false);
  const [activationCode, setActivationCode] = useState("");
  const [applyOnEStore, setApplyOnEStore] = useState(true);

  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3200);
    return () => clearTimeout(t);
  }, [toast]);

  useEffect(() => {
    setCampaignKind(campaign?.editorKind ?? "pwp-item");
    setWorkflow(deriveCampaignWorkflow(campaign));
  }, [campaign?.id, campaign?.editorKind, campaign?.workflowState, campaign?.status]);

  useEffect(() => {
    if (campaign) {
      setHistoryEntries(buildSeedChangeHistory(campaign.code, campaign.name));
    } else {
      setHistoryEntries([
        {
          id: "session-new",
          at: new Date().toISOString(),
          user: "You (demo)",
          field: "Session",
          oldValue: "—",
          newValue: "New campaign draft",
        },
      ]);
    }
  }, [campaign?.id, campaign?.code, campaign?.name]);

  function appendHistory(entry: Omit<CampaignChangeHistoryEntry, "id">) {
    setHistoryEntries((prev) => [{ id: newHistoryId(), ...entry }, ...prev]);
  }

  const typeLocked = workflow === "approved" || workflow === "active";

  function handleSaveAndApprove() {
    const next: CampaignWorkflowState = applyOnEStore ? "active" : "approved";
    const prev = WORKFLOW_LABEL[workflow];
    setWorkflow(next);
    appendHistory({
      at: new Date().toISOString(),
      user: "admin@dainn.dev (demo)",
      field: "Workflow",
      oldValue: prev,
      newValue: WORKFLOW_LABEL[next],
    });
    setToast(applyOnEStore ? "Approved and activated on eStore (demo)." : "Approved — not live until eStore flag is on (demo).");
  }

  function handleSaveAndSendForApproval() {
    const prev = WORKFLOW_LABEL[workflow];
    setWorkflow("pending_approval");
    appendHistory({
      at: new Date().toISOString(),
      user: "You (demo)",
      field: "Workflow",
      oldValue: prev,
      newValue: WORKFLOW_LABEL.pending_approval,
    });
    setToast("Saved and sent for approval (demo).");
  }

  function handleSaveAndArchive() {
    const prev = WORKFLOW_LABEL[workflow];
    setWorkflow("archived");
    appendHistory({
      at: new Date().toISOString(),
      user: "You (demo)",
      field: "Workflow",
      oldValue: prev,
      newValue: WORKFLOW_LABEL.archived,
    });
    setToast("Campaign archived (demo).");
  }

  function handleDeactivate() {
    const prev = WORKFLOW_LABEL[workflow];
    setWorkflow("deactivated");
    appendHistory({
      at: new Date().toISOString(),
      user: "You (demo)",
      field: "Workflow",
      oldValue: prev,
      newValue: WORKFLOW_LABEL.deactivated,
    });
    setToast("Campaign deactivated on eStore (demo).");
  }

  function handleReject() {
    const prev = WORKFLOW_LABEL[workflow];
    setWorkflow("rejected");
    appendHistory({
      at: new Date().toISOString(),
      user: "admin@dainn.dev (demo)",
      field: "Workflow",
      oldValue: prev,
      newValue: WORKFLOW_LABEL.rejected,
    });
    setToast("Campaign rejected — not activated (demo).");
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to campaigns
          </button>
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-2xl font-bold tracking-tight text-on-surface">{isAdd ? "New campaign" : "Edit campaign"}</h2>
            <span
              className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[10px] font-bold uppercase ${WORKFLOW_BADGE[workflow]}`}
              title="Approval / lifecycle (US-7)"
            >
              <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-current opacity-60" />
              {WORKFLOW_LABEL[workflow]}
            </span>
          </div>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            {CAMPAIGN_KIND_LABELS[campaignKind]}
            {campaign ? ` · ${campaign.code}` : ""}
          </p>
        </div>

        <CampaignActionButtons
          onSaveAndApprove={handleSaveAndApprove}
          onSaveAndSendForApproval={handleSaveAndSendForApproval}
          onSaveAndArchive={handleSaveAndArchive}
          onDeactivate={handleDeactivate}
          onReject={handleReject}
          onShowChangeHistory={() => setHistoryOpen(true)}
        />
      </div>

      <div className="flex-1 space-y-6 p-6 pb-24">
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Campaign information</h3>
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <div>
              <label className={labelBase}>
                Type <span className="text-error">*</span>
              </label>
              <select
                className={inputBase}
                value={campaignKind}
                disabled={typeLocked}
                aria-disabled={typeLocked}
                onChange={(e) => setCampaignKind(e.target.value as CampaignKind)}
              >
                {(Object.keys(CAMPAIGN_KIND_LABELS) as CampaignKind[]).map((k) => (
                  <option key={k} value={k}>
                    {CAMPAIGN_KIND_LABELS[k]}
                  </option>
                ))}
              </select>
              <p className={hintText}>
                {campaignKind === "pwp-item"
                  ? "US-2: add-on promotional products when the cart qualifies."
                  : campaignKind === "pwp-discount"
                    ? "US-3: discounted promotional products when the cart qualifies (fixed amount or %)."
                    : campaignKind === "mix-match"
                      ? "US-4: combo pricing — Single item, Per bundle, or Incremental discount logic."
                      : campaignKind === "product-discount"
                        ? "US-5: simple discount on qualifying products (no purchase-with-purchase)."
                        : "US-6: post-purchase email with promo code to encourage repeat purchase."}
              </p>
              {typeLocked && (
                <p className={`${hintText} text-amber-800/90`}>Type is locked while the campaign is approved or active on eStore (US-7).</p>
              )}
            </div>
            <div className="hidden lg:block" aria-hidden />
            <div>
              <label className={labelBase}>
                Code <span className="text-error">*</span>
              </label>
              <input type="text" className={inputBase} placeholder="e.g. PWP_SPRING26" value={code} onChange={(e) => setCode(e.target.value)} />
            </div>
            <div>
              <label className={labelBase}>
                Name <span className="text-error">*</span>
              </label>
              <input type="text" className={inputBase} placeholder="Campaign display name" value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div>
              <label className={labelBase}>
                Start date <span className="text-error">*</span>
              </label>
              <input type="datetime-local" className={inputBase} value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </div>
            <div>
              <label className={labelBase}>End date</label>
              <input type="datetime-local" className={inputBase} value={endDate} onChange={(e) => setEndDate(e.target.value)} />
            </div>
          </div>
          <div className="mt-6 space-y-3">
            <p className={labelBase}>Active days of week</p>
            <div className="flex flex-wrap gap-2">
              {DAYS.map((d) => (
                <button
                  key={d}
                  type="button"
                  onClick={() => setActiveDays((prev) => toggleIn(prev, d))}
                  className={`rounded-lg border px-3 py-1.5 text-xs font-bold uppercase tracking-wide transition-colors ${
                    activeDays.includes(d)
                      ? "border-primary bg-primary/10 text-primary"
                      : "border-outline-variant/30 text-on-surface-variant hover:border-primary/40"
                  }`}
                >
                  {d}
                </button>
              ))}
            </div>
          </div>
          <div className="mt-6 space-y-3">
            <p className={labelBase}>Active months</p>
            <div className="flex flex-wrap gap-2">
              {MONTHS.map((m) => (
                <button
                  key={m}
                  type="button"
                  onClick={() => setActiveMonths((prev) => toggleIn(prev, m))}
                  className={`rounded-lg border px-2.5 py-1.5 text-xs font-bold uppercase tracking-wide transition-colors ${
                    activeMonths.includes(m)
                      ? "border-primary bg-primary/10 text-primary"
                      : "border-outline-variant/30 text-on-surface-variant hover:border-primary/40"
                  }`}
                >
                  {m}
                </button>
              ))}
            </div>
          </div>
        </section>

        <section className={sectionCard}>
          <h3 className={sectionTitle}>Purchase qualifiers</h3>
          <label className="mb-4 flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none">
            <input
              type="checkbox"
              className="mt-0.5 h-4 w-4 accent-primary shrink-0"
              checked={inclusionEnabled}
              onChange={(e) => setInclusionEnabled(e.target.checked)}
            />
            <div>
              <p className="text-xs font-bold text-on-surface">Enable inclusion qualifier</p>
              <p className="mt-0.5 text-xs text-on-surface-variant leading-relaxed">Cart must match selected brands, categories, PIDs, and/or products.</p>
            </div>
          </label>
          {inclusionEnabled && (
            <div className="space-y-5 border-t border-outline-variant/10 pt-5">
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <SearchablePicker label="Brands" options={MOCK_BRANDS} selected={incBrands} onChange={setIncBrands} />
                <SearchablePicker label="Categories" options={MOCK_CATEGORIES} selected={incCategories} onChange={setIncCategories} />
                <SearchablePicker label="PID1" options={MOCK_PID1} selected={incPid1} onChange={setIncPid1} />
                <SearchablePicker label="PID2" options={MOCK_PID2} selected={incPid2} onChange={setIncPid2} />
                <div className="md:col-span-2">
                  <SearchablePicker label="Products" options={MOCK_PRODUCTS} selected={incProducts} onChange={setIncProducts} />
                </div>
              </div>
              <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 p-3 hover:bg-surface-container-low/80 transition-colors select-none">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                  checked={cartAllQualifiers}
                  onChange={(e) => setCartAllQualifiers(e.target.checked)}
                />
                <span className="text-xs text-on-surface">Cart should meet all selected product qualifiers</span>
              </label>
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className={labelBase}>Minimum spend on qualifying items</label>
                  <div className="flex">
                    <span className="flex shrink-0 items-center rounded-l-md border border-r-0 border-outline-variant/20 bg-surface-container-high px-3 text-xs font-bold text-on-surface-variant">
                      $
                    </span>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      className={`${inputBase} rounded-l-none`}
                      placeholder="0.00"
                      value={minSpend}
                      onChange={(e) => setMinSpend(e.target.value)}
                    />
                  </div>
                </div>
                <label className="flex cursor-pointer items-start gap-3 self-end rounded-lg border border-outline-variant/15 p-3 hover:bg-surface-container-low/80 transition-colors select-none md:mb-1">
                  <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={minSpendNet} onChange={(e) => setMinSpendNet(e.target.checked)} />
                  <span className="text-xs text-on-surface">Apply minimum spend based on net amount</span>
                </label>
              </div>
            </div>
          )}

          <label className="mt-6 flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none">
            <input
              type="checkbox"
              className="mt-0.5 h-4 w-4 accent-primary shrink-0"
              checked={exclusionEnabled}
              onChange={(e) => setExclusionEnabled(e.target.checked)}
            />
            <div>
              <p className="text-xs font-bold text-on-surface">Enable exclusion qualifier</p>
              <p className="mt-0.5 text-xs text-on-surface-variant leading-relaxed">Exclude carts matching brands, categories, PIDs, products, or membership rules.</p>
            </div>
          </label>
          {exclusionEnabled && (
            <div className="mt-4 space-y-5 border-t border-outline-variant/10 pt-5">
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <SearchablePicker label="Exclude brands" options={MOCK_BRANDS} selected={excBrands} onChange={setExcBrands} />
                <SearchablePicker label="Exclude categories" options={MOCK_CATEGORIES} selected={excCategories} onChange={setExcCategories} />
                <SearchablePicker label="Exclude PID1" options={MOCK_PID1} selected={excPid1} onChange={setExcPid1} />
                <SearchablePicker label="Exclude PID2" options={MOCK_PID2} selected={excPid2} onChange={setExcPid2} />
                <div className="md:col-span-2">
                  <SearchablePicker label="Exclude products" options={MOCK_PRODUCTS} selected={excProducts} onChange={setExcProducts} />
                </div>
                <SearchablePicker
                  label="Membership types"
                  options={MOCK_MEMBERSHIP_TYPES}
                  selected={excMembershipTypes}
                  onChange={setExcMembershipTypes}
                />
                <SearchablePicker label="Membership tiers" options={MOCK_MEMBERSHIP_TIERS} selected={excMembershipTiers} onChange={setExcMembershipTiers} />
              </div>
            </div>
          )}
        </section>

        <section className={sectionCard}>
          <h3 className={sectionTitle}>
            {campaignKind === "pwp-discount"
              ? "Promotion mechanics (PWP-Discount)"
              : campaignKind === "mix-match"
                ? "Promotion mechanics (Mix and Match)"
                : campaignKind === "product-discount"
                  ? "Promotion mechanics (Product Discount)"
                  : campaignKind === "after-sales"
                    ? "Promotion mechanics (After Sales Promotion)"
                    : "Promotion mechanics (PWP-Item)"}
          </h3>
          {campaignKind === "pwp-discount" ? (
            <PwpDiscountMechanicsSection value={discountMechanics} onChange={setDiscountMechanics} />
          ) : campaignKind === "mix-match" ? (
            <MixMatchMechanicsSection value={mixMatchMechanics} onChange={setMixMatchMechanics} />
          ) : campaignKind === "product-discount" ? (
            <ProductDiscountMechanicsSection value={productDiscountMechanics} onChange={setProductDiscountMechanics} />
          ) : campaignKind === "after-sales" ? (
            <AfterSalesMechanicsSection value={afterSalesMechanics} onChange={setAfterSalesMechanics} />
          ) : (
            <PwpItemMechanicsSection value={itemMechanics} onChange={setItemMechanics} />
          )}
        </section>

        <section className={sectionCard}>
          <h3 className={sectionTitle}>Promotion details</h3>
          <div className="space-y-5">
            <div>
              <label className={labelBase}>Order detail message</label>
              <input
                type="text"
                className={inputBase}
                placeholder="Short line shown on order confirmation"
                value={orderDetailMessage}
                onChange={(e) => setOrderDetailMessage(e.target.value)}
              />
            </div>
            <div>
              <label className={labelBase}>Promotion details</label>
              <textarea
                className={`${inputBase} min-h-[100px] resize-y`}
                placeholder="Longer description for internal or customer-facing use…"
                value={promotionDetails}
                onChange={(e) => setPromotionDetails(e.target.value)}
              />
            </div>
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none">
              <input
                type="checkbox"
                className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                checked={showPromoOnProductPage}
                onChange={(e) => setShowPromoOnProductPage(e.target.checked)}
              />
              <span className="text-xs font-bold text-on-surface">Show promotion details on product page</span>
            </label>
            <div>
              <label className={labelBase}>Promotion message priority</label>
              <input
                type="number"
                min="0"
                step="1"
                className={inputBase}
                placeholder="e.g. 10"
                value={promotionMessagePriority}
                onChange={(e) => setPromotionMessagePriority(e.target.value)}
              />
              <p className={hintText}>Higher runs first when multiple messages apply.</p>
            </div>
          </div>
        </section>

        <section className={sectionCard}>
          <h3 className={sectionTitle}>Advanced settings</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className={labelBase}>Campaign priority</label>
              <input
                type="number"
                className={inputBase}
                placeholder="e.g. 100"
                value={campaignPriority}
                onChange={(e) => setCampaignPriority(e.target.value)}
              />
            </div>
            <div className="hidden md:block" />
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 p-3 md:col-span-2 hover:bg-surface-container-low/80 transition-colors select-none">
              <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={blockOtherPromos} onChange={(e) => setBlockOtherPromos(e.target.checked)} />
              <span className="text-xs text-on-surface">Block other promotions when this campaign applies</span>
            </label>
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 p-3 md:col-span-2 hover:bg-surface-container-low/80 transition-colors select-none">
              <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={blockSamePriority} onChange={(e) => setBlockSamePriority(e.target.checked)} />
              <span className="text-xs text-on-surface">Block promotions with the same campaign priority</span>
            </label>
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 p-3 md:col-span-2 hover:bg-surface-container-low/80 transition-colors select-none">
              <input
                type="checkbox"
                className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                checked={activationCodeEnabled}
                onChange={(e) => setActivationCodeEnabled(e.target.checked)}
              />
              <span className="text-xs text-on-surface">Enable campaign activation code</span>
            </label>
            {activationCodeEnabled && (
              <div className="md:col-span-2">
                <label className={labelBase}>Campaign activation code</label>
                <input type="text" className={inputBase} placeholder="Customer-facing code" value={activationCode} onChange={(e) => setActivationCode(e.target.value)} />
              </div>
            )}
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 p-3 md:col-span-2 hover:bg-surface-container-low/80 transition-colors select-none">
              <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={applyOnEStore} onChange={(e) => setApplyOnEStore(e.target.checked)} />
              <span className="text-xs text-on-surface">Apply campaign on eStore</span>
            </label>
          </div>
        </section>
      </div>

      <CampaignChangeHistoryPanel
        open={historyOpen}
        onClose={() => setHistoryOpen(false)}
        campaignCode={(campaign?.code ?? code) || "—"}
        campaignName={(campaign?.name ?? name) || undefined}
        entries={historyEntries}
      />

      {toast && (
        <div
          className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl"
          role="status"
          aria-live="polite"
        >
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
