/**
 * DAI-656: Refund cases API client.
 * Routes through dCMS.Gateway — /gateway/v1/orders/... → order-api.
 *
 * Order API uses X-Tenant-Id / X-Store-Id headers; list responses use { data: { items, nextCursor }, error }.
 */

import { GATEWAY } from "../../estore/api/gatewayConfig";
import type { RefundCase, RefundStatus } from "../types";
import { orderHeaders } from "./ordersApi";

const BASE = GATEWAY.orders;

async function throwIfApiError(res: Response): Promise<void> {
  if (res.ok) return;
  let msg = `HTTP ${res.status}`;
  try {
    const body = (await res.json()) as { error?: { message?: string } };
    if (body?.error?.message) msg = body.error.message;
  } catch {
    // ignore
  }
  throw new Error(msg);
}

type RefundCaseListItemDto = {
  refundNo: string;
  referenceHash: string;
  orderId: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  paymentReferenceNo: string;
  requestDate: string;
  refundDate: string | null;
  status: string;
  remark: string;
};

function refundCaseFromDto(dto: RefundCaseListItemDto): RefundCase {
  return {
    refundNo: String(dto.refundNo ?? ""),
    referenceHash: String(dto.referenceHash ?? ""),
    returnNo: "—",
    doNumber: "—",
    orderId: String(dto.orderId ?? ""),
    customerName: String(dto.customerName ?? ""),
    customerPhone: String(dto.customerPhone ?? ""),
    customerEmail: String(dto.customerEmail ?? ""),
    amount: Number(dto.amount ?? 0),
    currency: String(dto.currency ?? "USD"),
    paymentMethod: String(dto.paymentMethod ?? ""),
    paymentDetail: "",
    paymentReferenceNo: String(dto.paymentReferenceNo ?? ""),
    requestDate: String(dto.requestDate ?? ""),
    refundDate: dto.refundDate ? String(dto.refundDate) : null,
    status: (dto.status ?? "Pending Refund") as RefundStatus,
    remark: String(dto.remark ?? ""),
    paymentGatewayMessage: "",
    isPaymentGatewayCase: true,
    doCancelledItems: [],
    refundBreakdown: [],
    changeHistory: [],
  };
}

export async function fetchRefundCases(
  tenantId: string,
  storeId: string,
  opts?: { status?: string; cursor?: string; limit?: number },
  token?: string
): Promise<{ cases: RefundCase[]; nextCursor: string | null }> {
  const params = new URLSearchParams();
  if (opts?.status) params.set("status", opts.status);
  if (opts?.cursor) params.set("cursor", opts.cursor);
  if (opts?.limit) params.set("limit", String(opts.limit));
  const qs = params.toString() ? `?${params.toString()}` : "";

  const res = await fetch(`${BASE}/refund-cases${qs}`, {
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
  });
  await throwIfApiError(res);
  const envelope = (await res.json()) as {
    data?: { items?: RefundCaseListItemDto[]; nextCursor?: string | null };
  };
  const data = envelope.data ?? {};
  return {
    cases: (data.items ?? []).map(refundCaseFromDto),
    nextCursor: data.nextCursor ?? null,
  };
}

/** Cursor walk for CSV export (optional cap). */
export async function fetchAllRefundCasesForExport(
  tenantId: string,
  storeId: string,
  token?: string,
  options?: { limit?: number; pageSize?: number; status?: string }
): Promise<{ cases: RefundCase[]; limited: boolean }> {
  const limit = options?.limit ?? 5000;
  const pageSize = Math.min(Math.max(options?.pageSize ?? 100, 1), 100);
  const out: RefundCase[] = [];
  let cursor: string | undefined = undefined;

  for (;;) {
    const { cases: page, nextCursor } = await fetchRefundCases(
      tenantId,
      storeId,
      { status: options?.status, cursor, limit: pageSize },
      token
    );
    for (const r of page) {
      out.push(r);
      if (out.length >= limit) return { cases: out, limited: true };
    }
    if (!nextCursor || page.length === 0) break;
    cursor = nextCursor;
  }

  return { cases: out, limited: false };
}

export async function updateRefundCaseStatus(
  tenantId: string,
  storeId: string,
  orderId: string,
  payload: { status: RefundStatus; remark: string },
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}/refund-case`, {
    method: "PUT",
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
    body: JSON.stringify({ status: payload.status, remark: payload.remark }),
  });
  await throwIfApiError(res);
}
