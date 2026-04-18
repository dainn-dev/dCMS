import { useCallback, useEffect, useRef, useState, type ChangeEvent } from "react";
import { MultiLangInput, MultiLangTextarea } from "../components/MultiLangField";
import {
  IconAdd,
  IconArrowBack,
  IconAutoFixHigh,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconCloudUpload,
  IconCrop,
  IconDelete,
  IconEditNote,
  IconFactCheck,
  IconFolder,
  IconFormatBold,
  IconFormatItalic,
  IconFormatListBulleted,
  IconFormatUnderlined,
  IconImage,
  IconInfo,
  IconLayers,
  IconLightbulb,
  IconLink,
  IconOpenInNew,
  IconOpenWith,
  IconRestartAlt,
  IconRotateCcw,
  IconRotateCw,
  IconSave,
  IconSearch,
  IconTextFields,
  IconTune,
  IconVisibility,
} from "../../orders/icons";
import type { ProductListRow } from "./ProductsPage";

// ── Style tokens ────────────────────────────────────────────────────────────
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

// ── Types ───────────────────────────────────────────────────────────────────
type EditTab = "general" | "product-page" | "recommendations";

type ItemType =
  | "physical"
  | "membership-points"
  | "membership-voucher"
  | "membership"
  | "stored-value-card";

type ProductOption = {
  id: string;
  type: "textbox" | "dropdown" | "bundle";
  label: string;
  required: boolean;
  dropdownOptions: string;
};

type ProductImage = {
  id: string;
  name: string;
  alt: string;
  /** bg colour used when `src` is missing */
  color: string;
  /** data URL (image) or blob: URL (video) */
  src?: string;
  isVideo?: boolean;
};

type Props = {
  mode: "add" | "edit";
  product?: ProductListRow;
  onBack: () => void;
};

// ── Mock data ───────────────────────────────────────────────────────────────
const MOCK_STORES = ["Main Store", "Outlet A", "Outlet B", "Online Store", "Pop-up Boutique"];
const MOCK_ATTRIBUTES = ["Color", "Size", "Material", "Weight", "Dimensions", "Style", "Pattern"];
const MOCK_BRANDS = ["Cronos Ltd.", "Sonic Bloom", "Velocity Sport", "Optic Visions", "Luxe Heritage Group"];
const MOCK_CATEGORIES = ["Electronics", "Audio", "Footwear", "Photography", "Timepieces", "Luxury", "Athletics"];
const MOCK_CARD_TIERS = ["Gold", "Silver", "Platinum", "Bronze"];
const MOCK_CARD_TYPES = ["Standard", "Premium", "Elite"];

// ── Shared: SearchablePicker (Brand / Category / Attributes) ────────────────
function SearchablePicker({
  label,
  options,
  selected,
  onChange,
  placeholder = "Type to search...",
  multi = true,
}: {
  label: string;
  options: string[];
  selected: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
  multi?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onDown(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, []);

  const filtered = options.filter((o) =>
    o.toLowerCase().includes(search.toLowerCase())
  );

  const toggle = (item: string) => {
    if (!multi) {
      onChange(selected.includes(item) ? [] : [item]);
      setOpen(false);
      return;
    }
    onChange(
      selected.includes(item)
        ? selected.filter((s) => s !== item)
        : [...selected, item]
    );
  };

  return (
    <div className="space-y-1.5">
      <label className={labelBase}>{label}</label>
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
                  aria-label={`Remove ${s}`}
                  className="rounded p-0.5 hover:bg-primary/20"
                  onClick={() => toggle(s)}
                >
                  <IconClose className="h-2.5 w-2.5" />
                </button>
              </span>
            ))}
          </div>
        )}
        <div className="relative">
          <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
          <input
            type="text"
            className={`${inputBase} pl-8`}
            placeholder={placeholder}
            value={search}
            onFocus={() => setOpen(true)}
            onChange={(e) => { setSearch(e.target.value); setOpen(true); }}
          />
        </div>
        {open && (
          <div className="absolute left-0 right-0 top-full z-30 mt-1 max-h-48 overflow-y-auto rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
            {filtered.length === 0 ? (
              <p className="px-4 py-3 text-xs italic text-on-surface-variant">No matches for "{search}"</p>
            ) : (
              filtered.map((item) => {
                const checked = selected.includes(item);
                return (
                  <label
                    key={item}
                    className={`flex cursor-pointer items-center gap-3 px-4 py-2.5 text-xs transition-colors hover:bg-surface-container-high ${
                      checked ? "bg-primary/5 font-semibold text-primary" : "text-on-surface"
                    }`}
                  >
                    {multi && (
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggle(item)}
                        className="h-3.5 w-3.5 accent-primary shrink-0"
                      />
                    )}
                    {item}
                  </label>
                );
              })
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// ── Component ───────────────────────────────────────────────────────────────
export function EditProductPage({ mode, product, onBack }: Props) {
  const isAdd = mode === "add";

  // ── UI state ──────────────────────────────────────────────────────────────
  const [tab, setTab] = useState<EditTab>("general");
  const [actionsOpen, setActionsOpen] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);
  const [successMessage, setSuccessMessage] = useState("");
  const actionsRef = useRef<HTMLDivElement>(null);

  // ── Image state ───────────────────────────────────────────────────────────
  const MOCK_IMAGES: ProductImage[] = isAdd
    ? []
    : [
        { id: "img-1", name: "product-front.jpg",    alt: "Front view of the product", color: "#c7d2fe" },
        { id: "img-2", name: "product-side.jpg",     alt: "Side view of the product",  color: "#bbf7d0" },
        { id: "img-3", name: "product-detail.png",   alt: "Close-up detail shot",      color: "#fde68a" },
      ];
  const [images, setImages] = useState<ProductImage[]>(MOCK_IMAGES);
  const [altTextModal, setAltTextModal] = useState<{ open: boolean; imageId: string; value: string }>(
    { open: false, imageId: "", value: "" }
  );
  const [deleteConfirm, setDeleteConfirm] = useState<{ open: boolean; imageId: string }>(
    { open: false, imageId: "" }
  );
  const [editorImageId, setEditorImageId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const imagesRef = useRef<ProductImage[]>([]);
  const [configureMainOpen, setConfigureMainOpen] = useState(false);
  const [variantNotice, setVariantNotice] = useState("");
  const [galleryNotice, setGalleryNotice] = useState("");

  const MAX_MEDIA_BYTES = 40 * 1024 * 1024;

  const ingestFiles = useCallback((files: FileList | File[]) => {
    for (const file of Array.from(files)) {
      if (!file.type.startsWith("image/") && !file.type.startsWith("video/")) continue;
      if (file.size > MAX_MEDIA_BYTES) continue;
      const id = `img-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
      if (file.type.startsWith("image/")) {
        const reader = new FileReader();
        reader.onload = () => {
          const src = reader.result;
          if (typeof src !== "string") return;
          setImages((prev) => [
            ...prev,
            { id, name: file.name, alt: "", color: "#e5e7eb", src, isVideo: false },
          ]);
        };
        reader.readAsDataURL(file);
      } else {
        const src = URL.createObjectURL(file);
        setImages((prev) => [
          ...prev,
          { id, name: file.name, alt: "", color: "#0f172a", src, isVideo: true },
        ]);
      }
    }
  }, []);

  imagesRef.current = images;
  useEffect(
    () => () => {
      imagesRef.current.forEach((img) => {
        if (img.src?.startsWith("blob:")) URL.revokeObjectURL(img.src);
      });
    },
    []
  );

  useEffect(() => {
    if (!variantNotice) return;
    const t = setTimeout(() => setVariantNotice(""), 4000);
    return () => clearTimeout(t);
  }, [variantNotice]);

  useEffect(() => {
    if (!galleryNotice) return;
    const t = setTimeout(() => setGalleryNotice(""), 4000);
    return () => clearTimeout(t);
  }, [galleryNotice]);

  // ── General tab state ─────────────────────────────────────────────────────
  const [itemType, setItemType] = useState<ItemType>("physical");
  const [selectedStores, setSelectedStores] = useState<string[]>(
    isAdd ? [] : ["Main Store", "Online Store"]
  );
  const [selectedAttributes, setSelectedAttributes] = useState<string[]>(
    isAdd ? [] : ["Color", "Size"]
  );
  const [productOptions, setProductOptions] = useState<ProductOption[]>([]);

  // Fulfilment flags
  const [enableCollection, setEnableCollection] = useState(!isAdd);
  const [enableDelivery, setEnableDelivery] = useState(!isAdd);
  const [enableOverseasDelivery, setEnableOverseasDelivery] = useState(false);
  const [shippingClause, setShippingClause] = useState(false);
  const [inventorySync, setInventorySync] = useState(false);
  const [priceSync, setPriceSync] = useState(false);
  const [allowOversell, setAllowOversell] = useState(false);

  // ── Product Page tab state ────────────────────────────────────────────────
  const [selectedBrands, setSelectedBrands] = useState<string[]>(
    isAdd ? [] : [product?.brand ?? ""]
  );
  const [selectedCategories, setSelectedCategories] = useState<string[]>(
    isAdd ? [] : (product?.categoryPath ? [product.categoryPath.split(" > ")[0]] : [])
  );
  const [hiddenProduct, setHiddenProduct] = useState(false);
  const [recommendSimilar, setRecommendSimilar] = useState(!isAdd);
  const [restockNotification, setRestockNotification] = useState(false);
  const [useTitleForUrl, setUseTitleForUrl] = useState(false);
  const [useTitleForPageTitle, setUseTitleForPageTitle] = useState(false);

  // ── Recommendations tab state ─────────────────────────────────────────────
  const otherProducts = [
    { id: "1", name: "Vantage Series 5 Watch", sku: "WT-550-B" },
    { id: "2", name: "Echo-Noise Headphones", sku: "AU-102-S" },
    { id: "3", name: "SwiftRun Pro Z", sku: "FT-99-R" },
    { id: "4", name: "InstaCam Retro X", sku: "CM-42-P" },
  ].filter((p) => p.id !== product?.id);
  const [recommendedIds, setRecommendedIds] = useState<string[]>([]);

  // ── Close actions dropdown on outside click ───────────────────────────────
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node))
        setActionsOpen(false);
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  // ── Handlers ──────────────────────────────────────────────────────────────
  function handleSave(message: string) {
    setActionsOpen(false);
    setSuccessMessage(message);
    setShowSuccessModal(true);
  }

  function addProductOption() {
    setProductOptions((prev) => [
      ...prev,
      { id: crypto.randomUUID(), type: "textbox", label: "", required: false, dropdownOptions: "" },
    ]);
  }

  function removeProductOption(id: string) {
    setProductOptions((prev) => prev.filter((o) => o.id !== id));
  }

  function updateProductOption(id: string, patch: Partial<ProductOption>) {
    setProductOptions((prev) =>
      prev.map((o) => (o.id === id ? { ...o, ...patch } : o))
    );
  }

  function toggleRecommended(id: string, checked: boolean) {
    setRecommendedIds((prev) =>
      checked ? [...prev, id] : prev.filter((r) => r !== id)
    );
  }

  // ── Tab nav config ────────────────────────────────────────────────────────
  const tabs: { id: EditTab; label: string; Icon: typeof IconInfo }[] = [
    { id: "general",         label: "General",          Icon: IconInfo        },
    { id: "product-page",   label: "Product Page",     Icon: IconOpenInNew   },
    { id: "recommendations", label: "Recommendations",  Icon: IconLayers      },
  ];

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Products
          </button>
          <h2 className="flex flex-wrap items-center gap-3 text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? (
              <span>Add Product</span>
            ) : (
              <>
                <span>Edit Product:</span>
                <span className="text-primary-container">{product?.name}</span>
                <span className="rounded-full bg-outline-variant/20 px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
                  {product?.statusLabel}
                </span>
              </>
            )}
          </h2>
        </div>

        <div className="flex items-center gap-3">
          {/* Actions dropdown */}
          <div className="relative" ref={actionsRef}>
            <button
              type="button"
              className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container"
              onClick={() => setActionsOpen((o) => !o)}
            >
              Actions
              <IconChevronDown className="h-4 w-4 shrink-0" />
            </button>
            {actionsOpen && (
              <div className="absolute right-0 top-full z-20 mt-1 w-56 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
                {[
                  { label: "Save and Approve",           icon: IconCheckCircle, nav: false },
                  { label: "Save and Send for Approval", icon: IconSave,        nav: false },
                  { label: "Save and Archive",           icon: IconFolder,      nav: false },
                  { label: "Save Changes",               icon: IconSave,        nav: false },
                ].map(({ label, icon: Icon }) => (
                  <button
                    key={label}
                    type="button"
                    className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface transition-colors hover:bg-surface-container"
                    onClick={() => handleSave(label)}
                  >
                    <Icon className="h-4 w-4 shrink-0 text-primary" />
                    {label}
                  </button>
                ))}
                <div className="mx-3 my-1 border-t border-outline-variant/15" />
                {[
                  { label: "Preview Product",  icon: IconVisibility },
                  { label: "View Product Page", icon: IconOpenInNew  },
                ].map(({ label, icon: Icon }) => (
                  <button
                    key={label}
                    type="button"
                    className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface transition-colors hover:bg-surface-container"
                    onClick={() => {
                      setActionsOpen(false);
                      console.info(`[EditProduct] ${label} (placeholder navigation)`);
                    }}
                  >
                    <Icon className="h-4 w-4 shrink-0 text-primary" />
                    {label}
                  </button>
                ))}
                <div className="mx-3 my-1 border-t border-outline-variant/15" />
                {[
                  { label: "Copy to New Product", icon: IconFactCheck },
                  { label: "Copy to New Variant", icon: IconLayers    },
                ].map(({ label, icon: Icon }) => (
                  <button
                    key={label}
                    type="button"
                    className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface transition-colors hover:bg-surface-container"
                    onClick={() => handleSave(label)}
                  >
                    <Icon className="h-4 w-4 shrink-0 text-primary" />
                    {label}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Body: sidebar + form ─────────────────────────────────────────── */}
      <div className="flex min-h-0 flex-1 flex-col gap-8 p-6 lg:flex-row">

        {/* Sidebar tab nav */}
        <aside className="flex w-full shrink-0 flex-row gap-1 overflow-x-auto lg:w-64 lg:flex-col lg:overflow-visible">
          {tabs.map(({ id, label, Icon }) => {
            const isActive = tab === id;
            return (
              <button
                key={id}
                type="button"
                onClick={() => setTab(id)}
                className={`flex items-center gap-3 whitespace-nowrap border-l-4 px-4 py-3 text-left text-sm font-medium transition-colors rounded-none lg:rounded-sm ${
                  isActive
                    ? "border-primary bg-surface-container-lowest font-bold text-primary shadow-sm"
                    : "border-transparent text-on-surface-variant hover:bg-surface-container-high"
                }`}
              >
                <Icon className="h-5 w-5 shrink-0" />
                {label}
              </button>
            );
          })}
        </aside>

        {/* Tab content */}
        <section
          className="min-h-0 flex-1 space-y-8 overflow-y-auto rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-8 shadow-[0_4px_24px_rgba(40,23,22,0.04)]"
          aria-label="Product form"
        >

          {/* ── GENERAL TAB ─────────────────────────────────────────── */}
          {tab === "general" && (
            <>
              {/* Identifiers */}
              <div>
                <h3 className={sectionTitle}>Identifiers</h3>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-4">
                  <div className="space-y-1.5">
                    <label className={labelBase}>UPC <span className="text-[9px] font-normal normal-case text-primary/70">system-generated</span></label>
                    <input
                      className={`${inputBase} font-mono text-on-surface-variant`}
                      readOnly
                      value={isAdd ? "Auto-generated on save" : product?.upc}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>SKU <span className="text-error">*</span></label>
                    <input className={`${inputBase} font-mono`} type="text" defaultValue={isAdd ? "" : product?.sku} placeholder="e.g. WT-550-B" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>PID1</label>
                    <input className={inputBase} type="text" placeholder="First identifier" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>PID2</label>
                    <input className={inputBase} type="text" placeholder="Additional identifier" />
                  </div>
                </div>
              </div>

              {/* Product Info */}
              <div>
                <h3 className={sectionTitle}>Product Information</h3>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Product Name <span className="text-error">*</span></label>
                    <input
                      className={inputBase}
                      type="text"
                      defaultValue={isAdd ? "" : product?.name}
                      placeholder="Name for printed documents (orders, delivery notes)"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Product Stores</label>
                    <div className="flex flex-wrap gap-3 rounded-md border border-outline-variant/20 bg-surface-container-lowest p-3">
                      {MOCK_STORES.map((store) => (
                        <label key={store} className="flex cursor-pointer items-center gap-2 text-xs text-on-surface-variant select-none">
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-primary"
                            checked={selectedStores.includes(store)}
                            onChange={(e) =>
                              setSelectedStores((prev) =>
                                e.target.checked ? [...prev, store] : prev.filter((s) => s !== store)
                              )
                            }
                          />
                          {store}
                        </label>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {/* Inventory & Pricing */}
              <div>
                <h3 className={sectionTitle}>Inventory & Pricing</h3>
                <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Total Quantity</label>
                    <div className="flex gap-2">
                      <input className={inputBase} type="number" defaultValue={isAdd ? 0 : product?.qty} min={0} />
                      <button type="button" className="shrink-0 whitespace-nowrap rounded-md border border-outline-variant/30 px-3 py-2 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-high transition-colors">
                        Update Qty
                      </button>
                    </div>
                    <p className="text-[10px] text-on-surface-variant">
                      Quantity can be retrieved per outlet on the product page.
                    </p>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Total eStore Quantity</label>
                    <input className={inputBase} type="number" defaultValue={isAdd ? 0 : product?.qty} min={0} />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>eStore Cut-Off Quantity</label>
                    <input className={inputBase} type="number" defaultValue={0} min={0} />
                    <p className="text-[10px] text-on-surface-variant">Product tagged out of stock when this threshold is reached.</p>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Retail Price</label>
                    <div className="relative">
                      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                      <input className={`${inputBase} pl-6`} type="text" defaultValue={isAdd ? "" : product?.price?.replace("$", "")} placeholder="0.00" />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Current Price (Incl Tax)</label>
                    <div className="relative">
                      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                      <input className={`${inputBase} pl-6`} type="text" defaultValue={isAdd ? "" : product?.price?.replace("$", "")} placeholder="0.00" />
                    </div>
                  </div>
                </div>
              </div>

              {/* Item Type */}
              <div>
                <h3 className={sectionTitle}>Item Type</h3>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Item Type</label>
                    <div className="relative">
                      <select
                        className={`${inputBase} appearance-none pr-10`}
                        value={itemType}
                        onChange={(e) => setItemType(e.target.value as ItemType)}
                      >
                        <option value="physical">Physical Product</option>
                        <option value="membership-points">Membership Points</option>
                        <option value="membership-voucher">Membership Voucher</option>
                        <option value="membership">Membership</option>
                        <option value="stored-value-card">Stored Value Card</option>
                      </select>
                      <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                    </div>
                  </div>

                  {/* Conditional sub-fields */}
                  {itemType === "membership-points" && (
                    <div className="space-y-1.5">
                      <label className={labelBase}>Points Awarded</label>
                      <input className={inputBase} type="number" min={0} placeholder="e.g. 100" />
                    </div>
                  )}
                  {itemType === "membership-voucher" && (
                    <div className="space-y-1.5">
                      <label className={labelBase}>Voucher Code</label>
                      <input className={`${inputBase} font-mono`} type="text" placeholder="e.g. VOUCHER-2024" />
                    </div>
                  )}
                  {(itemType === "membership" || itemType === "stored-value-card") && (
                    <>
                      <div className="space-y-1.5">
                        <label className={labelBase}>Card Tier</label>
                        <div className="relative">
                          <select className={`${inputBase} appearance-none pr-10`}>
                            {MOCK_CARD_TIERS.map((t) => <option key={t}>{t}</option>)}
                          </select>
                          <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                        </div>
                      </div>
                      <div className="space-y-1.5">
                        <label className={labelBase}>Card Type</label>
                        <div className="relative">
                          <select className={`${inputBase} appearance-none pr-10`}>
                            {MOCK_CARD_TYPES.map((t) => <option key={t}>{t}</option>)}
                          </select>
                          <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                        </div>
                      </div>
                      {itemType === "stored-value-card" && (
                        <div className="space-y-1.5">
                          <label className={labelBase}>Stored Value Amount</label>
                          <div className="relative">
                            <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                            <input className={`${inputBase} pl-6`} type="text" placeholder="0.00" />
                          </div>
                        </div>
                      )}
                    </>
                  )}
                </div>

                {(itemType === "membership-points" || itemType === "membership-voucher" || itemType === "membership" || itemType === "stored-value-card") && (
                  <div className="mt-4 flex items-start gap-3 rounded-md border border-amber-200/60 bg-amber-50/60 px-4 py-3">
                    <IconInfo className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
                    <p className="text-[11px] text-amber-800">This item type must be connected to the CRM. Ensure CRM integration is configured before publishing.</p>
                  </div>
                )}
              </div>

              {/* Product Options */}
              <div>
                <h3 className={sectionTitle}>Product Options</h3>
                <div className="space-y-3">
                  {productOptions.map((opt) => (
                    <div key={opt.id} className="rounded-lg border border-outline-variant/20 bg-surface p-4 space-y-3">
                      <div className="flex items-start gap-3">
                        <div className="flex-1 grid grid-cols-1 gap-3 sm:grid-cols-3">
                          <div className="space-y-1">
                            <label className={labelBase}>Type</label>
                            <div className="relative">
                              <select
                                className={`${inputBase} appearance-none pr-8`}
                                value={opt.type}
                                onChange={(e) => updateProductOption(opt.id, { type: e.target.value as ProductOption["type"] })}
                              >
                                <option value="textbox">Textbox</option>
                                <option value="dropdown">Dropdown List</option>
                                <option value="bundle">Product Bundle</option>
                              </select>
                              <IconChevronDown className="pointer-events-none absolute right-2 top-2.5 h-4 w-4 text-on-surface-variant" />
                            </div>
                          </div>
                          <div className="space-y-1">
                            <label className={labelBase}>Label</label>
                            <input
                              className={inputBase}
                              type="text"
                              value={opt.label}
                              onChange={(e) => updateProductOption(opt.id, { label: e.target.value })}
                              placeholder="Option label"
                            />
                          </div>
                          <div className="flex items-end pb-1">
                            <label className="flex cursor-pointer items-center gap-2 text-xs text-on-surface-variant select-none">
                              <input
                                type="checkbox"
                                className="h-3.5 w-3.5 accent-primary"
                                checked={opt.required}
                                onChange={(e) => updateProductOption(opt.id, { required: e.target.checked })}
                              />
                              Required field
                            </label>
                          </div>
                        </div>
                        <button
                          type="button"
                          className="shrink-0 rounded p-1 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                          onClick={() => removeProductOption(opt.id)}
                          aria-label="Remove option"
                        >
                          <IconDelete className="h-4 w-4" />
                        </button>
                      </div>
                      {opt.type === "dropdown" && (
                        <div className="space-y-1">
                          <label className={labelBase}>Options (one per line)</label>
                          <textarea
                            className={`${inputBase} resize-y min-h-[80px]`}
                            value={opt.dropdownOptions}
                            onChange={(e) => updateProductOption(opt.id, { dropdownOptions: e.target.value })}
                            placeholder={"Option 1\nOption 2\nOption 3"}
                          />
                        </div>
                      )}
                      {opt.type === "bundle" && (
                        <p className="text-[10px] text-on-surface-variant italic">
                          Bundle items have no inventory deduction. Set price to "0" for free items.
                        </p>
                      )}
                    </div>
                  ))}
                  <button
                    type="button"
                    className="flex items-center gap-2 rounded-md border border-dashed border-primary/30 px-4 py-2.5 text-xs font-semibold text-primary transition-colors hover:bg-primary/5 w-full justify-center"
                    onClick={addProductOption}
                  >
                    <IconAdd className="h-4 w-4 shrink-0" />
                    Add Option
                  </button>
                </div>
              </div>

              {/* Product Attributes */}
              <div>
                <h3 className={sectionTitle}>Product Attributes</h3>
                <SearchablePicker
                  label="Attributes"
                  options={MOCK_ATTRIBUTES}
                  selected={selectedAttributes}
                  onChange={setSelectedAttributes}
                  placeholder="Specify attribute name to find a match..."
                />
              </div>

              {/* Fulfilment flags */}
              <div>
                <h3 className={sectionTitle}>Fulfilment & Sync</h3>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {[
                    { label: "Enable Collection",         hint: "Customers can collect from retail store",          value: enableCollection,      set: setEnableCollection      },
                    { label: "Enable Delivery",            hint: "Customers receive product at local address",       value: enableDelivery,        set: setEnableDelivery        },
                    { label: "Enable Overseas Delivery",   hint: "International shipping option at checkout",        value: enableOverseasDelivery, set: setEnableOverseasDelivery},
                    { label: "Shipping & Returns Clause",  hint: "Display shipping/returns clause on product page",  value: shippingClause,        set: setShippingClause        },
                    { label: "Inventory Sync",             hint: "Enable scheduled inventory synchronisation",       value: inventorySync,         set: setInventorySync         },
                    { label: "Price Sync",                 hint: "Enable scheduled price synchronisation",           value: priceSync,             set: setPriceSync             },
                    { label: "Allow Oversell",             hint: "Available for purchase even when out of stock",    value: allowOversell,         set: setAllowOversell         },
                  ].map(({ label, hint, value, set }) => (
                    <label key={label} className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface p-3 select-none hover:bg-surface-container-low transition-colors">
                      <input
                        type="checkbox"
                        className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                        checked={value}
                        onChange={(e) => set(e.target.checked)}
                      />
                      <div>
                        <p className="text-xs font-semibold text-on-surface">{label}</p>
                        <p className="text-[10px] text-on-surface-variant">{hint}</p>
                      </div>
                    </label>
                  ))}
                </div>
              </div>

              {/* Scheduling & Delivery */}
              <div>
                <h3 className={sectionTitle}>Scheduling & Delivery</h3>
                <div className="grid grid-cols-1 gap-6 sm:grid-cols-3">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Delivery Start Date</label>
                    <input className={inputBase} type="date" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Delivery Lead Time (days)</label>
                    <input className={inputBase} type="number" min={0} placeholder="e.g. 3" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Handling Fee</label>
                    <div className="relative">
                      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                      <input className={`${inputBase} pl-6`} type="text" placeholder="0.00" />
                    </div>
                  </div>
                </div>
              </div>

              {/* Product Images */}
              <div>
                <h3 className={sectionTitle}>Product Images & Videos</h3>
                {galleryNotice ? (
                  <p className="mb-3 rounded-md border border-outline-variant/30 bg-surface-container-high px-3 py-2 text-[11px] text-on-surface-variant">
                    {galleryNotice}
                  </p>
                ) : null}

                {/* Inline image editor */}
                {editorImageId !== null && (() => {
                  const img = images.find((i) => i.id === editorImageId);
                  return (
                    <div className="overflow-hidden rounded-xl border border-outline-variant/20 bg-surface">
                      {/* Editor toolbar */}
                      <div className="flex flex-wrap items-center gap-1 border-b border-outline-variant/20 bg-surface-container-low px-3 py-2">
                        {[
                          { Icon: IconSave,        label: "Save"                   },
                          { Icon: IconRestartAlt,  label: "Undo"                   },
                          { Icon: IconRestartAlt,  label: "Redo"                   },
                        ].map(({ Icon, label }) => (
                          <button
                            key={label}
                            type="button"
                            aria-label={label}
                            title={label}
                            className="flex items-center gap-1.5 rounded px-2 py-1.5 text-[10px] font-semibold text-on-surface-variant transition-colors hover:bg-surface-container-high"
                            onClick={() => console.info(`[ImageEditor] ${label} (placeholder)`)}
                          >
                            <Icon className="h-4 w-4 shrink-0" />
                            {label}
                          </button>
                        ))}
                        <div className="mx-1 h-5 w-px bg-outline-variant/30" />
                        {[
                          { Icon: IconCrop,       label: "Crop"                },
                          { Icon: IconRotateCcw,  label: "Rotate CCW"          },
                          { Icon: IconRotateCw,   label: "Rotate CW"           },
                          { Icon: IconTune,       label: "Brightness/Contrast" },
                          { Icon: IconOpenWith,   label: "Move & Scale"        },
                          { Icon: IconEditNote,   label: "Eraser"              },
                        ].map(({ Icon, label }) => (
                          <button
                            key={label}
                            type="button"
                            aria-label={label}
                            title={label}
                            className="rounded p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container-high hover:text-primary"
                            onClick={() => console.info(`[ImageEditor] ${label} (placeholder)`)}
                          >
                            <Icon className="h-4 w-4 shrink-0" />
                          </button>
                        ))}
                        <div className="ml-auto">
                          <button
                            type="button"
                            className="rounded-md border border-outline-variant/30 px-3 py-1.5 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant hover:bg-surface-container-high transition-colors"
                            onClick={() => setEditorImageId(null)}
                          >
                            Close Editor
                          </button>
                        </div>
                      </div>
                      {/* Preview (uploaded media or colour placeholder) */}
                      <div className="flex min-h-[300px] items-center justify-center overflow-hidden rounded-b-xl bg-black/5">
                        {img?.src ? (
                          img.isVideo ? (
                            <video
                              src={img.src}
                              className="max-h-[min(70vh,520px)] w-full object-contain"
                              controls
                              muted
                              playsInline
                            />
                          ) : (
                            <img
                              src={img.src}
                              alt={img.alt || ""}
                              className="max-h-[min(70vh,520px)] w-full object-contain"
                            />
                          )
                        ) : (
                          <div
                            className="flex min-h-[300px] w-full flex-col items-center justify-center"
                            style={{ backgroundColor: img?.color ?? "#f3f4f6" }}
                          >
                            <div className="text-center">
                              <IconImage className="mx-auto mb-3 h-16 w-16 text-white/60" />
                              <p className="text-sm font-semibold text-white/80">{img?.name}</p>
                              <p className="mt-1 text-[11px] text-white/60">
                                Use the toolbar above to edit this image
                              </p>
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })()}

                {/* Gallery grid */}
                {editorImageId === null && (
                  <div className="flex flex-wrap gap-4">
                    {/* Image cards */}
                    {images.map((img) => (
                      <div
                        key={img.id}
                        className="group relative w-[160px] shrink-0 overflow-hidden rounded-xl border border-outline-variant/20 bg-surface shadow-sm"
                      >
                        {/* Thumbnail */}
                        <div className="relative flex h-[120px] w-full items-center justify-center overflow-hidden bg-black/5">
                          {img.src ? (
                            img.isVideo ? (
                              <video
                                src={img.src}
                                className="h-full w-full object-cover"
                                muted
                                playsInline
                                loop
                              />
                            ) : (
                              <img src={img.src} alt="" className="h-full w-full object-cover" />
                            )
                          ) : (
                            <>
                              <div className="absolute inset-0" style={{ backgroundColor: img.color }} />
                              <IconImage className="relative z-[1] h-10 w-10 text-white/60" />
                            </>
                          )}
                        </div>

                        {/* Filename */}
                        <div className="px-2.5 py-2">
                          <p className="truncate text-[10px] font-semibold text-on-surface" title={img.name}>{img.name}</p>
                          {img.alt && (
                            <p className="truncate text-[9px] text-on-surface-variant italic" title={img.alt}>{img.alt}</p>
                          )}
                        </div>

                        {/* Action bar — visible on hover */}
                        <div className="absolute bottom-0 left-0 right-0 flex translate-y-full items-center justify-around bg-surface-container-lowest/95 px-1 py-1.5 backdrop-blur-sm transition-transform duration-150 group-hover:translate-y-0">
                          <button
                            type="button"
                            aria-label="View Media"
                            title="View Media"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() => {
                              if (!img.src) {
                                setGalleryNotice("Add a file first — mock placeholders have no URL.");
                                return;
                              }
                              window.open(img.src, "_blank", "noopener,noreferrer");
                            }}
                          >
                            <IconVisibility className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Edit Alt Text"
                            title="Edit Alt Text"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() => setAltTextModal({ open: true, imageId: img.id, value: img.alt })}
                          >
                            <IconTextFields className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Optimize Image"
                            title="Optimize Image"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() =>
                              setGalleryNotice("Image optimization is not available in this demo build.")
                            }
                          >
                            <IconAutoFixHigh className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Edit Image"
                            title="Edit Image"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() => setEditorImageId(img.id)}
                          >
                            <IconEditNote className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Delete Image"
                            title="Delete Image"
                            className="rounded p-1 text-on-surface-variant hover:text-error transition-colors"
                            onClick={() => setDeleteConfirm({ open: true, imageId: img.id })}
                          >
                            <IconDelete className="h-4 w-4" />
                          </button>
                        </div>
                      </div>
                    ))}

                    {/* Add image dropzone */}
                    <input
                      ref={fileInputRef}
                      type="file"
                      multiple
                      accept="image/*,video/*"
                      className="sr-only"
                      onChange={(e: ChangeEvent<HTMLInputElement>) => {
                        if (e.target.files?.length) ingestFiles(e.target.files);
                        e.target.value = "";
                      }}
                    />
                    <div
                      className="flex w-[160px] shrink-0 cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-primary/20 bg-primary/5 px-3 py-6 transition-colors hover:border-primary/40 hover:bg-primary/10"
                      onDragOver={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                      }}
                      onDrop={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        if (e.dataTransfer.files?.length) ingestFiles(e.dataTransfer.files);
                      }}
                      onClick={() => fileInputRef.current?.click()}
                    >
                      <IconCloudUpload className="h-8 w-8 text-primary/40" />
                      <p className="text-center text-[10px] font-semibold leading-snug text-primary/70">
                        Drag & drop<br />or click here
                      </p>
                      <p className="text-center text-[9px] text-on-surface-variant">PNG · GIF · MP4 · WEBM</p>
                    </div>
                  </div>
                )}
              </div>

              {/* Variants */}
              <div>
                <h3 className={sectionTitle}>Variants</h3>
                {variantNotice ? (
                  <p className="mb-3 rounded-md border border-primary/20 bg-primary/5 px-3 py-2 text-[11px] text-primary">
                    {variantNotice}
                  </p>
                ) : null}
                <div className="flex items-center gap-4 rounded-lg border border-outline-variant/20 bg-surface p-4">
                  <div className="flex-1">
                    <p className="text-sm font-semibold text-on-surface">Main Product Configuration</p>
                    <p className="text-[11px] text-on-surface-variant">
                      If this product has variants, configure which one serves as the main product listing.
                    </p>
                  </div>
                  <button
                    type="button"
                    className="shrink-0 rounded-md bg-surface-container-high px-4 py-2 text-xs font-bold text-on-surface-variant transition-colors hover:bg-surface-variant"
                    onClick={() => setConfigureMainOpen(true)}
                  >
                    Configure Main Product
                  </button>
                </div>
              </div>

              {!isAdd && (
                <div className="flex items-center justify-end gap-3 border-t border-outline-variant/20 pt-8 mt-8">
                  <button type="button" className={btnFooterGhost} onClick={onBack}>
                    Cancel
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={() => handleSave("Save Changes")}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Save Changes
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── PRODUCT PAGE TAB ─────────────────────────────────────── */}
          {tab === "product-page" && (
            <>
              {/* Core */}
              <div>
                <h3 className={sectionTitle}>Product Page Settings</h3>
                <div className="grid grid-cols-1 gap-6">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Title <span className="text-error">*</span></label>
                    <input
                      className={inputBase}
                      type="text"
                      defaultValue={isAdd ? "" : product?.name}
                      placeholder="Title displayed on the product page"
                    />
                    <p className="text-[10px] text-on-surface-variant">This is the title customers use to identify the product on the storefront.</p>
                  </div>

                  <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                    <SearchablePicker
                      label="Brand"
                      options={MOCK_BRANDS}
                      selected={selectedBrands}
                      onChange={setSelectedBrands}
                      placeholder="Type brand name to find a match..."
                    />
                    <SearchablePicker
                      label="Category"
                      options={MOCK_CATEGORIES}
                      selected={selectedCategories}
                      onChange={setSelectedCategories}
                      placeholder="Type category name to find a match..."
                    />
                  </div>

                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <div className="space-y-1.5">
                      <label className={labelBase}>Publish From</label>
                      <input className={inputBase} type="datetime-local" defaultValue={isAdd ? "" : "2024-01-01T09:00"} />
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelBase}>Publish Until</label>
                      <input className={inputBase} type="datetime-local" />
                    </div>
                  </div>

                  {/* Overview */}
                  <div className="space-y-1.5">
                    <label className={labelBase}>Overview</label>
                    <div className="overflow-hidden rounded-md border border-outline-variant/20 bg-surface">
                      <div className="flex items-center gap-1 border-b border-outline-variant/20 bg-surface-container-low p-2">
                        {[
                          { Icon: IconFormatBold,          label: "Bold"      },
                          { Icon: IconFormatItalic,        label: "Italic"    },
                          { Icon: IconFormatUnderlined,    label: "Underline" },
                        ].map(({ Icon, label }) => (
                          <button key={label} type="button" aria-label={label} className="rounded p-1.5 hover:bg-surface-container-high">
                            <Icon className="h-4 w-4" />
                          </button>
                        ))}
                        <div className="mx-1 h-4 w-px bg-outline-variant/40" />
                        {[
                          { Icon: IconFormatListBulleted, label: "List"  },
                          { Icon: IconLink,               label: "Link"  },
                          { Icon: IconImage,              label: "Image" },
                        ].map(({ Icon, label }) => (
                          <button key={label} type="button" aria-label={label} className="rounded p-1.5 hover:bg-surface-container-high">
                            <Icon className="h-4 w-4" />
                          </button>
                        ))}
                      </div>
                      <MultiLangTextarea
                        label=""
                        rows={6}
                        placeholders={{
                          en: "Enter product overview and description...",
                          vn: "Nhập mô tả sản phẩm...",
                          zh: "输入产品概述和描述...",
                          ja: "製品の概要と説明を入力してください...",
                        }}
                        className="rounded-none border-0 focus:ring-0 min-h-[140px]"
                      />
                    </div>
                  </div>
                </div>
              </div>

              {/* Visibility flags */}
              <div>
                <h3 className={sectionTitleRow}>Visibility & Recommendations</h3>
                <p className={sectionIntroText}>Control product visibility and recommendation behaviour on the storefront.</p>
                <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
                  {[
                    { label: "Hidden Product",              hint: "Hide from search listings and search engines. Accessible only via direct URL.", value: hiddenProduct,        set: setHiddenProduct        },
                    { label: "Recommend Similar Products",  hint: "Allow system to automatically recommend products on this product's page.",    value: recommendSimilar,     set: setRecommendSimilar     },
                    { label: "Enable Restock Notification", hint: "Automatically send restock notification when inventory is replenished.",     value: restockNotification,  set: setRestockNotification  },
                  ].map(({ label, hint, value, set }) => (
                    <label key={label} className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface p-3 select-none hover:bg-surface-container-low transition-colors">
                      <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={value} onChange={(e) => set(e.target.checked)} />
                      <div>
                        <p className="text-xs font-semibold text-on-surface">{label}</p>
                        <p className="text-[10px] text-on-surface-variant">{hint}</p>
                      </div>
                    </label>
                  ))}
                  {recommendSimilar && (
                    <div className="space-y-1.5">
                      <label className={labelBase}>Recommendations Mode</label>
                      <div className="relative">
                        <select className={`${inputBase} appearance-none pr-10`}>
                          <option>Auto</option>
                          <option>Manual</option>
                          <option>Disabled</option>
                        </select>
                        <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* URL & Meta */}
              <div>
                <h3 className={sectionTitleRow}>URL & SEO</h3>
                <p className={sectionIntroText}>Configure the product URL and metadata for search engine optimisation.</p>
                <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
                  {/* Left: fields */}
                  <div className="space-y-6">
                    <div className="space-y-1.5">
                      <label className={labelBase}>Product URL</label>
                      <input
                        className={`${inputBase} ${useTitleForUrl ? "text-on-surface-variant" : ""}`}
                        type="text"
                        readOnly={useTitleForUrl}
                        placeholder="product-url-slug"
                        defaultValue={isAdd ? "" : product?.name?.toLowerCase().replace(/\s+/g, "-")}
                      />
                      <label className="flex cursor-pointer items-center gap-2 text-[10px] text-on-surface-variant select-none">
                        <input type="checkbox" className="h-3 w-3 accent-primary" checked={useTitleForUrl} onChange={(e) => setUseTitleForUrl(e.target.checked)} />
                        Use product title as URL
                      </label>
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelBase}>Page Title</label>
                      <input
                        className={`${inputBase} ${useTitleForPageTitle ? "text-on-surface-variant" : ""}`}
                        type="text"
                        readOnly={useTitleForPageTitle}
                        placeholder="Page title for browser tab"
                        defaultValue={isAdd ? "" : product?.name}
                      />
                      <label className="flex cursor-pointer items-center gap-2 text-[10px] text-on-surface-variant select-none">
                        <input type="checkbox" className="h-3 w-3 accent-primary" checked={useTitleForPageTitle} onChange={(e) => setUseTitleForPageTitle(e.target.checked)} />
                        Use product title as page title
                      </label>
                    </div>
                    <MultiLangInput
                      label="Meta Keywords"
                      placeholders={{
                        en: "Enter keywords separated by commas…",
                        vn: "Nhập từ khóa cách nhau bằng dấu phẩy...",
                        zh: "输入以逗号分隔的关键词...",
                        ja: "カンマ区切りでキーワードを入力...",
                      }}
                      hint="List relevant keywords that customers might use to find this product."
                    />
                    <MultiLangTextarea
                      label="Meta Description"
                      rows={3}
                      placeholders={{
                        en: "Brief description for search results listings…",
                        vn: "Mô tả ngắn cho kết quả tìm kiếm...",
                        zh: "搜索结果中显示的简短描述...",
                        ja: "検索結果に表示される簡単な説明...",
                      }}
                      hint="Recommended: 160 characters or fewer."
                    />
                  </div>

                  {/* Right: Google preview */}
                  <div className="space-y-4">
                    <div>
                      <h4 className={`${sectionTitleRow} mb-4 flex items-center gap-2`}>
                        <IconVisibility className="h-4 w-4 text-primary shrink-0" />
                        Google Search Preview
                      </h4>
                      <div className="rounded-md border border-outline-variant/20 bg-white p-4 font-sans">
                        <div className="mb-0.5 truncate text-sm text-on-surface-variant/80">
                          https://your-estore.com › products › {isAdd ? "product-name" : (product?.name?.toLowerCase().replace(/\s+/g, "-") ?? "product")}
                        </div>
                        <h5 className="mb-1 cursor-pointer truncate text-lg font-normal text-[#1a0dab] hover:underline">
                          {isAdd
                            ? <span className="italic text-outline-variant">Page title will appear here</span>
                            : product?.name}
                        </h5>
                        <p className="line-clamp-3 text-[13px] leading-snug text-[#4d5156]">
                          {isAdd
                            ? <span className="italic text-outline-variant">Meta description will appear here</span>
                            : "Discover premium quality products at unbeatable prices. Fast delivery and easy returns."}
                        </p>
                      </div>
                    </div>
                    <div className="rounded-md border border-outline-variant/10 bg-primary-container/10 p-4">
                      <div className="flex items-start gap-3">
                        <IconLightbulb className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
                        <p className="text-xs leading-relaxed text-on-surface-variant">
                          <strong>SEO Tip:</strong> Use specific, descriptive keywords in your meta title and description to improve click-through rates from search results.
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {!isAdd && (
                <div className="flex items-center justify-end gap-3 border-t border-outline-variant/20 pt-8 mt-8">
                  <button type="button" className={btnFooterGhost} onClick={() => console.info("[EditProduct] Discard")}>
                    Discard Changes
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={() => handleSave("Save Changes")}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Update Product Page
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── RECOMMENDATIONS TAB ──────────────────────────────────── */}
          {tab === "recommendations" && (
            <>
              <div>
                <h3 className={sectionTitleRow}>Product Recommendations</h3>
                <p className={sectionIntroText}>
                  Select the products that will be recommended on this product's page.
                </p>
              </div>

              <div className="mt-4 space-y-3">
                {otherProducts.map((p) => {
                  const checked = recommendedIds.includes(p.id);
                  return (
                    <label
                      key={p.id}
                      className={`flex cursor-pointer items-center gap-4 rounded-lg border p-4 transition-colors ${
                        checked
                          ? "border-primary/30 bg-primary/5"
                          : "border-outline-variant/20 hover:bg-surface-container-low"
                      }`}
                    >
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-primary shrink-0"
                        checked={checked}
                        onChange={(e) => toggleRecommended(p.id, e.target.checked)}
                      />
                      <span className="font-mono text-xs text-on-surface-variant w-20 shrink-0">{p.sku}</span>
                      <span className="flex-1 text-sm font-medium text-on-surface">{p.name}</span>
                      {checked && (
                        <span className="ml-auto shrink-0 rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest text-primary">
                          Recommended
                        </span>
                      )}
                    </label>
                  );
                })}
              </div>

              {recommendedIds.length > 0 && (
                <div className="mt-4 rounded-lg border border-outline-variant/20 bg-surface-container-low p-3">
                  <p className="text-xs text-on-surface-variant">
                    <span className="font-bold text-on-surface">{recommendedIds.length} product{recommendedIds.length !== 1 ? "s" : ""} selected</span>
                    {" — these will be shown as recommendations on this product's page."}
                  </p>
                </div>
              )}

              {!isAdd && (
                <div className="flex items-center justify-end gap-3 border-t border-outline-variant/20 pt-8 mt-8">
                  <button type="button" className={btnFooterGhost} onClick={() => setRecommendedIds([])}>
                    Reset
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={() => handleSave("Save Changes")}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Update Recommendations
                  </button>
                </div>
              )}
            </>
          )}
        </section>
      </div>

      {/* ── Configure main product (variants) ───────────────────────────── */}
      {configureMainOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm p-4"
          onClick={() => setConfigureMainOpen(false)}
        >
          <div
            className="w-full max-w-md rounded-2xl border border-outline-variant/20 bg-surface-container-lowest p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-base font-bold text-on-surface">Configure main product</h3>
            <p className="mt-2 text-xs leading-relaxed text-on-surface-variant">
              Choose how this SKU relates to variant listings. In production this would load variant SKUs from your
              catalog service.
            </p>
            <div className="mt-5 flex flex-col gap-3">
              <button
                type="button"
                className="w-full rounded-lg border border-primary/30 bg-primary/5 px-4 py-3 text-left text-xs font-semibold text-primary transition-colors hover:bg-primary/10"
                onClick={() => {
                  setVariantNotice("This SKU is set as the main product for storefront listing (demo).");
                  setConfigureMainOpen(false);
                }}
              >
                Set Self as Main Product
              </button>
              <button
                type="button"
                className={btnFooterGhost + " w-full text-center"}
                onClick={() => setConfigureMainOpen(false)}
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Edit Alt Text modal ──────────────────────────────────────────── */}
      {altTextModal.open && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setAltTextModal({ open: false, imageId: "", value: "" })}
        >
          <div
            className="mx-4 w-full max-w-sm rounded-2xl bg-surface-container-lowest p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="mb-1 text-base font-bold text-on-surface">Edit Alternate Text</h3>
            <p className="mb-4 text-[11px] text-on-surface-variant">
              Alt text helps search engines tag this image and is shown when the image fails to load.
            </p>
            <textarea
              className={`${inputBase} mb-4 resize-y min-h-[80px]`}
              value={altTextModal.value}
              onChange={(e) => setAltTextModal((m) => ({ ...m, value: e.target.value }))}
              placeholder="Describe the image for accessibility and SEO..."
              autoFocus
            />
            <div className="flex justify-end gap-3">
              <button
                type="button"
                className={btnFooterGhost}
                onClick={() => setAltTextModal({ open: false, imageId: "", value: "" })}
              >
                Cancel
              </button>
              <button
                type="button"
                className={btnFooterPrimary}
                onClick={() => {
                  setImages((prev) =>
                    prev.map((img) =>
                      img.id === altTextModal.imageId ? { ...img, alt: altTextModal.value } : img
                    )
                  );
                  setAltTextModal({ open: false, imageId: "", value: "" });
                }}
              >
                <IconSave className="h-4 w-4 shrink-0" />
                Save Changes
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Delete Confirmation popup ─────────────────────────────────────── */}
      {deleteConfirm.open && (() => {
        const img = images.find((i) => i.id === deleteConfirm.imageId);
        return (
          <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
            onClick={() => setDeleteConfirm({ open: false, imageId: "" })}
          >
            <div
              className="mx-4 w-full max-w-sm rounded-2xl bg-surface-container-lowest p-6 text-center shadow-2xl"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-error/10">
                <IconDelete className="h-6 w-6 text-error" />
              </div>
              <h3 className="mb-1 text-base font-bold text-on-surface">Delete Image?</h3>
              <p className="mb-5 text-sm text-on-surface-variant">
                <span className="font-semibold text-on-surface">{img?.name}</span>
                {" will be permanently removed from this product."}
              </p>
              <div className="flex justify-center gap-3">
                <button
                  type="button"
                  className={btnFooterGhost}
                  onClick={() => setDeleteConfirm({ open: false, imageId: "" })}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="rounded-md bg-error px-5 py-2 text-xs font-bold uppercase tracking-widest text-white hover:opacity-90 transition-opacity"
                  onClick={() => {
                    const rid = deleteConfirm.imageId;
                    const victim = images.find((i) => i.id === rid);
                    if (victim?.src?.startsWith("blob:")) URL.revokeObjectURL(victim.src);
                    setImages((prev) => prev.filter((i) => i.id !== rid));
                    if (editorImageId === rid) setEditorImageId(null);
                    setDeleteConfirm({ open: false, imageId: "" });
                  }}
                >
                  OK, Delete
                </button>
              </div>
            </div>
          </div>
        );
      })()}

      {/* ── Success modal ─────────────────────────────────────────────────── */}
      {showSuccessModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setShowSuccessModal(false)}
        >
          <div
            className="mx-4 w-full max-w-sm rounded-2xl bg-surface-container-lowest p-8 text-center shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-secondary-container/20">
              <IconCheckCircle className="h-8 w-8 text-secondary" />
            </div>
            <h3 className="mb-2 text-lg font-bold text-on-surface">
              {successMessage === "Save and Approve"
                ? "Product Approved & Published!"
                : successMessage === "Save and Send for Approval"
                ? "Sent for Approval!"
                : successMessage === "Save and Archive"
                ? "Product Archived!"
                : isAdd
                ? "Product Created!"
                : "Changes Saved!"}
            </h3>
            <p className="mb-6 text-sm text-on-surface-variant">
              {successMessage === "Save and Approve"
                ? "The product has been approved and is now live in your eStore."
                : successMessage === "Save and Send for Approval"
                ? "The product has been submitted for admin review."
                : successMessage === "Save and Archive"
                ? `"${product?.name ?? "Product"}" has been archived and is no longer visible in the eStore.`
                : isAdd
                ? "The new product has been successfully created and is available in the catalog."
                : `"${product?.name ?? "Product"}" has been successfully updated.`}
            </p>
            <div className="flex justify-center gap-3">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2 text-xs font-bold uppercase tracking-widest text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={onBack}
              >
                View Products
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2 text-xs font-bold uppercase tracking-widest text-on-primary hover:bg-primary-container transition-colors"
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
