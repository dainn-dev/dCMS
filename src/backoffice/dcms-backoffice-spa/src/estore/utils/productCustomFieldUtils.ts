import type { ProductFieldRecord } from "../api/productFieldConfigApi";

export type ProductCustomFieldsMap = Record<string, string | string[]>;

/** Parse persisted customFieldsJson from the Catalog API. */
export function parseProductCustomFields(json: string | null | undefined): ProductCustomFieldsMap {
  if (!json || !json.trim()) return {};
  try {
    const parsed = JSON.parse(json) as unknown;
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return {};
    const out: ProductCustomFieldsMap = {};
    for (const [key, val] of Object.entries(parsed as Record<string, unknown>)) {
      if (typeof val === "string") out[key] = val;
      else if (Array.isArray(val) && val.every((x) => typeof x === "string")) out[key] = val as string[];
    }
    return out;
  } catch {
    return {};
  }
}

/** Seed form state from field config + stored values. */
export function initCustomFieldValues(
  fields: ProductFieldRecord[],
  stored: ProductCustomFieldsMap
): ProductCustomFieldsMap {
  return Object.fromEntries(
    fields.map((f) => [
      f.id,
      stored[f.id] ?? (f.controlType === "Multiple Select" ? [] : ""),
    ])
  );
}

/** Strip empty values before POST/PUT (server validates required). */
export function buildCustomFieldsPayload(values: ProductCustomFieldsMap): ProductCustomFieldsMap {
  const out: ProductCustomFieldsMap = {};
  for (const [id, val] of Object.entries(values)) {
    const isEmpty = val === "" || (Array.isArray(val) && val.length === 0);
    if (!isEmpty) out[id] = val;
  }
  return out;
}

export function findMissingRequiredCustomField(
  fields: ProductFieldRecord[],
  values: ProductCustomFieldsMap
): ProductFieldRecord | null {
  for (const field of fields) {
    if (!field.enabled || !field.required) continue;
    const val = values[field.id];
    if (val === "" || val === undefined || (Array.isArray(val) && val.length === 0)) return field;
  }
  return null;
}

/** Tab to navigate when a required field on that target page is missing. */
export function tabForTargetPage(targetPage: string): "general" | "product-page" | "recommendations" {
  switch (targetPage) {
    case "Product Page":
      return "product-page";
    case "Recommendations":
      return "recommendations";
    default:
      return "general";
  }
}
