import { useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconClose,
  IconDelete,
  IconImage,
  IconSave,
  IconSearch,
} from "../../../orders/icons";

// TODO: Replace with real API calls:
//   GET  /umbraco/dcms/api/access/tenants/{tenantId}  (edit mode)
//   POST /umbraco/dcms/api/access/tenants              (add mode)
//   PUT  /umbraco/dcms/api/access/tenants/{tenantId}   (edit mode)

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const sectionTitleRow =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2 mb-5";

type TenantFormPageProps = {
  mode: "add" | "edit";
  tenantId?: string;
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
const MOCK_CATEGORIES = ["Electronics", "Fashion", "Beauty", "Home & Living", "Sports", "Food & Beverage"];

export function TenantFormPage({ mode, tenantId, onSave, onCancel }: TenantFormPageProps) {
  const isAdd = mode === "add";

  // General
  const [code, setCode] = useState(isAdd ? "" : `T-${tenantId ?? "001"}`);
  const [name, setName] = useState(isAdd ? "" : "Sample Tenant");
  const [description, setDescription] = useState(isAdd ? "" : "A demo tenant for backoffice review.");
  const [overview, setOverview] = useState("");
  const [additionalInfo, setAdditionalInfo] = useState("");

  // Configuration
  const [categoryInput, setCategoryInput] = useState("");
  const [categoryOpen, setCategoryOpen] = useState(false);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
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

  // Brands
  const [brandInput, setBrandInput] = useState("");
  const [brandDropOpen, setBrandDropOpen] = useState(false);
  const [selectedBrands, setSelectedBrands] = useState<string[]>([]);

  const [showSuccess, setShowSuccess] = useState(false);

  const filteredCategories = MOCK_CATEGORIES.filter((c) =>
    c.toLowerCase().includes(categoryInput.toLowerCase()),
  );
  const filteredBrands = MOCK_BRANDS.filter(
    (b) => b.toLowerCase().includes(brandInput.toLowerCase()) && !selectedBrands.includes(b),
  );

  function toggleCategory(cat: string) {
    setSelectedCategories((prev) =>
      prev.includes(cat) ? prev.filter((c) => c !== cat) : [...prev, cat],
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

  function handleSave() {
    if (!name.trim()) return;
    setShowSuccess(true);
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      {/* Top bar */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          {!isAdd && (
            <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
              <span>eStore</span>
              <span className="mx-2">/</span>
              <span>Access</span>
              <span className="mx-2">/</span>
              <button type="button" className="text-on-surface-variant hover:text-primary transition-colors" onClick={onCancel}>
                Tenants
              </button>
              <span className="mx-2">/</span>
              <span className="text-primary">Edit Tenant</span>
            </nav>
          )}
          <button type="button" onClick={onCancel} className="flex items-center gap-2 text-primary font-bold text-xs uppercase tracking-tighter mb-1 hover:opacity-80">
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Tenants
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? "Add New Tenant" : `Edit Tenant: ${name}`}
          </h2>
        </div>
        <div className="flex items-center gap-3">
          <button type="button" className="rounded-md border border-outline-variant/30 px-5 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors" onClick={onCancel}>
            Cancel
          </button>
          <button
            type="button"
            disabled={!name.trim()}
            className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
            onClick={handleSave}
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
            {/* Category picker */}
            <div className="md:col-span-2 relative">
              <label className={labelBase}>Category</label>
              {selectedCategories.length > 0 && (
                <div className="flex flex-wrap gap-1.5 mb-2">
                  {selectedCategories.map((cat) => (
                    <span key={cat} className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary">
                      {cat}
                      <button type="button" aria-label={`Remove ${cat}`} className="rounded p-0.5 hover:bg-primary/20 transition-colors" onClick={() => toggleCategory(cat)}>
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
                  value={categoryInput}
                  onChange={(e) => { setCategoryInput(e.target.value); setCategoryOpen(true); }}
                  onFocus={() => setCategoryOpen(true)}
                  onBlur={() => setTimeout(() => setCategoryOpen(false), 150)}
                  placeholder="Type to search categories…"
                />
              </div>
              {categoryOpen && filteredCategories.length > 0 && (
                <div className="absolute left-0 right-0 top-full z-30 mt-1 max-h-48 overflow-y-auto rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
                  {filteredCategories.map((cat) => {
                    const checked = selectedCategories.includes(cat);
                    return (
                      <label key={cat} className={`flex cursor-pointer items-center gap-3 px-4 py-2.5 text-xs transition-colors hover:bg-surface-container-high ${checked ? "bg-primary/5 text-primary font-semibold" : "text-on-surface"}`}>
                        <input type="checkbox" checked={checked} onChange={() => toggleCategory(cat)} className="h-3.5 w-3.5 accent-primary shrink-0" />
                        {cat}
                      </label>
                    );
                  })}
                </div>
              )}
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
