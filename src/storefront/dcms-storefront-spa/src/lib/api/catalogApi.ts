import { GATEWAY } from "./gateway";
import { callEnvelopeJson } from "./commerceFetch";

export interface ProductListItem {
  id: string;
  name: string;
  slug: string;
  minBasePrice: { amount: number; currency: string };
  hasInStockVariant: boolean;
}

export interface ProductListMeta {
  totalCount?: number;
  pageSize?: number;
  nextCursor?: string | null;
}

export interface VariantCombination {
  variantId: string;
  sku: string;
  basePriceAmount: number;
  inStock: boolean;
}

export interface ProductDetail {
  id: string;
  tenantId: string;
  storeId: string;
  slug: string;
  name: unknown;
  description?: unknown;
  variantMatrix: {
    axes: unknown[];
    combinations: Record<string, VariantCombination>;
  };
}

export interface SearchProductsParams {
  tenantId: string;
  storeId: string;
  q?: string;
  pageSize?: number;
  cursor?: string | null;
}

export function buildProductsSearchUrl(params: SearchProductsParams): string {
  const qs = new URLSearchParams({
    tenantId: params.tenantId,
    storeId: params.storeId,
  });
  if (params.q?.trim()) qs.set("q", params.q.trim());
  if (params.pageSize) qs.set("pageSize", String(params.pageSize));
  if (params.cursor) qs.set("cursor", params.cursor);
  return `${GATEWAY.catalog}/products?${qs}`;
}

export async function searchProducts(
  params: SearchProductsParams,
): Promise<{ items: ProductListItem[]; meta: ProductListMeta }> {
  const { data, meta } = await callEnvelopeJson<ProductListItem[]>(
    buildProductsSearchUrl(params),
  );
  return { items: data, meta: (meta ?? {}) as ProductListMeta };
}

export async function getProductBySlug(
  slug: string,
  tenantId: string,
  storeId: string,
): Promise<ProductDetail> {
  const qs = new URLSearchParams({ tenantId, storeId });
  const { data } = await callEnvelopeJson<ProductDetail>(
    `${GATEWAY.catalog}/products/${encodeURIComponent(slug)}?${qs}`,
  );
  return data;
}

export function productDisplayName(name: unknown): string {
  if (typeof name === "string" && name.trim()) return name;
  if (name && typeof name === "object" && !Array.isArray(name)) {
    const rec = name as Record<string, string>;
    const en = rec.en ?? rec["en-US"];
    const first = Object.values(rec).find(v => typeof v === "string" && v.trim());
    return en ?? first ?? "Product";
  }
  return "Product";
}
