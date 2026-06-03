import { IconCalendarToday, IconChevronDown, IconTune } from "../../orders/icons";
import type {
  ProductFieldControlType,
  ProductFieldRecord,
  ProductFieldTargetPage,
} from "../api/productFieldConfigApi";
import type { ProductCustomFieldsMap } from "../utils/productCustomFieldUtils";

const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";

function ProductCustomFieldInput({
  field,
  value,
  onChange,
}: {
  field: ProductFieldRecord;
  value: string | string[];
  onChange: (val: string | string[]) => void;
}) {
  const strVal = typeof value === "string" ? value : "";
  const arrVal = Array.isArray(value) ? value : [];
  const requiredMark = field.required ? <span className="text-error ml-0.5">*</span> : null;

  function renderControl() {
    switch (field.controlType as ProductFieldControlType) {
      case "Text Box":
        return (
          <input
            className={inputBase}
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
            className={`${inputBase} resize-y min-h-[96px]`}
            value={strVal}
            onChange={(e) => onChange(e.target.value)}
            placeholder={field.columnLabel}
            required={field.required}
          />
        );
      case "Checkbox":
        return (
          <label className="flex cursor-pointer select-none items-center gap-2">
            <input
              type="checkbox"
              className="h-4 w-4 accent-primary"
              checked={strVal === "true"}
              onChange={(e) => onChange(e.target.checked ? "true" : "")}
            />
            <span className="text-xs text-on-surface">{field.fieldName}</span>
          </label>
        );
      case "Date Picker":
        return (
          <div className="relative">
            <input
              className={`${inputBase} pr-9`}
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
              className={`${inputBase} appearance-none pr-8`}
              value={strVal}
              onChange={(e) => onChange(e.target.value)}
              required={field.required}
            >
              <option value="">— Select —</option>
              {field.options.map((opt) => (
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
            {field.options.map((opt) => (
              <label key={opt.value} className="flex cursor-pointer select-none items-center gap-2">
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
            {field.options.length === 0 ? (
              <p className="text-xs italic text-on-surface-variant">No options defined.</p>
            ) : null}
          </div>
        );
      default:
        return null;
    }
  }

  return (
    <div className="space-y-1">
      {field.controlType !== "Checkbox" ? (
        <label className={labelBase}>
          {field.fieldName}
          {requiredMark}
        </label>
      ) : null}
      {renderControl()}
    </div>
  );
}

type Props = {
  fields: ProductFieldRecord[];
  targetPage: ProductFieldTargetPage;
  values: ProductCustomFieldsMap;
  onChange: (id: string, val: string | string[]) => void;
  sectionTitleClass?: string;
};

export function ProductCustomFieldsSection({
  fields,
  targetPage,
  values,
  onChange,
  sectionTitleClass = "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2 mb-4",
}: Props) {
  const rows = fields.filter((f) => f.enabled && f.targetPage === targetPage);
  if (rows.length === 0) return null;

  return (
    <div>
      <h3 className={sectionTitleClass}>Additional Fields</h3>
      <p className="mb-4 text-[11px] text-on-surface-variant">
        Custom fields from{" "}
        <span className="font-semibold text-on-surface">Product Configuration</span> for this tab.
      </p>
      <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
        {rows.map((field) => (
          <ProductCustomFieldInput
            key={field.id}
            field={field}
            value={values[field.id] ?? (field.controlType === "Multiple Select" ? [] : "")}
            onChange={(val) => onChange(field.id, val)}
          />
        ))}
      </div>
    </div>
  );
}

export function ProductCustomFieldsEmptyHint() {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-outline-variant/30 bg-surface-container-low/30 py-10 text-center">
      <IconTune className="h-8 w-8 text-outline-variant" />
      <p className="text-xs text-on-surface-variant max-w-sm">
        No additional fields configured for products. Define them in{" "}
        <span className="font-semibold text-primary">Product Configuration</span>.
      </p>
    </div>
  );
}
