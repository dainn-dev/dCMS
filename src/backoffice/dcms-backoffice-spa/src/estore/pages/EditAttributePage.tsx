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
import { MultiLangInput } from "../components/MultiLangField";
import { exportSingleAttributeValuesXlsx } from "../exportAttributeTemplates";
import { fetchBrands } from "../api/brandsApi";
import { fetchCategories } from "../api/categoriesApi";
import type { CatNode } from "./CategoriesPage";
import {
  createAttribute,
  createAttributeValue,
  deleteAttributeValue,
  fetchAttribute,
  fetchAttributeValues,
  updateAttribute,
  updateAttributeValue,
  type AttributeValueRow,
} from "../api/attributesApi";

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

const CODE_RE = /^[a-z][a-z0-9_]*$/;
const HEX_RE = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/;

type GeneralFieldErrors = {
  name?: string;
  code?: string;
  values?: string;
};

type AdvancedFieldErrors = {
  searchFilter?: string;
};

type FieldErrors = {
  general: GeneralFieldErrors;
  advanced: AdvancedFieldErrors;
};

const EMPTY_FIELD_ERRORS: FieldErrors = { general: {}, advanced: {} };

function inputErrorClass(hasError: boolean) {
  return hasError ? "border-error/60 focus:ring-error/40" : "";
}

function validateValueEntry(v: AttrValue, vt: ValueType): string | null {
  if (!v.name.trim()) return "Value name is required.";
  const val = v.value.trim();
  if (vt === "Color") {
    if (!val) return "Color hex is required.";
    if (!HEX_RE.test(val)) return "Enter a valid hex color (e.g. #1565c0).";
  } else if (vt === "Image") {
    if (!val) return "Image URL or file is required.";
  } else if (!val) {
    return "Value is required.";
  }
  return null;
}

// ── Types ─────────────────────────────────────────────────────────────────────
type ValueType = "Text" | "Color" | "Image";
type AttrValue = { id: string; name: string; value: string };

function slugify(raw: string): string {
  return raw.trim().toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, "");
}

/** Prefer `en`, then first non-empty locale (slug / export / toast). */
function primaryLocaleName(values: Record<string, string>) {
  const en = (values.en ?? "").trim();
  if (en) return en;
  for (const v of Object.values(values)) {
    const t = (v ?? "").trim();
    if (t) return t;
  }
  return "";
}

function apiTypeToValueType(type?: AttributeListRow["type"]): ValueType {
  if (type === "COLOR") return "Color";
  if (type === "IMAGE") return "Image";
  return "Text";
}

function isPersistedValueId(id: string): boolean {
  return /^\d+$/.test(id);
}

function valueRowToAttrValue(row: AttributeValueRow, vt: ValueType): AttrValue {
  let value = row.code;
  if (vt === "Color") value = row.colorHex || row.code;
  else if (vt === "Image") value = row.imageUrl || row.code;
  return { id: String(row.id), name: row.name, value };
}

function attrValueToPayload(v: AttrValue, vt: ValueType, sortOrder: number) {
  const code = vt === "Text" ? slugify(v.value) || slugify(v.name) : slugify(v.name);
  if (vt === "Color") {
    return { name: v.name.trim(), code, colorHex: v.value.trim(), sortOrder };
  }
  if (vt === "Image") {
    return { name: v.name.trim(), code, imageUrl: v.value.trim(), sortOrder };
  }
  return { name: v.name.trim(), code, sortOrder };
}

// ── Mock data (Advanced tab only) ───────────────────────────────────────────
type PickerOption = { key: string; label: string };

function flattenCategoryNodes(nodes: CatNode[], prefix = ""): PickerOption[] {
  const out: PickerOption[] = [];
  for (const n of nodes) {
    const label = prefix ? `${prefix} > ${n.name}` : n.name;
    out.push({ key: String(n.id), label });
    if (n.children?.length) out.push(...flattenCategoryNodes(n.children, label));
  }
  return out;
}

function SearchablePicker({
  label,
  inputId,
  options,
  selected,
  onChange,
}: {
  label: string;
  inputId: string;
  options: PickerOption[];
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
    o.label.toLowerCase().includes(search.toLowerCase())
  );

  function toggle(opt: PickerOption) {
    onChange(
      selected.includes(opt.key)
        ? selected.filter((s) => s !== opt.key)
        : [...selected, opt.key]
    );
  }

  const selectedLabels = selected
    .map((key) => options.find((o) => o.key === key)?.label ?? key)
    .filter(Boolean);

  return (
    <div ref={ref} className="relative space-y-1.5">
      <p className={labelBase}>{label}</p>
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selectedLabels.map((s) => (
            <span
              key={s}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
            >
              {s}
              <button
                type="button"
                onClick={() => {
                  const opt = options.find((o) => o.label === s);
                  if (opt) toggle(opt);
                }}
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
              id={`${inputId}-search`}
              name={`${inputId}-search`}
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
                  key={opt.key}
                  className="flex cursor-pointer items-center gap-2.5 px-3 py-2 hover:bg-surface-container transition-colors select-none"
                >
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-primary"
                    checked={selected.includes(opt.key)}
                    onChange={() => toggle(opt)}
                  />
                  <span className="text-xs text-on-surface">{opt.label}</span>
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
  tenantId?: string;
  authToken?: string;
};

export function EditAttributePage({ mode, attribute, onBack, tenantId, authToken }: Props) {
  const isAdd = mode === "add";

  // ── General state ─────────────────────────────────────────────────────────
  const [tab, setTab] = useState<"general" | "advanced">("general");
  const [nameLocales, setNameLocales] = useState<Record<string, string>>(() => ({
    en: attribute?.name ?? "",
  }));
  const [code, setCode] = useState(attribute?.code ?? "");
  const [searchFilterOnly, setSearchFilterOnly] = useState(attribute?.required ?? false);
  const [valueType, setValueType] = useState<ValueType>(() => apiTypeToValueType(attribute?.type));
  const [values, setValues] = useState<AttrValue[]>([]);
  const [valuesLoading, setValuesLoading] = useState(false);
  const [valueSaving, setValueSaving] = useState(false);

  // Add-value inline form
  const [addingValue, setAddingValue] = useState(false);
  const [editingValueId, setEditingValueId] = useState<string | null>(null);
  const [draftValueName, setDraftValueName] = useState("");
  const [draftValue, setDraftValue] = useState("");

  // ── Advanced state ────────────────────────────────────────────────────────
  const [useAsSearchFilter, setUseAsSearchFilter] = useState(false);
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<string[]>([]);
  const [selectedBrandCodes, setSelectedBrandCodes] = useState<string[]>([]);
  const [categoryOptions, setCategoryOptions] = useState<PickerOption[]>([]);
  const [brandOptions, setBrandOptions] = useState<PickerOption[]>([]);
  const [detailLoading, setDetailLoading] = useState(false);

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
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>(EMPTY_FIELD_ERRORS);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  // Load category / brand pickers for Advanced tab
  useEffect(() => {
    if (!tenantId) return;
    let cancelled = false;
    Promise.all([fetchCategories(tenantId, authToken), fetchBrands(tenantId, { pageSize: 200 }, authToken)])
      .then(([cats, brandsResult]) => {
        if (cancelled) return;
        setCategoryOptions(flattenCategoryNodes(cats));
        setBrandOptions(brandsResult.rows.map((b) => ({ key: b.code, label: b.name })));
      })
      .catch((err) => {
        if (!cancelled) console.error("[EditAttributePage] load pickers failed", err);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, authToken]);

  // Load full attribute (advanced settings) when editing via API
  useEffect(() => {
    if (!tenantId || !attribute?.id || isAdd) return;
    let cancelled = false;
    setDetailLoading(true);
    fetchAttribute(tenantId, attribute.id, authToken)
      .then((detail) => {
        if (cancelled) return;
        setUseAsSearchFilter(Boolean(detail.useAsSearchFilter));
        setSelectedCategoryIds((detail.searchFilterCategoryIds ?? []).map(String));
        setSelectedBrandCodes(detail.searchFilterBrandCodes ?? []);
        if (detail.required !== undefined) setSearchFilterOnly(detail.required);
      })
      .catch((err) => {
        if (!cancelled) {
          console.error("[EditAttributePage] fetch attribute failed", err);
          setToast("Could not load attribute settings.");
        }
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, attribute?.id, authToken, isAdd]);

  // Load persisted values when editing via API
  useEffect(() => {
    if (!tenantId || !attribute?.id || isAdd) {
      setValues([]);
      return;
    }
    let cancelled = false;
    setValuesLoading(true);
    fetchAttributeValues(tenantId, attribute.id, authToken)
      .then((rows) => {
        if (!cancelled) {
          setValues(rows.map((r) => valueRowToAttrValue(r, apiTypeToValueType(attribute.type))));
        }
      })
      .catch((err) => {
        if (!cancelled) {
          console.error("[EditAttributePage] fetch values failed", err);
          setToast("Could not load attribute values.");
        }
      })
      .finally(() => {
        if (!cancelled) setValuesLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, attribute?.id, attribute?.type, authToken, isAdd]);

  // ── Auto-slug code from name ──────────────────────────────────────────────
  function handleNameBlur(values: Record<string, string>) {
    const n = primaryLocaleName(values);
    if (!code && n) {
      setCode(n.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, ""));
    }
  }

  async function exportValues() {
    const attrName = primaryLocaleName(nameLocales) || attribute?.name || "attribute";
    const attrCode = code || attribute?.code || "attribute";
    await exportSingleAttributeValuesXlsx(
      attrName,
      attrCode,
      values.map((v) => ({ name: v.name, value: v.value }))
    );
  }

  function validateAll(): { errors: FieldErrors; firstErrorTab: "general" | "advanced" | null } {
    const general: GeneralFieldErrors = {};
    const advanced: AdvancedFieldErrors = {};

    const labelName = primaryLocaleName(nameLocales);
    if (!labelName) general.name = "Name is required.";

    const normalizedCode = (code.trim() || (labelName ? slugify(labelName) : "")).trim().toLowerCase();
    if (!normalizedCode) general.code = "Code is required.";
    else if (normalizedCode.length > 100) general.code = "Code must be 100 characters or fewer.";
    else if (!CODE_RE.test(normalizedCode)) {
      general.code = "Use snake_case starting with a letter (e.g. color_primary).";
    }

    if (addingValue && (draftValueName.trim() || draftValue.trim())) {
      general.values = 'Finish adding the value or click "Cancel" before saving.';
    } else {
      for (const v of values) {
        const valueErr = validateValueEntry(v, valueType);
        if (valueErr) {
          general.values = `"${v.name || "Value"}": ${valueErr}`;
          break;
        }
      }
      if (!general.values) {
        const seenCodes = new Set<string>();
        for (const v of values) {
          const key = valueType === "Text" ? slugify(v.value) || slugify(v.name) : slugify(v.name);
          if (seenCodes.has(key)) {
            general.values = `Duplicate value code "${key}". Each value must be unique.`;
            break;
          }
          seenCodes.add(key);
        }
      }
    }

    if (useAsSearchFilter && selectedCategoryIds.length === 0 && selectedBrandCodes.length === 0) {
      advanced.searchFilter =
        "Select at least one category or brand when using this attribute as a search filter.";
    }

    const errors: FieldErrors = { general, advanced };
    const firstErrorTab = Object.keys(general).length > 0
      ? "general"
      : Object.keys(advanced).length > 0
        ? "advanced"
        : null;
    return { errors, firstErrorTab };
  }

  function clearGeneralError(key: keyof GeneralFieldErrors) {
    setFieldErrors((prev) => {
      if (!prev.general[key]) return prev;
      const nextGeneral = { ...prev.general };
      delete nextGeneral[key];
      return { ...prev, general: nextGeneral };
    });
  }

  function clearAdvancedError(key: keyof AdvancedFieldErrors) {
    setFieldErrors((prev) => {
      if (!prev.advanced[key]) return prev;
      const nextAdvanced = { ...prev.advanced };
      delete nextAdvanced[key];
      return { ...prev, advanced: nextAdvanced };
    });
  }

  async function persistPendingValues(attributeId: number) {
    if (!tenantId || values.length === 0) return;
    for (let i = 0; i < values.length; i++) {
      const v = values[i];
      if (isPersistedValueId(v.id)) continue;
      await createAttributeValue(
        tenantId,
        attributeId,
        attrValueToPayload(v, valueType, i),
        authToken
      );
    }
  }

  // ── Save ──────────────────────────────────────────────────────────────────
  async function handleSave() {
    setActionsOpen(false);

    const { errors, firstErrorTab } = validateAll();
    if (firstErrorTab) {
      setFieldErrors(errors);
      setTab(firstErrorTab);
      setToast("Please fix the highlighted fields before saving.");
      return;
    }
    setFieldErrors(EMPTY_FIELD_ERRORS);

    const labelName = primaryLocaleName(nameLocales);
    if (!labelName) {
      setFieldErrors({ general: { name: "Name is required." }, advanced: {} });
      setTab("general");
      setToast("Please fix the highlighted fields before saving.");
      return;
    }

    const normalizedCode = (code.trim() || slugify(labelName)).trim().toLowerCase();
    const apiType = valueType === "Color" ? "COLOR" : valueType === "Image" ? "IMAGE" : "TEXT";
    const payload = {
      name: labelName,
      code: normalizedCode,
      type: apiType as "TEXT" | "COLOR" | "IMAGE" | "SELECT" | "BOOLEAN",
      required: searchFilterOnly,
      sortOrder: 0,
      useAsSearchFilter,
      searchFilterCategoryIds: selectedCategoryIds.map((id) => Number(id)).filter((id) => id > 0),
      searchFilterBrandCodes: selectedBrandCodes,
    };

    if (tenantId) {
      try {
        if (isAdd) {
          const created = await createAttribute(tenantId, payload, authToken);
          if (created.id) await persistPendingValues(created.id);
        } else if (attribute?.id) {
          await updateAttribute(tenantId, attribute.id, payload, authToken);
        }
      } catch (err) {
        setToast(`Save failed: ${err instanceof Error ? err.message : "Unknown error"}`);
        return;
      }
    }

    setToast(isAdd ? `Attribute "${labelName}" created successfully.` : `Attribute "${labelName}" updated successfully.`);
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
    clearGeneralError("values");
  }

  async function saveValue() {
    if (!draftValueName.trim()) return;
    const draft: AttrValue = {
      id: editingValueId ?? `temp-${Date.now()}`,
      name: draftValueName.trim(),
      value: draftValue,
    };
    const valueErr = validateValueEntry(draft, valueType);
    if (valueErr) {
      setToast(valueErr);
      return;
    }
    clearGeneralError("values");
    const sortOrder = editingValueId
      ? values.findIndex((v) => v.id === editingValueId)
      : values.length;

    if (tenantId && attribute?.id && !isAdd) {
      setValueSaving(true);
      try {
        const payload = attrValueToPayload(draft, valueType, Math.max(0, sortOrder));
        if (editingValueId && isPersistedValueId(editingValueId)) {
          const updated = await updateAttributeValue(
            tenantId,
            attribute.id,
            Number(editingValueId),
            payload,
            authToken
          );
          setValues((prev) =>
            prev.map((v) =>
              v.id === editingValueId ? valueRowToAttrValue(updated, valueType) : v
            )
          );
        } else {
          const created = await createAttributeValue(tenantId, attribute.id, payload, authToken);
          setValues((prev) => [...prev, valueRowToAttrValue(created, valueType)]);
        }
        setAddingValue(false);
        setEditingValueId(null);
      } catch (err) {
        setToast(`Save value failed: ${err instanceof Error ? err.message : "Unknown error"}`);
      } finally {
        setValueSaving(false);
      }
      return;
    }

    if (editingValueId) {
      setValues((prev) =>
        prev.map((v) => (v.id === editingValueId ? { ...v, name: draft.name, value: draft.value } : v))
      );
    } else {
      setValues((prev) => [...prev, draft]);
    }
    setAddingValue(false);
    setEditingValueId(null);
  }

  async function deleteValue(id: string) {
    if (tenantId && attribute?.id && !isAdd && isPersistedValueId(id)) {
      setValueSaving(true);
      try {
        await deleteAttributeValue(tenantId, attribute.id, Number(id), authToken);
        setValues((prev) => prev.filter((v) => v.id !== id));
      } catch (err) {
        setToast(`Delete value failed: ${err instanceof Error ? err.message : "Unknown error"}`);
      } finally {
        setValueSaving(false);
      }
      return;
    }
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
              <div
                className="absolute right-0 top-full z-20 mt-1 w-44 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl"
                onMouseDown={(e) => e.stopPropagation()}
              >
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
        {(["general", "advanced"] as const).map((t) => {
          const hasErrors =
            t === "general"
              ? Object.keys(fieldErrors.general).length > 0
              : Object.keys(fieldErrors.advanced).length > 0;
          return (
          <button
            key={t}
            type="button"
            className={`${tabBase} ${tab === t ? tabActive : tabInactive} flex items-center gap-1.5`}
            onClick={() => setTab(t)}
          >
            {t === "general" ? "General" : "Advanced"}
            {hasErrors && (
              <span className="h-1.5 w-1.5 rounded-full bg-error" aria-label="Has validation errors" />
            )}
          </button>
          );
        })}
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
                  <MultiLangInput
                    label="Name"
                    requiredMark
                    inputId="attribute-name"
                    defaultValues={nameLocales}
                    onValuesChange={(next) => {
                      setNameLocales(next);
                      clearGeneralError("name");
                    }}
                    onBlur={handleNameBlur}
                    placeholders={{
                      en: "e.g. Primary Color",
                      vn: "vd. Màu chính",
                      zh: "例如：主色",
                      ja: "例：プライマリカラー",
                    }}
                    hint="Displayed in the eStore."
                    inputClassName={inputErrorClass(!!fieldErrors.general.name)}
                  />
                  {fieldErrors.general.name && (
                    <p className="mt-1 text-[10px] text-error">{fieldErrors.general.name}</p>
                  )}
                </div>

                {/* Code */}
                <div>
                  <label className={labelBase} htmlFor="attribute-code">Code <span className="text-error">*</span></label>
                  <input
                    id="attribute-code"
                    name="attribute-code"
                    type="text"
                    className={`${inputBase} ${inputErrorClass(!!fieldErrors.general.code)}`}
                    placeholder="e.g. color_primary"
                    value={code}
                    onChange={(e) => {
                      setCode(e.target.value);
                      clearGeneralError("code");
                    }}
                  />
                  {fieldErrors.general.code ? (
                    <p className="mt-1 text-[10px] text-error">{fieldErrors.general.code}</p>
                  ) : (
                    <p className="mt-1 text-[10px] text-on-surface-variant">
                      Auto-generated from Name; must be unique.
                    </p>
                  )}
                </div>

                {/* Search Filter Only */}
                <div className="md:col-span-2">
                  <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none">
                    <input
                      id="attribute-search-filter-only"
                      name="attribute-search-filter-only"
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
            <section className={`relative rounded-xl border bg-surface-container-lowest shadow-sm ${fieldErrors.general.values ? "border-error/40" : "border-outline-variant/10"}`}>
              {valuesLoading && (
                <div className="absolute inset-0 z-10 flex items-center justify-center rounded-xl bg-surface/60 backdrop-blur-sm">
                  <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
                </div>
              )}
              {fieldErrors.general.values && (
                <div className="border-b border-error/20 bg-error-container/30 px-6 py-2.5 text-xs text-error">
                  {fieldErrors.general.values}
                </div>
              )}
              <div className="flex flex-wrap items-center gap-4 border-b border-outline-variant/10 px-6 py-4">
                <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Attribute Values</h3>

                {/* Value Type selector */}
                <div className="ml-auto flex items-center gap-2">
                  <label className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Value Type:</label>
                  <select
                    id="attribute-value-type"
                    name="attribute-value-type"
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
                              className="rounded p-1.5 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors disabled:opacity-40"
                              disabled={valueSaving}
                              onClick={() => openEditValue(v)}
                            >
                              <IconEdit className="h-3.5 w-3.5" />
                            </button>
                            <button
                              type="button"
                              title="Delete"
                              className="rounded p-1.5 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors disabled:opacity-40"
                              disabled={valueSaving}
                              onClick={() => void deleteValue(v.id)}
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
                              disabled={!draftValueName.trim() || valueSaving}
                              onClick={() => void saveValue()}
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
                    className="flex items-center gap-2 rounded-md border border-primary/30 px-4 py-2 text-xs font-bold text-primary transition-colors hover:bg-primary/5 disabled:opacity-40"
                    disabled={valuesLoading || valueSaving}
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
          <section className={`relative rounded-xl border bg-surface-container-lowest p-6 shadow-sm ${fieldErrors.advanced.searchFilter ? "border-error/40" : "border-outline-variant/10"}`}>
            {detailLoading && (
              <div className="absolute inset-0 z-10 flex items-center justify-center rounded-xl bg-surface/60 backdrop-blur-sm">
                <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
              </div>
            )}
            <h3 className="mb-5 text-sm font-bold uppercase tracking-widest text-on-surface">Product Listing Search</h3>

            <div className="space-y-6">
              <label className={`flex cursor-pointer items-start gap-3 rounded-lg border bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none ${fieldErrors.advanced.searchFilter ? "border-error/40" : "border-outline-variant/20"}`}>
                <input
                  id="attribute-use-as-search-filter"
                  name="attribute-use-as-search-filter"
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                  checked={useAsSearchFilter}
                  onChange={(e) => {
                    setUseAsSearchFilter(e.target.checked);
                    clearAdvancedError("searchFilter");
                  }}
                />
                <div>
                  <p className="text-xs font-bold text-on-surface">Use as Search Filter</p>
                  <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                    Use this attribute as a search filter for the selected categories and brands.
                  </p>
                  {fieldErrors.advanced.searchFilter && (
                    <p className="mt-2 text-[10px] text-error">{fieldErrors.advanced.searchFilter}</p>
                  )}
                </div>
              </label>

              <SearchablePicker
                label="Selected Categories"
                inputId="attribute-categories"
                options={categoryOptions}
                selected={selectedCategoryIds}
                onChange={(next) => {
                  setSelectedCategoryIds(next);
                  clearAdvancedError("searchFilter");
                }}
              />

              <SearchablePicker
                label="Selected Brands"
                inputId="attribute-brands"
                options={brandOptions}
                selected={selectedBrandCodes}
                onChange={(next) => {
                  setSelectedBrandCodes(next);
                  clearAdvancedError("searchFilter");
                }}
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
