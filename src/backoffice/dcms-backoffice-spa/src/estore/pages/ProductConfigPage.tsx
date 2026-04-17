import { useEffect, useRef, useState } from "react";
import {
  IconAddCircle,
  IconArrowBack,
  IconCheckCircle,
  IconDelete,
  IconEdit,
  IconInfo,
  IconTune,
} from "../../orders/icons";

// ── Style tokens ─────────────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const selectBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95 disabled:pointer-events-none disabled:opacity-40";
const btnGhost =
  "text-xs font-bold uppercase tracking-widest text-on-surface-variant px-4 py-2.5 hover:bg-surface-container-high rounded-md transition-colors";

// ── Types ─────────────────────────────────────────────────────────────────────
type ControlType =
  | "Text Box"
  | "WYSIWYG (Text Area)"
  | "Dropdown List"
  | "Checkbox"
  | "Date Picker"
  | "Multiple Select";

type TargetPage = "General" | "Product Page" | "Recommendations";

type FieldRecord = {
  id: string;
  enabled: boolean;
  required: boolean;
  property: string;
  columnLabel: string;
  fieldName: string;
  controlType: ControlType;
  targetPage: TargetPage;
};

// ── Mock data ─────────────────────────────────────────────────────────────────
const INITIAL_FIELDS: FieldRecord[] = [
  {
    id: "f1",
    enabled: true,
    required: true,
    property: "warranty_period",
    columnLabel: "Warranty Period",
    fieldName: "Warranty Period",
    controlType: "Text Box",
    targetPage: "General",
  },
  {
    id: "f2",
    enabled: true,
    required: false,
    property: "product_origin",
    columnLabel: "Country of Origin",
    fieldName: "Country of Origin",
    controlType: "Dropdown List",
    targetPage: "General",
  },
  {
    id: "f3",
    enabled: true,
    required: false,
    property: "available_from",
    columnLabel: "Available From",
    fieldName: "Available From Date",
    controlType: "Date Picker",
    targetPage: "Product Page",
  },
  {
    id: "f4",
    enabled: false,
    required: false,
    property: "is_featured",
    columnLabel: "Featured",
    fieldName: "Mark as Featured",
    controlType: "Checkbox",
    targetPage: "General",
  },
];

const CONTROL_TYPES: ControlType[] = [
  "Text Box",
  "WYSIWYG (Text Area)",
  "Dropdown List",
  "Checkbox",
  "Date Picker",
  "Multiple Select",
];

const TARGET_PAGES: TargetPage[] = ["General", "Product Page", "Recommendations"];

// ── Helpers ───────────────────────────────────────────────────────────────────
function controlTypeChipClass(ct: ControlType) {
  const map: Record<ControlType, string> = {
    "Text Box": "bg-blue-50 text-blue-700",
    "WYSIWYG (Text Area)": "bg-purple-50 text-purple-700",
    "Dropdown List": "bg-amber-50 text-amber-700",
    "Checkbox": "bg-green-50 text-green-700",
    "Date Picker": "bg-cyan-50 text-cyan-700",
    "Multiple Select": "bg-orange-50 text-orange-700",
  };
  return map[ct] ?? "bg-surface-container-high text-on-surface";
}

// ── Empty form state ──────────────────────────────────────────────────────────
function emptyForm(): Omit<FieldRecord, "id"> {
  return {
    enabled: true,
    required: false,
    property: "",
    columnLabel: "",
    fieldName: "",
    controlType: "Text Box",
    targetPage: "General",
  };
}

// ── Component ─────────────────────────────────────────────────────────────────
type Props = { onBack: () => void };

export function ProductConfigPage({ onBack }: Props) {
  const [fields, setFields] = useState<FieldRecord[]>(INITIAL_FIELDS);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [form, setForm] = useState<Omit<FieldRecord, "id">>(emptyForm());
  const [errors, setErrors] = useState<Partial<Record<keyof FieldRecord, string>>>({});
  const [toast, setToast] = useState<string | null>(null);
  const formRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Form open / edit ────────────────────────────────────────────────────
  function openAdd() {
    setEditId(null);
    setForm(emptyForm());
    setErrors({});
    setShowForm(true);
    setTimeout(() => formRef.current?.scrollIntoView({ behavior: "smooth", block: "start" }), 80);
  }

  function openEdit(f: FieldRecord) {
    setEditId(f.id);
    setForm({
      enabled: f.enabled,
      required: f.required,
      property: f.property,
      columnLabel: f.columnLabel,
      fieldName: f.fieldName,
      controlType: f.controlType,
      targetPage: f.targetPage,
    });
    setErrors({});
    setShowForm(true);
    setTimeout(() => formRef.current?.scrollIntoView({ behavior: "smooth", block: "start" }), 80);
  }

  function cancelForm() {
    setShowForm(false);
    setEditId(null);
    setErrors({});
  }

  // ── Validation & Save ───────────────────────────────────────────────────
  function validate() {
    const e: Partial<Record<keyof FieldRecord, string>> = {};
    if (!form.property.trim()) e.property = "Property is required";
    if (!form.columnLabel.trim()) e.columnLabel = "Column Label is required";
    if (!form.fieldName.trim()) e.fieldName = "Field Name is required";
    return e;
  }

  function handleSave() {
    const e = validate();
    if (Object.keys(e).length > 0) { setErrors(e); return; }

    if (editId) {
      setFields((prev) => prev.map((f) => (f.id === editId ? { ...f, ...form } : f)));
      setToast(`Field "${form.fieldName}" updated successfully.`);
    } else {
      const newField: FieldRecord = { ...form, id: `f${Date.now()}` };
      setFields((prev) => [...prev, newField]);
      setToast(`Field "${form.fieldName}" added successfully.`);
    }

    setShowForm(false);
    setEditId(null);
  }

  // ── Delete ──────────────────────────────────────────────────────────────
  function handleDelete(id: string) {
    const f = fields.find((r) => r.id === id);
    setFields((prev) => prev.filter((r) => r.id !== id));
    if (editId === id) { setShowForm(false); setEditId(null); }
    setToast(`Field "${f?.fieldName}" removed.`);
  }

  // ── Field helpers ───────────────────────────────────────────────────────
  function set<K extends keyof typeof form>(key: K, value: (typeof form)[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
    setErrors((prev) => ({ ...prev, [key]: undefined }));
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 items-center gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Products
          </button>
          <div className="flex items-center gap-3">
            <IconTune className="h-6 w-6 shrink-0 text-primary" />
            <div>
              <h2 className="text-2xl font-bold tracking-tight text-on-surface">Product Configuration</h2>
              <p className="mt-0.5 text-sm text-on-surface-variant">
                Manage additional fields displayed on the Add / Edit Product page.
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* ── Body ────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* ── Info banner ─────────────────────────────────────────────── */}
        <div className="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
          <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Only <strong>enabled</strong> fields are displayed on the Add Products page. Scroll to the bottom and click{" "}
            <strong>Add Field Option</strong> to add a new custom field.
          </p>
        </div>

        {/* ── Fields table ────────────────────────────────────────────── */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
          <div className="border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Custom Fields</h3>
            <p className="mt-0.5 text-[11px] text-on-surface-variant">{fields.length} field{fields.length !== 1 ? "s" : ""} configured</p>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">Enabled</th>
                  <th className="px-4 py-3">Required</th>
                  <th className="px-4 py-3">Property</th>
                  <th className="px-4 py-3">Column Label</th>
                  <th className="px-4 py-3">Field Name</th>
                  <th className="px-4 py-3">Control Type</th>
                  <th className="px-4 py-3">Page</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/10">
                {fields.map((f) => (
                  <tr key={f.id} className="text-xs hover:bg-surface-container-low transition-colors">
                    <td className="px-5 py-3.5">
                      {f.enabled ? (
                        <span className="rounded-full bg-secondary-container/20 px-2 py-0.5 text-[9px] font-bold uppercase text-on-secondary-container">Yes</span>
                      ) : (
                        <span className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant/60">No</span>
                      )}
                    </td>
                    <td className="px-4 py-3.5">
                      {f.required ? (
                        <span className="rounded-full bg-error-container px-2 py-0.5 text-[9px] font-bold uppercase text-on-error-container">Yes</span>
                      ) : (
                        <span className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant/60">No</span>
                      )}
                    </td>
                    <td className="px-4 py-3.5 font-mono text-[10px] text-on-surface-variant">{f.property}</td>
                    <td className="px-4 py-3.5 text-on-surface-variant">{f.columnLabel}</td>
                    <td className="px-4 py-3.5 font-semibold text-on-surface">{f.fieldName}</td>
                    <td className="px-4 py-3.5">
                      <span className={`rounded-full px-2 py-0.5 text-[9px] font-bold ${controlTypeChipClass(f.controlType)}`}>
                        {f.controlType}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-on-surface-variant">{f.targetPage}</td>
                    <td className="px-4 py-3.5 text-right">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          title="Edit"
                          className="rounded p-1.5 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                          onClick={() => openEdit(f)}
                        >
                          <IconEdit className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          title="Delete"
                          className="rounded p-1.5 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                          onClick={() => handleDelete(f.id)}
                        >
                          <IconDelete className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {fields.length === 0 && (
                  <tr>
                    <td colSpan={8} className="px-6 py-12 text-center text-xs text-on-surface-variant italic">
                      No custom fields configured. Click "Add Field Option" below to add one.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Add Field Option trigger */}
          {!showForm && (
            <div className="border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="flex items-center gap-2 rounded-md border border-primary/30 px-4 py-2 text-xs font-bold text-primary transition-colors hover:bg-primary/5"
                onClick={openAdd}
              >
                <IconAddCircle className="h-4 w-4 shrink-0" />
                Add Field Option
              </button>
            </div>
          )}
        </section>

        {/* ── Add / Edit Field Form ────────────────────────────────────── */}
        {showForm && (
          <section ref={formRef} className="rounded-xl border border-primary/20 bg-surface-container-lowest shadow-sm">
            {/* Form header */}
            <div className="border-b border-outline-variant/10 bg-primary/5 px-6 py-4">
              <h3 className="text-sm font-bold uppercase tracking-widest text-primary">
                {editId ? "Edit Field Option" : "Add Field Option"}
              </h3>
            </div>

            <div className="space-y-6 px-6 py-6">
              {/* Enabled / Required row */}
              <div className="flex gap-8">
                <label className="flex cursor-pointer items-center gap-2.5 select-none">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-primary"
                    checked={form.enabled}
                    onChange={(e) => set("enabled", e.target.checked)}
                  />
                  <span className="text-xs font-bold text-on-surface">Enabled</span>
                  <span className="text-[10px] text-on-surface-variant">Display on Add Products page</span>
                </label>
                <label className="flex cursor-pointer items-center gap-2.5 select-none">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-primary"
                    checked={form.required}
                    onChange={(e) => set("required", e.target.checked)}
                  />
                  <span className="text-xs font-bold text-on-surface">Required</span>
                  <span className="text-[10px] text-on-surface-variant">Field must be filled</span>
                </label>
              </div>

              {/* Property / Column Label / Field Name */}
              <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
                <div>
                  <label className={labelBase}>
                    Property <span className="text-error">*</span>
                  </label>
                  <input
                    type="text"
                    className={`${inputBase} ${errors.property ? "ring-1 ring-error border-error" : ""}`}
                    placeholder="e.g. warranty_period"
                    value={form.property}
                    onChange={(e) => set("property", e.target.value)}
                  />
                  {errors.property && (
                    <p className="mt-1 text-[10px] text-error">{errors.property}</p>
                  )}
                  <p className="mt-1 text-[10px] text-on-surface-variant">Unique identifier; used in search.</p>
                </div>
                <div>
                  <label className={labelBase}>
                    Column Label <span className="text-error">*</span>
                  </label>
                  <input
                    type="text"
                    className={`${inputBase} ${errors.columnLabel ? "ring-1 ring-error border-error" : ""}`}
                    placeholder="e.g. Warranty Period"
                    value={form.columnLabel}
                    onChange={(e) => set("columnLabel", e.target.value)}
                  />
                  {errors.columnLabel && (
                    <p className="mt-1 text-[10px] text-error">{errors.columnLabel}</p>
                  )}
                  <p className="mt-1 text-[10px] text-on-surface-variant">Column name in fields report.</p>
                </div>
                <div>
                  <label className={labelBase}>
                    Field Name <span className="text-error">*</span>
                  </label>
                  <input
                    type="text"
                    className={`${inputBase} ${errors.fieldName ? "ring-1 ring-error border-error" : ""}`}
                    placeholder="e.g. Warranty Period"
                    value={form.fieldName}
                    onChange={(e) => set("fieldName", e.target.value)}
                  />
                  {errors.fieldName && (
                    <p className="mt-1 text-[10px] text-error">{errors.fieldName}</p>
                  )}
                  <p className="mt-1 text-[10px] text-on-surface-variant">Label displayed on Add Products page.</p>
                </div>
              </div>

              {/* Control ID (two dropdowns) */}
              <div className="space-y-2">
                <label className={labelBase}>Control ID</label>
                <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                  <div>
                    <p className="mb-1 text-[10px] text-on-surface-variant">Field Control Type</p>
                    <select
                      className={selectBase}
                      value={form.controlType}
                      onChange={(e) => set("controlType", e.target.value as ControlType)}
                    >
                      {CONTROL_TYPES.map((ct) => (
                        <option key={ct} value={ct}>{ct}</option>
                      ))}
                    </select>
                    {form.controlType === "Dropdown List" && (
                      <p className="mt-1.5 flex items-center gap-1 text-[10px] text-amber-700">
                        <span className="font-bold">Note:</span> Name and Value must not be empty.
                      </p>
                    )}
                  </div>
                  <div>
                    <p className="mb-1 text-[10px] text-on-surface-variant">Target Page</p>
                    <select
                      className={selectBase}
                      value={form.targetPage}
                      onChange={(e) => set("targetPage", e.target.value as TargetPage)}
                    >
                      {TARGET_PAGES.map((tp) => (
                        <option key={tp} value={tp}>{tp}</option>
                      ))}
                    </select>
                    <p className="mt-1 text-[10px] text-on-surface-variant">Page where this field will appear.</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Form footer */}
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button type="button" className={btnGhost} onClick={cancelForm}>
                Cancel
              </button>
              <button type="button" className={btnPrimary} onClick={handleSave}>
                <IconCheckCircle className="h-4 w-4 shrink-0" />
                Save
              </button>
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
