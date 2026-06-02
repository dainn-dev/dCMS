import { useEffect, useMemo, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
  IconChevronRight,
  IconClose,
  IconDelete,
  IconFolder,
  IconFolderOpen,
  IconImage,
  IconSave,
  IconSearch,
  IconTag,
  IconUnfoldLess,
  IconUnfoldMore,
} from "../../../orders/icons";
import {
  createTenant,
  updateTenant,
  validateTenantCode,
  type TenantRow,
} from "../../api/tenantsApi";

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-[10px] text-on-surface-variant leading-relaxed";
const sectionTitleRow =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2 mb-5";

export type TenantFormPageProps = {
  mode: "add" | "edit";
  /** Set when <c>mode === "edit"</c> — row from list (DAI-668 API). */
  tenant?: TenantRow;
  authToken?: string;
  onSave?: () => void;
  onCancel?: () => void;
};

const NAV_STYLES = ["Standard", "Mega", "Sidebar"] as const;
type NavStyle = (typeof NAV_STYLES)[number];

// Reusable image upload slot component
function ImageSlot({
  label,
  src,
  onFileSelected,
  onRemove,
}: {
  label: string;
  src?: string;
  onFileSelected: (dataUrl: string) => void;
  onRemove: () => void;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [error, setError] = useState("");

  function openPicker() {
    setError("");
    inputRef.current?.click();
  }

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    if (!file.type.startsWith("image/")) { setError("Choose a JPG, PNG, or WEBP image."); return; }
    if (file.size > 2 * 1024 * 1024) { setError("Max size is 2 MB."); return; }
    const reader = new FileReader();
    reader.onload = () => { if (typeof reader.result === "string") onFileSelected(reader.result); };
    reader.readAsDataURL(file);
  }

  return (
    <div className="space-y-1.5">
      <label className={labelBase}>{label}</label>
      <input ref={inputRef} type="file" accept="image/*" className="sr-only" onChange={handleChange} />
      <div className="border-2 border-dashed border-outline-variant/40 rounded-xl p-3 flex flex-col items-center bg-surface-container-low/20">
        {src ? (
          <>
            <div className="relative w-full aspect-video bg-white rounded-lg overflow-hidden mb-3 border border-outline-variant/10">
              <img className="w-full h-full object-cover" alt={label} src={src} />
              <button
                type="button"
                className="absolute top-1.5 right-1.5 p-1 bg-error text-white rounded-full"
                aria-label="Remove image"
                onClick={() => { setError(""); onRemove(); }}
              >
                <IconDelete className="h-3 w-3" />
              </button>
            </div>
            <button type="button" className="w-full py-1.5 bg-surface-container-high text-primary font-bold text-[10px] uppercase tracking-widest rounded" onClick={openPicker}>
              Replace Image
            </button>
          </>
        ) : (
          <>
            <IconImage className="h-8 w-8 text-outline-variant mb-2" />
            <p className="text-[10px] text-on-surface-variant text-center mb-2">JPG, PNG, WEBP — Max 2 MB</p>
            <button type="button" className="w-full py-1.5 bg-primary text-on-primary font-bold text-[10px] uppercase tracking-widest rounded" onClick={openPicker}>
              Choose File
            </button>
          </>
        )}
      </div>
      {error && <p className="text-[10px] text-error">{error}</p>}
    </div>
  );
}

const MOCK_BRANDS = ["Luxe Heritage Group", "Velocity Tech Systems", "Nomad Consulting Ltd.", "Aura Essentials", "Urban Edge Collective"];

type CategoryTreeNode = {
  id: string;
  name: string;
  children?: CategoryTreeNode[];
};

const MOCK_CATEGORY_TREE: CategoryTreeNode[] = [
  {
    id: "electronics",
    name: "Electronics",
    children: [
      { id: "electronics-phones", name: "Phones & Tablets" },
      { id: "electronics-laptops", name: "Laptops & Computers" },
      { id: "electronics-audio", name: "Audio & Headphones" },
      { id: "electronics-tv", name: "TV & Home Cinema" },
    ],
  },
  {
    id: "fashion",
    name: "Fashion",
    children: [
      { id: "fashion-mens", name: "Men's Apparel" },
      { id: "fashion-womens", name: "Women's Apparel" },
      { id: "fashion-kids", name: "Kids" },
      { id: "fashion-shoes", name: "Shoes" },
      { id: "fashion-accessories", name: "Accessories" },
    ],
  },
  {
    id: "beauty",
    name: "Beauty",
    children: [
      { id: "beauty-skincare", name: "Skincare" },
      { id: "beauty-makeup", name: "Makeup" },
      { id: "beauty-fragrance", name: "Fragrance" },
    ],
  },
  {
    id: "home-living",
    name: "Home & Living",
    children: [
      { id: "home-furniture", name: "Furniture" },
      { id: "home-decor", name: "Decor" },
      { id: "home-kitchen", name: "Kitchenware" },
      { id: "home-bedding", name: "Bedding" },
    ],
  },
  {
    id: "sports",
    name: "Sports",
    children: [
      { id: "sports-fitness", name: "Fitness Equipment" },
      { id: "sports-outdoor", name: "Outdoor & Camping" },
      { id: "sports-cycling", name: "Cycling" },
    ],
  },
  {
    id: "food-beverage",
    name: "Food & Beverage",
    children: [
      { id: "food-grocery", name: "Grocery" },
      { id: "food-beverages", name: "Beverages" },
      { id: "food-snacks", name: "Snacks & Confectionery" },
    ],
  },
];

function findCategoryName(tree: CategoryTreeNode[], id: string): string | undefined {
  for (const n of tree) {
    if (n.id === id) return n.name;
    if (n.children?.length) {
      const found = findCategoryName(n.children, id);
      if (found) return found;
    }
  }
  return undefined;
}

function collectExpandableCategoryIds(tree: CategoryTreeNode[], acc: string[] = []): string[] {
  for (const n of tree) {
    if (n.children?.length) {
      acc.push(n.id);
      collectExpandableCategoryIds(n.children, acc);
    }
  }
  return acc;
}

function CategoryTreePicker({
  tree,
  selectedIds,
  onToggle,
  filter,
}: {
  tree: CategoryTreeNode[];
  selectedIds: string[];
  onToggle: (id: string) => void;
  filter: string;
}) {
  const allExpandableIds = useMemo(() => collectExpandableCategoryIds(tree), [tree]);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(allExpandableIds));

  const filterLower = filter.trim().toLowerCase();

  const nodeMatches = (n: CategoryTreeNode): boolean => {
    if (!filterLower) return true;
    if (n.name.toLowerCase().includes(filterLower)) return true;
    return n.children?.some(nodeMatches) ?? false;
  };

  // When filtering, force-expand all so matches deep in the tree are visible.
  const expandedEffective = filterLower ? new Set(allExpandableIds) : expanded;

  const toggleExpand = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const renderNode = (node: CategoryTreeNode, depth = 0): React.ReactNode => {
    if (!nodeMatches(node)) return null;
    const hasChildren = Boolean(node.children?.length);
    const isOpen = hasChildren && expandedEffective.has(node.id);
    const isChecked = selectedIds.includes(node.id);

    const rowCls = `group flex w-full items-center gap-1 rounded p-1.5 text-[13px] transition-colors cursor-pointer select-none ${
      isChecked ? "bg-primary/10 text-primary font-semibold" : "text-on-surface-variant hover:bg-surface-container-high"
    }`;

    if (!hasChildren) {
      return (
        <div key={node.id} style={{ marginLeft: depth ? 24 : 0 }}>
          <label className={rowCls}>
            <span className="inline-flex w-5 shrink-0" aria-hidden />
            <input
              type="checkbox"
              checked={isChecked}
              onChange={() => onToggle(node.id)}
              className="h-3.5 w-3.5 shrink-0 accent-primary"
            />
            <IconTag className="h-4 w-4 shrink-0 opacity-80" />
            <span className="truncate">{node.name}</span>
          </label>
        </div>
      );
    }

    return (
      <div key={node.id} className={depth ? "mt-1" : ""}>
        <label className={rowCls}>
          <button
            type="button"
            className="shrink-0 rounded p-0.5 hover:bg-surface-container-high"
            aria-expanded={isOpen}
            onClick={(e) => {
              e.preventDefault();
              toggleExpand(node.id);
            }}
          >
            {isOpen ? <IconChevronDown className="h-4 w-4" /> : <IconChevronRight className="h-4 w-4" />}
          </button>
          <input
            type="checkbox"
            checked={isChecked}
            onChange={() => onToggle(node.id)}
            className="h-3.5 w-3.5 shrink-0 accent-primary"
          />
          <span className="flex min-w-0 flex-1 items-center gap-1.5">
            {isOpen ? (
              <IconFolderOpen className="h-4 w-4 shrink-0 text-primary/80" />
            ) : (
              <IconFolder className="h-4 w-4 shrink-0 text-primary/60" />
            )}
            <span className="truncate">{node.name}</span>
          </span>
        </label>
        {isOpen && node.children && (
          <div className="ml-6 mt-1 space-y-1 border-l border-outline-variant/30 pl-2">
            {node.children.map((child) => renderNode(child, depth + 1))}
          </div>
        )}
      </div>
    );
  };

  const visibleRoots = tree.filter(nodeMatches);

  return (
    <div className="rounded-lg border border-outline-variant/20 bg-surface-container-lowest">
      <div className="flex items-center justify-between border-b border-outline-variant/10 px-3 py-1.5">
        <span className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
          Hierarchy
        </span>
        <div className="flex gap-1">
          <button
            type="button"
            className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high"
            title="Expand all"
            onClick={() => setExpanded(new Set(allExpandableIds))}
          >
            <IconUnfoldMore className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high"
            title="Collapse all"
            onClick={() => setExpanded(new Set())}
          >
            <IconUnfoldLess className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
      <div className="max-h-64 space-y-1 overflow-y-auto p-2">
        {visibleRoots.length === 0 ? (
          <p className="px-2 py-4 text-center text-[11px] italic text-on-surface-variant">
            No categories match &ldquo;{filter}&rdquo;.
          </p>
        ) : (
          visibleRoots.map((n) => renderNode(n))
        )}
      </div>
    </div>
  );
}

export function TenantFormPage({ mode, tenant, authToken, onSave, onCancel }: TenantFormPageProps) {
  const isAdd = mode === "add";

  // General
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [overview, setOverview] = useState("");
  const [additionalInfo, setAdditionalInfo] = useState("");

  // Configuration
  const categoryPickerRef = useRef<HTMLDivElement>(null);
  const [categoryOpen, setCategoryOpen] = useState(false);
  const [categoryFilter, setCategoryFilter] = useState("");
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<string[]>([]);
  const [navStyle, setNavStyle] = useState<NavStyle>("Standard");
  const [distributionCenter, setDistributionCenter] = useState("");

  // Branding
  const [logoSrc, setLogoSrc] = useState("");
  const [imageSrc, setImageSrc] = useState("");

  // Contact
  const [officeContact1, setOfficeContact1] = useState("");
  const [officeContact2, setOfficeContact2] = useState("");
  const [contactName, setContactName] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [contactNo, setContactNo] = useState("");
  const [address, setAddress] = useState("");

  /** Persisted field — maps to <c>dcms_tenants.brand_count</c>. */
  const [portfolioBrandCount, setPortfolioBrandCount] = useState(0);
  const [active, setActive] = useState(true);

  // Brands
  const [brandInput, setBrandInput] = useState("");
  const [brandDropOpen, setBrandDropOpen] = useState(false);
  const [selectedBrands, setSelectedBrands] = useState<string[]>([]);

  const [showSuccess, setShowSuccess] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (categoryPickerRef.current && !categoryPickerRef.current.contains(e.target as Node)) {
        setCategoryOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  useEffect(() => {
    setSaveError(null);
    if (isAdd) {
      setCode("");
      setName("");
      setDescription("");
      setOverview("");
      setAdditionalInfo("");
      setSelectedCategoryIds([]);
      setCategoryFilter("");
      setNavStyle("Standard");
      setDistributionCenter("");
      setLogoSrc("");
      setImageSrc("");
      setOfficeContact1("");
      setOfficeContact2("");
      setContactName("");
      setContactEmail("");
      setContactNo("");
      setAddress("");
      setPortfolioBrandCount(0);
      setActive(true);
      setSelectedBrands([]);
      return;
    }
    if (!tenant) return;
    setCode(tenant.code);
    setName(tenant.name);
    setContactName(tenant.contactName);
    setContactEmail(tenant.contactEmail);
    setPortfolioBrandCount(tenant.brandCount);
    setActive(tenant.active);
  }, [isAdd, tenant?.id]);

  const filteredBrands = MOCK_BRANDS.filter(
    (b) => b.toLowerCase().includes(brandInput.toLowerCase()) && !selectedBrands.includes(b),
  );

  function toggleCategory(id: string) {
    setSelectedCategoryIds((prev) =>
      prev.includes(id) ? prev.filter((c) => c !== id) : [...prev, id],
    );
  }

  function addBrand(brand: string) {
    if (!selectedBrands.includes(brand)) {
      setSelectedBrands((prev) => [...prev, brand]);
    }
    setBrandInput("");
    setBrandDropOpen(false);
  }

  function removeBrand(brand: string) {
    setSelectedBrands((prev) => prev.filter((b) => b !== brand));
  }

  async function handleSave() {
    setSaveError(null);
    const codeErr = validateTenantCode(code);
    if (codeErr) {
      setSaveError(codeErr);
      return;
    }
    if (!name.trim()) {
      setSaveError("Name is required.");
      return;
    }
    if (!isAdd && !tenant?.id) {
      setSaveError("Missing tenant id.");
      return;
    }
    setSaving(true);
    try {
      const payload = {
        code: code.trim(),
        name: name.trim(),
        contactName: contactName.trim(),
        contactEmail: contactEmail.trim(),
        brandCount: Math.max(0, Math.floor(portfolioBrandCount)),
        active,
      };
      if (isAdd) {
        await createTenant(payload, authToken);
      } else {
        await updateTenant(tenant!.id, payload, authToken);
      }
      setShowSuccess(true);
    } catch (e: unknown) {
      setSaveError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="relative -m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      {saving && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-surface/60 backdrop-blur-sm">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      )}
      {/* Top bar */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button type="button" onClick={onCancel} className="flex items-center gap-2 text-primary font-bold text-xs uppercase tracking-tighter mb-1 hover:opacity-80">
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Tenants
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? "Add New Tenant" : `Edit Tenant: ${name}`}
          </h2>
          {saveError && (
            <p className="mt-3 max-w-xl rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-xs font-medium text-error" role="alert">
              {saveError}
            </p>
          )}
        </div>
        <div className="flex items-center gap-3">
          <button type="button" className="rounded-md border border-outline-variant/30 px-5 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors" onClick={onCancel}>
            Cancel
          </button>
          <button
            type="button"
            disabled={!name.trim() || saving}
            className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
            onClick={() => void handleSave()}
          >
            <IconSave className="h-4 w-4 shrink-0" />
            {isAdd ? "Create Tenant" : "Save Changes"}
          </button>
        </div>
      </div>

      {/* Body */}
      <div className="flex-1 p-6 space-y-6 max-w-4xl">

        {/* General */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm p-6">
          <h3 className={sectionTitleRow}>General</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className={labelBase}>Code <span className="text-error">*</span></label>
              <input
                className={`${inputBase} font-mono ${!isAdd ? "bg-surface-container-high text-on-surface-variant cursor-not-allowed" : ""}`}
                value={code}
                onChange={(e) => isAdd && setCode(e.target.value)}
                readOnly={!isAdd}
                placeholder="e.g. GES"
              />
              {!isAdd && <p className="mt-1 text-[10px] text-on-surface-variant italic">Code cannot be changed after creation.</p>}
            </div>
            <div>
              <label className={labelBase}>Name <span className="text-error">*</span></label>
              <input className={inputBase} value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Global Enterprise Solutions" />
            </div>
            {!isAdd && (
              <div className="md:col-span-2">
                <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 select-none hover:border-primary/30 transition-colors">
                  <input
                    type="checkbox"
                    className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                    checked={active}
                    onChange={(e) => setActive(e.target.checked)}
                  />
                  <div>
                    <p className="text-xs font-bold text-on-surface">Active tenant</p>
                    <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                      Inactive tenants remain in the database but should not be treated as operational Siêu thị.
                    </p>
                  </div>
                </label>
              </div>
            )}
            <div className="md:col-span-2">
              <label className={labelBase}>Description</label>
              <textarea className={`${inputBase} resize-none`} rows={3} value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Short tenant description." />
            </div>
            <div>
              <label className={labelBase}>Overview</label>
              <textarea className={`${inputBase} resize-none`} rows={3} value={overview} onChange={(e) => setOverview(e.target.value)} placeholder="Overview text for the storefront." />
            </div>
            <div>
              <label className={labelBase}>Additional Information</label>
              <textarea className={`${inputBase} resize-none`} rows={3} value={additionalInfo} onChange={(e) => setAdditionalInfo(e.target.value)} placeholder="Any supplementary notes." />
            </div>
          </div>
        </section>

        {/* Configuration */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm p-6">
          <h3 className={sectionTitleRow}>Configuration</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            {/* Category picker — tree view (mirrors CategoriesPage hierarchy) */}
            <div className="md:col-span-2">
              <label className={labelBase}>Category</label>
              <div ref={categoryPickerRef} className="relative space-y-2">
                {selectedCategoryIds.length > 0 && (
                  <div className="flex flex-wrap gap-1.5">
                    {selectedCategoryIds.map((id) => {
                      const name = findCategoryName(MOCK_CATEGORY_TREE, id) ?? id;
                      return (
                        <span key={id} className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary">
                          {name}
                          <button type="button" aria-label={`Remove ${name}`} className="rounded p-0.5 hover:bg-primary/20 transition-colors" onClick={() => toggleCategory(id)}>
                            <IconClose className="h-2.5 w-2.5" />
                          </button>
                        </span>
                      );
                    })}
                  </div>
                )}

                <button
                  type="button"
                  className={`${inputBase} flex items-center justify-between gap-2 text-left`}
                  onClick={() => setCategoryOpen((v) => !v)}
                >
                  <span className={selectedCategoryIds.length ? "text-on-surface" : "italic text-on-surface-variant"}>
                    {selectedCategoryIds.length
                      ? `${selectedCategoryIds.length} selected`
                      : "Click to choose categories"}
                  </span>
                  <IconChevronDown
                    className={`h-4 w-4 shrink-0 text-outline transition-transform ${categoryOpen ? "rotate-180" : ""}`}
                  />
                </button>

                {categoryOpen && (
                  <div className="absolute left-0 right-0 top-full z-40 mt-1 rounded-lg border border-outline-variant/20 bg-surface shadow-xl p-2">
                    <div className="relative mb-2">
                      <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
                      <input
                        className={`${inputBase} pl-8`}
                        value={categoryFilter}
                        onChange={(e) => setCategoryFilter(e.target.value)}
                        placeholder="Filter categories…"
                        autoFocus
                      />
                    </div>
                    <CategoryTreePicker
                      tree={MOCK_CATEGORY_TREE}
                      selectedIds={selectedCategoryIds}
                      onToggle={toggleCategory}
                      filter={categoryFilter}
                    />
                  </div>
                )}
              </div>
              <p className={hintText}>Pick one or more categories from the hierarchy. Sub-categories can be selected independently of their parent.</p>
            </div>
            <div>
              <label className={labelBase}>Navigation Style</label>
              <select className={inputBase} value={navStyle} onChange={(e) => setNavStyle(e.target.value as NavStyle)}>
                {NAV_STYLES.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
            <div>
              <label className={labelBase}>Distribution Center</label>
              <input className={inputBase} value={distributionCenter} onChange={(e) => setDistributionCenter(e.target.value)} placeholder="e.g. DC-Central" />
            </div>
          </div>
        </section>

        {/* Branding */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm p-6">
          <h3 className={sectionTitleRow}>Branding</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <ImageSlot label="Logo" src={logoSrc || undefined} onFileSelected={setLogoSrc} onRemove={() => setLogoSrc("")} />
            <ImageSlot label="Cover Image" src={imageSrc || undefined} onFileSelected={setImageSrc} onRemove={() => setImageSrc("")} />
          </div>
        </section>

        {/* Contact */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm p-6">
          <h3 className={sectionTitleRow}>Contact</h3>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className={labelBase}>Office Contact No. 1</label>
              <input className={inputBase} value={officeContact1} onChange={(e) => setOfficeContact1(e.target.value)} placeholder="+1 (555) 000-0001" />
            </div>
            <div>
              <label className={labelBase}>Office Contact No. 2</label>
              <input className={inputBase} value={officeContact2} onChange={(e) => setOfficeContact2(e.target.value)} placeholder="+1 (555) 000-0002" />
            </div>
            <div>
              <label className={labelBase}>Contact Name</label>
              <input className={inputBase} value={contactName} onChange={(e) => setContactName(e.target.value)} placeholder="e.g. Jane Smith" />
            </div>
            <div>
              <label className={labelBase}>Contact Email</label>
              <input className={inputBase} type="email" value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} placeholder="contact@tenant.com" />
            </div>
            <div>
              <label className={labelBase}>Contact No.</label>
              <input className={inputBase} value={contactNo} onChange={(e) => setContactNo(e.target.value)} placeholder="+1 (555) 000-0000" />
            </div>
            <div className="md:col-span-2">
              <label className={labelBase}>Tenant Address</label>
              <textarea className={`${inputBase} resize-none`} rows={3} value={address} onChange={(e) => setAddress(e.target.value)} placeholder="Full mailing address" />
            </div>
          </div>
        </section>

        {/* Brands */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm p-6">
          <h3 className={sectionTitleRow}>Brands</h3>
          <div className="mb-5">
            <label className={labelBase}>Portfolio brand count (saved to API)</label>
            <input
              type="number"
              min={0}
              step={1}
              className={inputBase}
              value={Number.isNaN(portfolioBrandCount) ? 0 : portfolioBrandCount}
              onChange={(e) => {
                const v = e.target.value;
                setPortfolioBrandCount(v === "" ? 0 : Math.max(0, parseInt(v, 10) || 0));
              }}
            />
            <p className={hintText}>Stored on the tenant row as brand portfolio size (not tied to the mock brand picker below).</p>
          </div>
          {selectedBrands.length > 0 && (
            <div className="flex flex-wrap gap-1.5 mb-3">
              {selectedBrands.map((b) => (
                <span key={b} className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold text-primary">
                  {b}
                  <button type="button" aria-label={`Remove ${b}`} className="rounded p-0.5 hover:bg-primary/20 transition-colors" onClick={() => removeBrand(b)}>
                    <IconClose className="h-2.5 w-2.5" />
                  </button>
                </span>
              ))}
            </div>
          )}
          <div className="relative">
            <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
            <input
              className={`${inputBase} pl-8`}
              value={brandInput}
              onChange={(e) => { setBrandInput(e.target.value); setBrandDropOpen(true); }}
              onFocus={() => setBrandDropOpen(true)}
              onBlur={() => setTimeout(() => setBrandDropOpen(false), 150)}
              placeholder="Search and assign brands to this tenant…"
            />
          </div>
          {brandDropOpen && filteredBrands.length > 0 && (
            <div className="mt-1 max-h-48 overflow-y-auto rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
              {filteredBrands.map((b) => (
                <button
                  key={b}
                  type="button"
                  className="w-full text-left flex items-center gap-3 px-4 py-2.5 text-xs text-on-surface hover:bg-surface-container-high transition-colors"
                  onClick={() => addBrand(b)}
                >
                  {b}
                </button>
              ))}
            </div>
          )}
        </section>
      </div>

      {/* Success modal */}
      {showSuccess && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm" onClick={() => setShowSuccess(false)}>
          <div className="bg-surface-container-lowest rounded-2xl shadow-2xl p-8 max-w-sm w-full mx-4 text-center" onClick={(e) => e.stopPropagation()}>
            <div className="w-16 h-16 rounded-full bg-secondary-container/20 flex items-center justify-center mx-auto mb-4">
              <IconCheckCircle className="h-8 w-8 text-secondary" />
            </div>
            <h3 className="text-lg font-bold text-on-surface mb-2">
              {isAdd ? "Tenant Created!" : "Changes Saved!"}
            </h3>
            <p className="text-sm text-on-surface-variant mb-6">
              {isAdd ? `"${name}" has been created as a new tenant.` : `"${name}" has been updated successfully.`}
            </p>
            <div className="flex gap-3 justify-center">
              <button type="button" className="px-5 py-2 rounded-md border border-outline-variant/30 text-on-surface-variant font-bold text-xs uppercase tracking-widest hover:bg-surface-container-high transition-colors" onClick={() => { setShowSuccess(false); onSave?.(); }}>
                Back to Tenants
              </button>
              <button type="button" className="px-5 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest hover:bg-primary-container transition-colors" onClick={() => setShowSuccess(false)}>
                {isAdd ? "Add Another" : "Continue Editing"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
