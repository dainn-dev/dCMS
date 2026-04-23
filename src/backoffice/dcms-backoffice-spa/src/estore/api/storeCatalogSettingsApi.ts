/**
 * DAI-616: Per-store catalog settings (approval + low stock threshold).
 * Gateway: /gateway/v1/catalog → Catalog.Api /api/v1/tenants/{tenantId}/stores/{storeId}/store-catalog-settings
 */

import { GATEWAY } from "./gatewayConfig";

const CATALOG_BASE = GATEWAY.catalog;

export type StoreCatalogSettingsDto = {
  approvalRequired: boolean;
  lowStockThreshold: number | null;
  updatedAt: string | null;
};

type ApiResponse<T> = { data: T; meta: unknown | null; error: { code?: string; message?: string } | null };

function hdr(token?: string): Record<string, string> {
  const h: Record<string, string> = {
    Accept: "application/json",
    "Content-Type": "application/json",
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
      // ignore
    }
    throw new Error(errMsg);
  }
}

export async function fetchStoreCatalogSettings(
  tenantId: string,
  storeId: string,
  token?: string
): Promise<StoreCatalogSettingsDto> {
  const res = await fetch(
    `${CATALOG_BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/store-catalog-settings`,
    { credentials: "same-origin", headers: hdr(token) }
  );
  await checkOk(res);
  const body: ApiResponse<StoreCatalogSettingsDto> = await res.json();
  const d = body.data;
  return {
    approvalRequired: !!d?.approvalRequired,
    lowStockThreshold: typeof d?.lowStockThreshold === "number" ? d.lowStockThreshold : null,
    updatedAt: d?.updatedAt ?? null,
  };
}

export type PatchStoreCatalogSettingsPayload = {
  approvalRequired?: boolean;
  lowStockThreshold?: number | null;
};

export async function patchStoreCatalogSettings(
  tenantId: string,
  storeId: string,
  payload: PatchStoreCatalogSettingsPayload,
  token?: string
): Promise<StoreCatalogSettingsDto> {
  const res = await fetch(
    `${CATALOG_BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/store-catalog-settings`,
    {
      method: "PATCH",
      credentials: "same-origin",
      headers: hdr(token),
      body: JSON.stringify({
        approvalRequired: payload.approvalRequired,
        lowStockThreshold: payload.lowStockThreshold,
      }),
    }
  );
  await checkOk(res);
  const body: ApiResponse<StoreCatalogSettingsDto> = await res.json();
  const d = body.data;
  return {
    approvalRequired: !!d?.approvalRequired,
    lowStockThreshold: typeof d?.lowStockThreshold === "number" ? d.lowStockThreshold : null,
    updatedAt: d?.updatedAt ?? null,
  };
}
