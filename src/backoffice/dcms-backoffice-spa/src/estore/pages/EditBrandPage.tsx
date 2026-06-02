import { type ChangeEvent, useEffect, useRef, useState } from "react";
import type { BrandListRow } from "../brands-columns";
import type {
  BrandAdditionalField,
  BrandAdditionalFieldOption,
  BrandFieldControlType,
} from "./BrandConfigPage";
import { MultiLangInput, MultiLangTextarea } from "../components/MultiLangField";
import { MultiLangLexicalRichText } from "../components/MultiLangLexicalRichText";
import { uploadImage } from "../api/mediaApi";
import {
  IconArrowBack,
  IconCalendarToday,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconDelete,
  IconFactCheck,
  IconGroup,
  IconImage,
  IconInfo,
  IconLightbulb,
  IconLocationOn,
  IconMoreHoriz,
  IconSave,
  IconSearch,
  IconTune,
  IconVisibility,
  IconWarning,
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
  additionalInfo?: string;
  /** Additional fields configured in BrandConfigPage — drives the "Other" tab. */
  additionalFields?: BrandAdditionalField[];
  brandOptions?: Pick<BrandListRow, "code" | "name">[];
  /** Real catalog categories for the Categories picker (stored by id). */
  categoryOptions?: CategoryOption[];
  /** Current tenant identifier — shown read-only on the form. */
  tenantId?: string;
  onBack: () => void;
  /** row = core brand data; additionalInfo = JSON-serialised additional values from "Other" tab.
   *  May be async and reject — the form shows the error instead of a false success. */
  onSave?: (row: BrandListRow, additionalInfo: string) => void | Promise<void>;
};

// ── Category Picker ────────────────────────────────────────────────────────
// `options` are real categories from the catalog API; selection is stored by
// category **id** (stable) while the UI shows category names.
export type CategoryOption = { id: string; name: string };

function CategoryPicker({
  options,
  selected,
  onChange,
}: {
  options: CategoryOption[];
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

  const nameById = new Map(options.map((o) => [o.id, o.name]));
  const filtered = options.filter((o) =>
    o.name.toLowerCase().includes(search.toLowerCase())
  );

  const toggle = (id: string) => {
    onChange(
      selected.includes(id)
        ? selected.filter((c) => c !== id)
        : [...selected, id]
    );
  };

  return (
    <div ref={containerRef} className="relative space-y-1.5">
      {/* Selected chips */}
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((id) => (
            <span
              key={id}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
            >
              {nameById.get(id) ?? id}
              <button
                type="button"
                aria-label={`Remove ${nameById.get(id) ?? id}`}
                className="rounded p-0.5 hover:bg-primary/20 transition-colors"
                onClick={() => toggle(id)}
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
          {options.length === 0 ? (
            <p className="px-4 py-3 text-xs italic text-on-surface-variant">
              No categories defined yet. Create them under eStore → Categories.
            </p>
          ) : filtered.length === 0 ? (
            <p className="px-4 py-3 text-xs italic text-on-surface-variant">
              No categories match "{search}"
            </p>
          ) : (
            filtered.map((opt) => {
              const checked = selected.includes(opt.id);
              return (
                <label
                  key={opt.id}
                  className={`flex cursor-pointer items-center gap-3 px-4 py-2.5 text-xs transition-colors hover:bg-surface-container-high ${
                    checked ? "bg-primary/5 font-semibold text-primary" : "text-on-surface"
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={() => toggle(opt.id)}
                    className="h-3.5 w-3.5 accent-primary shrink-0"
                  />
                  {opt.name}
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

type LangValues = Record<string, string>;

type BrandAdditionalInfo = {
  general?: {
    displayName?: LangValues;
    promoCodePrefix?: string;
    categories?: string[];
    publishFrom?: string;
    publishTo?: string;
    description?: LangValues;
    mobileImageSrc?: string;
  };
  contacts?: {
    officeNo1?: string;
    officeNo2?: string;
    contactPerson?: string;
    contactPersonPhone?: string;
    contactEmails?: string;
    outOfStockEmails?: string;
    headquartersAddress?: string;
  };
  recommendations?: {
    excludedBrandCodes?: string[];
  };
  seo?: {
    metaTitle?: LangValues;
    metaKeywords?: LangValues;
    metaDescription?: LangValues;
  };
  customFields?: Record<string, string | string[]>;
};

const EMPTY_ADDITIONAL_INFO: BrandAdditionalInfo = {};

function parseBrandAdditionalInfo(raw?: string): BrandAdditionalInfo {
  if (!raw?.trim()) return EMPTY_ADDITIONAL_INFO;
  try {
    const parsed = JSON.parse(raw) as BrandAdditionalInfo;
    return parsed && typeof parsed === "object" ? parsed : EMPTY_ADDITIONAL_INFO;
  } catch {
    return EMPTY_ADDITIONAL_INFO;
  }
}

function cleanRecord<T extends Record<string, unknown>>(record: T): Partial<T> {
  return Object.fromEntries(
    Object.entries(record).filter(([, value]) => {
      if (value === "" || value === undefined || value === null) return false;
      if (Array.isArray(value)) return value.length > 0;
      if (typeof value === "object") return Object.keys(value as object).length > 0;
      return true;
    })
  ) as Partial<T>;
}

function firstLang(values?: LangValues, fallback = "") {
  return values?.en?.trim() || Object.values(values ?? {}).find((v) => v.trim()) || fallback;
}

// ── Image upload slot — uploads to the media endpoint and stores the URL ──────
function ImageSlot({
  label,
  src,
  alt,
  folder = "brands",
  maxBytes = 2 * 1024 * 1024,
  onFileSelected,
  onRemove,
}: {
  label: string;
  src?: string;
  alt?: string;
  /** media subfolder, e.g. "brands". */
  folder?: string;
  maxBytes?: number;
  /** Receives the served URL of the uploaded image. */
  onFileSelected: (url: string) => void;
  onRemove: () => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");
  const [uploading, setUploading] = useState(false);

  function openPicker() {
    setError("");
    inputRef.current?.click();
  }

  async function handleFileChange(e: ChangeEvent<HTMLInputElement>) {
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
    setError("");
    setUploading(true);
    try {
      const url = await uploadImage(file, folder);
      onFileSelected(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Upload failed. Please try again.");
    } finally {
      setUploading(false);
    }
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
              disabled={uploading}
              className="w-full py-1.5 bg-surface-container-high text-primary font-bold text-[10px] uppercase tracking-widest rounded hover:bg-surface-container transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={openPicker}
            >
              {uploading ? "Uploading…" : "Replace Image"}
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
              disabled={uploading}
              className="w-full py-1.5 bg-primary text-on-primary font-bold text-[10px] uppercase tracking-widest rounded hover:bg-primary-container transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              onClick={openPicker}
            >
              {uploading ? "Uploading…" : "Choose File"}
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
  additionalInfo = "{}",
  additionalFields = [],
  brandOptions = ALL_BRANDS,
  categoryOptions = [],
  tenantId,
  onBack,
  onSave,
}: Props) {
  const isAdd = mode === "add";
  const parsedAdditionalInfo = parseBrandAdditionalInfo(additionalInfo);

  // UI state
  const [tab, setTab] = useState<EditTab>("general");
  const [actionsOpen, setActionsOpen] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);
  // Error surfaced when onSave rejects (e.g. backend validation) — no more false "success".
  const [saveError, setSaveError] = useState("");
  const [saving, setSaving] = useState(false);
  // null = follow the auto-generated code; string = user typed an explicit override (add mode).
  const [codeOverride, setCodeOverride] = useState<string | null>(null);
  // null = follow the auto-derived promo prefix; string = explicit user value.
  const [promoOverride, setPromoOverride] = useState<string | null>(
    parsedAdditionalInfo.general?.promoCodePrefix ?? null
  );
  const [brandImageSrc, setBrandImageSrc] = useState(logoSrc?.trim() ? logoSrc : "");
  const [mobileImageSrc, setMobileImageSrc] = useState(parsedAdditionalInfo.general?.mobileImageSrc ?? "");
  const actionsRef = useRef<HTMLDivElement>(null);

  // Additional-info field values (keyed by field.id)
  const [additionalValues, setAdditionalValues] = useState<Record<string, string | string[]>>(() =>
    Object.fromEntries(
      additionalFields.map((f) => [
        f.id,
        parsedAdditionalInfo.customFields?.[f.id] ??
          (f.controlType === "Multiple Select" ? [] : ""),
      ])
    )
  );

  const [displayNameValues, setDisplayNameValues] = useState<LangValues>(
    parsedAdditionalInfo.general?.displayName ?? (brandName ? { en: brandName.split(/\s+/).slice(0, 2).join(" ") } : {})
  );
  const [descriptionValues, setDescriptionValues] = useState<LangValues>(
    parsedAdditionalInfo.general?.description ?? {}
  );
  const [publishFrom, setPublishFrom] = useState(parsedAdditionalInfo.general?.publishFrom ?? "");
  const [publishTo, setPublishTo] = useState(parsedAdditionalInfo.general?.publishTo ?? "");

  const [contactValues, setContactValues] = useState<Required<NonNullable<BrandAdditionalInfo["contacts"]>>>(() => ({
    officeNo1: parsedAdditionalInfo.contacts?.officeNo1 ?? "",
    officeNo2: parsedAdditionalInfo.contacts?.officeNo2 ?? "",
    contactPerson: parsedAdditionalInfo.contacts?.contactPerson ?? "",
    contactPersonPhone: parsedAdditionalInfo.contacts?.contactPersonPhone ?? "",
    contactEmails: parsedAdditionalInfo.contacts?.contactEmails ?? "",
    outOfStockEmails: parsedAdditionalInfo.contacts?.outOfStockEmails ?? "",
    headquartersAddress: parsedAdditionalInfo.contacts?.headquartersAddress ?? "",
  }));

  const [seoValues, setSeoValues] = useState<Required<NonNullable<BrandAdditionalInfo["seo"]>>>(() => ({
    metaTitle: parsedAdditionalInfo.seo?.metaTitle ?? {},
    metaKeywords: parsedAdditionalInfo.seo?.metaKeywords ?? {},
    metaDescription: parsedAdditionalInfo.seo?.metaDescription ?? {},
  }));

  function setAdditionalValue(id: string, val: string | string[]) {
    setAdditionalValues((prev) => ({ ...prev, [id]: val }));
  }

  // Active status state (controlled so Save can read it)
  const [isActive, setIsActive] = useState(active);

  useEffect(() => {
    const nextInfo = parseBrandAdditionalInfo(additionalInfo);
    setBrandImageSrc(logoSrc?.trim() ? logoSrc : "");
    setMobileImageSrc(nextInfo.general?.mobileImageSrc ?? "");
    setIsActive(active);
    setPublishFrom(nextInfo.general?.publishFrom ?? "");
    setPublishTo(nextInfo.general?.publishTo ?? "");
    setSelectedCategories(nextInfo.general?.categories ?? []);
    setExcludedBrands(nextInfo.recommendations?.excludedBrandCodes ?? []);
    setPromoOverride(nextInfo.general?.promoCodePrefix ?? null);
    setDisplayNameValues(
      nextInfo.general?.displayName ??
        (brandName ? { en: brandName.split(/\s+/).slice(0, 2).join(" ") } : {})
    );
    setDescriptionValues(nextInfo.general?.description ?? {});
    setContactValues({
      officeNo1: nextInfo.contacts?.officeNo1 ?? "",
      officeNo2: nextInfo.contacts?.officeNo2 ?? "",
      contactPerson: nextInfo.contacts?.contactPerson ?? "",
      contactPersonPhone: nextInfo.contacts?.contactPersonPhone ?? "",
      contactEmails: nextInfo.contacts?.contactEmails ?? "",
      outOfStockEmails: nextInfo.contacts?.outOfStockEmails ?? "",
      headquartersAddress: nextInfo.contacts?.headquartersAddress ?? "",
    });
    setSeoValues({
      metaTitle: nextInfo.seo?.metaTitle ?? {},
      metaKeywords: nextInfo.seo?.metaKeywords ?? {},
      metaDescription: nextInfo.seo?.metaDescription ?? {},
    });
    setAdditionalValues(
      Object.fromEntries(
        additionalFields.map((f) => [
          f.id,
          nextInfo.customFields?.[f.id] ?? (f.controlType === "Multiple Select" ? [] : ""),
        ])
      )
    );
  }, [additionalInfo, additionalFields, logoSrc, mode, brandCode, active, brandName]);

  // Brand Name reactive state (drives auto-populate in add mode)
  const [brandNameInput, setBrandNameInput] = useState(brandName || "");

  useEffect(() => {
    setBrandNameInput(brandName || "");
    setCodeOverride(null);
  }, [brandName, brandCode, mode]);

  // Derive auto-populated values from brand name input
  const words = brandNameInput.replace(/[^a-zA-Z0-9\s]/g, " ").trim().split(/\s+/).filter(Boolean);
  const autoDisplayName = words.slice(0, 2).join(" ");
  const autoCodePrefix = (words[0] ?? "").slice(0, 3).toUpperCase().padEnd(3, "X");
  // Backend requires: 2–5 uppercase letters, dash, 1–6 digits (e.g. CAS-7721).
  // The digit segment must stay numeric — deriving it from the 2nd word produced
  // invalid codes like "AUR-LIVI". Default to 0001; user can override the field.
  const autoCode = brandNameInput ? `${autoCodePrefix}-0001` : "";
  const effectiveCode = codeOverride ?? autoCode;
  const codeIsValid = /^[A-Z]{2,5}-\d{1,6}$/.test(effectiveCode);
  const autoPromo = brandNameInput.replace(/[^a-zA-Z]/g, "").slice(0, 4).toUpperCase() || "";

  // Display Name: seed remounts MultiLangInput only when brand name field loses focus
  // so user edits on other languages are preserved while typing the brand name.
  const [displayNameSeed, setDisplayNameSeed] = useState(0);
  const [confirmedDisplayName, setConfirmedDisplayName] = useState(brandName || "");

  function handleBrandNameBlur() {
    if (isAdd && autoDisplayName !== confirmedDisplayName) {
      setConfirmedDisplayName(autoDisplayName);
      setDisplayNameValues((prev) => ({ ...prev, en: autoDisplayName }));
      setDisplayNameSeed((s) => s + 1);
    }
  }

  // Edit mode: use prop-derived values (static)
  const displayNameGuess = brandName.split(/\s+/).slice(0, 2).join(" ");
  const promoGuess = isAdd
    ? autoPromo
    : brandCode.replace(/[^a-zA-Z0-9]/g, "").slice(0, 4).toUpperCase() || "BRND";
  // User-editable promo prefix; falls back to the auto-derived value until overridden.
  const effectivePromo = promoOverride ?? promoGuess;
  const codeValue = isAdd ? effectiveCode : brandCode;

  // Categories state
  const [selectedCategories, setSelectedCategories] = useState<string[]>(
    parsedAdditionalInfo.general?.categories ?? []
  );

  // Recommendations state
  const [excludedBrands, setExcludedBrands] = useState<string[]>(
    parsedAdditionalInfo.recommendations?.excludedBrandCodes ?? []
  );

  // Available brands for recommendations (exclude self)
  const availableBrands = brandOptions.filter((b) => b.code !== brandCode);

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

  async function handleSave() {
    const code = isAdd ? effectiveCode : brandCode;
    setActionsOpen(false);

    // Client-side guard so the user gets immediate, specific feedback before the round-trip.
    if (isAdd && !codeIsValid) {
      setSaveError(
        `Brand code "${code}" is invalid. Expected 2–5 uppercase letters, a dash, then 1–6 digits (e.g. CAS-7721).`
      );
      return;
    }

    const trimmedName = (brandNameInput || brandName).trim();
    if (!trimmedName) {
      setSaveError("Brand name is required.");
      return;
    }

    const missingRequiredField = additionalFields.find((field) => {
      if (!field.enabled || !field.required) return false;
      const value = additionalValues[field.id];
      return value === "" || value === undefined || (Array.isArray(value) && value.length === 0);
    });
    if (missingRequiredField) {
      setSaveError(`${missingRequiredField.columnLabel || missingRequiredField.fieldName} is required.`);
      return;
    }

    const savedRow: BrandListRow = {
      code,
      name: trimmedName,
      imageSrc: brandImageSrc,
      imageAlt: logoAlt || trimmedName,
      active: isActive,
    };
    // Serialise additional values — filter out empty/default so JSON is clean
    const customFields: Record<string, string | string[]> = {};
    for (const [id, val] of Object.entries(additionalValues)) {
      const isEmpty = val === "" || (Array.isArray(val) && val.length === 0);
      if (!isEmpty) customFields[id] = val;
    }

    const additionalInfoPayload = cleanRecord({
      general: cleanRecord({
        displayName: cleanRecord(displayNameValues),
        promoCodePrefix: effectivePromo,
        categories: selectedCategories,
        publishFrom,
        publishTo,
        description: cleanRecord(descriptionValues),
        mobileImageSrc,
      }),
      contacts: cleanRecord(contactValues),
      recommendations: cleanRecord({ excludedBrandCodes: excludedBrands }),
      seo: cleanRecord({
        metaTitle: cleanRecord(seoValues.metaTitle),
        metaKeywords: cleanRecord(seoValues.metaKeywords),
        metaDescription: cleanRecord(seoValues.metaDescription),
      }),
      customFields,
    }) as BrandAdditionalInfo;
    const additionalInfoStr = JSON.stringify(additionalInfoPayload);
    savedRow.additionalInfo = additionalInfoStr;

    setSaveError("");
    setSaving(true);
    try {
      // Await so a rejected save (e.g. backend validation) surfaces as an error
      // instead of a false "Brand Created!" toast over a phantom row.
      await onSave?.(savedRow, additionalInfoStr);
      setShowSuccessModal(true);
    } catch (e) {
      setSaveError(
        e instanceof Error && e.message
          ? e.message
          : "Could not save the brand. Please try again."
      );
    } finally {
      setSaving(false);
    }
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
                  disabled={saving}
                  className="w-full px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container flex items-center gap-2.5 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  onClick={handleSave}
                >
                  <IconSave className="h-4 w-4 shrink-0 text-primary" />
                  {saving ? "Saving…" : "Save"}
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
                  <input
                    className={`${inputBase} bg-surface-container text-on-surface-variant`}
                    type="text"
                    value={tenantId || "—"}
                    readOnly
                  />
                  <p className="text-[10px] text-on-surface-variant">
                    Brands belong to the current tenant and cannot be reassigned.
                  </p>
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
                    defaultValues={displayNameValues}
                    onValuesChange={setDisplayNameValues}
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
                    {isAdd && autoCode && codeOverride === null && (
                      <span className="ml-1.5 text-[9px] font-normal normal-case text-primary/70 tracking-normal">
                        auto-filled
                      </span>
                    )}
                  </label>
                  <input
                    className={`${inputBase} font-mono ${
                      isAdd && !codeIsValid
                        ? "border-error focus:ring-error"
                        : isAdd && codeOverride === null
                          ? "text-on-surface-variant"
                          : ""
                    }`}
                    type="text"
                    value={codeValue}
                    readOnly={!isAdd}
                    placeholder="e.g. LHG-7721"
                    onChange={(e) =>
                      isAdd && setCodeOverride(e.target.value.toUpperCase())
                    }
                  />
                  {isAdd && !codeIsValid && (
                    <p className="text-[10px] text-error">
                      Use 2–5 letters, a dash, then 1–6 digits (e.g. CAS-7721).
                    </p>
                  )}
                </div>

                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>
                    Promo Code Prefix
                    {promoOverride === null && effectivePromo && (
                      <span className="ml-1.5 text-[9px] font-normal normal-case text-primary/70 tracking-normal">
                        auto-filled
                      </span>
                    )}
                  </label>
                  <input
                    className={`${inputBase} font-mono ${promoOverride === null ? "text-on-surface-variant" : ""}`}
                    type="text"
                    value={effectivePromo}
                    placeholder="e.g. LHG"
                    onChange={(e) => setPromoOverride(e.target.value.toUpperCase())}
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
                    options={categoryOptions}
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
                    value={publishFrom}
                    onChange={(e) => setPublishFrom(e.target.value)}
                  />
                </div>
                <div className="col-span-12 md:col-span-3 space-y-1.5">
                  <label className={labelBase}>Publish To</label>
                  <input
                    className={inputBase}
                    type="date"
                    value={publishTo}
                    onChange={(e) => setPublishTo(e.target.value)}
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
                    defaultValues={descriptionValues}
                    onValuesChange={setDescriptionValues}
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
                  <button type="button" disabled={saving} className={`${btnFooterPrimary} disabled:opacity-50 disabled:cursor-not-allowed`} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    {saving ? "Saving…" : "Save Changes"}
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
                    <input
                      className={inputBase}
                      type="text"
                      value={contactValues.officeNo1}
                      onChange={(e) => setContactValues((prev) => ({ ...prev, officeNo1: e.target.value }))}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Office No 2</label>
                    <input
                      className={inputBase}
                      placeholder="Alternative contact line"
                      type="text"
                      value={contactValues.officeNo2}
                      onChange={(e) => setContactValues((prev) => ({ ...prev, officeNo2: e.target.value }))}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Contact Person</label>
                    <input
                      className={`${inputBase} font-medium`}
                      type="text"
                      value={contactValues.contactPerson}
                      onChange={(e) => setContactValues((prev) => ({ ...prev, contactPerson: e.target.value }))}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Contact Person Phone No</label>
                    <input
                      className={inputBase}
                      type="text"
                      value={contactValues.contactPersonPhone}
                      onChange={(e) => setContactValues((prev) => ({ ...prev, contactPersonPhone: e.target.value }))}
                    />
                  </div>
                  <div className="md:col-span-2 space-y-1.5">
                    <label className={labelBase}>Contact Person&apos;s Email</label>
                    <textarea
                      className={`${inputBase} resize-none`}
                      rows={3}
                      value={contactValues.contactEmails}
                      onChange={(e) => setContactValues((prev) => ({ ...prev, contactEmails: e.target.value }))}
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
                    value={contactValues.outOfStockEmails}
                    onChange={(e) => setContactValues((prev) => ({ ...prev, outOfStockEmails: e.target.value }))}
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
                      value={contactValues.headquartersAddress}
                      onChange={(e) => setContactValues((prev) => ({ ...prev, headquartersAddress: e.target.value }))}
                      placeholder="Enter HQ address"
                    />
                  </div>
                </div>
              </div>

              {!isAdd && (
                <div className="flex justify-end items-center gap-3 pt-8 mt-8 border-t border-outline-variant/20">
                  <button type="button" className={btnFooterGhost} onClick={() => console.info("[EditBrand] Contacts discard")}>
                    Discard Changes
                  </button>
                  <button type="button" disabled={saving} className={`${btnFooterPrimary} disabled:opacity-50 disabled:cursor-not-allowed`} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    {saving ? "Saving…" : "Update Contacts"}
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
                  <button type="button" disabled={saving} className={`${btnFooterPrimary} disabled:opacity-50 disabled:cursor-not-allowed`} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    {saving ? "Saving…" : "Update Recommendations"}
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
                      defaultValues={seoValues.metaTitle}
                      onValuesChange={(values) => setSeoValues((prev) => ({ ...prev, metaTitle: values }))}
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
                      defaultValues={seoValues.metaKeywords}
                      onValuesChange={(values) => setSeoValues((prev) => ({ ...prev, metaKeywords: values }))}
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
                      defaultValues={seoValues.metaDescription}
                      onValuesChange={(values) => setSeoValues((prev) => ({ ...prev, metaDescription: values }))}
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
                        https://your-estore.com › brands › {(brandNameInput || brandName).toLowerCase().replace(/\s+/g, "-") || "brand-name"}
                      </div>
                      <h4 className="text-lg text-[#1a0dab] hover:underline cursor-pointer font-normal mb-1 truncate">
                        {firstLang(seoValues.metaTitle) ? (
                          firstLang(seoValues.metaTitle)
                        ) : isAdd ? (
                          <span className="text-outline-variant italic">Meta title will appear here</span>
                        ) : (
                          "T - 10.Deep | TANGS Singapore"
                        )}
                      </h4>
                      <p className="text-[13px] text-[#4d5156] leading-snug line-clamp-3">
                        {firstLang(seoValues.metaDescription) ? (
                          firstLang(seoValues.metaDescription)
                        ) : isAdd ? (
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
                  <button type="button" disabled={saving} className={`${btnFooterPrimary} disabled:opacity-50 disabled:cursor-not-allowed`} onClick={handleSave}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    {saving ? "Saving…" : "Update SEO Settings"}
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
              saving={saving}
            />
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

      {/* ── Error modal ──────────────────────────────────────────────────── */}
      {saveError && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setSaveError("")}
        >
          <div
            className="bg-surface-container-lowest rounded-2xl shadow-2xl p-8 max-w-sm w-full mx-4 text-center"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="w-16 h-16 rounded-full bg-error-container/30 flex items-center justify-center mx-auto mb-4">
              <IconWarning className="h-8 w-8 text-error" />
            </div>
            <h3 className="text-lg font-bold text-on-surface mb-2">
              {isAdd ? "Couldn’t create brand" : "Couldn’t save changes"}
            </h3>
            <p className="text-sm text-on-surface-variant mb-6">{saveError}</p>
            <div className="flex justify-center">
              <button
                type="button"
                className="px-5 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest hover:bg-primary-container transition-colors"
                onClick={() => setSaveError("")}
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

// ── OtherTab ───────────────────────────────────────────────────────────────────

const SECTION_ORDER = [
  "General Information",
  "Contacts",
  "Product Recommendations",
  "SEO Configuration",
] as const;

function OtherTab({
  fields,
  values,
  onChange,
  isAdd,
  onSave,
  saving,
}: {
  fields: BrandAdditionalField[];
  values: Record<string, string | string[]>;
  onChange: (id: string, val: string | string[]) => void;
  isAdd: boolean;
  onSave: () => void;
  saving: boolean;
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
              {rows.map((field) => (
                <AdditionalFieldInput
                  key={field.id}
                  field={field}
                  value={values[field.id] ?? (field.controlType === "Multiple Select" ? [] : "")}
                  onChange={(val) => onChange(field.id, val)}
                />
              ))}
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
            disabled={saving}
            className="px-6 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-primary/20 hover:scale-[1.02] active:scale-95 transition-all flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100"
            onClick={onSave}
          >
            <IconSave className="h-4 w-4 shrink-0" />
            {saving ? "Saving…" : "Save Additional Info"}
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
  value: string | string[];
  onChange: (val: string | string[]) => void;
}) {
  const strVal = typeof value === "string" ? value : "";
  const arrVal = Array.isArray(value) ? value : [];
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
          <textarea
            className={`${inputBase2} resize-y min-h-[96px]`}
            value={strVal}
            onChange={(e) => onChange(e.target.value)}
            placeholder={field.columnLabel}
            required={field.required}
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
      {field.required && strVal === "" && arrVal.length === 0 && (
        <p className="text-[10px] text-error">Required field</p>
      )}
    </div>
  );
}
