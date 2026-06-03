/**
 * Per-store Best Seller widget configuration.
 * Gateway: /gateway/v1/catalog/tenants/{tenantId}/stores/{storeId}/best-seller-settings
 */

import { GATEWAY } from "./gatewayConfig";

const BASE = GATEWAY.catalog;

export type BestSellerSettings = {
  displayList: boolean;
  popularityDurationDays: number;
  genderBased: boolean;
  recommendationLogic: "sales-quantity" | "sales-amount" | "views" | "manual";
  maxItems: number;
  whitelistedCategoryIds: number[];
  blacklistedCategoryIds: number[];
  whitelistedBrandIds: string[];
  blacklistedBrandIds: string[];
  includedProductIds: string[];
  excludedProductIds: string[];
  manualProductIds: string[];
};

export type BestSellerHistoryEntry = {
  id: number;
  userId: string;
  userRole: string;
  createdAt: string;
  settings: BestSellerSettings | null;
};

export type BestSellerPreviewItem = {
  id: string;
  name: string;
  slug: string;
  salesCount30d: number;
  pageViews30d: number;
};

export const DEFAULT_BEST_SELLER_SETTINGS: BestSellerSettings = {
  displayList: true,
  popularityDurationDays: 30,
  genderBased: true,
  recommendationLogic: "sales-quantity",
  maxItems: 4,
  whitelistedCategoryIds: [],
  blacklistedCategoryIds: [],
  whitelistedBrandIds: [],
  blacklistedBrandIds: [],
  includedProductIds: [],
  excludedProductIds: [],
  manualProductIds: [],
};

type ApiResponse<T> = { data: T; meta: unknown | null; error: { code?: string; message?: string } | null };

type SettingsDto = {
  configured: boolean;
  settings: {
    displayList: boolean;
    popularityDurationDays: number;
    genderBased: boolean;
    recommendationLogic: string;
    maxItems: number;
    whitelistedCategoryIds?: number[];
    blacklistedCategoryIds?: number[];
    whitelistedBrandIds?: string[];
    blacklistedBrandIds?: string[];
    includedProductIds?: string[];
    excludedProductIds?: string[];
    manualProductIds?: string[];
  };
  updatedAt?: string | null;
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

function normalize(raw: SettingsDto["settings"]): BestSellerSettings {
  const logic = raw.recommendationLogic as BestSellerSettings["recommendationLogic"];
  return {
    displayList: raw.displayList ?? true,
    popularityDurationDays: raw.popularityDurationDays ?? 30,
    genderBased: raw.genderBased ?? true,
    recommendationLogic:
      logic === "sales-amount" || logic === "views" || logic === "manual" ? logic : "sales-quantity",
    maxItems: raw.maxItems ?? 4,
    whitelistedCategoryIds: raw.whitelistedCategoryIds ?? [],
    blacklistedCategoryIds: raw.blacklistedCategoryIds ?? [],
    whitelistedBrandIds: raw.whitelistedBrandIds ?? [],
    blacklistedBrandIds: raw.blacklistedBrandIds ?? [],
    includedProductIds: raw.includedProductIds ?? [],
    excludedProductIds: raw.excludedProductIds ?? [],
    manualProductIds: raw.manualProductIds ?? [],
  };
}

function toPayload(settings: BestSellerSettings) {
  return {
    settings: {
      displayList: settings.displayList,
      popularityDurationDays: settings.popularityDurationDays,
      genderBased: settings.genderBased,
      recommendationLogic: settings.recommendationLogic,
      maxItems: settings.maxItems,
      whitelistedCategoryIds: settings.whitelistedCategoryIds,
      blacklistedCategoryIds: settings.blacklistedCategoryIds,
      whitelistedBrandIds: settings.whitelistedBrandIds,
      blacklistedBrandIds: settings.blacklistedBrandIds,
      includedProductIds: settings.includedProductIds,
      excludedProductIds: settings.excludedProductIds,
      manualProductIds: settings.manualProductIds,
    },
  };
}

function settingsPath(tenantId: string, storeId: string, suffix = "") {
  return `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/best-seller-settings${suffix}`;
}

export async function fetchBestSellerSettings(
  tenantId: string,
  storeId: string,
  token?: string
): Promise<{ configured: boolean; settings: BestSellerSettings; updatedAt: string | null }> {
  const res = await fetch(settingsPath(tenantId, storeId), { credentials: "same-origin", headers: headers(token) });
  await checkOk(res);
  const body: ApiResponse<SettingsDto> = await res.json();
  return {
    configured: Boolean(body.data?.configured),
    settings: normalize(body.data?.settings ?? ({} as SettingsDto["settings"])),
    updatedAt: body.data?.updatedAt ?? null,
  };
}

export async function saveBestSellerSettings(
  tenantId: string,
  storeId: string,
  settings: BestSellerSettings,
  token?: string
): Promise<BestSellerSettings> {
  const res = await fetch(settingsPath(tenantId, storeId), {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(toPayload(settings)),
  });
  await checkOk(res);
  const body: ApiResponse<SettingsDto> = await res.json();
  return normalize(body.data?.settings ?? ({} as SettingsDto["settings"]));
}

export async function fetchBestSellerHistory(
  tenantId: string,
  storeId: string,
  token?: string,
  limit = 20
): Promise<BestSellerHistoryEntry[]> {
  const res = await fetch(`${settingsPath(tenantId, storeId)}/history?limit=${limit}`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body: ApiResponse<BestSellerHistoryEntry[]> = await res.json();
  return body.data ?? [];
}

export async function fetchBestSellerPreview(
  tenantId: string,
  storeId: string,
  token?: string
): Promise<{ items: BestSellerPreviewItem[]; updatedAt: string | null }> {
  const res = await fetch(`${settingsPath(tenantId, storeId)}/preview`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body: ApiResponse<{ items: BestSellerPreviewItem[]; updatedAt?: string | null }> = await res.json();
  return { items: body.data?.items ?? [], updatedAt: body.data?.updatedAt ?? null };
}
