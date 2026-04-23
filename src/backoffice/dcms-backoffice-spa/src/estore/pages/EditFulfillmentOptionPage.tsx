import { useEffect, useMemo, useRef, useState } from "react";
import {
  IconAddCircle,
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconDelete,
  IconEdit,
  IconInfo,
  IconSearch,
} from "../../orders/icons";
import type {
  CollectionLocation,
  FulfillmentDeliveryMode,
  FulfillmentDynamicField,
  FulfillmentGrouping,
  FulfillmentPredefinedFieldKey,
  FulfillmentPredefinedFieldSetting,
  LogisticPartner,
  StockLocation,
} from "../EStoreApp";

// ── Style tokens ─────────────────────────────────────────────────────────────
const sectionCard = "rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm";
const sectionTitle = "mb-5 text-sm font-bold uppercase tracking-widest text-on-surface";
const labelBase = "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase = "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-[10px] text-on-surface-variant";

// ── Mock options ─────────────────────────────────────────────────────────────
const MOCK_TENANTS = ["Tenant A", "Tenant B", "Tenant C"];
const DELIVERY_MODES: FulfillmentDeliveryMode[] = ["Store Collection", "Local Delivery", "Overseas Delivery"];
const SELECTION_UNITS = ["days", "months", "years"] as const;
const MOCK_LOGISTIC_PARTNERS = ["DHL", "FedEx", "Ninja Van", "J&T Express", "Self Pickup"];
const MOCK_CATEGORIES = ["Electronics", "Furniture", "Audio", "Footwear", "Watches", "Home & Living"];
const MOCK_BRANDS = ["Cronos Ltd.", "SoundWave Co.", "Apex Footwear", "NovaCam", "FurniCraft"];

// ── Searchable multi-select picker ────────────────────────────────────────────
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
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((s) => (
            <span
              key={s}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
            >
              {s}
              <button
                type="button"
                onClick={() => toggle(s)}
                className="rounded p-0.5 hover:bg-primary/20 transition-colors"
              >
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
              <p className="px-3 py-3 text-center text-[10px] text-on-surface-variant">No results</p>
            ) : (
              filtered.map((opt) => (
                <label
                  key={opt}
                  className="flex cursor-pointer items-center gap-2.5 px-3 py-2 hover:bg-surface-container transition-colors select-none"
                >
                  <input type="checkbox" className="h-3.5 w-3.5 accent-primary" checked={selected.includes(opt)} onChange={() => toggle(opt)} />
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

type Props = {
  grouping?: FulfillmentGrouping;
  slot?: import("../EStoreApp").FulfillmentSlot;
  collectionLocations: CollectionLocation[];
  stockLocations: StockLocation[];
  dynamicFields?: FulfillmentDynamicField[];
  predefinedFieldSettings?: FulfillmentPredefinedFieldSetting[];
  logisticPartners?: LogisticPartner[];
  onSave?: (slot: import("../EStoreApp").FulfillmentSlot) => void;
  onBack: () => void;
};

type BlockOff = { id: string; start: string; end: string };

function seedId(prefix: string) {
  return `${prefix}-${Math.random().toString(36).slice(2, 8)}`;
}

export function EditFulfillmentOptionPage({
  grouping,
  slot,
  collectionLocations,
  stockLocations,
  dynamicFields = [],
  predefinedFieldSettings = [],
  logisticPartners = [],
  onSave,
  onBack,
}: Props) {
  const tenantVisible = grouping?.tenantEnabled ?? false;

  const predefinedEnabled = useMemo(() => {
    const map = new Map<FulfillmentPredefinedFieldKey, boolean>();
    for (const f of predefinedFieldSettings) map.set(f.key, f.enabled);
    return (key: FulfillmentPredefinedFieldKey) => map.get(key) ?? true;
  }, [predefinedFieldSettings]);

  // ── General Information ─────────────────────────────────────────────────
  const [name, setName] = useState(slot?.name ?? (grouping ? `${grouping.groupName} (Slot)` : ""));
  const [code, setCode] = useState(slot?.code ?? "");
  const [tenant, setTenant] = useState(MOCK_TENANTS[0]);
  const [mode, setMode] = useState<FulfillmentDeliveryMode>(slot?.mode ?? grouping?.deliveryMode ?? "Local Delivery");
  const [startingDate, setStartingDate] = useState(slot?.startingDate ?? "");
  const [endingDate, setEndingDate] = useState(slot?.endingDate ?? "");
  const [selectionLimitValue, setSelectionLimitValue] = useState<number>(7);
  const [selectionLimitUnit, setSelectionLimitUnit] = useState<(typeof SELECTION_UNITS)[number]>("days");
  const [price, setPrice] = useState(slot?.price ?? "");

  // ── Qualifier ────────────────────────────────────────────────────────────
  const [freeDeliveryMinSpend, setFreeDeliveryMinSpend] = useState("");
  const [includedCategories, setIncludedCategories] = useState<string[]>([]);
  const [includedBrands, setIncludedBrands] = useState<string[]>([]);
  const [includedProducts, setIncludedProducts] = useState<string[]>([]);
  const [excludedCategories, setExcludedCategories] = useState<string[]>([]);
  const [excludedBrands, setExcludedBrands] = useState<string[]>([]);
  const [excludedProducts, setExcludedProducts] = useState<string[]>([]);
  const [draftProduct, setDraftProduct] = useState("");

  // ── Fulfillment Settings ────────────────────────────────────────────────
  const [disabledDays, setDisabledDays] = useState<string[]>([]);
  const [blockedDates, setBlockedDates] = useState<string[]>([]);
  const [blockedDateDraft, setBlockedDateDraft] = useState("");
  const [leadTimeHours, setLeadTimeHours] = useState<number>(6);
  const [slotsPerDay, setSlotsPerDay] = useState<number>(50);
  const activeLocations = useMemo(() => collectionLocations.filter((l) => l.active), [collectionLocations]);
  const [collectionLocationId, setCollectionLocationId] = useState(activeLocations[0]?.id ?? "");
  const selectedLocation = useMemo(
    () => activeLocations.find((l) => l.id === collectionLocationId) ?? null,
    [activeLocations, collectionLocationId]
  );
  const [openingHours, setOpeningHours] = useState(selectedLocation?.openingHours ?? "10:00");
  const [closingHours, setClosingHours] = useState(selectedLocation?.closingHours ?? "22:00");
  const [blockOffs, setBlockOffs] = useState<BlockOff[]>([]);
  const [blockOffStart, setBlockOffStart] = useState("");
  const [blockOffEnd, setBlockOffEnd] = useState("");

  // ── Dynamic field values (keyed by property) ────────────────────────────
  const [dynamicValues, setDynamicValues] = useState<Record<string, string>>({});

  function setDynamicValue(property: string, value: string) {
    setDynamicValues((prev) => ({ ...prev, [property]: value }));
  }

  const additionalInfoFields = useMemo(
    () => dynamicFields.filter((f) => f.enabled && f.section === "Additional Info"),
    [dynamicFields]
  );

  // ── Advanced Settings ───────────────────────────────────────────────────
  const [pickFromStockLocation, setPickFromStockLocation] = useState(grouping?.limitSelectedDistributionCenter ?? false);
  const [advancedStockLocation, setAdvancedStockLocation] = useState(grouping?.stockLocation ?? "");
  const [sequence, setSequence] = useState<number>(1);
  const [blockOther, setBlockOther] = useState(false);
  const [enableDateSelection, setEnableDateSelection] = useState(true);
  const enabledPartners = useMemo(() => logisticPartners.filter((p) => p.enabled), [logisticPartners]);
  const [logisticPartner, setLogisticPartner] = useState<string>(enabledPartners[0]?.name ?? "—");
  const [notificationEmails, setNotificationEmails] = useState("");

  // Keep address/hours in sync when location changes (auto-populate)
  useEffect(() => {
    if (!selectedLocation) return;
    setOpeningHours(selectedLocation.openingHours ?? openingHours);
    setClosingHours(selectedLocation.closingHours ?? closingHours);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedLocation?.id]);

  const selectedAddress = useMemo(() => {
    if (!selectedLocation) return "";
    const lines = [selectedLocation.address1, selectedLocation.address2, selectedLocation.address3].filter(
      (x): x is string => Boolean((x ?? "").trim())
    );
    const tail = [selectedLocation.postalCode, selectedLocation.country].filter((x): x is string => Boolean((x ?? "").trim()));
    return [...lines, tail.join(" ")].filter((x) => x.trim()).join(", ");
  }, [selectedLocation]);

  // ── Actions dropdown + toast ────────────────────────────────────────────
  const [actionsOpen, setActionsOpen] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node)) setActionsOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  const [historyOpen, setHistoryOpen] = useState(false);

  function handleSaveApprove() {
    setActionsOpen(false);
    setToast("Fulfillment option saved and approved.");
    if (grouping && onSave) {
      onSave({
        id: slot?.id ?? `slot-${Math.random().toString(36).slice(2, 8)}`,
        groupingId: grouping.id,
        name: name.trim() || "Delivery Slot",
        code: (code.trim() || "DELIVERY_SLOT").toUpperCase(),
        mode,
        startingDate,
        endingDate,
        price: price || "0.00",
        updatedAt: new Date().toISOString().slice(0, 16).replace("T", " "),
      });
    }
    setTimeout(() => onBack(), 1800);
  }
  function handleSaveChanges() {
    setActionsOpen(false);
    setToast("Fulfillment option saved.");
    if (grouping && onSave) {
      onSave({
        id: slot?.id ?? `slot-${Math.random().toString(36).slice(2, 8)}`,
        groupingId: grouping.id,
        name: name.trim() || "Delivery Slot",
        code: (code.trim() || "DELIVERY_SLOT").toUpperCase(),
        mode,
        startingDate,
        endingDate,
        price: price || "0.00",
        updatedAt: new Date().toISOString().slice(0, 16).replace("T", " "),
      });
    }
  }
  function handleChangeHistory() {
    setActionsOpen(false);
    setHistoryOpen(true);
  }

  function addProductTag(kind: "include" | "exclude") {
    const v = draftProduct.trim();
    if (!v) return;
    if (kind === "include") setIncludedProducts((prev) => (prev.includes(v) ? prev : [...prev, v]));
    else setExcludedProducts((prev) => (prev.includes(v) ? prev : [...prev, v]));
    setDraftProduct("");
  }

  function toggleDay(day: string) {
    setDisabledDays((prev) => (prev.includes(day) ? prev.filter((d) => d !== day) : [...prev, day]));
  }

  function addBlockedDate() {
    if (!blockedDateDraft) return;
    setBlockedDates((prev) => (prev.includes(blockedDateDraft) ? prev : [...prev, blockedDateDraft]));
    setBlockedDateDraft("");
  }

  function addBlockOff() {
    if (!blockOffStart || !blockOffEnd) return;
    setBlockOffs((prev) => [...prev, { id: seedId("bo"), start: blockOffStart, end: blockOffEnd }]);
    setBlockOffStart("");
    setBlockOffEnd("");
  }

  const title = grouping ? "Fulfillment Options Management" : "Add Fulfillment Option";

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      {/* Top bar */}
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" /> Back to Fulfillment Options
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">{title}</h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            {grouping ? (
              <>
                Group:{" "}
                <code className="rounded bg-surface-container-high px-1.5 py-0.5 text-xs font-mono font-bold">
                  {grouping.code}
                </code>
                <span className="ml-2 text-xs text-on-surface-variant">{grouping.deliveryMode}</span>
              </>
            ) : (
              "Configure delivery slots and qualifiers."
            )}
          </p>
        </div>

        {/* Actions dropdown */}
        <div className="relative" ref={actionsRef}>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90"
            onClick={() => setActionsOpen((o) => !o)}
          >
            Actions
            <IconChevronDown className={`h-3.5 w-3.5 shrink-0 transition-transform ${actionsOpen ? "rotate-180" : ""}`} />
          </button>
          {actionsOpen && (
            <div className="absolute right-0 top-full z-20 mt-1 w-52 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={handleSaveApprove}
              >
                <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
                Save and Approve
              </button>
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={handleSaveChanges}
              >
                <IconEdit className="h-4 w-4 shrink-0 text-on-surface-variant" />
                Save Changes
              </button>
              <div className="my-1 border-t border-outline-variant/10" />
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={handleChangeHistory}
              >
                <IconInfo className="h-4 w-4 shrink-0 text-on-surface-variant" />
                Show Change History
              </button>
            </div>
          )}
        </div>
      </div>

      <div className="flex-1 space-y-6 p-6 pb-24">
        {/* General Information */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>General Information</h3>
          <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
            <div>
              <label className={labelBase}>Name <span className="text-error">*</span></label>
              <input className={inputBase} value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. 10am – 4pm" />
            </div>
            <div>
              <label className={labelBase}>Code</label>
              <input className={inputBase} value={code} onChange={(e) => setCode(e.target.value)} placeholder="e.g. SLOT_10_16" />
            </div>
            {tenantVisible ? (
              <div>
                <label className={labelBase}>Tenant</label>
                <select className={inputBase} value={tenant} onChange={(e) => setTenant(e.target.value)}>
                  {MOCK_TENANTS.map((t) => (
                    <option key={t} value={t}>{t}</option>
                  ))}
                </select>
                <p className={hintText}>Tenant field appears because Tenant is enabled on the group.</p>
              </div>
            ) : (
              <div>
                <label className={labelBase}>Mode</label>
                <select className={inputBase} value={mode} onChange={(e) => setMode(e.target.value as FulfillmentDeliveryMode)}>
                  {DELIVERY_MODES.map((m) => (
                    <option key={m} value={m}>{m}</option>
                  ))}
                </select>
              </div>
            )}
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-3">
            <div>
              <label className={labelBase}>Starting Date</label>
              <input type="datetime-local" className={inputBase} value={startingDate} onChange={(e) => setStartingDate(e.target.value)} />
            </div>
            <div>
              <label className={labelBase}>Ending Date</label>
              <input type="datetime-local" className={inputBase} value={endingDate} onChange={(e) => setEndingDate(e.target.value)} />
            </div>
            <div>
              <label className={labelBase}>Selection Limit</label>
              <div className="grid grid-cols-2 gap-3">
                <input type="number" min="1" className={inputBase} value={selectionLimitValue} onChange={(e) => setSelectionLimitValue(Number(e.target.value))} />
                <select className={inputBase} value={selectionLimitUnit} onChange={(e) => setSelectionLimitUnit(e.target.value as (typeof SELECTION_UNITS)[number])}>
                  {SELECTION_UNITS.map((u) => <option key={u} value={u}>{u}</option>)}
                </select>
              </div>
              <p className={hintText}>Limit how far ahead customers can select a delivery date.</p>
            </div>
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
            <div>
              <label className={labelBase}>Mode</label>
              <select className={inputBase} value={mode} onChange={(e) => setMode(e.target.value as FulfillmentDeliveryMode)}>
                {DELIVERY_MODES.map((m) => (
                  <option key={m} value={m}>{m}</option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelBase}>Price</label>
              <div className="flex items-center overflow-hidden rounded-md border border-outline-variant/20 focus-within:ring-1 focus-within:ring-primary">
                <span className="shrink-0 bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant">$</span>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  className="flex-1 bg-surface-container-lowest px-3 py-2 text-xs outline-none"
                  value={price}
                  onChange={(e) => setPrice(e.target.value)}
                  placeholder="0.00"
                />
              </div>
              <p className={hintText}>Fixed charge applied during checkout.</p>
            </div>
          </div>
        </section>

        {/* Qualifier */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Qualifier</h3>
          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            {predefinedEnabled("freeDeliveryMinSpend") ? (
            <div>
              <label className={labelBase}>Free Delivery Minimum Spend</label>
              <div className="flex items-center overflow-hidden rounded-md border border-outline-variant/20 focus-within:ring-1 focus-within:ring-primary">
                <span className="shrink-0 bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant">$</span>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  className="flex-1 bg-surface-container-lowest px-3 py-2 text-xs outline-none"
                  value={freeDeliveryMinSpend}
                  onChange={(e) => setFreeDeliveryMinSpend(e.target.value)}
                  placeholder="0.00"
                />
              </div>
              <p className={hintText}>If met, delivery charge will be zero.</p>
            </div>
            ) : (
              <div />
            )}
            <div />
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
            {predefinedEnabled("includedCategories") ? (
            <div>
              <label className={labelBase}>Included Categories</label>
              <SearchablePicker label="Categories" options={MOCK_CATEGORIES} selected={includedCategories} onChange={setIncludedCategories} />
            </div>
            ) : (
              <div />
            )}
            {predefinedEnabled("includedBrands") ? (
            <div>
              <label className={labelBase}>Included Brands</label>
              <SearchablePicker label="Brands" options={MOCK_BRANDS} selected={includedBrands} onChange={setIncludedBrands} />
            </div>
            ) : (
              <div />
            )}
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
            {predefinedEnabled("excludedCategories") ? (
            <div>
              <label className={labelBase}>Excluded Categories</label>
              <SearchablePicker label="Categories" options={MOCK_CATEGORIES} selected={excludedCategories} onChange={setExcludedCategories} />
            </div>
            ) : (
              <div />
            )}
            {predefinedEnabled("excludedBrands") ? (
            <div>
              <label className={labelBase}>Excluded Brands</label>
              <SearchablePicker label="Brands" options={MOCK_BRANDS} selected={excludedBrands} onChange={setExcludedBrands} />
            </div>
            ) : (
              <div />
            )}
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
            {predefinedEnabled("includedProducts") ? (
            <div>
              <label className={labelBase}>Included Products</label>
              <div className="flex items-center gap-2">
                <input className={inputBase} value={draftProduct} onChange={(e) => setDraftProduct(e.target.value)} placeholder="Type product name / UPC…" />
                <button type="button" className="rounded-md bg-primary px-4 py-2 text-xs font-bold text-on-primary hover:opacity-90 transition-opacity" onClick={() => addProductTag("include")}>
                  <IconAddCircle className="h-4 w-4" />
                </button>
              </div>
              {includedProducts.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {includedProducts.map((p) => (
                    <span key={p} className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary">
                      {p}
                      <button type="button" onClick={() => setIncludedProducts((prev) => prev.filter((x) => x !== p))} className="rounded p-0.5 hover:bg-primary/20 transition-colors">
                        <IconClose className="h-2.5 w-2.5" />
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>
            ) : (
              <div />
            )}
            {predefinedEnabled("excludedProducts") ? (
            <div>
              <label className={labelBase}>Excluded Products</label>
              <div className="flex items-center gap-2">
                <input className={inputBase} value={draftProduct} onChange={(e) => setDraftProduct(e.target.value)} placeholder="Type product name / UPC…" />
                <button type="button" className="rounded-md bg-primary px-4 py-2 text-xs font-bold text-on-primary hover:opacity-90 transition-opacity" onClick={() => addProductTag("exclude")}>
                  <IconAddCircle className="h-4 w-4" />
                </button>
              </div>
              {excludedProducts.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {excludedProducts.map((p) => (
                    <span key={p} className="inline-flex items-center gap-1 rounded-full bg-surface-container-high px-2.5 py-0.5 text-[10px] font-bold uppercase text-on-surface-variant">
                      {p}
                      <button type="button" onClick={() => setExcludedProducts((prev) => prev.filter((x) => x !== p))} className="rounded p-0.5 hover:bg-surface-container transition-colors">
                        <IconClose className="h-2.5 w-2.5" />
                      </button>
                    </span>
                  ))}
                </div>
              )}
              <p className={hintText}>Enter product name / UPC to exclude this delivery option.</p>
            </div>
            ) : (
              <div />
            )}
          </div>
        </section>

        {/* Fulfillment Settings */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Fulfillment Settings</h3>
          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            <div>
              <label className={labelBase}>Disabled Days</label>
              <div className="flex flex-wrap gap-2">
                {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => (
                  <label key={d} className="flex items-center gap-2 rounded-full bg-surface-container-high px-3 py-1 text-xs text-on-surface-variant cursor-pointer select-none">
                    <input
                      type="checkbox"
                      className="h-3.5 w-3.5 accent-primary"
                      checked={disabledDays.includes(d)}
                      onChange={() => toggleDay(d)}
                    />
                    {d}
                  </label>
                ))}
              </div>
            </div>
            {predefinedEnabled("blockedDates") ? (
            <div>
              <label className={labelBase}>Blocked Dates</label>
              <div className="flex items-center gap-2">
                <input type="date" className={inputBase} value={blockedDateDraft} onChange={(e) => setBlockedDateDraft(e.target.value)} />
                <button type="button" className="rounded-md bg-primary px-4 py-2 text-xs font-bold text-on-primary hover:opacity-90 transition-opacity" onClick={addBlockedDate}>
                  Add
                </button>
              </div>
              {blockedDates.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {blockedDates.map((d) => (
                    <span key={d} className="inline-flex items-center gap-1 rounded-full bg-surface-container-high px-2.5 py-0.5 text-[10px] font-bold uppercase text-on-surface-variant">
                      {d}
                      <button type="button" onClick={() => setBlockedDates((prev) => prev.filter((x) => x !== d))} className="rounded p-0.5 hover:bg-surface-container transition-colors">
                        <IconClose className="h-2.5 w-2.5" />
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>
            ) : (
              <div />
            )}
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-3">
            <div>
              <label className={labelBase}>Lead Time For Delivery Slot (Hours)</label>
              <input type="number" min="0" className={inputBase} value={leadTimeHours} onChange={(e) => setLeadTimeHours(Number(e.target.value))} />
            </div>
            {predefinedEnabled("numberOfDeliverySlots") ? (
            <div>
              <label className={labelBase}>Number of Delivery Slots</label>
              <input type="number" min="0" className={inputBase} value={slotsPerDay} onChange={(e) => setSlotsPerDay(Number(e.target.value))} />
            </div>
            ) : (
              <div />
            )}
            {predefinedEnabled("collectionLocations") ? (
            <div>
              <label className={labelBase}>Collection Location</label>
              <select className={inputBase} value={collectionLocationId} onChange={(e) => setCollectionLocationId(e.target.value)}>
                <option value="">Select location…</option>
                {activeLocations.map((l) => (
                  <option key={l.id} value={l.id}>
                    {l.name}
                  </option>
                ))}
              </select>
              <p className={hintText}>Locations are configured in Collection Location Manager.</p>
            </div>
            ) : (
              <div />
            )}
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-3">
            {predefinedEnabled("collectionLocations") ? (
              <div className="md:col-span-2">
                <label className={labelBase}>Address</label>
                <input className={`${inputBase} bg-surface-container-high`} value={selectedAddress} readOnly placeholder="Auto-populated" />
                <p className={hintText}>Address is auto-populated once a Collection Location is selected.</p>
              </div>
            ) : (
              <div className="md:col-span-2" />
            )}
            {predefinedEnabled("openingAndClosingHours") ? (
              <div>
                <label className={labelBase}>Opening / Closing Hours</label>
                <div className="grid grid-cols-2 gap-3">
                  <input type="time" className={inputBase} value={openingHours} onChange={(e) => setOpeningHours(e.target.value)} />
                  <input type="time" className={inputBase} value={closingHours} onChange={(e) => setClosingHours(e.target.value)} />
                </div>
              </div>
            ) : (
              <div />
            )}
          </div>

          <div className="mt-6">
            {predefinedEnabled("blockOffDatesAndTimes") && (
              <>
                <label className={labelBase}>Block Off Date and Times</label>
                <p className={hintText}>Block-off entries cannot be edited; delete and create a new one.</p>
                <div className="mt-2 grid grid-cols-1 gap-4 md:grid-cols-3">
                  <div>
                    <p className="mb-1 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">Start</p>
                    <input type="datetime-local" className={inputBase} value={blockOffStart} onChange={(e) => setBlockOffStart(e.target.value)} />
                  </div>
                  <div>
                    <p className="mb-1 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">End</p>
                    <input type="datetime-local" className={inputBase} value={blockOffEnd} onChange={(e) => setBlockOffEnd(e.target.value)} />
                  </div>
                  <div className="flex items-end">
                    <button type="button" className="w-full rounded-md bg-primary px-4 py-2.5 text-xs font-bold text-on-primary hover:opacity-90 transition-opacity" onClick={addBlockOff}>
                      Add Block Off
                    </button>
                  </div>
                </div>

                {blockOffs.length > 0 && (
                  <div className="mt-4 overflow-x-auto rounded-lg border border-outline-variant/10">
                    <table className="w-full border-collapse text-left">
                      <thead>
                        <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                          <th className="px-4 py-2.5">Start</th>
                          <th className="px-4 py-2.5">End</th>
                          <th className="px-4 py-2.5 text-right">Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {blockOffs.map((b) => (
                          <tr key={b.id} className="text-xs hover:bg-surface-container-low transition-colors">
                            <td className="px-4 py-2.5 font-mono text-[11px] text-on-surface-variant">{b.start.replace("T", " ")}</td>
                            <td className="px-4 py-2.5 font-mono text-[11px] text-on-surface-variant">{b.end.replace("T", " ")}</td>
                            <td className="px-4 py-2.5 text-right">
                              <button
                                type="button"
                                className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                                onClick={() => setBlockOffs((prev) => prev.filter((x) => x.id !== b.id))}
                              >
                                <IconDelete className="h-4 w-4" />
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </>
            )}
          </div>
        </section>

        {/* Additional Info */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Additional Info</h3>
          {additionalInfoFields.length === 0 ? (
            <div className="flex items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4">
              <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-on-surface-variant" />
              <p className="text-xs text-on-surface-variant leading-relaxed">
                Dynamic fields will appear here once configured in Fulfillment Options Configuration.
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
              {additionalInfoFields.map((f) => (
                <div key={f.id} className={f.fieldType === "WYSIWYG" ? "md:col-span-2" : ""}>
                  <label className={labelBase}>
                    {f.columnLabel}
                    {f.required && <span className="ml-1 text-error">*</span>}
                  </label>
                  {f.fieldType === "Text Box" && (
                    <input
                      className={inputBase}
                      value={dynamicValues[f.property] ?? ""}
                      onChange={(e) => setDynamicValue(f.property, e.target.value)}
                      placeholder={f.fieldName}
                    />
                  )}
                  {f.fieldType === "WYSIWYG" && (
                    <textarea
                      rows={4}
                      className={inputBase + " resize-y"}
                      value={dynamicValues[f.property] ?? ""}
                      onChange={(e) => setDynamicValue(f.property, e.target.value)}
                      placeholder={f.fieldName}
                    />
                  )}
                  {f.fieldType === "Checkbox" && (
                    <label className="flex cursor-pointer items-center gap-2 select-none mt-1">
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-primary"
                        checked={dynamicValues[f.property] === "true"}
                        onChange={(e) => setDynamicValue(f.property, e.target.checked ? "true" : "false")}
                      />
                      <span className="text-xs text-on-surface">{f.columnLabel}</span>
                    </label>
                  )}
                  {f.fieldType === "Date Picker" && (
                    <input
                      type="date"
                      className={inputBase}
                      value={dynamicValues[f.property] ?? ""}
                      onChange={(e) => setDynamicValue(f.property, e.target.value)}
                    />
                  )}
                  {f.fieldType === "Geo Location" && (
                    <div className="flex gap-2">
                      <input
                        className={inputBase}
                        placeholder="Latitude"
                        value={(dynamicValues[f.property + "_lat"]) ?? ""}
                        onChange={(e) => setDynamicValue(f.property + "_lat", e.target.value)}
                      />
                      <input
                        className={inputBase}
                        placeholder="Longitude"
                        value={(dynamicValues[f.property + "_lng"]) ?? ""}
                        onChange={(e) => setDynamicValue(f.property + "_lng", e.target.value)}
                      />
                    </div>
                  )}
                  {(f.fieldType === "Dropdown List" || f.fieldType === "Multiple Select") && (
                    <select
                      className={inputBase}
                      value={dynamicValues[f.property] ?? ""}
                      onChange={(e) => setDynamicValue(f.property, e.target.value)}
                    >
                      <option value="">— Select —</option>
                    </select>
                  )}
                  <p className={hintText}>{f.fieldName && `Field: ${f.fieldName}`}</p>
                </div>
              ))}
            </div>
          )}
        </section>

        {/* Advanced Settings */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Advanced Settings</h3>
          <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
              <input
                type="checkbox"
                className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                checked={pickFromStockLocation}
                onChange={(e) => setPickFromStockLocation(e.target.checked)}
              />
              <div>
                <p className="text-xs font-bold text-on-surface">Pick stocks from selected Stock Location</p>
                <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                  Issue stocks from a specific stock location.
                </p>
              </div>
            </label>
            <div>
              <label className={labelBase}>Stock Location</label>
              <select
                className={inputBase}
                value={advancedStockLocation}
                onChange={(e) => setAdvancedStockLocation(e.target.value)}
                disabled={!pickFromStockLocation}
              >
                <option value="">Select stock location…</option>
                {stockLocations.filter((s) => s.active).map((s) => (
                  <option key={s.id} value={s.code}>
                    {s.name} ({s.code})
                  </option>
                ))}
              </select>
              <p className={hintText}>Shown when “Pick stocks…” is enabled.</p>
            </div>
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-3">
            <div>
              <label className={labelBase}>Sequence</label>
              <input type="number" min="1" className={inputBase} value={sequence} onChange={(e) => setSequence(Number(e.target.value))} />
            </div>
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
              <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={blockOther} onChange={(e) => setBlockOther(e.target.checked)} />
              <div>
                <p className="text-xs font-bold text-on-surface">Block Other Delivery Options</p>
                <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                  Remove other delivery options if this option is available.
                </p>
              </div>
            </label>
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
              <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={enableDateSelection} onChange={(e) => setEnableDateSelection(e.target.checked)} />
              <div>
                <p className="text-xs font-bold text-on-surface">Enable Date Selection and Delivery Slots</p>
                <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                  Customers can choose a delivery date and timeslot at checkout.
                </p>
              </div>
            </label>
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
            {predefinedEnabled("logisticPartner") ? (
              <div>
                <label className={labelBase}>Logistic Partner</label>
              <select className={inputBase} value={logisticPartner} onChange={(e) => setLogisticPartner(e.target.value)}>
                <option value="—">—</option>
                {enabledPartners.map((p) => (
                  <option key={p.id} value={p.name}>
                    {p.name}
                  </option>
                ))}
              </select>
              </div>
            ) : (
              <div />
            )}
            {predefinedEnabled("recipientNotificationEmails") ? (
              <div>
                <label className={labelBase}>Notification Recipient Emails</label>
                <textarea
                  className={`${inputBase} min-h-[84px] resize-y`}
                  placeholder="staff1@company.com; staff2@company.com"
                  value={notificationEmails}
                  onChange={(e) => setNotificationEmails(e.target.value)}
                />
                <p className={hintText}>Multiple email addresses should be separated by semicolon.</p>
              </div>
            ) : (
              <div />
            )}
          </div>
        </section>
      </div>

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}

      {/* Change history modal */}
      {historyOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[720px] max-w-[calc(100vw-2rem)] overflow-hidden rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start justify-between gap-4 border-b border-outline-variant/10 px-6 py-5">
              <div>
                <h3 className="text-base font-bold text-on-surface">Change History</h3>
                <p className="mt-1 text-xs text-on-surface-variant">
                  Delivery slot:{" "}
                  <code className="rounded bg-surface-container-high px-1.5 py-0.5 font-mono text-[11px] font-bold">
                    {(slot?.code || code || "DELIVERY_SLOT").toUpperCase()}
                  </code>
                </p>
              </div>
              <button
                type="button"
                className="rounded p-2 hover:bg-surface-container transition-colors"
                onClick={() => setHistoryOpen(false)}
                aria-label="Close"
              >
                <IconClose className="h-5 w-5 text-on-surface-variant" />
              </button>
            </div>

            <div className="max-h-[60vh] overflow-y-auto px-6 py-6">
              <div className="overflow-x-auto rounded-xl border border-outline-variant/10">
                <table className="w-full border-collapse text-left">
                  <thead>
                    <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                      <th className="px-4 py-2.5">Date / Time</th>
                      <th className="px-4 py-2.5">Action</th>
                      <th className="px-4 py-2.5">User</th>
                      <th className="px-4 py-2.5">Notes</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[
                      {
                        at: slot?.updatedAt ?? "—",
                        action: "Save and Approve",
                        user: "Admin User",
                        notes: "Updated delivery slot settings.",
                      },
                      {
                        at: "2026-04-08 14:05",
                        action: "Save Changes",
                        user: "Admin User",
                        notes: "Adjusted opening and closing hours.",
                      },
                      {
                        at: "2026-04-02 09:12",
                        action: "Created",
                        user: "Admin User",
                        notes: "Initial slot setup.",
                      },
                    ].map((r, idx) => (
                      <tr key={idx} className="text-xs hover:bg-surface-container-low transition-colors">
                        <td className="px-4 py-2.5 font-mono text-[11px] text-on-surface-variant">{r.at}</td>
                        <td className="px-4 py-2.5 font-semibold text-on-surface">{r.action}</td>
                        <td className="px-4 py-2.5 text-on-surface-variant">{r.user}</td>
                        <td className="px-4 py-2.5 text-on-surface-variant">{r.notes}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="mt-3 text-[10px] text-on-surface-variant">
                History is mocked for now; will be replaced once backend audit logs are available.
              </p>
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setHistoryOpen(false)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

