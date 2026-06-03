/**
 * Per-store product quantity limit configuration.
 * Gateway: /gateway/v1/catalog/tenants/{tenantId}/stores/{storeId}/quantity-limit-settings
 */

import { GATEWAY } from "./gatewayConfig";

const BASE = GATEWAY.catalog;

export type QuantityLimitRule = {
  id: string;
  name: string;
  limitType: "per_cart" | "per_user";
  perProduct: boolean;
  quantityLimit: number;
  startDate: string;
  endDate: string | null;
  brandId: string | null;
  categoryIds: number[];
  productId: string | null;
  membershipType: string | null;
  membershipTier: string | null;
  modifiedBy: string;
  updatedAt?: string | null;
};

export type QuantityLimitHistoryEntry = {
  id: number;
  userId: string;
  userRole: string;
  action: string;
  createdAt: string;
  snapshot: Record<string, unknown> | null;
};

export type QuantityLimitValidateResult = {
  valid: boolean;
  violations: {
    productId: string;
    requested: number;
    limit: number;
    limitScope: string;
    ruleId?: string | null;
    ruleName?: string | null;
  }[];
};

export type QuantityLimitSettings = {
  cartLimitPerProduct: number;
  updatedAt: string | null;
  rules: QuantityLimitRule[];
};

type ApiResponse<T> = { data: T; meta: unknown | null; error: { code?: string; message?: string } | null };

type RuleDto = {
  id?: string;
  name: string;
  limitType: string;
  perProduct: boolean;
  quantityLimit: number;
  startDate: string;
  endDate?: string | null;
  brandId?: string | null;
  categoryIds?: number[];
  productId?: string | null;
  membershipType?: string | null;
  membershipTier?: string | null;
  modifiedBy?: string;
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

function settingsPath(tenantId: string, storeId: string, suffix = "") {
  return `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/quantity-limit-settings${suffix}`;
}

function normalizeRule(raw: RuleDto): QuantityLimitRule {
  const limitType = raw.limitType === "per_user" ? "per_user" : "per_cart";
  return {
    id: raw.id ?? "",
    name: raw.name ?? "",
    limitType,
    perProduct: Boolean(raw.perProduct),
    quantityLimit: raw.quantityLimit ?? 0,
    startDate: raw.startDate ?? "",
    endDate: raw.endDate ?? null,
    brandId: raw.brandId ?? null,
    categoryIds: raw.categoryIds ?? [],
    productId: raw.productId ?? null,
    membershipType: raw.membershipType ?? null,
    membershipTier: raw.membershipTier ?? null,
    modifiedBy: raw.modifiedBy ?? "",
    updatedAt: raw.updatedAt ?? null,
  };
}

function toRulePayload(rule: Omit<QuantityLimitRule, "id" | "modifiedBy" | "updatedAt">) {
  return {
    rule: {
      name: rule.name,
      limitType: rule.limitType,
      perProduct: rule.perProduct,
      quantityLimit: rule.quantityLimit,
      startDate: rule.startDate,
      endDate: rule.endDate,
      brandId: rule.brandId,
      categoryIds: rule.categoryIds,
      productId: rule.productId,
      membershipType: rule.membershipType,
      membershipTier: rule.membershipTier,
    },
  };
}

export function formatLimitTypeDisplay(limitType: QuantityLimitRule["limitType"]): string {
  return limitType === "per_user" ? "Per User" : "Per Cart";
}

export function formatDisplayDate(iso: string | null | undefined): string {
  if (!iso) return "";
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  const dt = new Date(y, m - 1, d);
  return dt.toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }).replace(/ /g, "-");
}

export async function fetchQuantityLimitSettings(
  tenantId: string,
  storeId: string,
  token?: string
): Promise<QuantityLimitSettings> {
  const res = await fetch(settingsPath(tenantId, storeId), { credentials: "same-origin", headers: headers(token) });
  await checkOk(res);
  const body: ApiResponse<{
    cartLimitPerProduct: number;
    updatedAt?: string | null;
    rules?: RuleDto[];
  }> = await res.json();
  return {
    cartLimitPerProduct: body.data?.cartLimitPerProduct ?? 1000,
    updatedAt: body.data?.updatedAt ?? null,
    rules: (body.data?.rules ?? []).map(normalizeRule),
  };
}

export async function saveCartQuantityLimit(
  tenantId: string,
  storeId: string,
  cartLimitPerProduct: number,
  token?: string
): Promise<{ cartLimitPerProduct: number; updatedAt: string | null }> {
  const res = await fetch(settingsPath(tenantId, storeId), {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify({ cartLimitPerProduct }),
  });
  await checkOk(res);
  const body: ApiResponse<{ cartLimitPerProduct: number; updatedAt?: string | null }> = await res.json();
  return {
    cartLimitPerProduct: body.data?.cartLimitPerProduct ?? cartLimitPerProduct,
    updatedAt: body.data?.updatedAt ?? null,
  };
}

export async function createQuantityLimitRule(
  tenantId: string,
  storeId: string,
  rule: Omit<QuantityLimitRule, "id" | "modifiedBy" | "updatedAt">,
  token?: string
): Promise<QuantityLimitRule> {
  const res = await fetch(`${settingsPath(tenantId, storeId)}/rules`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(toRulePayload(rule)),
  });
  await checkOk(res);
  const body: ApiResponse<RuleDto> = await res.json();
  return normalizeRule(body.data ?? ({} as RuleDto));
}

export async function updateQuantityLimitRule(
  tenantId: string,
  storeId: string,
  ruleId: string,
  rule: Omit<QuantityLimitRule, "id" | "modifiedBy" | "updatedAt">,
  token?: string
): Promise<QuantityLimitRule> {
  const res = await fetch(`${settingsPath(tenantId, storeId)}/rules/${encodeURIComponent(ruleId)}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(toRulePayload(rule)),
  });
  await checkOk(res);
  const body: ApiResponse<RuleDto> = await res.json();
  return normalizeRule(body.data ?? ({} as RuleDto));
}

export async function deleteQuantityLimitRule(
  tenantId: string,
  storeId: string,
  ruleId: string,
  token?: string
): Promise<void> {
  const res = await fetch(`${settingsPath(tenantId, storeId)}/rules/${encodeURIComponent(ruleId)}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}

export async function fetchQuantityLimitHistory(
  tenantId: string,
  storeId: string,
  token?: string,
  limit = 20
): Promise<QuantityLimitHistoryEntry[]> {
  const res = await fetch(`${settingsPath(tenantId, storeId)}/history?limit=${limit}`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body: ApiResponse<QuantityLimitHistoryEntry[]> = await res.json();
  return body.data ?? [];
}
