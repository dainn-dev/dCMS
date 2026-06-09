/**
 * DAI-660: Approval API client — products (catalog), campaigns + promo codes (promotions).
 * Routes through dCMS.Gateway (YARP).
 */

import { GATEWAY } from "../../estore/api/gatewayConfig";
import type { CampaignApprovalRow, CampaignApprovalStatus } from "../campaign-approval-columns";
import type { ProductApprovalRow, ProductApprovalStatus } from "../product-approval-columns";
import type { PromoCodeApprovalRow, PromoCodeApprovalStatus } from "../promo-code-approval-columns";

const CATALOG = GATEWAY.catalog;
const PROMOTIONS = GATEWAY.promotions;

type ApiEnvelope<TData, TMeta = unknown> = {
  data: TData;
  meta: TMeta | null;
  error: { code?: string; message?: string } | null;
};

export class DcmsApiError extends Error {
  readonly code?: string;
  readonly status: number;

  constructor(message: string, status: number, code?: string) {
    super(message);
    this.name = "DcmsApiError";
    this.status = status;
    this.code = code;
  }
}

function hdr(token?: string): Record<string, string> {
  const h: Record<string, string> = {
    Accept: "application/json",
    "Content-Type": "application/json",
  };
  if (token) h["Authorization"] = `Bearer ${token}`;
  return h;
}

/** A unique key for the `Idempotency-Key` header the Catalog/Promotions/Voucher write endpoints require. */
function newIdempotencyKey(): string {
  try {
    if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") return crypto.randomUUID();
  } catch {
    // fall through to the timestamp-based fallback
  }
  return `idem-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

/** `hdr` plus a fresh `Idempotency-Key` — required on every approve/reject (mutating) request. */
function writeHdr(token?: string): Record<string, string> {
  return { ...hdr(token), "Idempotency-Key": newIdempotencyKey() };
}

async function parseJsonEnvelope<TData, TMeta = unknown>(res: Response): Promise<ApiEnvelope<TData, TMeta>> {
  let body: ApiEnvelope<TData, TMeta>;
  try {
    body = (await res.json()) as ApiEnvelope<TData, TMeta>;
  } catch {
    throw new DcmsApiError(`HTTP ${res.status}: invalid JSON`, res.status);
  }
  if (!res.ok || body.error) {
    const msg = body.error?.message ?? `HTTP ${res.status}`;
    throw new DcmsApiError(msg, res.status, body.error?.code);
  }
  return body;
}

// ── Product (catalog) ─────────────────────────────────────────────────────────

type PendingProductItemDto = {
  id: string;
  name: string;
  brandName: string;
  categoryPath: string;
  submittedByUserId: string;
  submittedAt: string;
  status: string;
};

type PendingProductsData = {
  items: PendingProductItemDto[];
  nextCursor: string | null;
  total: number;
};

function mapProductStatus(s: string): ProductApprovalStatus {
  const x = (s ?? "").toLowerCase();
  if (x.includes("reject")) return "rejected";
  if (x.includes("pending")) return "pending";
  if (x.includes("approv") || x === "active") return "approved";
  return "pending";
}

function formatDateShort(iso: string | undefined): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString("en-CA");
  } catch {
    return iso;
  }
}

function productFromPendingDto(d: PendingProductItemDto): ProductApprovalRow {
  return {
    id: d.id,
    // Backend returns the localized Name JSON (e.g. {"en":"…","vi":"…"}); render a single locale.
    productName: parseNameJson(d.name ?? "") || (d.name ?? ""),
    brand: d.brandName ?? "",
    category: d.categoryPath ?? "",
    submittedBy: d.submittedByUserId ?? "—",
    submittedDate: formatDateShort(d.submittedAt),
    status: mapProductStatus(d.status),
  };
}

export async function fetchPendingProducts(
  tenantId: string,
  storeId: string,
  opts?: { cursor?: string | null; limit?: number },
  token?: string
): Promise<{ items: ProductApprovalRow[]; nextCursor: string | null; total: number }> {
  const params = new URLSearchParams();
  if (opts?.limit != null && opts.limit > 0) params.set("limit", String(Math.min(opts.limit, 100)));
  if (opts?.cursor) params.set("cursor", opts.cursor);
  const qs = params.toString() ? `?${params.toString()}` : "";

  const res = await fetch(
    `${CATALOG}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/pending-approvals${qs}`,
    { credentials: "same-origin", headers: hdr(token) }
  );
  const body = await parseJsonEnvelope<PendingProductsData>(res);
  const d = body.data;
  return {
    items: (d.items ?? []).map(productFromPendingDto),
    nextCursor: d.nextCursor ?? null,
    total: d.total ?? (d.items?.length ?? 0),
  };
}

export async function approveProduct(
  tenantId: string,
  storeId: string,
  productId: string,
  token?: string
): Promise<void> {
  const res = await fetch(
    `${CATALOG}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(productId)}/approve`,
    { method: "POST", credentials: "same-origin", headers: writeHdr(token), body: "{}" }
  );
  await parseJsonEnvelope<unknown>(res);
}

export async function rejectProduct(
  tenantId: string,
  storeId: string,
  productId: string,
  comment: string,
  token?: string
): Promise<void> {
  const res = await fetch(
    `${CATALOG}/tenants/${encodeURIComponent(tenantId)}/stores/${encodeURIComponent(storeId)}/products/${encodeURIComponent(productId)}/reject`,
    {
      method: "POST",
      credentials: "same-origin",
      headers: writeHdr(token),
      body: JSON.stringify({ comment }),
    }
  );
  await parseJsonEnvelope<unknown>(res);
}

// ── Campaign (promotions) ─────────────────────────────────────────────────────

type CampaignDto = {
  id: string;
  code: string;
  nameJson: string;
  editorKind: string;
  workflowState: string;
  channel: string;
  startDate: string | null;
  endDate: string | null;
  createdAt: string;
  updatedAt: string;
  // DAI-662: populated by backend LATERAL JOIN when status=pending_approval
  submittedByUserId?: string | null;
  submittedAt?: string | null;
};

function parseNameJson(nameJson: string): string {
  try {
    const o = JSON.parse(nameJson) as Record<string, string>;
    return o.vi ?? o.en ?? Object.values(o)[0] ?? "";
  } catch {
    return nameJson;
  }
}

function mapCampaignApprovalStatus(ws: string): CampaignApprovalStatus {
  const x = (ws ?? "").toLowerCase();
  if (x === "rejected") return "rejected";
  if (x === "approved" || x === "active") return "approved";
  return "pending";
}

function campaignToApprovalRow(d: CampaignDto): CampaignApprovalRow {
  const start = formatDateShort(d.startDate ?? undefined);
  const end = formatDateShort(d.endDate ?? undefined);
  const period = d.startDate || d.endDate ? `${start} – ${end}` : "—";
  return {
    id: d.id,
    campaignName: parseNameJson(d.nameJson) || d.code,
    campaignCode: d.code,
    campaignType: d.editorKind ?? "—",
    validPeriod: period,
    submittedBy: d.submittedByUserId ?? "—",
    submittedDate: formatDateShort(d.submittedAt ?? undefined) !== "—" ? formatDateShort(d.submittedAt ?? undefined) : formatDateShort(d.updatedAt),
    status: mapCampaignApprovalStatus(d.workflowState),
  };
}

export async function fetchPendingCampaigns(
  tenantId: string,
  paging?: { page?: number; pageSize?: number },
  token?: string
): Promise<{ items: CampaignApprovalRow[]; total: number }> {
  const page = Math.max(1, paging?.page ?? 1);
  const pageSize = Math.min(Math.max(paging?.pageSize ?? 50, 1), 200);
  const params = new URLSearchParams({
    status: "pending_approval",
    page: String(page),
    pageSize: String(pageSize),
  });

  const res = await fetch(
    `${PROMOTIONS}/tenants/${encodeURIComponent(tenantId)}/campaigns?${params.toString()}`,
    { credentials: "same-origin", headers: hdr(token) }
  );
  const body = await parseJsonEnvelope<CampaignDto[], { total: number; page: number; pageSize: number }>(res);
  const total = body.meta?.total ?? (body.data?.length ?? 0);
  return { items: (body.data ?? []).map(campaignToApprovalRow), total };
}

export async function approveCampaign(tenantId: string, campaignId: string, token?: string): Promise<void> {
  const res = await fetch(
    `${PROMOTIONS}/tenants/${encodeURIComponent(tenantId)}/campaigns/${encodeURIComponent(campaignId)}/approve`,
    { method: "POST", credentials: "same-origin", headers: writeHdr(token), body: "{}" }
  );
  await parseJsonEnvelope<unknown>(res);
}

export async function rejectCampaign(
  tenantId: string,
  campaignId: string,
  comment: string,
  token?: string
): Promise<void> {
  const res = await fetch(
    `${PROMOTIONS}/tenants/${encodeURIComponent(tenantId)}/campaigns/${encodeURIComponent(campaignId)}/reject`,
    {
      method: "POST",
      credentials: "same-origin",
      headers: writeHdr(token),
      body: JSON.stringify({ comment }),
    }
  );
  await parseJsonEnvelope<unknown>(res);
}

// ── Promo code (promotions) ───────────────────────────────────────────────────

type PromoCodeDto = {
  id: string;
  code: string;
  nameJson: string;
  discountType: string;
  discountValue: string;
  workflowState: string;
  submittedBy?: string;
  submittedDate?: string | null;
  createdAt: string;
  updatedAt: string;
};

function mapPromoApprovalStatus(ws: string): PromoCodeApprovalStatus {
  const x = (ws ?? "").toLowerCase();
  if (x === "rejected") return "rejected";
  if (x === "approved") return "approved";
  return "pending";
}

function promoToApprovalRow(d: PromoCodeDto): PromoCodeApprovalRow {
  return {
    id: d.id,
    promoCode: d.code,
    promoName: parseNameJson(d.nameJson) || d.code,
    discountType: d.discountType ?? "—",
    discountValue: d.discountValue ?? "—",
    submittedBy: d.submittedBy ?? "—",
    submittedDate: d.submittedDate ? formatDateShort(d.submittedDate) : formatDateShort(d.updatedAt),
    status: mapPromoApprovalStatus(d.workflowState),
  };
}

export async function fetchPendingPromoCodes(
  tenantId: string,
  paging?: { page?: number; pageSize?: number },
  token?: string
): Promise<{ items: PromoCodeApprovalRow[]; total: number }> {
  const page = Math.max(1, paging?.page ?? 1);
  const pageSize = Math.min(Math.max(paging?.pageSize ?? 50, 1), 200);
  const params = new URLSearchParams({
    status: "pending_approval",
    page: String(page),
    pageSize: String(pageSize),
  });

  const res = await fetch(
    `${PROMOTIONS}/tenants/${encodeURIComponent(tenantId)}/promo-codes?${params.toString()}`,
    { credentials: "same-origin", headers: hdr(token) }
  );
  const body = await parseJsonEnvelope<PromoCodeDto[], { total: number; page: number; pageSize: number }>(res);
  const total = body.meta?.total ?? (body.data?.length ?? 0);
  return { items: (body.data ?? []).map(promoToApprovalRow), total };
}

// ── Load-all helpers ──────────────────────────────────────────────────────────
// Page through the FULL pending set (cursor or page based) so large approval
// queues aren't silently truncated at the first batch. The UI then paginates the
// accumulated rows client-side. Safety-capped to avoid runaway loops.

export async function fetchAllPendingProducts(
  tenantId: string,
  storeId: string,
  token?: string
): Promise<{ items: ProductApprovalRow[]; total: number }> {
  const all: ProductApprovalRow[] = [];
  let cursor: string | null | undefined;
  let total = 0;
  for (let i = 0; i < 500; i++) {
    const res = await fetchPendingProducts(tenantId, storeId, { cursor, limit: 100 }, token);
    all.push(...res.items);
    total = res.total || all.length;
    if (!res.nextCursor || res.items.length === 0) break;
    cursor = res.nextCursor;
  }
  return { items: all, total };
}

export async function fetchAllPendingCampaigns(
  tenantId: string,
  token?: string
): Promise<{ items: CampaignApprovalRow[]; total: number }> {
  const all: CampaignApprovalRow[] = [];
  const pageSize = 200;
  let total = 0;
  for (let page = 1; page <= 500; page++) {
    const res = await fetchPendingCampaigns(tenantId, { page, pageSize }, token);
    all.push(...res.items);
    total = res.total || all.length;
    if (res.items.length < pageSize || all.length >= total) break;
  }
  return { items: all, total };
}

export async function fetchAllPendingPromoCodes(
  tenantId: string,
  token?: string
): Promise<{ items: PromoCodeApprovalRow[]; total: number }> {
  const all: PromoCodeApprovalRow[] = [];
  const pageSize = 200;
  let total = 0;
  for (let page = 1; page <= 500; page++) {
    const res = await fetchPendingPromoCodes(tenantId, { page, pageSize }, token);
    all.push(...res.items);
    total = res.total || all.length;
    if (res.items.length < pageSize || all.length >= total) break;
  }
  return { items: all, total };
}

export async function approvePromoCode(tenantId: string, promoCodeId: string, token?: string): Promise<void> {
  const res = await fetch(
    `${PROMOTIONS}/tenants/${encodeURIComponent(tenantId)}/promo-codes/${encodeURIComponent(promoCodeId)}/approve`,
    { method: "POST", credentials: "same-origin", headers: writeHdr(token), body: "{}" }
  );
  await parseJsonEnvelope<unknown>(res);
}

export async function rejectPromoCode(
  tenantId: string,
  promoCodeId: string,
  comment: string,
  token?: string
): Promise<void> {
  const res = await fetch(
    `${PROMOTIONS}/tenants/${encodeURIComponent(tenantId)}/promo-codes/${encodeURIComponent(promoCodeId)}/reject`,
    {
      method: "POST",
      credentials: "same-origin",
      headers: writeHdr(token),
      body: JSON.stringify({ comment }),
    }
  );
  await parseJsonEnvelope<unknown>(res);
}
