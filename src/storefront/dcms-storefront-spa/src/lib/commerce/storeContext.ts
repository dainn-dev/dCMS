const DEFAULT_STORE_BY_TENANT: Record<string, string> = {
  "aeon-bt": "s1",
  "aeon-tp": "s1",
  "aeon-bd": "s1",
};

const DEFAULT_WAREHOUSE_BY_STORE: Record<string, string> = {
  s1: "wh-main",
};

export interface StoreScope {
  tenantId: string;
  storeId: string;
  warehouseId: string;
}

export function parseJsonMap(raw: string | undefined, fallback: Record<string, string>): Record<string, string> {
  if (!raw?.trim()) return { ...fallback };
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) return { ...fallback };
    const out: Record<string, string> = {};
    for (const [k, v] of Object.entries(parsed)) {
      if (typeof v === "string" && v.trim()) out[k] = v.trim();
    }
    return Object.keys(out).length > 0 ? out : { ...fallback };
  } catch {
    return { ...fallback };
  }
}

export function resolveStoreScope(tenantId: string): StoreScope {
  const storeMap = parseJsonMap(import.meta.env.VITE_STORE_BY_TENANT, DEFAULT_STORE_BY_TENANT);
  const warehouseMap = parseJsonMap(import.meta.env.VITE_WAREHOUSE_BY_STORE, DEFAULT_WAREHOUSE_BY_STORE);
  const storeId = storeMap[tenantId] ?? storeMap["default"] ?? "s1";
  const warehouseId = warehouseMap[storeId] ?? warehouseMap["default"] ?? "wh-main";
  return { tenantId, storeId, warehouseId };
}
