import { type ChangeEvent, useEffect, useRef, useState } from "react";
import type { BrandListRow } from "../brands-columns";
import type {
  BrandAdditionalField,
  BrandAdditionalFieldOption,
  BrandFieldControlType,
} from "./BrandConfigPage";
import { MultiLangInput, MultiLangTextarea } from "../components/MultiLangField";
import { MultiLangLexicalRichText } from "../components/MultiLangLexicalRichText";
import {
  IconArrowBack,
  IconCalendarToday,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconDelete,
  IconFactCheck,
  IconGroup,
  IconHistory,
  IconImage,
  IconInfo,
  IconLightbulb,
  IconLocationOn,
  IconMap,
  IconMoreHoriz,
  IconSave,
  IconSearch,
  IconTune,
  IconVisibility,
} from "../../orders/icons";

// ── Shared style tokens ────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const sectionTitle =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2 mb-6";
const sectionTitleRow =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2";
const sectionIntroText = "text-sm text-on-surface-variant mt-2";
const btnFooterGhost =
  "text-on-surface-variant font-bold text-xs uppercase tracking-widest px-6 py-2 hover:bg-surface-container-high rounded-md transition-colors";
const btnFooterPrimary =
  "px-6 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-primary/20 hover:scale-[1.02] active:scale-95 transition-all flex items-center gap-2";

// ── Types ──────────────────────────────────────────────────────────────────
type EditTab = "general" | "contacts" | "seo" | "recommendations" | "other";

type Props = {
  mode: "add" | "edit";
  brandCode?: string;
  brandName?: string;
  active?: boolean;
  logoSrc?: string;
  logoAlt?: string;
  /** Additional fields configured in BrandConfigPage — drives the "Other" tab. */
  additionalFields?: BrandAdditionalField[];
  onBack: () => void;
  /** row = core brand data; additionalInfo = JSON-serialised additional values from "Other" tab. */
  onSave?: (row: BrandListRow, additionalInfo: string) => void;
};

// ── Mock category list (mirrors CategoriesPage INITIAL_TREE) ─────────────
const MOCK_CATEGORIES = [
  "@12%rebate",
  "Sub-category A",
  "Sub-category B",
  "1-12-REBATE",
  "Anniversary",
  "CGCategory",
  "Electronics",
  "Furniture",
];

// ── Category Picker ────────────────────────────────────────────────────────
function CategoryPicker({
  selected,
  onChange,
}: {
  selected: string[];
  onChange: (next: string[]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  const filtered = MOCK_CATEGORIES.filter((c) =>
    c.toLowerCase().includes(search.toLowerCase())
  );

  const toggle = (cat: string) => {
    onChange(
      selected.includes(cat)
        ? selected.filter((c) => c !== cat)
        : [...selected, cat]
    );
  };

  return (
    <div ref={containerRef} className="relative space-y-1.5">
      {/* Selected chips */}
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((cat) => (
            <span
              key={cat}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
            >
              {cat}
              <button
                type="button"
                aria-label={`Remove ${cat}`}
                className="rounded p-0.5 hover:bg-primary/20 transition-colors"
                onClick={() => toggle(cat)}
              >
                <IconClose className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}

      {/* Search input */}
      <div className="relative">
        <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
        <input
          type="text"
          className={`${inputBase} pl-8 pr-3`}
          placeholder="Type category name to search..."
          value={search}
          onFocus={() => setOpen(true)}
          onChange={(e) => { setSearch(e.target.value); setOpen(true); }}
        />
      </div>

      {/* Dropdown */}
      {open && (
        <div className="absolute left-0 right-0 top-full z-30 mt-1 max-h-52 overflow-y-auto rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
          {filtered.length === 0 ? (
            <p className="px-4 py-3 text-xs italic text-on-surface-variant">
              No categories match "{search}"
            </p>
          ) : (
            filtered.map((cat) => {
              const checked = selected.includes(cat);
              return (
                <label
                  key={cat}
                  className={`flex cursor-pointer items-center gap-3 px-4 py-2.5 text-xs transition-colors hover:bg-surface-container-high ${
                    checked ? "bg-primary/5 font-semibold text-primary" : "text-on-surface"
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={() => toggle(cat)}
                    className="h-3.5 w-3.5 accent-primary shrink-0"
                  />
                  {cat}
                </label>
              );
            })
          )}
        </div>
      )}

      {selected.length === 0 && !open && (
        <p className="text-[10px] text-on-surface-variant">
          Click the box above to assign categories.
        </p>
      )}
    </div>
  );
}

// ── Mock brand list for Recommendations tab ────────────────────────────────
const ALL_BRANDS = [
  { code: "CAS-7721", name: "Luxe Heritage Group" },
  { code: "VEL-4490", name: "Velocity Tech Systems" },
  { code: "NOM-1022", name: "Nomad Consulting Ltd." },
  { code: "AUR-5501", name: "Aura Essentials" },
];

type BrandHistoryRow = { at: string; actor: string; field: string; old: string; new: string };

function mockBrandHistory(code: string, name: string): BrandHistoryRow[] {
  const safeName = name || "—";
  const safeCode = code || "—";
  return [
    { at: "2026-04-12 14:32", actor: "eStore Admin", field: "Status", old: "Inactive", new: "Active" },
    { at: "2026-04-11 10:08", actor: "minh.hoang@example.com", field: "Brand Name", old: "Working Title", new: safeName },
    { at: "2026-04-11 10:07", actor: "minh.hoang@example.com", field: "Brand Code", old: "TMP-0000", new: safeCode },
    { at: "2026-04-09 15:21", actor: "Content Bot", field: "Description (EN)", old: "(empty)", new: "Updated marketing copy." },
  ];
}

// ── Image upload slot (real file input; mock preview only) ─────────────────
function ImageSlot({
  label,
  src,
  alt,
  maxBytes = 2 * 1024 * 1024,
  onFileSelected,
  onRemove,
}: {
  label: string;
  src?: string;
  alt?: string;
  maxBytes?: number;
  onFileSelected: (dataUrl: string) => void;
  onRemove: () => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");

  function openPicker() {
    setError("");
    inputRef.current?.click();
  }

  function handleFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      setError("Choose a JPG, PNG, or WEBP image.");
      return;
    }
    if (file.size > maxBytes) {
      setError(`Max size is ${Math.round(maxBytes / (1024 * 1024))} MB.`);
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result;
      if (typeof result === "string") onFileSelected(result);
    };
    reader.readAsDataURL(file);
  }

  return (
    <div className="space-y-1.5">
      <label className={labelBase}>{label}</label>
      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp,image/*"
        className="sr-only"
        onChange={handleFileChange}
      />
      <div className="border-2 border-dashed border-outline-variant/40 rounded-xl p-3 flex flex-col items-center justify-center bg-surface-container-low/20">
        {src ? (
          <>
            <div className="relative w-full aspect-video bg-white rounded-lg shadow-sm overflow-hidden mb-3 border border-outline-variant/10">
              <img className="w-full h-full object-cover" alt={alt ?? label} src={src} />
              <button
                type="button"
                className="absolute top-1.5 right-1.5 p-1 bg-error text-white rounded-full shadow active:scale-90 transition-transform"
                aria-label="Remove image"
                onClick={() => {
                  setError("");
                  onRemove();
                }}
              >
                <IconDelete className="h-3 w-3" />
              </button>
            </div>
            <button
              type="button"
              className="w-full py-1.5 bg-surface-container-high text-primary font-bold text-[10px] uppercase tracking-widest rounded hover:bg-surface-container transition-colors"
              onClick={openPicker}
            >
              Replace Image
            </button>
          </>
        ) : (
          <>
            <IconImage className="h-8 w-8 text-outline-variant mb-2" aria-hidden />
            <p className="text-[10px] text-on-surface-variant text-center mb-2">
              JPG, PNG, WEBP — Max 2 MB
            </p>
            <button
              type="button"
              className="w-full py-1.5 bg-primary text-on-primary font-bold text-[10px] uppercase tracking-widest rounded hover:bg-primary-container transition-colors"
              onClick={openPicker}
            >
              Choose File
            </button>
          </>
        )}
      </div>
      {error ? <p className="text-[10px] text-error">{error}</p> : null}
    </div>
  );
}

// ── Component ──────────────────────────────────────────────────────────────
export function EditBrandPage({
  mode,
  brandCode = "",
  brandName = "",
  active = true,
  logoSrc = "",
  logoAlt = "",
  additionalFields = [],
  onBack,
  onSave,
}: Props) {
  const isAdd = mode === "add";

  // UI state
  const [tab, setTab] = useState<EditTab>("general");
  const [actionsOpen, setActionsOpen] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);
  const [showHistoryModal, setShowHistoryModal] = useState(false);
  const [brandImageSrc, setBrandImageSrc] = useState(logoSrc?.trim() ? logoSrc : "");
  const [mobileImageSrc, setMobileImageSrc] = useState("");
  const actionsRef = useRef<HTMLDivElement>(null);

  // Additional-info field values (keyed by field.id).
  // WYSIWYG fields store a per-locale HTML map (Record<string,string>) so they
  // round-trip through the same shape as MultiLangLexicalRichText emits.
  type AdditionalValue = string | string[] | Record<string, string>;
  const [additionalValues, setAdditionalValues] = useState<Record<string, AdditionalValue>>(() =>
    Object.fromEntries(
      additionalFields.map((f) => [
        f.id,
        f.controlType === "Multiple Select"
          ? []
          : f.controlType === "WYSIWYG (Text Area)"
          ? ({} as Record<string, string>)
          : "",
      ])
    )
  );

  function setAdditionalValue(id: string, val: AdditionalValue) {
    setAdditionalValues((prev) => ({ ...prev, [id]: val }));
  }

  // Active status state (controlled so Save can read it)
  const [isActive, setIsActive] = useState(active);

  useEffect(() => {
    setBrandImageSrc(logoSrc?.trim() ? logoSrc : "");
    setMobileImageSrc("");
    setIsActive(active);
  }, [logoSrc, mode, brandCode, active]);

  // Brand Name reactive state (drives auto-populate in add mode)
  const [brandNameInput, setBrandNameInput] = useState(brandName || "");

  // Derive auto-populated values from brand name input
  const words = brandNameInput.replace(/[^a-zA-Z0-9\s]/g, " ").trim().split(/\s+/).filter(Boolean);
  const autoDisplayName = words.slice(0, 2).join(" ");
  const autoCodePrefix = (words[0] ?? "").slice(0, 3).toUpperCase().padEnd(3, "X");
  const autoCode = brandNameInput ? `${autoCodePrefix}-${words.length > 1 ? (words[1] ?? "").slice(0, 4).toUpperCase().padEnd(4, "0") : "0001"}` : "";
  const autoPromo = brandNameInput.replace(/[^a-zA-Z]/g, "").slice(0, 4).toUpperCase() || "";

  // Display Name: seed remounts MultiLangInput only when brand name field loses focus
  // so user edits on other languages are preserved while typing the brand name.
  const [displayNameSeed, setDisplayNameSeed] = useState(0);
  const [confirmedDisplayName, setConfirmedDisplayName] = useState(brandName || "");

  function handleBrandNameBlur() {
    if (isAdd && autoDisplayName !== confirmedDisplayName) {
      setConfirmedDisplayName(autoDisplayName);
      setDisplayNameSeed((s) => s + 1);
    }
  }

  // Edit mode: use prop-derived values (static)
  const displayNameGuess = brandName.split(/\s+/).slice(0, 2).join(" ");
  const promoGuess = isAdd
    ? autoPromo
    : brandCode.replace(/[^a-zA-Z0-9]/g, "").slice(0, 4).toUpperCase() || "BRND";
  const codeValue = isAdd ? autoCode : brandCode;

  // Categories state
  const [selectedCategories, setSelectedCategories] = useState<string[]>(
    isAdd ? [] : ["Luxury", "Fragrance"]
  );

  // Recommendations state
  const [excludedBrands, setExcludedBrands] = useState<string[]>([]);

  // Available brands for recommendations (exclude self)
  const availableBrands = ALL_BRANDS.filter((b) => b.code !== brandCode);

  // Close actions dropdown on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node)) {
        setActionsOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  function handleSave() {
    const code = isAdd ? autoCode || `BRD-${Date.now()}` : brandCode;
    const savedRow: BrandListRow = {
      code,
      name: brandNameInput || brandName,
      imageSrc: brandImageSrc,
      imageAlt: logoAlt || brandNameInput || brandName,
      active: isActive,
    };
    // Serialise additional values — filter out empty/default so JSON is clean
    let additionalInfoStr = "{}";
    try {
      const cleaned: Record<string, unknown> = {};
      for (const [id, val] of Object.entries(additionalValues)) {
        const isEmpty =
          val === "" ||
          (Array.isArray(val) && val.length === 0) ||
          (typeof val === "object" &&
            val !== null &&
            !Array.isArray(val) &&
            Object.values(val as Record<string, string>).every((v) => !v?.trim()));
        if (!isEmpty) cleaned[id] = val;
      }
      additionalInfoStr = JSON.stringify(cleaned);
    } catch {
      additionalInfoStr = "{}";
    }
    onSave?.(savedRow, additionalInfoStr);
    setActionsOpen(false);
    setShowSuccessModal(true);
  }

  function toggleExclusion(code: string, checked: boolean) {
    setExcludedBrands((prev) =>
      checked ? [...prev, code] : prev.filter((c) => c !== code)
    );
  }

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="flex items-center gap-2 text-primary font-bold text-xs uppercase tracking-tighter mb-1 hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Brands
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface flex flex-wrap items-center gap-3">
            {isAdd ? (
              <span>Add Brand</span>
            ) : (
              <>
                <span>Edit Brand:</span>
                <span className="text-primary-container">{brandName}</span>
                {isActive ? (
                  <span className="bg-secondary-container/20 text-on-secondary-container text-[10px] px-2 py-0.5 rounded-full uppercase font-bold tracking-widest">
                    Active
                  </span>
                ) : (
                  <span className="bg-outline-variant/20 text-on-surface-variant text-[10px] px-2 py-0.5 rounded-full uppercase font-bold tracking-widest">
                    Inactive
                  </span>
                )}
              </>
            )}
          </h2>
        </div>

        <div className="flex items-center gap-3">
          {!isAdd && (
            <button
              type="button"
              className="px-4 py-2 rounded-md border border-outline-variant/30 text-on-surface-variant font-semibold text-xs hover:bg-surface-container-high transition-colors flex items-center gap-2"
              onClick={() => setShowHistoryModal(true)}
            >
              <IconHistory className="h-4 w-4 shrink-0" />
              Show Change History
            </button>
          )}

          {/* Actions dropdown */}
          <div className="relative" ref={actionsRef}>
            <button
              type="button"
              className="flex items-center gap-2 px-6 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-primary/20 hover:bg-primary-container transition-colors"
              onClick={() => setActionsOpen((o) => !o)}
            >
              Actions
              <IconChevronDown className="h-4 w-4 shrink-0" />
            </button>
            {actionsOpen && (
              <div className="absolute right-0 top-full mt-1 w-44 bg-surface-container-lowest border border-outline-variant/20 rounded-lg shadow-xl z-20 overflow-hidden">
                <button
                  type="button"
                  className="w-full px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container flex items-center gap-2.5 transition-colors"
                  onClick={handleSave}
                >
                  <IconSave className="h-4 w-4 shrink-0 text-primary" />
                  Save
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Body: sidebar tabs + form ────────────────────────────────────── */}
      <div className="flex min-h-0 flex-1 flex-col gap-8 p-6 lg:flex-row">

        {/* Sidebar tab nav */}
        <aside className="w-full lg:w-64 flex flex-row lg:flex-col gap-1 shrink-0 overflow-x-auto lg:overflow-visible">
          {(
            [
              { id: "general" as const,         label: "General",                  Icon: IconInfo        },
              { id: "contacts" as const,         label: "Contacts",                 Icon: IconGroup       },
              { id: "recommendations" as const,  label: "Product Recommendations",  Icon: IconMoreHoriz   },
              { id: "seo" as const,              label: "SEO Configuration",        Icon: IconFactCheck   },
              { id: "other" as const,            label: "Other",                    Icon: IconTune        },
            ] satisfies { id: EditTab; label: string; Icon: typeof IconInfo }[]
          ).map(({ id, label, Icon }) => {
            const isActive = tab === id;
            return (
              <button
                key={id}
                type="button"
                onClick={() => setTab(id)}
                className={`flex items-center gap-3 px-4 py-3 text-left whitespace-nowrap rounded-none lg:rounded-sm border-l-4 transition-colors font-medium text-sm ${
                  isActive
                    ? "bg-surface-container-lowest border-primary text-primary font-bold shadow-sm"
                    : "border-transparent text-on-surface-variant hover:bg-surface-container-high"
                }`}
              >
                <Icon className="h-5 w-5 shrink-0" />
                {label}
              </button>
            );
          })}
        </aside>

        {/* Tab content panel */}
        <section
          className="flex-1 space-y-8 bg-surface-container-lowest p-8 rounded-xl shadow-[0_4px_24px_rgba(40,23,22,0.04)] border border-outline-variant/10 min-h-0 overflow-y-auto"
          aria-label="Brand form"
        >

          {/* ── GENERAL ─────────────────────────────────────────────────── */}
          {tab === "general" && (
            <>
              {/* General Information */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12">
                  <h3 className={sectionTitle}>General Information</h3>
                </div>

                <div className="col-span-12 md:col-span-4 space-y-1.5">
                  <label className={labelBase}>Tenant</label>
                  <div className="relative">
                    <select className={`${inputBase} appearance-none pr-10`}>
                      <option>Global Enterprise Solutions</option>
                      <option>EMEA Retail Group</option>
                      <option>North America Brand Div</option>
                    </select>
                    <IconChevronDown className="absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant pointer-events-none" />
                  </div>
                </div>

                <div className="col-span-12 md:col-span-4 space-y-1.5">
                  <label className={labelBase}>Brand Name <span className="text-error">*</span></label>
                  <input
                    className={inputBase}
                    type="text"
                    value={brandNameInput}
                    onChange={(e) => setBrandNameInput(e.target.value)}
                    onBlur={handleBrandNameBlur}
                    placeholder="Enter brand name"
                  />
                  {isAdd && autoDisplayName && (
                    <p className="text-[10px] text-primary/70">
                      Display Name will be set to &ldquo;{autoDisplayName}&rdquo; on confirm.
                    </p>
                  )}
                </div>

                <div className="col-span-12 md:col-span-4">
                  <MultiLangInput
                    key={isAdd ? displayNameSeed : undefined}
                    label="Display Name"
                    defaultValues={{
                      en: isAdd ? confirmedDisplayName : displayNameGuess,
                    }}
                    placeholders={{
                      en: "Auto-filled from Brand Name",
                      vn: "Tên hiển thị",
                      zh: "显示名称",
                      ja: "表示名",
                    }}
                  />
                </div>

                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>
                    Brand Code
                    {isAdd && autoCode && (
                      <span className="ml-1.5 text-[9px] font-normal normal-case text-primary/70 tracking-normal">
                        auto-filled
                      </span>
                    )}
                  </label>
                  <input
                    className={`${inputBase} font-mono ${isAdd && autoCode ? "text-on-surface-variant" : ""}`}
                    type="text"
                    value={codeValue}
                    readOnly={isAdd && Boolean(autoCode)}
                    placeholder="e.g. LHG-7721"
                    onChange={() => {}}
                  />
                </div>

                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>
                    Promo Code Prefix
                    {isAdd && autoPromo && (
                      <span className="ml-1.5 text-[9px] font-normal normal-case text-primary/70 tracking-normal">
                        auto-filled
                      </span>
                    )}
                  </label>
                  <input
                    className={`${inputBase} ${isAdd && autoPromo ? "text-on-surface-variant" : ""}`}
                    type="text"
                    value={isAdd ? autoPromo : promoGuess}
                    readOnly={isAdd && Boolean(autoPromo)}
                    placeholder="e.g. LHG"
                    onChange={() => {}}
                  />
                </div>

                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Status</label>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={isActive}
                    onClick={() => setIsActive((v) => !v)}
                    className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-primary focus:ring-offset-1 ${
                      isActive ? "bg-primary" : "bg-outline-variant/40"
                    }`}
                  >
                    <span
                      className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
                        isActive ? "translate-x-6" : "translate-x-1"
                      }`}
                    />
                  </button>
                  <p className="text-[10px] text-on-surface-variant mt-1">
                    {isActive ? "Active — visible in storefront" : "Inactive — hidden from storefront"}
                  </p>
                </div>

                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Categories</label>
                  <CategoryPicker
                    selected={selectedCategories}
                    onChange={setSelectedCategories}
                  />
                </div>
              </div>

              {/* Scheduling */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12">
                  <h3 className={sectionTitle}>Scheduling</h3>
                </div>
                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Publish From</label>
                  <input
                    className={inputBase}
                    type="date"
                    defaultValue={isAdd ? "" : "2024-05-15"}
                  />
                </div>
                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Publish To</label>
                  <input
                    className={inputBase}
                    type="date"
                    defaultValue={isAdd ? "" : "2025-05-15"}
                  />
                </div>
              </div>

              {/* Marketing Content */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12">
                  <h3 className={sectionTitle}>Marketing Content</h3>
                </div>

                {/* Description (Lexical rich text, HTML per locale) */}
                <div className="col-span-12 md:col-span-8 space-y-1.5">
                  <MultiLangLexicalRichText
                    label="Description"
                    defaultValues={{
                      en: isAdd
                        ? ""
                        : "<p>Velvet Aura Luxury represents the pinnacle of olfactory craftsmanship. Established in 2024, our mission is to curate sensory experiences that transcend the ordinary.</p>",
                    }}
                    placeholders={{
                      en: "Enter brand description...",
                      vn: "Nhập mô tả thương hiệu...",
                      zh: "输入品牌描述...",
                      ja: "ブランドの説明を入力してください...",
                    }}
                  />
                </div>

                {/* Brand Image + Mobile Image */}
                <div className="col-span-12 md:col-span-4 space-y-4">
                  <ImageSlot
                    label="Brand Image"
                    src={brandImageSrc || undefined}
                    alt={logoAlt || undefined}
                    onFileSelected={setBrandImageSrc}
                    onRemove={() => setBrandImageSrc("")}
                  />
                  <ImageSlot
                    label="Mobile Image"
                    src={mobileImageSrc || undefined}
                    alt={`${brandNameInput || brandName || "Brand"} — mobile`}
                    onFileSelected={setMobileImageSrc}
                    onRemove={() => setMobileImageSrc("")}
                  />
                </div>
              </div>

              {!isAdd && (
                <div className="flex justify-end items-center gap-3 pt-8 mt-8 border-t border-outline-variant/20">
                  <button type="button" className={btnFooterGhost} onClick={onBack}>
                    Cancel
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Save Changes
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── CONTACTS ────────────────────────────────────────────────── */}
          {tab === "contacts" && (
            <>
              {/* Tenant Contacts */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12 mb-2">
                  <h3 className={sectionTitleRow}>Tenant Contacts</h3>
                  <p className={sectionIntroText}>Primary operational contact details for the brand workspace.</p>
                </div>
                <div className="col-span-12 grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-6">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Office No 1</label>
                    <input className={inputBase} type="text" defaultValue={isAdd ? "" : "+1 (555) 0123-456"} />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Office No 2</label>
                    <input className={inputBase} placeholder="Alternative contact line" type="text" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Contact Person</label>
                    <input className={`${inputBase} font-medium`} type="text" defaultValue={isAdd ? "" : "Julian Montgomery"} />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Contact Person Phone No</label>
                    <input className={inputBase} type="text" defaultValue={isAdd ? "" : "+1 (555) 987-6543"} />
                  </div>
                  <div className="md:col-span-2 space-y-1.5">
                    <label className={labelBase}>Contact Person&apos;s Email</label>
                    <textarea
                      className={`${inputBase} resize-none`}
                      rows={3}
                      defaultValue={isAdd ? "" : "julian.montgomery@editorial-executive.co\noperations.desk@brandportal.io"}
                    />
                    <p className="text-xs text-on-surface-variant italic">
                      Enter multiple email addresses separated by a new line.
                    </p>
                  </div>
                </div>
              </div>

              {/* Out of Stock Contact */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12 mb-2">
                  <h3 className={sectionTitleRow}>Out of Stock Notifications</h3>
                  <p className={sectionIntroText}>
                    Specify email addresses to notify when brand products are out of stock.
                  </p>
                </div>
                <div className="col-span-12 space-y-1.5">
                  <label className={labelBase}>Out of Stock Contact Email List</label>
                  <textarea
                    className={`${inputBase} resize-none`}
                    placeholder="Enter recipient emails, one per line (e.g. warehouse@brand.com)"
                    rows={4}
                    defaultValue={isAdd ? "" : "stock-alerts@luxeheritage.com\nwarehouse@brandportal.io"}
                  />
                  <p className="text-xs text-on-surface-variant italic">
                    Enter multiple email addresses separated by a new line.
                  </p>
                </div>
              </div>

              {/* Location HQ */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12 mb-2">
                  <h3 className={sectionTitleRow}>Location Headquarters</h3>
                </div>
                <div className="col-span-12 space-y-1.5">
                  <div className="relative">
                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                      <IconLocationOn className="h-4 w-4 text-primary shrink-0" />
                    </div>
                    <input
                      className={`${inputBase} pl-10 font-medium`}
                      type="text"
                      defaultValue={isAdd ? "" : "350 Fifth Avenue, Empire State Building, New York, NY"}
                      placeholder="Enter HQ address"
                    />
                  </div>
                  {!isAdd && (
                    <div className="mt-4 grid grid-cols-12 gap-4 h-48">
                      <div className="col-span-12 md:col-span-8 relative rounded-md overflow-hidden border border-outline-variant/20">
                        <img
                          alt="Location map preview"
                          className="w-full h-full object-cover grayscale contrast-125"
                          src="https://lh3.googleusercontent.com/aida-public/AB6AXuAka-AEjzWcje_ee7Igh5WHmjM0XiGOjU2H-34r-UdC2Fpbrz5DHvaEA6vGo39s-3PswnBjdStVsxaHPvV_QMvy-4d5pbVW8ujPZb4_iYz4YHGgxYGugNmVywsLVOKvjSWifRFxDWwDU_IRrNObVhwnNRrqVUM8NAxwtYna7elsh58kjsT4G_0498_IT8vf9SYGqP1rTiqm2Jmf4QlUGL_5ZndRf3z4QFDkLO0nFM2BZkEZujXSmXe65CW6OpXVTcFgbr_t1KumRjo"
                        />
                        <div className="absolute inset-0 bg-primary/5 pointer-events-none" />
                        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 pointer-events-none">
                          <div className="w-10 h-10 bg-primary/20 rounded-full flex items-center justify-center animate-pulse">
                            <div className="w-3 h-3 bg-primary rounded-full border-2 border-white" />
                          </div>
                        </div>
                      </div>
                      <div className="col-span-12 md:col-span-4">
                        <div className="h-full min-h-[12rem] bg-surface-container-high rounded-md p-4 flex flex-col justify-center items-center text-center space-y-2 border border-outline-variant/20">
                          <IconMap className="h-6 w-6 text-primary shrink-0" />
                          <p className="text-[10px] font-bold text-on-surface uppercase">Zone Coverage</p>
                          <p className="text-lg font-black text-on-background">Tier 1</p>
                          <div className="px-3 py-1 bg-primary/10 rounded-full">
                            <p className="text-[9px] font-bold text-primary">North America</p>
                          </div>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {!isAdd && (
                <div className="flex justify-end items-center gap-3 pt-8 mt-8 border-t border-outline-variant/20">
                  <button type="button" className={btnFooterGhost} onClick={() => console.info("[EditBrand] Contacts discard")}>
                    Discard Changes
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Update Contacts
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── PRODUCT RECOMMENDATIONS ──────────────────────────────────── */}
          {tab === "recommendations" && (
            <>
              <div className="mb-6">
                <h3 className={sectionTitleRow}>Product Recommendation Exclusions</h3>
                <p className={sectionIntroText}>
                  Select brands to exclude from product recommendations on this brand&apos;s product pages.
                  This prevents other brands from being recommended alongside this brand&apos;s products.
                </p>
              </div>

              <div className="space-y-2">
                {availableBrands.length === 0 ? (
                  <p className="text-sm text-on-surface-variant italic">No other brands available.</p>
                ) : (
                  availableBrands.map((brand) => {
                    const checked = excludedBrands.includes(brand.code);
                    return (
                      <label
                        key={brand.code}
                        className={`flex items-center gap-4 p-4 rounded-lg border transition-colors cursor-pointer ${
                          checked
                            ? "border-primary/30 bg-primary/5"
                            : "border-outline-variant/20 hover:bg-surface-container-low"
                        }`}
                      >
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={(e) => toggleExclusion(brand.code, e.target.checked)}
                          className="accent-primary h-4 w-4 shrink-0"
                        />
                        <span className="font-mono text-xs text-on-surface-variant w-20 shrink-0">
                          {brand.code}
                        </span>
                        <span className="text-sm font-medium text-on-surface">{brand.name}</span>
                        {checked && (
                          <span className="ml-auto text-[10px] font-bold uppercase tracking-widest text-primary bg-primary/10 px-2 py-0.5 rounded-full">
                            Excluded
                          </span>
                        )}
                      </label>
                    );
                  })
                )}
              </div>

              {excludedBrands.length > 0 && (
                <div className="mt-4 p-3 rounded-lg bg-surface-container-low border border-outline-variant/20">
                  <p className="text-xs text-on-surface-variant">
                    <span className="font-bold text-on-surface">{excludedBrands.length} brand{excludedBrands.length !== 1 ? "s" : ""} excluded</span>
                    {" — these brands will not appear as recommendations on this brand's product pages."}
                  </p>
                </div>
              )}

              {!isAdd && (
                <div className="flex justify-end items-center gap-3 pt-8 mt-8 border-t border-outline-variant/20">
                  <button type="button" className={btnFooterGhost} onClick={() => setExcludedBrands([])}>
                    Reset
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Update Recommendations
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── SEO ─────────────────────────────────────────────────────── */}
          {tab === "seo" && (
            <>
              <div className="grid grid-cols-12 gap-8">
                <div className="col-span-12 mb-2">
                  <h3 className={sectionTitleRow}>SEO Configuration</h3>
                  <p className={sectionIntroText}>
                    Optimize brand visibility for global search engine indexing.
                  </p>
                </div>

                {/* Left: fields */}
                <div className="col-span-12 lg:col-span-8 space-y-6">
                  <div className="space-y-6">
                    <MultiLangInput
                      label="Meta Title"
                      defaultValues={{
                        en: isAdd ? "" : "T - 10.Deep | TANGS Singapore",
                      }}
                      placeholders={{
                        en: "Enter the text that will appear on the browser's title bar and tab",
                        vn: "Tiêu đề xuất hiện trên tab trình duyệt",
                        zh: "浏览器标签栏中显示的文字",
                        ja: "ブラウザのタブに表示されるテキスト",
                      }}
                      hint="Recommended: 60 characters or fewer."
                    />

                    <MultiLangInput
                      label="Meta Keywords"
                      placeholders={{
                        en: "Enter keywords separated by commas…",
                        vn: "Nhập từ khóa cách nhau bằng dấu phẩy...",
                        zh: "输入以逗号分隔的关键词...",
                        ja: "カンマ区切りでキーワードを入力...",
                      }}
                      hint="List relevant keywords to help search engines find this brand."
                    />

                    <MultiLangTextarea
                      label="Meta Description"
                      rows={4}
                      defaultValues={{
                        en: isAdd
                          ? ""
                          : "T - 10.Deep demo ecommerce sirius brand meta description. Experience premium lifestyle curation with global delivery options.",
                      }}
                      placeholders={{
                        en: "Provide a brief overview of the brand that will be displayed on search results listings",
                        vn: "Mô tả ngắn về thương hiệu hiển thị trên kết quả tìm kiếm",
                        zh: "提供将在搜索结果中显示的品牌简短概述",
                        ja: "検索結果に表示されるブランドの概要を入力してください",
                      }}
                      hint="Recommended: 160 characters or fewer."
                    />
                  </div>
                </div>

                {/* Right: preview */}
                <div className="col-span-12 lg:col-span-4 space-y-6">
                  <div>
                    <h3 className={`${sectionTitleRow} mb-4 flex items-center gap-2`}>
                      <IconVisibility className="h-4 w-4 text-primary shrink-0" />
                      Google Search Preview
                    </h3>
                    <div className="bg-white p-4 rounded-md border border-outline-variant/20 font-sans">
                      <div className="text-sm text-on-surface-variant/80 mb-0.5 truncate">
                        https://your-estore.com › brands › {brandName.toLowerCase().replace(/\s+/g, "-") || "brand-name"}
                      </div>
                      <h4 className="text-lg text-[#1a0dab] hover:underline cursor-pointer font-normal mb-1 truncate">
                        {isAdd ? (
                          <span className="text-outline-variant italic">Meta title will appear here</span>
                        ) : (
                          "T - 10.Deep | TANGS Singapore"
                        )}
                      </h4>
                      <p className="text-[13px] text-[#4d5156] leading-snug line-clamp-3">
                        {isAdd ? (
                          <span className="italic text-outline-variant">Meta description will appear here</span>
                        ) : (
                          "T - 10.Deep demo ecommerce sirius brand meta description. Experience premium lifestyle curation with global delivery options."
                        )}
                      </p>
                    </div>
                  </div>

                  <div className="p-4 bg-primary-container/10 rounded-md border border-outline-variant/10">
                    <div className="flex items-start gap-3">
                      <IconLightbulb className="h-5 w-5 text-primary shrink-0 mt-0.5" />
                      <p className="text-xs text-on-surface-variant leading-relaxed">
                        <strong>SEO Tip:</strong> High-quality meta descriptions can increase click-through rates.
                        Ensure primary keywords appear near the beginning.
                      </p>
                    </div>
                  </div>
                </div>
              </div>

              {!isAdd && (
                <div className="flex justify-end gap-3 items-center pt-8 mt-8 border-t border-outline-variant/20">
                  <button type="button" className={btnFooterGhost} onClick={() => console.info("[EditBrand] SEO discard")}>
                    Discard Changes
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Update SEO Settings
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── OTHER (Additional Info) ─────────────────────────────────── */}
          {tab === "other" && (
            <OtherTab
              fields={additionalFields}
              values={additionalValues}
              onChange={setAdditionalValue}
              isAdd={isAdd}
              onSave={handleSave}
            />
          )}
        </section>
      </div>

      {/* ── Change history (mock audit trail) ───────────────────────────── */}
      {showHistoryModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm p-4"
          onClick={() => setShowHistoryModal(false)}
        >
          <div
            className="flex max-h-[min(85vh,720px)] w-full max-w-3xl flex-col overflow-hidden rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between gap-3 border-b border-outline-variant/15 px-6 py-4">
              <div>
                <h3 className="text-lg font-bold text-on-surface">Change history</h3>
                <p className="text-xs text-on-surface-variant mt-0.5">
                  Mock data for UI review (date, actor, field, old, new).
                </p>
              </div>
              <button
                type="button"
                aria-label="Close"
                className="rounded-full p-2 text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setShowHistoryModal(false)}
              >
                <IconClose className="h-5 w-5" />
              </button>
            </div>
            <div className="min-h-0 flex-1 overflow-auto p-4">
              <table className="w-full border-collapse text-left text-xs">
                <thead>
                  <tr className="border-b border-outline-variant/20 text-on-surface-variant uppercase tracking-wider">
                    <th className="py-2 pr-3 font-bold">Date / time</th>
                    <th className="py-2 pr-3 font-bold">Actor</th>
                    <th className="py-2 pr-3 font-bold">Field</th>
                    <th className="py-2 pr-3 font-bold">Old value</th>
                    <th className="py-2 font-bold">New value</th>
                  </tr>
                </thead>
                <tbody>
                  {mockBrandHistory(codeValue, brandNameInput || brandName).map((row) => (
                    <tr key={`${row.at}-${row.field}`} className="border-b border-outline-variant/10 align-top">
                      <td className="py-2.5 pr-3 whitespace-nowrap text-on-surface-variant">{row.at}</td>
                      <td className="py-2.5 pr-3 text-on-surface">{row.actor}</td>
                      <td className="py-2.5 pr-3 font-medium text-on-surface">{row.field}</td>
                      <td className="py-2.5 pr-3 text-on-surface-variant break-words max-w-[140px]">{row.old}</td>
                      <td className="py-2.5 text-on-surface break-words max-w-[140px]">{row.new}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-outline-variant/10 px-6 py-3 flex justify-end">
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2 text-xs font-bold uppercase tracking-widest text-on-primary hover:bg-primary-container transition-colors"
                onClick={() => setShowHistoryModal(false)}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Success modal ────────────────────────────────────────────────── */}
      {showSuccessModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setShowSuccessModal(false)}
        >
          <div
            className="bg-surface-container-lowest rounded-2xl shadow-2xl p-8 max-w-sm w-full mx-4 text-center"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="w-16 h-16 rounded-full bg-secondary-container/20 flex items-center justify-center mx-auto mb-4">
              <IconCheckCircle className="h-8 w-8 text-secondary" />
            </div>
            <h3 className="text-lg font-bold text-on-surface mb-2">
              {isAdd ? "Brand Created!" : "Changes Saved!"}
            </h3>
            <p className="text-sm text-on-surface-variant mb-6">
              {isAdd
                ? "The new brand has been successfully created and is now available in your eStore."
                : `"${brandName}" has been successfully updated.`}
            </p>
            <div className="flex gap-3 justify-center">
              <button
                type="button"
                className="px-5 py-2 rounded-md border border-outline-variant/30 text-on-surface-variant font-bold text-xs uppercase tracking-widest hover:bg-surface-container-high transition-colors"
                onClick={onBack}
              >
                View Brands
              </button>
              <button
                type="button"
                className="px-5 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest hover:bg-primary-container transition-colors"
                onClick={() => setShowSuccessModal(false)}
              >
                {isAdd ? "Add Another" : "Continue Editing"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ── OtherTab ───────────────────────────────────────────────────────────────────

const SECTION_ORDER = [
  "General Information",
  "Contacts",
  "Product Recommendations",
  "SEO Configuration",
] as const;

type OtherTabValue = string | string[] | Record<string, string>;

function OtherTab({
  fields,
  values,
  onChange,
  isAdd,
  onSave,
}: {
  fields: BrandAdditionalField[];
  values: Record<string, OtherTabValue>;
  onChange: (id: string, val: OtherTabValue) => void;
  isAdd: boolean;
  onSave: () => void;
}) {
  const enabledFields = fields.filter((f) => f.enabled);

  if (enabledFields.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
        <IconTune className="h-10 w-10 text-outline-variant" />
        <div>
          <p className="text-sm font-bold text-on-surface">No additional fields configured</p>
          <p className="mt-1 text-xs text-on-surface-variant max-w-xs">
            Go to <span className="font-semibold text-primary">Brand Configuration</span> to define
            custom fields that will appear here.
          </p>
        </div>
      </div>
    );
  }

  // Group enabled fields by section in defined order
  const groups = SECTION_ORDER.map((section) => ({
    section,
    rows: enabledFields.filter((f) => f.section === section),
  })).filter((g) => g.rows.length > 0);

  // Ungrouped fallback (custom sections)
  const knownSections = new Set(SECTION_ORDER as readonly string[]);
  const ungrouped = enabledFields.filter((f) => !knownSections.has(f.section));
  if (ungrouped.length > 0) {
    groups.push({ section: "Other" as (typeof SECTION_ORDER)[number], rows: ungrouped });
  }

  return (
    <>
      <div className="mb-2">
        <h3 className="text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2">
          Additional Information
        </h3>
        <p className="mt-2 text-sm text-on-surface-variant">
          Custom fields defined in{" "}
          <span className="font-semibold text-on-surface">Brand Configuration</span>.
          Enabled fields are shown below, grouped by section.
        </p>
      </div>

      <div className="space-y-8">
        {groups.map(({ section, rows }) => (
          <div key={section}>
            <h4 className="mb-4 text-xs font-bold uppercase tracking-widest text-on-surface-variant border-b border-outline-variant/10 pb-1.5">
              {section}
            </h4>
            <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
              {rows.map((field) => {
                const fallback: OtherTabValue =
                  field.controlType === "Multiple Select"
                    ? []
                    : field.controlType === "WYSIWYG (Text Area)"
                    ? ({} as Record<string, string>)
                    : "";
                return (
                  <AdditionalFieldInput
                    key={field.id}
                    field={field}
                    value={values[field.id] ?? fallback}
                    onChange={(val) => onChange(field.id, val)}
                  />
                );
              })}
            </div>
          </div>
        ))}
      </div>

      {!isAdd && (
        <div className="flex justify-end gap-3 items-center pt-8 mt-8 border-t border-outline-variant/20">
          <button
            type="button"
            className="text-on-surface-variant font-bold text-xs uppercase tracking-widest px-6 py-2 hover:bg-surface-container-high rounded-md transition-colors"
            onClick={() => {/* reset handled by parent re-init */}}
          >
            Discard Changes
          </button>
          <button
            type="button"
            className="px-6 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-primary/20 hover:scale-[1.02] active:scale-95 transition-all flex items-center gap-2"
            onClick={onSave}
          >
            <IconSave className="h-4 w-4 shrink-0" />
            Save Additional Info
          </button>
        </div>
      )}
    </>
  );
}

// ── AdditionalFieldInput ───────────────────────────────────────────────────────

const inputBase2 =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const labelBase2 =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";

function AdditionalFieldInput({
  field,
  value,
  onChange,
}: {
  field: BrandAdditionalField;
  value: string | string[] | Record<string, string>;
  onChange: (val: string | string[] | Record<string, string>) => void;
}) {
  const strVal = typeof value === "string" ? value : "";
  const arrVal = Array.isArray(value) ? value : [];
  const mapVal =
    value && typeof value === "object" && !Array.isArray(value)
      ? (value as Record<string, string>)
      : {};
  const requiredMark = field.required ? (
    <span className="text-error ml-0.5">*</span>
  ) : null;

  function renderControl() {
    switch (field.controlType as BrandFieldControlType) {
      case "Text Box":
        return (
          <input
            className={inputBase2}
            type="text"
            value={strVal}
            onChange={(e) => onChange(e.target.value)}
            placeholder={field.columnLabel}
            required={field.required}
          />
        );

      case "WYSIWYG (Text Area)":
        return (
          <MultiLangLexicalRichText
            label=""
            defaultValues={mapVal}
            onValuesChange={(next) => onChange(next)}
            placeholders={{
              en: field.columnLabel,
              vn: field.columnLabel,
              zh: field.columnLabel,
              ja: field.columnLabel,
            }}
          />
        );

      case "Checkbox":
        return (
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              className="h-4 w-4 accent-primary"
              checked={strVal === "true"}
              onChange={(e) => onChange(e.target.checked ? "true" : "")}
            />
            <span className="text-xs text-on-surface">{field.columnLabel}</span>
          </label>
        );

      case "Date Picker":
        return (
          <div className="relative">
            <input
              className={`${inputBase2} pr-9`}
              type="date"
              value={strVal}
              onChange={(e) => onChange(e.target.value)}
              required={field.required}
            />
            <IconCalendarToday className="pointer-events-none absolute right-2.5 top-2.5 h-4 w-4 text-on-surface-variant" />
          </div>
        );

      case "Dropdown List":
        return (
          <div className="relative">
            <select
              className={`${inputBase2} appearance-none pr-8`}
              value={strVal}
              onChange={(e) => onChange(e.target.value)}
              required={field.required}
            >
              <option value="">— Select —</option>
              {field.options.map((opt: BrandAdditionalFieldOption) => (
                <option key={opt.value} value={opt.value}>
                  {opt.name}
                </option>
              ))}
            </select>
            <IconChevronDown className="pointer-events-none absolute right-2.5 top-2.5 h-4 w-4 text-on-surface-variant" />
          </div>
        );

      case "Multiple Select":
        return (
          <div className="space-y-1.5">
            {field.options.map((opt: BrandAdditionalFieldOption) => (
              <label key={opt.value} className="flex items-center gap-2 cursor-pointer select-none">
                <input
                  type="checkbox"
                  className="h-4 w-4 accent-primary"
                  checked={arrVal.includes(opt.value)}
                  onChange={(e) => {
                    const next = e.target.checked
                      ? [...arrVal, opt.value]
                      : arrVal.filter((v) => v !== opt.value);
                    onChange(next);
                  }}
                />
                <span className="text-xs text-on-surface">{opt.name}</span>
              </label>
            ))}
            {field.options.length === 0 && (
              <p className="text-xs italic text-on-surface-variant">No options defined.</p>
            )}
          </div>
        );

      default:
        return null;
    }
  }

  return (
    <div className="space-y-1">
      {field.controlType !== "Checkbox" && (
        <label className={labelBase2}>
          {field.columnLabel}
          {requiredMark}
        </label>
      )}
      {renderControl()}
      {field.required &&
        strVal === "" &&
        arrVal.length === 0 &&
        Object.values(mapVal).every((v) => !v?.trim()) && (
          <p className="text-[10px] text-error">Required field</p>
        )}
    </div>
  );
}
