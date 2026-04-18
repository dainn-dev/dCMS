import { Fragment, useEffect, useMemo, useState } from "react";
import {
  IconAddCircle,
  IconCheckCircle,
  IconDelete,
  IconSave,
  IconWarning,
} from "../../orders/icons";

const inputBase =
  "w-full min-w-0 bg-surface-container-lowest border border-outline-variant/20 rounded-md py-1.5 px-2 text-xs focus:ring-1 focus:ring-primary outline-none";

export type BrandFieldControlType =
  | "Text Box"
  | "WYSIWYG (Text Area)"
  | "Dropdown List"
  | "Checkbox"
  | "Date Picker"
  | "Multiple Select";

export type BrandFieldSection =
  | "General Information"
  | "Contacts"
  | "Product Recommendations"
  | "SEO Configuration";

export type BrandAdditionalFieldOption = { name: string; value: string };

export type BrandAdditionalField = {
  id: string;
  enabled: boolean;
  required: boolean;
  property: string;
  columnLabel: string;
  fieldName: string;
  controlType: BrandFieldControlType;
  section: BrandFieldSection;
  options: BrandAdditionalFieldOption[];
};

const CONTROL_TYPES: BrandFieldControlType[] = [
  "Text Box",
  "WYSIWYG (Text Area)",
  "Dropdown List",
  "Checkbox",
  "Date Picker",
  "Multiple Select",
];

const SECTIONS: BrandFieldSection[] = [
  "General Information",
  "Contacts",
  "Product Recommendations",
  "SEO Configuration",
];

export const DEFAULT_BRAND_ADDITIONAL_FIELDS: BrandAdditionalField[] = [
  {
    id: "baf-seed-1",
    enabled: true,
    required: false,
    property: "brand.loyaltyTier",
    columnLabel: "Loyalty tier",
    fieldName: "loyaltyTier",
    controlType: "Dropdown List",
    section: "General Information",
    options: [
      { name: "Gold", value: "gold" },
      { name: "Silver", value: "silver" },
      { name: "Bronze", value: "bronze" },
    ],
  },
  {
    id: "baf-seed-2",
    enabled: true,
    required: true,
    property: "brand.complianceAck",
    columnLabel: "Compliance acknowledgement",
    fieldName: "complianceAck",
    controlType: "Checkbox",
    section: "General Information",
    options: [],
  },
  {
    id: "baf-seed-3",
    enabled: false,
    required: false,
    property: "brand.seoNotes",
    columnLabel: "Internal SEO notes",
    fieldName: "seoNotes",
    controlType: "WYSIWYG (Text Area)",
    section: "SEO Configuration",
    options: [],
  },
];

function newId() {
  return `baf-${Math.random().toString(36).slice(2, 10)}`;
}

function cloneRows(rows: BrandAdditionalField[]): BrandAdditionalField[] {
  return rows.map((r) => ({
    ...r,
    options: r.options.map((o) => ({ ...o })),
  }));
}

type Props = {
  savedFields: BrandAdditionalField[];
  onSave: (next: BrandAdditionalField[]) => void;
  onNavigateToBrands: () => void;
};

export function BrandConfigPage({ savedFields, onSave, onNavigateToBrands }: Props) {
  const [draft, setDraft] = useState<BrandAdditionalField[]>(() => cloneRows(savedFields));
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; visible: boolean }>({
    message: "",
    visible: false,
  });

  useEffect(() => {
    setDraft(cloneRows(savedFields));
  }, [savedFields]);

  const dirty = useMemo(
    () => JSON.stringify(draft) !== JSON.stringify(savedFields),
    [draft, savedFields]
  );

  useEffect(() => {
    if (!toast.visible) return;
    const t = setTimeout(() => setToast((p) => ({ ...p, visible: false })), 3000);
    return () => clearTimeout(t);
  }, [toast.visible]);

  function showToast(message: string) {
    setToast({ message, visible: true });
  }

  function handleSave() {
    onSave(cloneRows(draft));
    showToast("Brand field configuration saved.");
  }

  function addBlankRow() {
    setDraft((prev) => [
      ...prev,
      {
        id: newId(),
        enabled: true,
        required: false,
        property: "",
        columnLabel: "",
        fieldName: "",
        controlType: "Text Box",
        section: "General Information",
        options: [],
      },
    ]);
  }

  function confirmDelete() {
    if (!deleteTargetId) return;
    setDraft((prev) => prev.filter((r) => r.id !== deleteTargetId));
    setDeleteTargetId(null);
    showToast("Field removed from draft. Save to persist.");
  }

  function needsOptions(t: BrandFieldControlType) {
    return t === "Dropdown List" || t === "Multiple Select";
  }

  function updateRow(id: string, patch: Partial<BrandAdditionalField>) {
    setDraft((prev) =>
      prev.map((r) => {
        if (r.id !== id) return r;
        const next = { ...r, ...patch };
        if (patch.controlType && !needsOptions(patch.controlType)) {
          next.options = [];
        }
        if (patch.controlType && needsOptions(patch.controlType) && next.options.length === 0) {
          next.options = [{ name: "", value: "" }];
        }
        return next;
      })
    );
  }

  function updateOption(fieldId: string, index: number, patch: Partial<BrandAdditionalFieldOption>) {
    setDraft((prev) =>
      prev.map((r) => {
        if (r.id !== fieldId) return r;
        const options = r.options.map((o, i) => (i === index ? { ...o, ...patch } : o));
        return { ...r, options };
      })
    );
  }

  function addOption(fieldId: string) {
    setDraft((prev) =>
      prev.map((r) => (r.id === fieldId ? { ...r, options: [...r.options, { name: "", value: "" }] } : r))
    );
  }

  function removeOption(fieldId: string, index: number) {
    setDraft((prev) =>
      prev.map((r) => (r.id === fieldId ? { ...r, options: r.options.filter((_, i) => i !== index) } : r))
    );
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Brand configuration">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-start md:justify-between">
        <div className="space-y-2 min-w-0">
          <nav className="mb-1 flex flex-wrap gap-x-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="text-on-surface-variant/50">/</span>
            <button
              type="button"
              className="text-primary hover:underline"
              onClick={onNavigateToBrands}
            >
              Brands
            </button>
            <span className="text-on-surface-variant/50">/</span>
            <span className="text-primary">Brand Configuration</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Brand Configuration</h1>
          <p className="text-sm text-on-surface-variant max-w-2xl">
            Define additional fields shown on the Add / Edit Brand form. Changes apply after Save (demo: stored in
            browser only).
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-3">
          <button
            type="button"
            disabled={!dirty}
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-xs font-semibold text-on-primary shadow-sm transition-all hover:bg-primary-container disabled:opacity-40 disabled:pointer-events-none"
            onClick={handleSave}
          >
            <IconSave className="h-4 w-4 shrink-0" />
            Save
          </button>
        </div>
      </header>

      <div className="flex-1 p-6 space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-sm font-bold uppercase tracking-widest text-on-surface-variant">Additional fields</h2>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg border border-outline-variant/40 px-3 py-2 text-xs font-medium text-on-surface transition-colors hover:bg-surface-variant"
            onClick={addBlankRow}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Add Field Option
          </button>
        </div>

        {draft.length === 0 ? (
          <div className="rounded-xl border border-outline-variant/15 bg-surface-container-lowest px-6 py-12 text-center text-sm text-on-surface-variant italic shadow-sm">
            No additional fields yet. Use &ldquo;Add Field Option&rdquo; to create one.
          </div>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-outline-variant/15 bg-surface-container-lowest shadow-sm">
            <table className="w-full min-w-[960px] border-collapse text-left text-xs">
              <thead>
                <tr className="border-b border-outline-variant/20 bg-surface-container-low text-on-surface-variant uppercase tracking-wider">
                  <th className="px-3 py-2.5 font-bold w-10 text-center">On</th>
                  <th className="px-3 py-2.5 font-bold w-10 text-center">Req</th>
                  <th className="px-3 py-2.5 font-bold min-w-[120px]">Property</th>
                  <th className="px-3 py-2.5 font-bold min-w-[120px]">Column label</th>
                  <th className="px-3 py-2.5 font-bold min-w-[120px]">Field name</th>
                  <th className="px-3 py-2.5 font-bold min-w-[140px]">Control type</th>
                  <th className="px-3 py-2.5 font-bold min-w-[160px]">Page / section</th>
                  <th className="px-3 py-2.5 font-bold w-12 text-center" aria-label="Delete" />
                </tr>
              </thead>
              <tbody>
                {draft.map((row) => (
                  <Fragment key={row.id}>
                    <tr className="border-b border-outline-variant/10 align-middle hover:bg-surface-container-low/50">
                      <td className="px-3 py-2 text-center">
                        <input
                          type="checkbox"
                          className="h-3.5 w-3.5 accent-primary cursor-pointer"
                          checked={row.enabled}
                          onChange={(e) => updateRow(row.id, { enabled: e.target.checked })}
                          aria-label="Enabled"
                        />
                      </td>
                      <td className="px-3 py-2 text-center">
                        <input
                          type="checkbox"
                          className="h-3.5 w-3.5 accent-primary cursor-pointer"
                          checked={row.required}
                          onChange={(e) => updateRow(row.id, { required: e.target.checked })}
                          aria-label="Required"
                        />
                      </td>
                      <td className="px-3 py-2">
                        <input
                          className={inputBase}
                          value={row.property}
                          onChange={(e) => updateRow(row.id, { property: e.target.value })}
                          placeholder="tenant.namespace.key"
                        />
                      </td>
                      <td className="px-3 py-2">
                        <input
                          className={inputBase}
                          value={row.columnLabel}
                          onChange={(e) => updateRow(row.id, { columnLabel: e.target.value })}
                          placeholder="Label shown in UI"
                        />
                      </td>
                      <td className="px-3 py-2">
                        <input
                          className={inputBase}
                          value={row.fieldName}
                          onChange={(e) => updateRow(row.id, { fieldName: e.target.value })}
                          placeholder="camelCase"
                        />
                      </td>
                      <td className="px-3 py-2">
                        <select
                          className={`${inputBase} appearance-none pr-8`}
                          value={row.controlType}
                          onChange={(e) =>
                            updateRow(row.id, { controlType: e.target.value as BrandFieldControlType })
                          }
                        >
                          {CONTROL_TYPES.map((t) => (
                            <option key={t} value={t}>
                              {t}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td className="px-3 py-2">
                        <select
                          className={`${inputBase} appearance-none pr-8`}
                          value={row.section}
                          onChange={(e) => updateRow(row.id, { section: e.target.value as BrandFieldSection })}
                        >
                          {SECTIONS.map((s) => (
                            <option key={s} value={s}>
                              {s}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td className="px-3 py-2 text-center">
                        <button
                          type="button"
                          className="inline-flex rounded-md p-1.5 text-error hover:bg-error-container/30 transition-colors"
                          aria-label="Delete field"
                          onClick={() => setDeleteTargetId(row.id)}
                        >
                          <IconDelete className="h-4 w-4" />
                        </button>
                      </td>
                    </tr>
                    {needsOptions(row.controlType) && (
                      <tr className="border-b border-outline-variant/10 bg-surface-container-low/40">
                        <td colSpan={8} className="px-6 py-3">
                          <p className="text-[10px] font-bold uppercase tracking-widest text-on-surface-variant mb-2">
                            Dropdown / multi-select options (name / value)
                          </p>
                          <div className="space-y-2">
                            {row.options.map((opt, i) => (
                              <div key={i} className="flex flex-wrap items-center gap-2">
                                <input
                                  className={`${inputBase} flex-1 min-w-[140px]`}
                                  placeholder="Display name"
                                  value={opt.name}
                                  onChange={(e) => updateOption(row.id, i, { name: e.target.value })}
                                />
                                <input
                                  className={`${inputBase} flex-1 min-w-[140px]`}
                                  placeholder="Value"
                                  value={opt.value}
                                  onChange={(e) => updateOption(row.id, i, { value: e.target.value })}
                                />
                                <button
                                  type="button"
                                  className="rounded-md border border-outline-variant/30 px-2 py-1 text-[10px] font-bold uppercase text-on-surface-variant hover:bg-surface-container-high"
                                  onClick={() => removeOption(row.id, i)}
                                >
                                  Remove
                                </button>
                              </div>
                            ))}
                            <button
                              type="button"
                              className="text-[10px] font-bold uppercase tracking-widest text-primary hover:underline"
                              onClick={() => addOption(row.id)}
                            >
                              + Add name/value pair
                            </button>
                          </div>
                        </td>
                      </tr>
                    )}
                  </Fragment>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {deleteTargetId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete field</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Delete this field? This action cannot be undone.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteTargetId(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={confirmDelete}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      <div
        aria-live="polite"
        className={`fixed bottom-6 left-1/2 z-50 -translate-x-1/2 transition-all duration-300 ${
          toast.visible ? "translate-y-0 opacity-100" : "translate-y-4 opacity-0 pointer-events-none"
        }`}
      >
        <div className="flex items-center gap-3 rounded-xl bg-on-surface px-5 py-3 shadow-2xl">
          <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
          <span className="text-sm font-medium text-surface">{toast.message}</span>
        </div>
      </div>
    </div>
  );
}
