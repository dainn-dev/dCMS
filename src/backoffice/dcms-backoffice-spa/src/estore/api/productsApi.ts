/**
 * DAI-618: Product CRUD/search API client.
 * Routes through dCMS.Gateway (YARP) — /gateway/v1/catalog/... → catalog-api /api/v1/...
 */
 
 import type { ProductListRow } from "../pages/ProductsPage";
 import { GATEWAY } from "./gatewayConfig";
 
 const BASE = `${GATEWAY.catalog}`;
 
 type ApiEnvelope<TData, TMeta = unknown> = { data: TData; meta: TMeta | null; error: { code?: string; message?: string } | null };
 
 function headers(token?: string): Record<string, string> {
   const h: Record<string, string> = {
     Accept: "application/json",
     "Content-Type": "application/json",
   };
   if (token) h["Authorization"] = `Bearer ${token}`;
   return h;
 }
 
 /** Generate a unique Idempotency-Key (required by the catalog API for POST/PUT/PATCH on /products). */
 function newIdempotencyKey(): string {
   try {
     if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") return crypto.randomUUID();
   } catch {
     // fall through to manual generation
   }
   return `idem-${Date.now()}-${Math.random().toString(36).slice(2)}-${Math.random().toString(36).slice(2)}`;
 }
 
 /** Headers for a mutating request: adds a fresh Idempotency-Key on top of the base headers. */
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
 
 export type ProductFilters = {
   upc?: string;
   sku?: string;
   name?: string;
   priceMin?: number;
   priceMax?: number;
   brand?: string;
   category?: string;
   qtyMin?: number;
   qtyMax?: number;
   estoreStatus?: "live" | "draft" | "inactive";
   quickAccess?: ("zero-qty" | "restock" | "sell-on-estore" | "estore-only")[];
 };
 
 type SearchMeta = { totalCount: number; pageSize: number; nextCursor: string | null };
 
 export type ProductSearchDto = {
   id: string;
   name: string;
   nameByLocale: Record<string, string> | null;
   minBasePrice: { amount: number; currency: string } | null;
   hasInStockVariant: boolean;
   slug: string;
   status?: string;
 };
 
 const LOCALE_PREFERENCE = ["vi", "en"] as const;
 
 function pickName(dto: ProductSearchDto): string {
   const by = dto.nameByLocale ?? {};
   for (const k of LOCALE_PREFERENCE) {
     const v = by[k];
     if (typeof v === "string" && v.trim()) return v;
   }
   const first = Object.values(by).find((v) => typeof v === "string" && v.trim());
   if (first) return first;
   return dto.name ?? "";
 }
 
 function formatMoney(amountMinor: number, currency: string): string {
   // `amount` in API is minor units (e.g. cents); keep behavior stable across locales by using browser default.
   const value = Number.isFinite(amountMinor) ? amountMinor / 100 : 0;
   try {
     return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(value);
   } catch {
     return `${value.toFixed(2)} ${currency}`;
   }
 }
 
 /** Maps a persisted product status to the eStore badge (live = published & visible). */
 function eStoreBadge(status: string, inStock: boolean): { label: string; variant: "live" | "low-stock" | "offline" } {
   if (status !== "active") return { label: "Offline", variant: "offline" };
   if (!inStock) return { label: "Live · No stock", variant: "low-stock" };
   return { label: "Live", variant: "live" };
 }
 
 /** Maps a persisted product status to the workflow status badge. */
 function statusBadge(status: string): { label: string; variant: "active" | "out-of-stock" } {
   switch (status) {
     case "active":
       return { label: "Active", variant: "active" };
     case "draft":
       return { label: "Draft", variant: "out-of-stock" };
     case "pending_approval":
       return { label: "Pending Approval", variant: "out-of-stock" };
     case "pending_archive":
       return { label: "Pending Archive", variant: "out-of-stock" };
     case "hidden":
       return { label: "Hidden", variant: "out-of-stock" };
     case "archived":
       return { label: "Archived", variant: "out-of-stock" };
     default:
       return { label: status || "Unknown", variant: "out-of-stock" };
   }
 }
 
 export function productFromDto(dto: ProductSearchDto): ProductListRow {
   const inStock = !!dto.hasInStockVariant;
   const status = dto.status ?? "active";
   const price =
     dto.minBasePrice && typeof dto.minBasePrice.amount === "number" && dto.minBasePrice.currency
       ? formatMoney(dto.minBasePrice.amount, dto.minBasePrice.currency)
       : "";
 
   const eStore = eStoreBadge(status, inStock);
   const stat = statusBadge(status);
   return {
     id: dto.id,
     name: pickName(dto),
     categoryPath: "",
     brand: "",
     upc: "",
     sku: "",
     price,
     qty: inStock ? 1 : 0,
     eStoreLabel: eStore.label,
     eStoreVariant: eStore.variant,
     statusLabel: stat.label,
     statusVariant: stat.variant,
     modified: "",
     imageSrc: "",
     imageAlt: "",
   };
 }
 
/** Product Page / SEO metadata + visibility flags sent alongside the core product fields. */
export type ProductPageMetadataPayload = {
  pageTitleJson: string;
  metaKeywordsJson: string;
  metaDescriptionJson: string;
  publishFrom: string | null;
  publishUntil: string | null;
  recommendSimilar: boolean;
  recommendationsMode: string;
  restockNotification: boolean;
};

export type ProductWritePayload = {
  categoryId: number;
  nameJson: string;
  descriptionJson?: string;
  slug: string;
  brandId?: string | null;
  metadata?: ProductPageMetadataPayload;
  customFields?: Record<string, string | string[]>;
};

export type ProductPageMetadataInput = {
  pageTitleByLocale?: Record<string, string>;
  metaKeywordsByLocale?: Record<string, string>;
  metaDescriptionByLocale?: Record<string, string>;
  publishFrom?: string | null;
  publishUntil?: string | null;
  recommendSimilar?: boolean;
  recommendationsMode?: string;
  restockNotification?: boolean;
};

export type ProductWriteRow = Pick<ProductListRow, "name"> & {
  categoryId: number;
  slug: string;
  nameByLocale?: Record<string, string>;
  descriptionByLocale?: Record<string, string>;
  brandId?: string | null;
  metadata?: ProductPageMetadataInput;
  customFields?: Record<string, string | string[]>;
};

export function productToPayload(row: ProductWriteRow): ProductWritePayload {
  const nameByLocale = row.nameByLocale ?? { vi: row.name };
  const descriptionByLocale = row.descriptionByLocale ?? {};
  const payload: ProductWritePayload = {
    categoryId: row.categoryId,
    nameJson: JSON.stringify(nameByLocale),
    descriptionJson: JSON.stringify(descriptionByLocale),
    slug: row.slug,
    brandId: row.brandId ?? null,
  };
  if (row.metadata) {
    const m = row.metadata;
    payload.metadata = {
      pageTitleJson: JSON.stringify(m.pageTitleByLocale ?? {}),
      metaKeywordsJson: JSON.stringify(m.metaKeywordsByLocale ?? {}),
      metaDescriptionJson: JSON.stringify(m.metaDescriptionByLocale ?? {}),
      publishFrom: m.publishFrom ?? null,
      publishUntil: m.publishUntil ?? null,
      recommendSimilar: m.recommendSimilar ?? true,
      recommendationsMode: m.recommendationsMode ?? "auto",
      restockNotification: m.restockNotification ?? false,
    };
  }
  if (row.customFields !== undefined) {
    payload.customFields = row.customFields;
  }
  return payload;
}
 
 function buildSearchQuery(filters?: ProductFilters): string | undefined {
   if (!filters) return undefined;
  const parts = [filters.name, filters.sku, filters.upc].filter((x): x is string => Boolean(x && x.trim()));
   return parts.length ? parts.join(" ") : undefined;
 }
 
 function buildCursor(page?: number, pageSize?: number): string | undefined {
   if (!page || page <= 1) return undefined;
   if (!pageSize || pageSize <= 0) return undefined;
   return String((page - 1) * pageSize);
 }
 
 export async function fetchProducts(
   tenantId: string,
   storeId: string,
   filters?: ProductFilters,
   paging?: { page?: number; pageSize?: number },
   token?: string
 ): Promise<{ rows: ProductListRow[]; total: number }> {
   const params = new URLSearchParams();
   const q = buildSearchQuery(filters);
   if (q) params.set("q", q);
   if (filters?.priceMin !== undefined) params.set("minPrice", String(Math.round(filters.priceMin)));
   if (filters?.priceMax !== undefined) params.set("maxPrice", String(Math.round(filters.priceMax)));
   if (filters?.estoreStatus) params.set("status", filters.estoreStatus);
   if (filters?.category) {
     const catId = Number(filters.category);
     if (Number.isInteger(catId) && catId > 0) params.set("category", String(catId));
   }
   if (filters?.brand && filters.brand !== "all") params.set("brand", filters.brand);
   if (paging?.pageSize) params.set("pageSize", String(paging.pageSize));
   const cursor = buildCursor(paging?.page, paging?.pageSize);
   if (cursor) params.set("cursor", cursor);
 
   const qs = params.toString() ? `?${params.toString()}` : "";
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products${qs}`,
     { credentials: "same-origin", headers: headers(token) }
   );
   await checkOk(res);
   const body: ApiEnvelope<ProductSearchDto[], SearchMeta> = await res.json();
   return { rows: (body.data ?? []).map(productFromDto), total: body.meta?.totalCount ?? 0 };
 }
 
export async function fetchAllProductsForExport(
  tenantId: string,
  storeId: string,
  filters: ProductFilters | undefined,
  token?: string,
  options?: { limit?: number; pageSize?: number }
): Promise<{ rows: ProductListRow[]; total: number; limited: boolean }> {
  const limit = options?.limit ?? 5000;
  const pageSize = Math.min(Math.max(options?.pageSize ?? 200, 1), 500);
  let page = 1;
  let total = 0;
  const out: ProductListRow[] = [];

  // Loop paging until we collected all or hit limit
  for (;;) {
    const { rows, total: t } = await fetchProducts(tenantId, storeId, filters, { page, pageSize }, token);
    if (page === 1) total = t;
    if (!rows.length) break;

    for (const r of rows) {
      out.push(r);
      if (out.length >= limit) return { rows: out, total, limited: true };
    }

    if (out.length >= total) break;
    page++;
    // safety: don't infinite loop if total missing
    if (page > 1000) return { rows: out, total, limited: true };
  }

  return { rows: out, total, limited: false };
}

 export type ProductDetailDto = {
   id: string;
   tenantId: string;
   storeId: string;
   categoryId: number;
   brandId: string | null;
   nameJson: string;
   descriptionJson: string;
   slug: string;
   status: string;
   salesCount30d: number;
   createdAt: string;
   updatedAt: string;
   pageTitleJson?: string;
   metaKeywordsJson?: string;
   metaDescriptionJson?: string;
   publishFrom?: string | null;
   publishUntil?: string | null;
   recommendSimilar?: boolean;
   recommendationsMode?: string;
   restockNotification?: boolean;
   customFieldsJson?: string;
 };

export type RecommendationDto = {
  id: string;
  nameJson: string;
  slug: string;
  status: string;
  sortOrder: number;
};

/** Manual recommendations (related products) currently saved for a product. */
export async function listRecommendations(
  tenantId: string,
  storeId: string,
  productId: string,
  token?: string
): Promise<RecommendationDto[]> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
      productId
    )}/recommendations`,
    { credentials: "same-origin", headers: headers(token) }
  );
  await checkOk(res);
  const body: ApiEnvelope<{ items: RecommendationDto[] }> = await res.json();
  return body.data?.items ?? [];
}

/** Replaces the full ordered recommendation list for a product. Returns the persisted ordered ids. */
export async function setRecommendations(
  tenantId: string,
  storeId: string,
  productId: string,
  recommendedProductIds: string[],
  token?: string
): Promise<string[]> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
      productId
    )}/recommendations`,
    {
      method: "PUT",
      credentials: "same-origin",
      headers: writeHeaders(token),
      body: JSON.stringify({ recommendedProductIds }),
    }
  );
  await checkOk(res);
  const body: ApiEnvelope<{ items: string[] }> = await res.json();
  return body.data?.items ?? [];
}
 
 export async function getProduct(
   tenantId: string,
   storeId: string,
   productId: string,
   token?: string
 ): Promise<ProductDetailDto> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(productId)}`,
     { credentials: "same-origin", headers: headers(token) }
   );
   await checkOk(res);
   const body: ApiEnvelope<ProductDetailDto> = await res.json();
   return body.data;
 }
 
 export async function createProduct(
   tenantId: string,
   storeId: string,
   payload: ProductWritePayload,
   token?: string
 ): Promise<{ id: string; slug: string; status: string }> {
   const res = await fetch(`${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products`, {
     method: "POST",
     credentials: "same-origin",
     headers: writeHeaders(token),
     body: JSON.stringify(payload),
   });
   await checkOk(res);
   const body: ApiEnvelope<{ id: string; slug: string; status: string }> = await res.json();
   return body.data;
 }
 
 export async function updateProduct(
   tenantId: string,
   storeId: string,
   productId: string,
   payload: ProductWritePayload,
   token?: string
 ): Promise<{ id: string }> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(productId)}`,
     {
       method: "PUT",
       credentials: "same-origin",
       headers: writeHeaders(token),
       body: JSON.stringify(payload),
     }
   );
   await checkOk(res);
   const body: ApiEnvelope<{ id: string }> = await res.json();
   return body.data;
 }
 
 /** Soft-archive (server implements as archive). */
 export async function deleteProduct(
   tenantId: string,
   storeId: string,
   productId: string,
   token?: string
 ): Promise<void> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(productId)}`,
     {
       method: "DELETE",
       credentials: "same-origin",
       headers: headers(token),
     }
   );
   await checkOk(res);
 }
 
 export type BulkCreateProductsPayload = { items: ProductWritePayload[] };
 export type BulkUpdateProductsPayload = { items: (ProductWritePayload & { productId: string })[] };
 
 export async function bulkUpdateProducts(
   tenantId: string,
   storeId: string,
   payload: BulkUpdateProductsPayload,
   token?: string
 ): Promise<{ succeeded: number; failed: number }> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/bulk`,
     {
       method: "PUT",
       credentials: "same-origin",
       headers: writeHeaders(token),
       body: JSON.stringify(payload),
     }
   );
   await checkOk(res);
   const body: ApiEnvelope<{ succeeded: number; failed: number }> = await res.json();
   return body.data;
 }
 
 export async function publishProduct(tenantId: string, storeId: string, productId: string, token?: string) {
   return stateAction(tenantId, storeId, productId, "publish", token);
 }
 export async function hideProduct(tenantId: string, storeId: string, productId: string, token?: string) {
   return stateAction(tenantId, storeId, productId, "hide", token);
 }
 export async function unhideProduct(tenantId: string, storeId: string, productId: string, token?: string) {
   return stateAction(tenantId, storeId, productId, "unhide", token);
 }
 export async function archiveProduct(tenantId: string, storeId: string, productId: string, token?: string) {
   return stateAction(tenantId, storeId, productId, "archive", token);
 }
 
export type BulkProductOp = "publish" | "hide" | "archive";

/**
 * DAI-621: Bulk operations for product state.
 *
 * Note: Catalog API's bulk endpoint currently updates product fields only (name/slug/category/description).
 * State transitions are implemented via dedicated endpoints, so we fan out calls per id.
 */
export async function bulkOperateProducts(
  tenantId: string,
  storeId: string,
  ids: string[],
  op: BulkProductOp,
  token?: string
): Promise<{ succeeded: number; failed: number }> {
  const unique = Array.from(new Set(ids.map((x) => String(x)).filter((x) => x.trim())));
  if (unique.length === 0) return { succeeded: 0, failed: 0 };

  const settled = await Promise.allSettled(unique.map((id) => stateAction(tenantId, storeId, id, op, token)));
  let succeeded = 0;
  let failed = 0;
  for (const r of settled) {
    if (r.status === "fulfilled") succeeded++;
    else failed++;
  }
  return { succeeded, failed };
}

 async function stateAction(
   tenantId: string,
   storeId: string,
   productId: string,
   action: "publish" | "hide" | "unhide" | "archive",
   token?: string
 ): Promise<{ id: string; status: string }> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
       productId
     )}/${action}`,
     {
       method: "POST",
       credentials: "same-origin",
       headers: writeHeaders(token),
     }
   );
   await checkOk(res);
   const body: ApiEnvelope<{ id: string; status: string }> = await res.json();
   return body.data;
 }

 /** POST a no-body approval/workflow action on a product. */
 async function workflowAction(
   tenantId: string,
   storeId: string,
   productId: string,
   action: "submit-for-approval" | "approve" | "submit-for-archive",
   token?: string
 ): Promise<{ id: string; status: string }> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
       productId
     )}/${action}`,
     { method: "POST", credentials: "same-origin", headers: writeHeaders(token) }
   );
   await checkOk(res);
   const body: ApiEnvelope<{ id: string; status: string }> = await res.json();
   return body.data;
 }

 export function submitProductForApproval(tenantId: string, storeId: string, productId: string, token?: string) {
   return workflowAction(tenantId, storeId, productId, "submit-for-approval", token);
 }
 export function approveProduct(tenantId: string, storeId: string, productId: string, token?: string) {
   return workflowAction(tenantId, storeId, productId, "approve", token);
 }
 /** Request archival (active|hidden → pending_archive); an approver then approves to finalize. */
 export function submitProductForArchive(tenantId: string, storeId: string, productId: string, token?: string) {
   return workflowAction(tenantId, storeId, productId, "submit-for-archive", token);
 }

 /** Reject a pending product (requires a reviewer comment). */
 export async function rejectProduct(
   tenantId: string,
   storeId: string,
   productId: string,
   comment: string,
   token?: string
 ): Promise<{ id: string; status: string }> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
       productId
     )}/reject`,
     { method: "POST", credentials: "same-origin", headers: writeHeaders(token), body: JSON.stringify({ comment }) }
   );
   await checkOk(res);
   const body: ApiEnvelope<{ id: string; status: string }> = await res.json();
   return body.data;
 }

 /** Change a product's category by re-saving its current details with a new categoryId. */
 export async function changeProductCategory(
   tenantId: string,
   storeId: string,
   productId: string,
   categoryId: number,
   token?: string
 ): Promise<{ id: string }> {
   const detail = await getProduct(tenantId, storeId, productId, token);
   return updateProduct(
     tenantId,
     storeId,
     productId,
     {
       categoryId,
       nameJson: detail.nameJson,
       descriptionJson: detail.descriptionJson,
       slug: detail.slug,
       brandId: detail.brandId ?? null,
     },
     token
   );
 }

 /** Fan out a per-product async action over a unique id set, tallying success/failure (DAI-621 pattern). */
 async function fanOut(
   ids: string[],
   fn: (id: string) => Promise<unknown>
 ): Promise<{ succeeded: number; failed: number }> {
   const unique = Array.from(new Set(ids.map((x) => String(x)).filter((x) => x.trim())));
   if (unique.length === 0) return { succeeded: 0, failed: 0 };
   const settled = await Promise.allSettled(unique.map((id) => fn(id)));
   let succeeded = 0;
   let failed = 0;
   for (const r of settled) {
     if (r.status === "fulfilled") succeeded++;
     else failed++;
   }
   return { succeeded, failed };
 }

 export function bulkSubmitForApproval(tenantId: string, storeId: string, ids: string[], token?: string) {
   return fanOut(ids, (id) => submitProductForApproval(tenantId, storeId, id, token));
 }
 export function bulkApprove(tenantId: string, storeId: string, ids: string[], token?: string) {
   return fanOut(ids, (id) => approveProduct(tenantId, storeId, id, token));
 }
 export function bulkReject(tenantId: string, storeId: string, ids: string[], comment: string, token?: string) {
   return fanOut(ids, (id) => rejectProduct(tenantId, storeId, id, comment, token));
 }
 /** Restore = un-hide (hidden → active). */
 export function bulkRestore(tenantId: string, storeId: string, ids: string[], token?: string) {
   return fanOut(ids, (id) => unhideProduct(tenantId, storeId, id, token));
 }
 export function bulkChangeCategory(
   tenantId: string,
   storeId: string,
   ids: string[],
   categoryId: number,
   token?: string
 ) {
   return fanOut(ids, (id) => changeProductCategory(tenantId, storeId, id, categoryId, token));
 }
 export function bulkSubmitForArchive(tenantId: string, storeId: string, ids: string[], token?: string) {
   return fanOut(ids, (id) => submitProductForArchive(tenantId, storeId, id, token));
 }

 // ---------------------------------------------------------------------------
 // Variants (SKU + price). The catalog API requires a 64-char hex SHA-256
 // combination hash per variant; for manually-entered SKUs we derive a stable
 // hash from the SKU so each SKU row is unique within the product.
 // ---------------------------------------------------------------------------

 export type ProductVariantDto = {
   id: string;
   sku: string;
   combinationHash: string;
   combinationCanonical: string;
   status: string;
   sortOrder: number;
   basePriceAmount: number;
 };

 /** SHA-256 hex of the input (browser SubtleCrypto, with a deterministic fallback). */
 async function sha256Hex(input: string): Promise<string> {
   try {
     if (typeof crypto !== "undefined" && crypto.subtle) {
       const bytes = new TextEncoder().encode(input);
       const digest = await crypto.subtle.digest("SHA-256", bytes);
       return Array.from(new Uint8Array(digest))
         .map((b) => b.toString(16).padStart(2, "0"))
         .join("");
     }
   } catch {
     // fall through
   }
   // Deterministic non-crypto fallback (only used when SubtleCrypto is unavailable).
   let h = 0;
   for (let i = 0; i < input.length; i++) h = (Math.imul(31, h) + input.charCodeAt(i)) | 0;
   const seed = (h >>> 0).toString(16);
   return (seed + "0".repeat(64)).slice(0, 64);
 }

 export async function listVariants(
   tenantId: string,
   storeId: string,
   productId: string,
   token?: string
 ): Promise<ProductVariantDto[]> {
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
       productId
     )}/variants`,
     { credentials: "same-origin", headers: headers(token) }
   );
   await checkOk(res);
   const body: ApiEnvelope<{ items: ProductVariantDto[] }> = await res.json();
   return body.data?.items ?? [];
 }

 export async function createManualVariant(
   tenantId: string,
   storeId: string,
   productId: string,
   input: { sku: string; basePriceAmount: number; status?: string },
   token?: string
 ): Promise<ProductVariantDto> {
   const sku = input.sku.trim();
   const canonical = `sku=${sku}`;
   const combinationHash = await sha256Hex(`${productId}|${canonical}`);
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
       productId
     )}/variants`,
     {
       method: "POST",
       credentials: "same-origin",
       headers: writeHeaders(token),
       body: JSON.stringify({
         sku,
         combinationHash,
         combinationCanonical: canonical,
         basePriceAmount: Math.max(0, Math.round(input.basePriceAmount)),
         status: input.status ?? "active",
       }),
     }
   );
   await checkOk(res);
   const body: ApiEnvelope<ProductVariantDto> = await res.json();
   return body.data;
 }

 export async function updateVariant(
   tenantId: string,
   storeId: string,
   productId: string,
   variantId: string,
   input: { sku?: string; status?: string; basePriceAmount?: number; sortOrder?: number },
   token?: string
 ): Promise<{ id: string; productId: string }> {
   const payload: Record<string, unknown> = {};
   if (input.sku !== undefined) payload.sku = input.sku.trim();
   if (input.status !== undefined) payload.status = input.status;
   if (input.sortOrder !== undefined) payload.sortOrder = input.sortOrder;
   if (input.basePriceAmount !== undefined) payload.basePriceAmount = Math.max(0, Math.round(input.basePriceAmount));
   const res = await fetch(
     `${BASE}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(
       productId
     )}/variants/${encodeURIComponent(variantId)}`,
     {
       method: "PUT",
       credentials: "same-origin",
       headers: writeHeaders(token),
       body: JSON.stringify(payload),
     }
   );
   await checkOk(res);
   const body: ApiEnvelope<{ id: string; productId: string }> = await res.json();
   return body.data;
 }
 
