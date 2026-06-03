/**
 * Brand Configuration API client — tenant-scoped dynamic "additional fields"
 * shown on the Add/Edit Brand form.
 *
 * Routes through dCMS.Gateway (YARP) — /gateway/v1/catalog/tenants/{tenantId}/brand-field-config
 * Replaces the previous browser-only (localStorage) persistence so the config is
 * shared across all admins of a tenant and is properly TenantId-scoped server-side.
 */

import type { BrandAdditionalField } from "../pages/BrandConfigPage";
import { GATEWAY } from "./gatewayConfig";

const CATALOG_BASE = GATEWAY.catalog;

type ConfigDto = { configured: boolean; fields: BrandAdditionalField[] };
type ApiResponse<T> = { data: T; meta: unknown; error: unknown };

function headers(token?: string): Record<string, string> {
  const h: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "application/json",
  };
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
      // ignore parse error
    }
    throw new Error(errMsg);
  }
}

/** Defensive normalisation — guarantees options is always an array. */
function normalize(fields: BrandAdditionalField[] | undefined | null): BrandAdditionalField[] {
  return (fields ?? []).map((f) => ({ ...f, options: f.options ?? [] }));
}

/**
 * Loads the tenant's saved field configuration.
 * `configured` is false when the tenant has never saved a config — callers should
 * then fall back to seed defaults.
 */
export async function fetchBrandFieldConfig(
  tenantId: string,
  token?: string
): Promise<{ configured: boolean; fields: BrandAdditionalField[] }> {
  const res = await fetch(`${CATALOG_BASE}/tenants/${tenantId}/brand-field-config`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body: ApiResponse<ConfigDto> = await res.json();
  return {
    configured: Boolean(body.data?.configured),
    fields: normalize(body.data?.fields),
  };
}

/** Persists the whole ordered field list. Server validates (dup/empty/options); throws on rejection. */
export async function saveBrandFieldConfig(
  tenantId: string,
  fields: BrandAdditionalField[],
  token?: string
): Promise<BrandAdditionalField[]> {
  const res = await fetch(`${CATALOG_BASE}/tenants/${tenantId}/brand-field-config`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify({ fields }),
  });
  await checkOk(res);
  const body: ApiResponse<ConfigDto> = await res.json();
  return normalize(body.data?.fields);
}
