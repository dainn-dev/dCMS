/**
 * Inventory stock API client.
 * Routes through dCMS.Gateway (YARP) — /gateway/v1/inventory/... → inventory-api /api/v1/...
 */

import { GATEWAY } from "./gatewayConfig";

const BASE = `${GATEWAY.inventory}`;

type ApiEnvelope<TData, TMeta = unknown> = { data: TData; meta: TMeta | null; error: { code?: string; message?: string } | null };

function headers(token?: string): Record<string, string> {
  const h: Record<string, string> = { Accept: "application/json", "Content-Type": "application/json" };
  if (token) h["Authorization"] = `Bearer ${token}`;
  return h;
}

function newIdempotencyKey(): string {
  try {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") return crypto.randomUUID();
  } catch {
    // fall through
  }
  return `idem-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function writeHeaders(token?: string): Record<string, string> {
  return { ...headers(token), "Idempotency-Key": newIdempotencyKey() };
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

export type WarehouseDto = { id: string; name: string; address: string | null; isActive: boolean };

export type VariantStockRow = {
  warehouseId: string;
  warehouseName: string;
  quantity: number;
  reservedQuantity: number;
  availableQuantity: number;
};

export async function listWarehouses(tenantId: string, storeId: string, token?: string): Promise<WarehouseDto[]> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/warehouses`,
    { credentials: "same-origin", headers: headers(token) }
  );
  await checkOk(res);
  const body: ApiEnvelope<{ items: WarehouseDto[] }> = await res.json();
  return body.data?.items ?? [];
}

/** Total on-hand quantity for a variant across all warehouses (0 when no stock rows exist yet). */
export async function getVariantStock(
  tenantId: string,
  storeId: string,
  variantId: string,
  token?: string
): Promise<{ rows: VariantStockRow[]; totalQuantity: number; totalAvailable: number }> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/stock/variants/${encodeURIComponent(
      variantId
    )}`,
    { credentials: "same-origin", headers: headers(token) }
  );
  await checkOk(res);
  const body: ApiEnvelope<{ variantId: string; items: VariantStockRow[] }> = await res.json();
  const rows = body.data?.items ?? [];
  const totalQuantity = rows.reduce((sum, r) => sum + (r.quantity ?? 0), 0);
  const totalAvailable = rows.reduce((sum, r) => sum + (r.availableQuantity ?? 0), 0);
  return { rows, totalQuantity, totalAvailable };
}

/**
 * Sets the absolute on-hand quantity for a variant. When no warehouse is supplied the inventory API uses the store's
 * first warehouse (auto-creating a default "main" warehouse if the store has none).
 */
export async function setVariantOnHand(
  tenantId: string,
  storeId: string,
  variantId: string,
  quantity: number,
  warehouseId?: string,
  token?: string
): Promise<{ variantId: string; warehouseId: string; quantity: number }> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/stock/set-on-hand`,
    {
      method: "POST",
      credentials: "same-origin",
      headers: writeHeaders(token),
      body: JSON.stringify({ variantId, warehouseId: warehouseId ?? null, quantity: Math.max(0, Math.round(quantity)) }),
    }
  );
  await checkOk(res);
  const body: ApiEnvelope<{ variantId: string; warehouseId: string; quantity: number }> = await res.json();
  return body.data;
}
