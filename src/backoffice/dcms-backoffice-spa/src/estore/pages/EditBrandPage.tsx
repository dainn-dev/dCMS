import { useEffect, useMemo, useRef, useState } from "react";
import { langLabel, useUmbracoLanguages } from "../useUmbracoLanguages";
import {
  IconArrowBack,
  IconCalendarToday,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconDelete,
  IconFactCheck,
  IconFormatBold,
  IconFormatItalic,
  IconFormatListBulleted,
  IconFormatUnderlined,
  IconGroup,
  IconHistory,
  IconImage,
  IconInfo,
  IconLightbulb,
  IconLink,
  IconLocationOn,
  IconMap,
  IconMoreHoriz,
  IconSave,
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
type EditTab = "general" | "contacts" | "seo" | "recommendations";
type LangRecord = Record<string, string>;

type Props = {
  mode: "add" | "edit";
  brandCode?: string;
  brandName?: string;
  active?: boolean;
  logoSrc?: string;
  logoAlt?: string;
  onBack: () => void;
};

// ── Mock brand list for Recommendations tab ────────────────────────────────
const ALL_BRANDS = [
  { code: "CAS-7721", name: "Luxe Heritage Group" },
  { code: "VEL-4490", name: "Velocity Tech Systems" },
  { code: "NOM-1022", name: "Nomad Consulting Ltd." },
  { code: "AUR-5501", name: "Aura Essentials" },
];

// ── Image upload slot ──────────────────────────────────────────────────────
function ImageSlot({
  label,
  src,
  alt,
  onRemove,
  onReplace,
}: {
  label: string;
  src?: string;
  alt?: string;
  onRemove: () => void;
  onReplace: () => void;
}) {
  return (
    <div className="space-y-1.5">
      <label className={labelBase}>{label}</label>
      <div className="border-2 border-dashed border-outline-variant/40 rounded-xl p-3 flex flex-col items-center justify-center bg-surface-container-low/20">
        {src ? (
          <>
            <div className="relative w-full aspect-video bg-white rounded-lg shadow-sm overflow-hidden mb-3 border border-outline-variant/10">
              <img className="w-full h-full object-cover" alt={alt ?? label} src={src} />
              <button
                type="button"
                className="absolute top-1.5 right-1.5 p-1 bg-error text-white rounded-full shadow active:scale-90 transition-transform"
                aria-label="Remove image"
                onClick={onRemove}
              >
                <IconDelete className="h-3 w-3" />
              </button>
            </div>
            <button
              type="button"
              className="w-full py-1.5 bg-surface-container-high text-primary font-bold text-[10px] uppercase tracking-widest rounded hover:bg-surface-container transition-colors"
              onClick={onReplace}
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
              onClick={onReplace}
            >
              Choose File
            </button>
          </>
        )}
      </div>
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
  onBack,
}: Props) {
  const isAdd = mode === "add";

  // UI state
  const [tab, setTab] = useState<EditTab>("general");
  const [actionsOpen, setActionsOpen] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);

  // SEO state
  const umbracoLangs = useUmbracoLanguages();
  const languages = useMemo(
    () => (umbracoLangs.status !== "loading" ? umbracoLangs.languages : []),
    [umbracoLangs]
  );
  const defaultIso = useMemo(
    () => languages.find((l) => l.isDefault)?.isoCode ?? languages[0]?.isoCode ?? "en-US",
    [languages]
  );

  const [seoLang, setSeoLang] = useState<string>("");
  // Sync seoLang to Umbraco default once languages are loaded
  useEffect(() => {
    if (languages.length > 0 && !seoLang) setSeoLang(defaultIso);
  }, [defaultIso, languages, seoLang]);

  const [metaTitles, setMetaTitles] = useState<LangRecord>({});
  const [metaKeywords, setMetaKeywords] = useState<LangRecord>({});
  const [metaDescriptions, setMetaDescriptions] = useState<LangRecord>({});
  // Pre-populate edit-mode defaults for the Umbraco default language
  useEffect(() => {
    if (!defaultIso || isAdd) return;
    setMetaTitles((p) => ({ ...p, [defaultIso]: p[defaultIso] ?? "T - 10.Deep | TANGS Singapore" }));
    setMetaDescriptions((p) => ({
      ...p,
      [defaultIso]: p[defaultIso] ?? "T - 10.Deep demo ecommerce sirius brand meta description. Experience premium lifestyle curation with global delivery options.",
    }));
  // Only on first load
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [defaultIso]);

  // Recommendations state
  const [excludedBrands, setExcludedBrands] = useState<string[]>([]);

  // Auto-populate helpers (edit mode: derived from existing code/name)
  const displayNameGuess = brandName.split(/\s+/).slice(0, 2).join(" ");
  const promoGuess =
    brandCode.replace(/[^a-zA-Z0-9]/g, "").slice(0, 4).toUpperCase() || "BRND";

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
    setActionsOpen(false);
    setShowSuccessModal(true);
    console.info("[EditBrand] Save", { mode, brandCode, brandName });
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
                {active ? (
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
              onClick={() => console.info("[EditBrand] History (placeholder)")}
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
                  <input className={inputBase} type="text" defaultValue={brandName} placeholder="Enter brand name" />
                </div>

                <div className="col-span-12 md:col-span-4 space-y-1.5">
                  <label className={labelBase}>Display Name</label>
                  <input className={inputBase} type="text" defaultValue={isAdd ? "" : displayNameGuess} placeholder="Auto-filled from Brand Name" />
                </div>

                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Brand Code</label>
                  <input className={`${inputBase} font-mono`} type="text" defaultValue={brandCode} placeholder="e.g. LHG-7721" />
                </div>

                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Promo Code Prefix</label>
                  <input className={inputBase} type="text" defaultValue={isAdd ? "" : promoGuess} placeholder="e.g. LHG" />
                </div>

                <div className="col-span-12 md:col-span-6 space-y-1.5">
                  <label className={labelBase}>Categories</label>
                  <div className="flex flex-wrap gap-2 p-2 bg-surface border border-outline-variant/20 rounded-md min-h-[38px] items-center">
                    {(!isAdd ? ["Luxury", "Fragrance"] : []).map((c) => (
                      <span
                        key={c}
                        className="inline-flex items-center gap-1.5 px-2 py-0.5 bg-primary/10 text-primary text-[10px] font-bold uppercase rounded-full"
                      >
                        {c}
                        <button
                          type="button"
                          className="p-0.5 rounded hover:bg-primary/20"
                          aria-label={`Remove ${c}`}
                          onClick={() => console.info("[EditBrand] Remove category", c)}
                        >
                          <IconClose className="h-3 w-3" />
                        </button>
                      </span>
                    ))}
                    <button
                      type="button"
                      className="text-primary text-[10px] font-bold uppercase ml-1 hover:underline"
                      onClick={() => console.info("[EditBrand] Add category (placeholder)")}
                    >
                      + Add Category
                    </button>
                  </div>
                </div>
              </div>

              {/* Scheduling */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12">
                  <h3 className={sectionTitle}>Scheduling</h3>
                </div>
                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Publish From</label>
                  <div className="relative">
                    <input className={`${inputBase} pr-10`} type="text" defaultValue={isAdd ? "" : "2024-05-15"} placeholder="YYYY-MM-DD" />
                    <IconCalendarToday className="absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant pointer-events-none" />
                  </div>
                </div>
                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Publish To</label>
                  <div className="relative">
                    <input className={`${inputBase} pr-10`} type="text" defaultValue={isAdd ? "" : "2025-05-15"} placeholder="YYYY-MM-DD" />
                    <IconCalendarToday className="absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant pointer-events-none" />
                  </div>
                </div>
              </div>

              {/* Marketing Content */}
              <div className="grid grid-cols-12 gap-x-8 gap-y-6">
                <div className="col-span-12">
                  <h3 className={sectionTitle}>Marketing Content</h3>
                </div>

                {/* Description */}
                <div className="col-span-12 md:col-span-8 space-y-1.5">
                  <label className={labelBase}>Description</label>
                  <div className="border border-outline-variant/20 rounded-md overflow-hidden bg-surface">
                    <div className="flex items-center gap-1 p-2 border-b border-outline-variant/20 bg-surface-container-low">
                      {[
                        { Icon: IconFormatBold,          label: "Bold"      },
                        { Icon: IconFormatItalic,         label: "Italic"    },
                        { Icon: IconFormatUnderlined,     label: "Underline" },
                      ].map(({ Icon, label }) => (
                        <button key={label} type="button" className="p-1.5 hover:bg-surface-container-high rounded" aria-label={label} onClick={() => console.info("[EditBrand] Toolbar", label)}>
                          <Icon className="h-4 w-4" />
                        </button>
                      ))}
                      <div className="w-px h-4 bg-outline-variant/40 mx-1" />
                      {[
                        { Icon: IconFormatListBulleted, label: "List"  },
                        { Icon: IconLink,               label: "Link"  },
                        { Icon: IconImage,              label: "Image" },
                      ].map(({ Icon, label }) => (
                        <button key={label} type="button" className="p-1.5 hover:bg-surface-container-high rounded" aria-label={label} onClick={() => console.info("[EditBrand] Toolbar", label)}>
                          <Icon className="h-4 w-4" />
                        </button>
                      ))}
                    </div>
                    <div className="p-4 min-h-[160px] text-sm text-on-surface">
                      {!isAdd && (
                        <p>
                          Velvet Aura Luxury represents the pinnacle of olfactory craftsmanship. Established in 2024,
                          our mission is to curate sensory experiences that transcend the ordinary.
                        </p>
                      )}
                    </div>
                  </div>
                </div>

                {/* Brand Image + Mobile Image */}
                <div className="col-span-12 md:col-span-4 space-y-4">
                  <ImageSlot
                    label="Brand Image"
                    src={logoSrc || undefined}
                    alt={logoAlt || undefined}
                    onRemove={() => console.info("[EditBrand] Remove brand image (placeholder)")}
                    onReplace={() => console.info("[EditBrand] Replace brand image (placeholder)")}
                  />
                  <ImageSlot
                    label="Mobile Image"
                    src={undefined}
                    onRemove={() => console.info("[EditBrand] Remove mobile image (placeholder)")}
                    onReplace={() => console.info("[EditBrand] Replace mobile image (placeholder)")}
                  />
                </div>
              </div>

              {/* Footer */}
              <div className="flex justify-end items-center gap-3 pt-8 mt-8 border-t border-outline-variant/20">
                <button type="button" className={btnFooterGhost} onClick={onBack}>
                  Cancel
                </button>
                <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                  <IconSave className="h-4 w-4 shrink-0" />
                  {isAdd ? "Create Brand" : "Save Changes"}
                </button>
              </div>
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

              {/* Footer */}
              <div className="flex justify-end items-center gap-3 pt-8 mt-8 border-t border-outline-variant/20">
                <button type="button" className={btnFooterGhost} onClick={() => console.info("[EditBrand] Contacts discard")}>
                  Discard Changes
                </button>
                <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                  <IconSave className="h-4 w-4 shrink-0" />
                  {isAdd ? "Save & Continue" : "Update Contacts"}
                </button>
              </div>
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

              {/* Footer */}
              <div className="flex justify-end items-center gap-3 pt-8 mt-8 border-t border-outline-variant/20">
                <button type="button" className={btnFooterGhost} onClick={() => setExcludedBrands([])}>
                  Reset
                </button>
                <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                  <IconSave className="h-4 w-4 shrink-0" />
                  {isAdd ? "Save & Continue" : "Update Recommendations"}
                </button>
              </div>
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
                    {/* Meta Title */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className={labelBase}>Meta Title</label>
                        <div className="flex items-center gap-0.5">
                          {umbracoLangs.status === "loading" ? (
                            <span className="text-[10px] text-on-surface-variant/40 italic">Loading…</span>
                          ) : (
                            languages.map((lang) => (
                              <button
                                key={lang.isoCode}
                                type="button"
                                title={lang.name}
                                onClick={() => setSeoLang(lang.isoCode)}
                                className={`rounded px-1.5 py-0.5 text-[10px] font-bold transition-colors ${
                                  seoLang === lang.isoCode
                                    ? "bg-primary text-on-primary"
                                    : "text-on-surface-variant/50 hover:bg-surface-container-high hover:text-on-surface-variant"
                                }`}
                              >
                                {langLabel(lang.isoCode)}
                              </button>
                            ))
                          )}
                        </div>
                      </div>
                      <input
                        className={inputBase}
                        type="text"
                        value={metaTitles[seoLang] ?? ""}
                        onChange={(e) =>
                          setMetaTitles((prev) => ({ ...prev, [seoLang]: e.target.value }))
                        }
                        placeholder="Enter the text that will appear on the browser's title bar and tab"
                      />
                      <p className={`text-xs mt-1 flex justify-end ${(metaTitles[seoLang] ?? "").length > 60 ? "text-error font-semibold" : "text-on-surface-variant"}`}>
                        {(metaTitles[seoLang] ?? "").length} / 60
                      </p>
                    </div>

                    {/* Meta Keywords */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className={labelBase}>Meta Keywords</label>
                        <div className="flex items-center gap-0.5">
                          {umbracoLangs.status === "loading" ? (
                            <span className="text-[10px] text-on-surface-variant/40 italic">Loading…</span>
                          ) : (
                            languages.map((lang) => (
                              <button
                                key={lang.isoCode}
                                type="button"
                                title={lang.name}
                                onClick={() => setSeoLang(lang.isoCode)}
                                className={`rounded px-1.5 py-0.5 text-[10px] font-bold transition-colors ${
                                  seoLang === lang.isoCode
                                    ? "bg-primary text-on-primary"
                                    : "text-on-surface-variant/50 hover:bg-surface-container-high hover:text-on-surface-variant"
                                }`}
                              >
                                {langLabel(lang.isoCode)}
                              </button>
                            ))
                          )}
                        </div>
                      </div>
                      <input
                        className={inputBase}
                        type="text"
                        value={metaKeywords[seoLang] ?? ""}
                        onChange={(e) =>
                          setMetaKeywords((prev) => ({ ...prev, [seoLang]: e.target.value }))
                        }
                        placeholder="Enter keywords separated by commas…"
                      />
                      <p className="text-xs text-on-surface-variant mt-1">
                        List relevant keywords to help search engines find this brand.
                      </p>
                    </div>

                    {/* Meta Description */}
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className={labelBase}>Meta Description</label>
                        <div className="flex items-center gap-0.5">
                          {umbracoLangs.status === "loading" ? (
                            <span className="text-[10px] text-on-surface-variant/40 italic">Loading…</span>
                          ) : (
                            languages.map((lang) => (
                              <button
                                key={lang.isoCode}
                                type="button"
                                title={lang.name}
                                onClick={() => setSeoLang(lang.isoCode)}
                                className={`rounded px-1.5 py-0.5 text-[10px] font-bold transition-colors ${
                                  seoLang === lang.isoCode
                                    ? "bg-primary text-on-primary"
                                    : "text-on-surface-variant/50 hover:bg-surface-container-high hover:text-on-surface-variant"
                                }`}
                              >
                                {langLabel(lang.isoCode)}
                              </button>
                            ))
                          )}
                        </div>
                      </div>
                      <textarea
                        className={`${inputBase} resize-none`}
                        rows={4}
                        value={metaDescriptions[seoLang] ?? ""}
                        onChange={(e) =>
                          setMetaDescriptions((prev) => ({ ...prev, [seoLang]: e.target.value }))
                        }
                        placeholder="Provide a brief overview of the brand that will be displayed on search results listings"
                      />
                      <p className={`text-xs mt-1 flex justify-end ${(metaDescriptions[seoLang] ?? "").length > 160 ? "text-error font-semibold" : "text-on-surface-variant"}`}>
                        {(metaDescriptions[seoLang] ?? "").length} / 160
                      </p>
                    </div>
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
                        {(metaTitles[seoLang] ?? "") || <span className="text-outline-variant italic">Meta title will appear here</span>}
                      </h4>
                      <p className="text-[13px] text-[#4d5156] leading-snug line-clamp-3">
                        {(metaDescriptions[seoLang] ?? "") || <span className="italic text-outline-variant">Meta description will appear here</span>}
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

              {/* Footer */}
              <div className="flex justify-end gap-3 items-center pt-8 mt-8 border-t border-outline-variant/20">
                <button type="button" className={btnFooterGhost} onClick={() => console.info("[EditBrand] SEO discard")}>
                  Discard Changes
                </button>
                <button type="button" className={btnFooterPrimary} onClick={handleSave}>
                  <IconSave className="h-4 w-4 shrink-0" />
                  {isAdd ? "Save & Continue" : "Update SEO Settings"}
                </button>
              </div>
            </>
          )}
        </section>
      </div>

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
