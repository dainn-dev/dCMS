/**
 * DAI-665: Promo codes API client (Promotions service via gateway).
 * Base: /gateway/v1/promotions/tenants/{tenantId}/promo-codes
 */

import type { PromoListRow } from "../promotions-columns";
import { GATEWAY } from "./gatewayConfig";

const BASE = GATEWAY.promotions;

type ApiEnvelope<TData, TMeta = unknown> = {
  data: TData;
  meta: TMeta | null;
  error: { code?: string; message?: string } | null;
};

type ListMeta = { total: number; page: number; pageSize: number };

export type PromoCodeDto = {
  id: string;
  tenantId: string;
  code: string;
  nameJson: string;
  discountType: string;
  discountValue: string;
  workflowState: string;
  createdAt: string;
  updatedAt: string;
  promoTypeLabel: string;
  minSpend: string;
  startDate: string | null;
  endDate: string | null;
  submittedBy?: string | null;
  submittedDate?: string | null;
};

export type PromoCodePayload = {
  code: string;
  nameJson?: string;
  discountType?: string;
  discountValue?: string | null;
  promoTypeLabel?: string | null;
  minSpend?: string;
  startDate?: string | null;
  endDate?: string | null;
};

function headers(token?: string): Record<string, string> {
  const h: Record<string, string> = {
    Accept: "application/json",
    "Content-Type": "application/json",
  };
  if (token) h.Authorization = `Bearer ${token}`;
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

function formatScheduleDate(iso: string | null): string {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString("en-US", { month: "short", day: "2-digit", year: "numeric" });
  } catch {
    return iso;
  }
}

function discountKindLabel(discountType: string): string {
  const t = discountType.toLowerCase();
  if (t === "percentage") return "Percentage";
  if (t === "fixed") return "Fixed";
  if (t === "free_shipping") return "Shipping";
  return discountType;
}

function listStatus(workflowState: string): PromoListRow["status"] {
  const s = workflowState.toLowerCase();
  if (s === "approved") return "approved";
  if (s === "pending_approval" || s === "draft") return "pending";
  return "expired";
}

function listActiveDot(dto: PromoCodeDto, now: Date): PromoListRow["activeDot"] {
  const s = dto.workflowState.toLowerCase();
  if (s === "archived" || s === "rejected") return "off";
  if (s === "pending_approval" || s === "draft") return "warning";

  if (s !== "approved") return "off";

  const start = dto.startDate ? new Date(dto.startDate) : null;
  const end = dto.endDate ? new Date(dto.endDate) : null;
  if (start && !Number.isNaN(start.getTime()) && now < start) return "warning";
  if (end && !Number.isNaN(end.getTime()) && now > end) return "off";
  return "live";
}

/** Maps Promotions API DTO → table row for Promotions UI. */
export function promoCodeDtoToListRow(dto: PromoCodeDto, now: Date = new Date()): PromoListRow {
  const label = (dto.promoTypeLabel ?? "").trim();
  const discount = discountKindLabel(dto.discountType);
  const value = (dto.discountValue ?? "").trim() || "—";
  const minSpend = (dto.minSpend ?? "").trim() || "—";
  const scheduleStart = formatScheduleDate(dto.startDate);
  const scheduleEnd = dto.endDate?.trim()
    ? `to ${formatScheduleDate(dto.endDate)}`
    : "Permanent";

  return {
    id: dto.id,
    promoType: label || dto.discountType,
    discount,
    value,
    minSpend,
    code: dto.code,
    scheduleStart,
    scheduleEnd,
    activeDot: listActiveDot(dto, now),
    status: listStatus(dto.workflowState),
    usedPct: 0,
  };
}

function writeBody(payload: PromoCodePayload): Record<string, unknown> {
  return {
    code: payload.code,
    nameJson: payload.nameJson ?? "{}",
    discountType: payload.discountType ?? "percentage",
    discountValue: payload.discountValue ?? null,
    promoTypeLabel: payload.promoTypeLabel ?? null,
    minSpend: payload.minSpend ?? "",
    startDate: payload.startDate ?? null,
    endDate: payload.endDate ?? null,
  };
}

function mergePayload(current: PromoCodeDto, patch: Partial<PromoCodePayload>): PromoCodePayload {
  return {
    code: patch.code ?? current.code,
    nameJson: patch.nameJson ?? current.nameJson,
    discountType: patch.discountType ?? current.discountType,
    discountValue: patch.discountValue !== undefined ? patch.discountValue : current.discountValue,
    promoTypeLabel: patch.promoTypeLabel !== undefined ? patch.promoTypeLabel : current.promoTypeLabel,
    minSpend: patch.minSpend !== undefined ? patch.minSpend : current.minSpend,
    startDate: patch.startDate !== undefined ? patch.startDate : current.startDate,
    endDate: patch.endDate !== undefined ? patch.endDate : current.endDate,
  };
}

// ── List / read ───────────────────────────────────────────────────────────────

export async function fetchPromoCodes(
  tenantId: string,
  opts?: { status?: string; page?: number; pageSize?: number },
  token?: string
): Promise<{ rows: PromoListRow[]; total: number; page: number; pageSize: number }> {
  const params = new URLSearchParams();
  if (opts?.status) params.set("status", opts.status);
  params.set("page", String(Math.max(1, opts?.page ?? 1)));
  const pageSize = Math.min(Math.max(opts?.pageSize ?? 50, 1), 200);
  params.set("pageSize", String(pageSize));

  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/promo-codes?${params}`,
    { credentials: "same-origin", headers: headers(token) }
  );
  await checkOk(res);
  const body: ApiEnvelope<PromoCodeDto[], ListMeta> = await res.json();
  const meta = body.meta;
  const total = meta?.total ?? 0;
  const page = meta?.page ?? opts?.page ?? 1;
  const ps = meta?.pageSize ?? pageSize;
  return {
    rows: (body.data ?? []).map((d) => promoCodeDtoToListRow(d)),
    total,
    page,
    pageSize: ps,
  };
}

/** Pages through list until all rows are fetched or `limit` (default 1000) is reached. */
export async function fetchAllPromoCodesForExport(
  tenantId: string,
  opts?: { status?: string },
  token?: string,
  options?: { limit?: number; pageSize?: number }
): Promise<{ rows: PromoListRow[]; total: number; limited: boolean }> {
  const limit = options?.limit ?? 1000;
  const pageSize = Math.min(Math.max(options?.pageSize ?? 200, 1), 200);
  let page = 1;
  let total = 0;
  const out: PromoListRow[] = [];

  for (;;) {
    const batch = await fetchPromoCodes(tenantId, { ...opts, page, pageSize }, token);
    if (page === 1) total = batch.total;

    if (!batch.rows.length) break;
    for (const r of batch.rows) {
      out.push(r);
      if (out.length >= limit) return { rows: out, total, limited: true };
    }

    if (out.length >= batch.total) break;
    page++;
    if (page > 500) return { rows: out, total, limited: true };
  }

  return { rows: out, total, limited: false };
}

export async function getPromoCode(tenantId: string, id: string, token?: string): Promise<PromoCodeDto> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/promo-codes/${encodeURIComponent(id)}`,
    { credentials: "same-origin", headers: headers(token) }
  );
  await checkOk(res);
  const body: ApiEnvelope<PromoCodeDto> = await res.json();
  return body.data;
}

// ── Write ─────────────────────────────────────────────────────────────────────

export async function createPromoCode(
  tenantId: string,
  payload: PromoCodePayload,
  token?: string
): Promise<PromoListRow> {
  const res = await fetch(`${BASE}/tenants/${encodeURIComponent(tenantId)}/promo-codes`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(writeBody(payload)),
  });
  await checkOk(res);
  const body: ApiEnvelope<PromoCodeDto> = await res.json();
  return promoCodeDtoToListRow(body.data);
}

export async function updatePromoCode(
  tenantId: string,
  id: string,
  patch: Partial<PromoCodePayload>,
  token?: string
): Promise<PromoListRow> {
  const current = await getPromoCode(tenantId, id, token);
  const merged = mergePayload(current, patch);
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/promo-codes/${encodeURIComponent(id)}`,
    {
      method: "PUT",
      credentials: "same-origin",
      headers: headers(token),
      body: JSON.stringify(writeBody(merged)),
    }
  );
  await checkOk(res);
  const body: ApiEnvelope<PromoCodeDto> = await res.json();
  return promoCodeDtoToListRow(body.data);
}

async function postWorkflow(
  tenantId: string,
  id: string,
  action: string,
  token?: string,
  comment?: string
): Promise<PromoListRow> {
  const res = await fetch(
    `${BASE}/tenants/${encodeURIComponent(tenantId)}/promo-codes/${encodeURIComponent(id)}/${action}`,
    {
      method: "POST",
      credentials: "same-origin",
      headers: headers(token),
      body: JSON.stringify({ comment: comment ?? "" }),
    }
  );
  await checkOk(res);
  const body: ApiEnvelope<PromoCodeDto> = await res.json();
  return promoCodeDtoToListRow(body.data);
}

export function submitPromoCode(
  tenantId: string,
  id: string,
  token?: string,
  comment?: string
): Promise<PromoListRow> {
  return postWorkflow(tenantId, id, "submit", token, comment);
}

export function archivePromoCode(
  tenantId: string,
  id: string,
  token?: string,
  comment?: string
): Promise<PromoListRow> {
  return postWorkflow(tenantId, id, "archive", token, comment);
}
