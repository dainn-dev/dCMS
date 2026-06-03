/**
 * Store-scoped Product Configuration custom fields.
 * Gateway: /gateway/v1/catalog/tenants/{tenantId}/stores/{storeId}/product-field-config
 */

import { GATEWAY } from "./gatewayConfig";

const BASE = GATEWAY.catalog;

export type ProductFieldControlType =
  | "Text Box"
  | "WYSIWYG (Text Area)"
  | "Dropdown List"
  | "Checkbox"
  | "Date Picker"
  | "Multiple Select";

export type ProductFieldTargetPage = "General" | "Product Page" | "Recommendations";

export type ProductFieldOption = { name: string; value: string };

export type ProductFieldRecord = {
  id: string;
  enabled: boolean;
  required: boolean;
  property: string;
  columnLabel: string;
  fieldName: string;
  controlType: ProductFieldControlType;
  targetPage: ProductFieldTargetPage;
  options: ProductFieldOption[];
};

type ApiResponse<T> = { data: T; meta: unknown | null; error: { code?: string; message?: string } | null };

type ConfigDto = {
  configured: boolean;
  fields: ProductFieldRecord[];
  updatedAt?: string | null;
  reindexQueued?: boolean;
};

function headers(token?: string): Record<string, string> {
  const h: Record<string, string> = { Accept: "application/json", "Content-Type": "application/json" };
  if (token) h["Authorization"] = `Bearer ${token}`;
  return h;
}

async function checkOk(res: Response): Promise<void> {
  if (!res.ok) {
    let errMsg = `HTTP ${res.status}`;
    try {
      const body = await res.json();
      if (body?.error?.message) errMsg = body.error.message;
    } catch {
      // ignore
    }
    throw new Error(errMsg);
  }
}

const CONTROL_TYPES: ProductFieldControlType[] = [
  "Text Box",
  "WYSIWYG (Text Area)",
  "Dropdown List",
  "Checkbox",
  "Date Picker",
  "Multiple Select",
];

const TARGET_PAGES: ProductFieldTargetPage[] = ["General", "Product Page", "Recommendations"];

function normalizeField(raw: ProductFieldRecord): ProductFieldRecord {
  const controlType = (CONTROL_TYPES.find((c) => c === raw.controlType) ?? "Text Box") as ProductFieldControlType;
  const targetPage = (TARGET_PAGES.find((t) => t === raw.targetPage) ?? "General") as ProductFieldTargetPage;
  return {
    id: raw.id ?? `pfld-${Date.now()}`,
    enabled: Boolean(raw.enabled),
    required: Boolean(raw.required),
    property: (raw.property ?? "").trim().toLowerCase(),
    columnLabel: raw.columnLabel ?? "",
    fieldName: raw.fieldName ?? "",
    controlType,
    targetPage,
    options: (raw.options ?? []).map((o) => ({ name: o.name ?? "", value: o.value ?? "" })),
  };
}

export async function fetchProductFieldConfig(
  tenantId: string,
  storeId: string,
  token?: string
): Promise<{ configured: boolean; fields: ProductFieldRecord[]; updatedAt: string | null }> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/product-field-config`,
    { credentials: "same-origin", headers: headers(token) }
  );
  await checkOk(res);
  const body: ApiResponse<ConfigDto> = await res.json();
  return {
    configured: Boolean(body.data?.configured),
    fields: (body.data?.fields ?? []).map(normalizeField),
    updatedAt: body.data?.updatedAt ?? null,
  };
}

export async function saveProductFieldConfig(
  tenantId: string,
  storeId: string,
  fields: ProductFieldRecord[],
  token?: string
): Promise<{ fields: ProductFieldRecord[]; reindexQueued: boolean; updatedAt: string | null }> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/product-field-config`,
    {
      method: "PUT",
      credentials: "same-origin",
      headers: headers(token),
      body: JSON.stringify({ fields }),
    }
  );
  await checkOk(res);
  const body: ApiResponse<ConfigDto> = await res.json();
  return {
    fields: (body.data?.fields ?? []).map(normalizeField),
    reindexQueued: Boolean(body.data?.reindexQueued),
    updatedAt: body.data?.updatedAt ?? null,
  };
}
