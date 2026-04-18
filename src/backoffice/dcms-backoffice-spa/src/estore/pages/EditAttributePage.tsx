import { useEffect, useRef, useState } from "react";
import {
  IconAddCircle,
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconCloudUpload,
  IconDelete,
  IconDownload,
  IconEdit,
  IconSearch,
} from "../../orders/icons";
import type { AttributeListRow } from "../attributes-columns";
import { exportSingleAttributeValuesXlsx } from "../exportAttributeTemplates";

const MAX_IMAGE_UPLOAD_BYTES = 2 * 1024 * 1024;

// ── Style tokens ─────────────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const selectBase =
  "bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const tabBase =
  "px-5 py-2.5 text-xs font-bold uppercase tracking-widest transition-colors border-b-2";
const tabActive = "border-primary text-primary";
const tabInactive = "border-transparent text-on-surface-variant hover:text-on-surface";

// ── Types ─────────────────────────────────────────────────────────────────────
type ValueType = "Text" | "Color" | "Image";
type AttrValue = { id: string; name: string; value: string };

// ── Mock data ─────────────────────────────────────────────────────────────────
const MOCK_CATEGORIES = [
  "Electronics", "Furniture", "Timepieces > Luxury", "Audio > Wireless",
  "Footwear > Athletics", "Cameras > Instant", "Anniversary", "CGCategory",
];

const MOCK_BRANDS = [
  "Cronos Ltd.", "SoundWave Co.", "Apex Footwear", "NovaCam", "FurniCraft",
];

function buildInitialValues(row?: AttributeListRow): AttrValue[] {
  if (!row) return [];
  if (row.type === "COLOR") {
    return [
      { id: "v1", name: "Midnight Black", value: "#1a1a1a" },
      { id: "v2", name: "Ocean Blue",     value: "#1565c0" },
      { id: "v3", name: "Forest Green",   value: "#2e7d32" },
    ];
  }
  if (row.type === "IMAGE") {
    return [
      { id: "v1", name: "Lifestyle Shot",  value: "lifestyle.jpg" },
      { id: "v2", name: "Detail Close-up", value: "detail.jpg" },
    ];
  }
  return [
    { id: "v1", name: "Option A", value: "option_a" },
    { id: "v2", name: "Option B", value: "option_b" },
  ];
}

// ── Inline SearchablePicker (categories or brands) ────────────────────────────
function SearchablePicker({
  label,
  options,
  selected,
  onChange,
}: {
  label: string;
  options: string[];
  selected: string[];
  onChange: (next: string[]) => void;
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

  const filtered = options.filter((o) =>
    o.toLowerCase().includes(search.toLowerCase())
  );

  function toggle(opt: string) {
    onChange(
      selected.includes(opt)
        ? selected.filter((s) => s !== opt)
        : [...selected, opt]
    );
  }

  return (
    <div ref={ref} className="relative space-y-1.5">
      <p className={labelBase}>{label}</p>
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
          {selected.length === 0
            ? `Search ${label.toLowerCase()}…`
            : `${selected.length} selected`}
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
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-primary"
                    checked={selected.includes(opt)}
                    onChange={() => toggle(opt)}
                  />
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

// ── Component ─────────────────────────────────────────────────────────────────
type Props = {
  mode: "add" | "edit";
  attribute?: AttributeListRow;
  onBack: () => void;
};

export function EditAttributePage({ mode, attribute, onBack }: Props) {
  const isAdd = mode === "add";

  // ── General state ─────────────────────────────────────────────────────────
  const [tab, setTab] = useState<"general" | "advanced">("general");
  const [name, setName] = useState(attribute?.name ?? "");
  const [code, setCode] = useState(attribute?.code ?? "");
  const [searchFilterOnly, setSearchFilterOnly] = useState(attribute?.required ?? false);
  const [valueType, setValueType] = useState<ValueType>(
    attribute?.type === "COLOR" ? "Color" : attribute?.type === "IMAGE" ? "Image" : "Text"
  );
  const [values, setValues] = useState<AttrValue[]>(buildInitialValues(attribute));

  // Add-value inline form
  const [addingValue, setAddingValue] = useState(false);
  const [editingValueId, setEditingValueId] = useState<string | null>(null);
  const [draftValueName, setDraftValueName] = useState("");
  const [draftValue, setDraftValue] = useState("");

  // ── Advanced state ────────────────────────────────────────────────────────
  const [useAsSearchFilter, setUseAsSearchFilter] = useState(false);
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [selectedBrands, setSelectedBrands] = useState<string[]>([]);

  // ── Generate Forms dropdown ───────────────────────────────────────────────
  const [genFormsOpen, setGenFormsOpen] = useState(false);
  const genFormsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (genFormsRef.current && !genFormsRef.current.contains(e.target as Node)) setGenFormsOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // ── Actions dropdown ──────────────────────────────────────────────────────
  const [actionsOpen, setActionsOpen] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);
  const imageFileRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node)) setActionsOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // ── Toast ─────────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Auto-slug code from name ──────────────────────────────────────────────
  function handleNameBlur() {
    if (!code && name) {
      setCode(name.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, ""));
    }
  }

  async function exportValues() {
    const attrName = name || attribute?.name || "attribute";
    const attrCode = code || attribute?.code || "attribute";
    await exportSingleAttributeValuesXlsx(
      attrName,
      attrCode,
      values.map((v) => ({ name: v.name, value: v.value }))
    );
  }

  // ── Save ──────────────────────────────────────────────────────────────────
  function handleSave() {
    setActionsOpen(false);
    setToast(isAdd ? `Attribute "${name}" created successfully.` : `Attribute "${name}" updated successfully.`);
    setTimeout(() => onBack(), 1800);
  }

  // ── Value table actions ───────────────────────────────────────────────────
  function openAddValue() {
    setEditingValueId(null);
    setDraftValueName("");
    setDraftValue(valueType === "Color" ? "#000000" : "");
    setAddingValue(true);
  }

  function openEditValue(v: AttrValue) {
    setEditingValueId(v.id);
    setDraftValueName(v.name);
    setDraftValue(v.value);
    setAddingValue(true);
  }

  function cancelValueForm() {
    setAddingValue(false);
    setEditingValueId(null);
  }

  function saveValue() {
    if (!draftValueName.trim()) return;
    if (valueType === "Image" && !draftValue.trim()) {
      setToast("Add an image (upload) or paste a URL / filename for the value.");
      return;
    }
    if (editingValueId) {
      setValues((prev) =>
        prev.map((v) => v.id === editingValueId ? { ...v, name: draftValueName, value: draftValue } : v)
      );
    } else {
      setValues((prev) => [...prev, { id: `v${Date.now()}`, name: draftValueName, value: draftValue }]);
    }
    setAddingValue(false);
    setEditingValueId(null);
  }

  function deleteValue(id: string) {
    setValues((prev) => prev.filter((v) => v.id !== id));
  }

  // ── Value preview cell ────────────────────────────────────────────────────
  function ValuePreview({ v }: { v: AttrValue }) {
    if (valueType === "Color") {
      return (
        <span className="inline-flex items-center gap-2">
          <span className="h-5 w-5 rounded border border-outline-variant/20 shadow-sm" style={{ background: v.value }} />
          <span className="font-mono text-[10px] text-on-surface-variant">{v.value}</span>
        </span>
      );
    }
    if (valueType === "Image") {
      const isUrl = /^https?:\/\//i.test(v.value) || v.value.startsWith("data:image/");
      if (isUrl) {
        return (
          <span className="inline-flex items-center gap-2">
            <img
              src={v.value}
              alt=""
              className="h-10 w-10 rounded border border-outline-variant/20 object-cover bg-surface-container-high"
            />
            <span className="max-w-[140px] truncate font-mono text-[10px] text-on-surface-variant" title={v.value}>
              {v.value.startsWith("data:") ? "Uploaded image" : v.value}
            </span>
          </span>
        );
      }
      return (
        <span className="inline-flex items-center gap-1.5 text-[10px] text-on-surface-variant">
          <span className="rounded bg-surface-container-high px-1.5 py-0.5 font-mono">{v.value}</span>
        </span>
      );
    }
    return <span className="font-mono text-[10px] text-on-surface-variant">{v.value}</span>;
  }

  function onImageFilePick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    if (!file.type.startsWith("image/")) {
      setToast("Please choose an image file.");
      return;
    }
    if (file.size > MAX_IMAGE_UPLOAD_BYTES) {
      setToast("Image must be 2 MB or smaller.");
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string") setDraftValue(reader.result);
    };
    reader.readAsDataURL(file);
  }

  // ── Inline value input ────────────────────────────────────────────────────
  function ValueInput() {
    if (valueType === "Color") {
      return (
        <div className="flex items-center gap-2">
          <input
            type="color"
            value={draftValue}
            onChange={(e) => setDraftValue(e.target.value)}
            className="h-8 w-12 cursor-pointer rounded border border-outline-variant/20 bg-surface-container-lowest p-0.5"
          />
          <input
            type="text"
            value={draftValue}
            onChange={(e) => setDraftValue(e.target.value)}
            placeholder="#000000"
            className={`${inputBase} w-28`}
          />
        </div>
      );
    }
    if (valueType === "Image") {
      return (
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <input ref={imageFileRef} type="file" accept="image/*" className="hidden" onChange={onImageFilePick} />
          <button
            type="button"
            className="inline-flex shrink-0 items-center gap-2 rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-[10px] font-bold uppercase tracking-wide text-primary hover:bg-primary/10"
            onClick={() => imageFileRef.current?.click()}
          >
            <IconCloudUpload className="h-4 w-4 shrink-0" />
            Upload image
          </button>
          <input
            type="text"
            value={draftValue.startsWith("data:image/") ? "" : draftValue}
            onChange={(e) => setDraftValue(e.target.value)}
            placeholder="Or paste image URL / filename"
            className={`${inputBase} min-w-0 flex-1`}
          />
          {draftValue.startsWith("data:image/") && (
            <button
              type="button"
              className="shrink-0 text-[10px] font-bold uppercase tracking-wide text-error hover:underline"
              onClick={() => setDraftValue("")}
            >
              Clear image
            </button>
          )}
        </div>
      );
    }
    return (
      <input
        type="text"
        value={draftValue}
        onChange={(e) => setDraftValue(e.target.value)}
        placeholder="Value"
        className={inputBase}
      />
    );
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Attributes
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? "Add Attribute" : "Edit Attribute"}
          </h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            {isAdd
              ? "Define a new product attribute and its values."
              : `Editing attribute: ${attribute?.name}`}
          </p>
        </div>

        <div className="flex items-center gap-3">
          {/* Generate Forms dropdown */}
          <div className="relative" ref={genFormsRef}>
            <button
              type="button"
              className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-4 py-2.5 text-xs font-bold text-on-surface transition-colors hover:bg-surface-container-high"
              onClick={() => setGenFormsOpen((o) => !o)}
            >
              Generate Forms
              <IconChevronDown className={`h-3.5 w-3.5 shrink-0 text-on-surface-variant transition-transform ${genFormsOpen ? "rotate-180" : ""}`} />
            </button>
            {genFormsOpen && (
              <div className="absolute right-0 top-full z-20 mt-1 w-44 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => {
                    setGenFormsOpen(false);
                    void exportValues();
                  }}
                >
                  <IconDownload className="h-4 w-4 shrink-0 text-primary" />
                  Export Values
                </button>
              </div>
            )}
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
              <div className="absolute right-0 top-full z-20 mt-1 w-44 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={handleSave}
                >
                  <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
                  Save
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Tabs ────────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 gap-0 border-b border-outline-variant/15 bg-surface px-6">
        {(["general", "advanced"] as const).map((t) => (
          <button
            key={t}
            type="button"
            className={`${tabBase} ${tab === t ? tabActive : tabInactive}`}
            onClick={() => setTab(t)}
          >
            {t === "general" ? "General" : "Advanced"}
          </button>
        ))}
      </div>

      {/* ── Body ────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* ══ GENERAL TAB ══════════════════════════════════════════════════ */}
        {tab === "general" && (
          <>
            {/* Basic info */}
            <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
              <h3 className="mb-5 text-sm font-bold uppercase tracking-widest text-on-surface">Attribute Information</h3>
              <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                {/* Name */}
                <div>
                  <label className={labelBase}>Name <span className="text-error">*</span></label>
                  <input
                    type="text"
                    className={inputBase}
                    placeholder="e.g. Primary Color"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    onBlur={handleNameBlur}
                  />
                  <p className="mt-1 text-[10px] text-on-surface-variant">
                    Displayed in the eStore.
                  </p>
                </div>

                {/* Code */}
                <div>
                  <label className={labelBase}>Code <span className="text-error">*</span></label>
                  <input
                    type="text"
                    className={inputBase}
                    placeholder="e.g. color_primary"
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                  />
                  <p className="mt-1 text-[10px] text-on-surface-variant">
                    Auto-generated from Name; must be unique.
                  </p>
                </div>

                {/* Search Filter Only */}
                <div className="md:col-span-2">
                  <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none">
                    <input
                      type="checkbox"
                      className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                      checked={searchFilterOnly}
                      onChange={(e) => setSearchFilterOnly(e.target.checked)}
                    />
                    <div>
                      <p className="text-xs font-bold text-on-surface">Search Filter Only</p>
                      <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                        Use this attribute as a search filter. Filters can be tagged to any product.
                      </p>
                    </div>
                  </label>
                </div>
              </div>
            </section>

            {/* Attribute Values */}
            <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
              <div className="flex flex-wrap items-center gap-4 border-b border-outline-variant/10 px-6 py-4">
                <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Attribute Values</h3>

                {/* Value Type selector */}
                <div className="ml-auto flex items-center gap-2">
                  <label className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Value Type:</label>
                  <select
                    className={`${selectBase} text-xs`}
                    value={valueType}
                    onChange={(e) => setValueType(e.target.value as ValueType)}
                  >
                    {(["Text", "Color", "Image"] as ValueType[]).map((vt) => (
                      <option key={vt} value={vt}>{vt}</option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Values table */}
              <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left">
                  <thead>
                    <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                      <th className="px-6 py-3">Value Name</th>
                      <th className="px-4 py-3">Value</th>
                      <th className="px-4 py-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-outline-variant/10">
                    {values.map((v) => (
                      <tr key={v.id} className="text-xs hover:bg-surface-container-low transition-colors">
                        <td className="px-6 py-3.5 font-semibold text-on-surface">{v.name}</td>
                        <td className="px-4 py-3.5"><ValuePreview v={v} /></td>
                        <td className="px-4 py-3.5 text-right">
                          <div className="flex items-center justify-end gap-1">
                            <button
                              type="button"
                              title="Edit"
                              className="rounded p-1.5 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                              onClick={() => openEditValue(v)}
                            >
                              <IconEdit className="h-3.5 w-3.5" />
                            </button>
                            <button
                              type="button"
                              title="Delete"
                              className="rounded p-1.5 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                              onClick={() => deleteValue(v.id)}
                            >
                              <IconDelete className="h-3.5 w-3.5" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                    {values.length === 0 && !addingValue && (
                      <tr>
                        <td colSpan={3} className="px-6 py-8 text-center text-xs italic text-on-surface-variant">
                          No values yet. Click "Add Value" to add the first one.
                        </td>
                      </tr>
                    )}

                    {/* Inline Add / Edit form row */}
                    {addingValue && (
                      <tr className="bg-primary/5">
                        <td className="px-6 py-3">
                          <input
                            autoFocus
                            type="text"
                            className={inputBase}
                            placeholder="Value Name"
                            value={draftValueName}
                            onChange={(e) => setDraftValueName(e.target.value)}
                          />
                        </td>
                        <td className="px-4 py-3">
                          <ValueInput />
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              type="button"
                              className="rounded-md border border-outline-variant/30 px-3 py-1.5 text-[10px] font-bold uppercase text-on-surface-variant hover:bg-surface-container-high transition-colors"
                              onClick={cancelValueForm}
                            >
                              Cancel
                            </button>
                            <button
                              type="button"
                              className="flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-[10px] font-bold uppercase text-on-primary hover:opacity-90 transition-opacity disabled:opacity-40"
                              disabled={!draftValueName.trim()}
                              onClick={saveValue}
                            >
                              <IconCheckCircle className="h-3 w-3 shrink-0" />
                              Save Changes
                            </button>
                          </div>
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              {/* Add Value button */}
              {!addingValue && (
                <div className="border-t border-outline-variant/10 px-6 py-4">
                  <button
                    type="button"
                    className="flex items-center gap-2 rounded-md border border-primary/30 px-4 py-2 text-xs font-bold text-primary transition-colors hover:bg-primary/5"
                    onClick={openAddValue}
                  >
                    <IconAddCircle className="h-4 w-4 shrink-0" />
                    Add Value
                  </button>
                </div>
              )}
            </section>
          </>
        )}

        {/* ══ ADVANCED TAB ═════════════════════════════════════════════════ */}
        {tab === "advanced" && (
          <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
            <h3 className="mb-5 text-sm font-bold uppercase tracking-widest text-on-surface">Product Listing Search</h3>

            <div className="space-y-6">
              {/* Use as Search Filter */}
              <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                  checked={useAsSearchFilter}
                  onChange={(e) => setUseAsSearchFilter(e.target.checked)}
                />
                <div>
                  <p className="text-xs font-bold text-on-surface">Use as Search Filter</p>
                  <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                    Use this attribute as a search filter for the selected categories and brands.
                  </p>
                </div>
              </label>

              {/* Selected Categories */}
              <SearchablePicker
                label="Selected Categories"
                options={MOCK_CATEGORIES}
                selected={selectedCategories}
                onChange={setSelectedCategories}
              />

              {/* Selected Brands */}
              <SearchablePicker
                label="Selected Brands"
                options={MOCK_BRANDS}
                selected={selectedBrands}
                onChange={setSelectedBrands}
              />
            </div>
          </section>
        )}
      </div>

      {/* ── Toast ───────────────────────────────────────────────────────── */}
      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
