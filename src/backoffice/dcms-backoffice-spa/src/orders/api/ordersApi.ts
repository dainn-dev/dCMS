/**
 * DAI-625: Orders API client (cursor pagination + state actions).
 * Routes through dCMS.Gateway (YARP) — /gateway/v1/orders/... → order-api.
 *
 * NOTE: Order API uses header-based tenant/store: X-Tenant-Id, X-Store-Id.
 * Response shape is not the standard { data, meta, error } envelope; it returns either:
 * - { items, nextCursor } for list
 * - full order detail JSON for get
 * - { error: { code, message } } on failure
 */

import type { Order } from "../types";
import { GATEWAY } from "../../estore/api/gatewayConfig";

const BASE = GATEWAY.orders;

export type OrderFilters = {
  customerId?: string;
  status?: string; // backend enum string
};

export function orderHeaders(tenantId: string, storeId: string, token?: string): HeadersInit {
  const h: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "application/json",
    "X-Tenant-Id": tenantId,
    "X-Store-Id": storeId,
  };
  if (token) h.Authorization = `Bearer ${token}`;
  return h;
}

function fmtMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency,
      maximumFractionDigits: currency === "JPY" ? 0 : 2,
    }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}

function fmtDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "2-digit" });
}

export function mapApiStatusToUiLabel(status: string): Order["status"] {
  switch ((status ?? "").trim()) {
    case "Created":
      return "Open Order";
    case "Processing":
      return "Processing";
    case "ReadyForDelivery":
      return "Ready for Delivery";
    case "ReadyForPickup":
      return "Ready for Pickup";
    case "Shipped":
      return "Out for Delivery";
    case "Delivered":
      return "Delivered";
    case "Returned":
      return "Returned";
    case "AdminCancelled":
      return "Admin Cancelled";
    case "PendingCancellation":
      return "Pending Cancellation";
    case "UserCancelled":
      return "User Cancelled";
    case "PartiallyFulfilled":
      return "Partially Fulfilled";
    // DAI-637 failure states (snake_case from backend ToApiStatus)
    case "payment_failed":
    case "PaymentFailed":
      return "Payment Failed";
    case "auth_failed":
    case "AuthFailed":
      return "Auth Failed";
    case "address_error":
    case "AddressError":
      return "Address Error";
    case "stock_error":
    case "StockError":
      return "Stock Error";
    case "system_error":
    case "SystemError":
      return "System Error";
    default:
      return "Processing";
  }
}

export function mapUiStatusToApiFilter(label: string): string {
  switch (label) {
    case "Open Order":
      return "Created";
    case "Out for Delivery":
      return "Shipped";
    case "Ready for Delivery":
      return "ReadyForDelivery";
    case "Ready for Pickup":
      return "ReadyForPickup";
    case "Admin Cancelled":
      return "AdminCancelled";
    case "Pending Cancellation":
      return "PendingCancellation";
    case "User Cancelled":
      return "UserCancelled";
    case "Partially Fulfilled":
      return "PartiallyFulfilled";
    // DAI-637 failure states → backend snake_case (TryMapApiStatusToDb)
    case "Payment Failed":
      return "payment_failed";
    case "Auth Failed":
      return "auth_failed";
    case "Address Error":
      return "address_error";
    case "Stock Error":
      return "stock_error";
    case "System Error":
      return "system_error";
    default:
      return label.replace(/\s+/g, "");
  }
}

// ── DAI-637 Failure helpers ────────────────────────────────────────────────

export const FAILURE_STATUSES = [
  "Payment Failed",
  "Auth Failed",
  "Address Error",
  "Stock Error",
  "System Error",
] as const;

export type FailureStatus = (typeof FAILURE_STATUSES)[number];

export function isFailureStatus(s: string): s is FailureStatus {
  return (FAILURE_STATUSES as readonly string[]).includes(s);
}

export type OrderListItemDto = {
  orderId: string;
  customerId: string;
  status: string;
  totalAmount: number;
  currency: string;
  createdAt: string;
  lineCount: number;
};

export function orderListItemFromDto(dto: OrderListItemDto): Order {
  return {
    doNumber: "—",
    orderId: String(dto.orderId ?? ""),
    orderDate: fmtDate(String(dto.createdAt ?? "")),
    type: "Online",
    customerName: "",
    customerEmail: "",
    customerContactNo: null,
    status: mapApiStatusToUiLabel(String(dto.status ?? "")),
    fulfilledDate: null,
    deliveryDate: null,
    deliveryOption: null,
    shippingStatus: null,
    processingOfficer: null,
    tags: null,
    paymentGateway: null,
    store: "",
    storeAutoId: null,
    distributionCentre: undefined,
    paymentMethod: undefined,
    paymentDate: undefined,
    shippingAddressLine1: null,
    shippingAddressLine2: null,
    shippingPostalCode: null,
    shippingCountry: null,
    billingAddressLine1: null,
    billingAddressLine2: null,
    billingPostalCode: null,
    billingCountry: null,
  };
}

export type OrderDetailDto = {
  orderId: string;
  tenantId: string;
  storeId: string;
  customerId: string;
  status: string;
  total: { amount: number; currency: string };
  paymentIntentId: string | null;
  createdAt: string;
  // DAI-640 failure context (null when order has never been in a failure state)
  failureReason: string | null;
  failureErrorCode: string | null;
  failedAt: string | null;
  retryCount: number;
  shippingAddress: {
    line1: string | null;
    line2: string | null;
    city: string | null;
    region: string | null;
    postalCode: string | null;
    countryCode: string | null;
  } | null;
  shipment: {
    status: string | null;
    carrier: string | null;
    trackingNumber: string | null;
  } | null;
};

export function orderDetailFromDto(dto: OrderDetailDto): Order {
  const ship = dto.shippingAddress;
  const total = dto.total?.amount ?? 0;
  const curr = dto.total?.currency ?? "USD";
  const _ = fmtMoney(total, curr);
  void _;
  return {
    doNumber: "—",
    orderId: String(dto.orderId ?? ""),
    orderDate: fmtDate(String(dto.createdAt ?? "")),
    type: "Online",
    customerName: "",
    customerEmail: "",
    customerContactNo: null,
    status: mapApiStatusToUiLabel(String(dto.status ?? "")),
    fulfilledDate: null,
    deliveryDate: null,
    deliveryOption: null,
    shippingStatus: null,
    processingOfficer: null,
    tags: null,
    paymentGateway: null,
    store: "",
    storeAutoId: null,
    shippingAddressLine1: ship?.line1 ?? null,
    shippingAddressLine2: ship?.line2 ?? null,
    shippingPostalCode: ship?.postalCode ?? null,
    shippingCountry: ship?.countryCode ?? null,
    billingAddressLine1: ship?.line1 ?? null,
    billingAddressLine2: ship?.line2 ?? null,
    billingPostalCode: ship?.postalCode ?? null,
    billingCountry: ship?.countryCode ?? null,
  };
}

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

export async function fetchOrders(
  tenantId: string,
  storeId: string,
  filters: OrderFilters | undefined,
  paging: { cursor?: string | null; limit?: number } | undefined,
  token?: string
): Promise<{ rows: Order[]; nextCursor: string | null }> {
  const params = new URLSearchParams();
  if (filters?.customerId) params.set("customerId", filters.customerId);
  if (filters?.status) params.set("status", filters.status);
  if (paging?.cursor) params.set("cursor", paging.cursor);
  if (paging?.limit) params.set("limit", String(paging.limit));
  const qs = params.toString() ? `?${params.toString()}` : "";

  const res = await fetch(`${BASE}/orders${qs}`, {
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
  });
  await throwIfApiError(res);
  const body = (await res.json()) as { items: OrderListItemDto[]; nextCursor: string | null };
  return {
    rows: (body.items ?? []).map(orderListItemFromDto),
    nextCursor: body.nextCursor ?? null,
  };
}

export async function fetchAllOrdersForExport(
  tenantId: string,
  storeId: string,
  filters: OrderFilters | undefined,
  token?: string,
  options?: { limit?: number; pageSize?: number }
): Promise<{ rows: Order[]; total: number; limited: boolean }> {
  const limit = options?.limit ?? 5000;
  const pageSize = Math.min(Math.max(options?.pageSize ?? 100, 1), 100);
  const out: Order[] = [];
  let cursor: string | null | undefined = null;

  for (;;) {
    const { rows, nextCursor } = await fetchOrders(tenantId, storeId, filters, { cursor, limit: pageSize }, token);
    for (const r of rows) {
      out.push(r);
      if (out.length >= limit) return { rows: out, total: out.length, limited: true };
    }
    if (!nextCursor || rows.length === 0) break;
    cursor = nextCursor;
  }

  return { rows: out, total: out.length, limited: false };
}

export async function getOrder(tenantId: string, storeId: string, orderId: string, token?: string): Promise<Order> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}`, {
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
  });
  await throwIfApiError(res);
  const dto = (await res.json()) as OrderDetailDto;
  return orderDetailFromDto(dto);
}

export async function getOrderDetail(
  tenantId: string,
  storeId: string,
  orderId: string,
  token?: string
): Promise<OrderDetailDto> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}`, {
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
  });
  await throwIfApiError(res);
  return (await res.json()) as OrderDetailDto;
}

export async function cancelOrder(
  tenantId: string,
  storeId: string,
  orderId: string,
  reason: string | null,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}/cancel`, {
    method: "POST",
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
    body: JSON.stringify(reason ? { reason } : {}),
  });
  await throwIfApiError(res);
}

export async function shipOrder(
  tenantId: string,
  storeId: string,
  orderId: string,
  payload: { carrier: string; trackingNumber: string },
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}/ship`, {
    method: "POST",
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
    body: JSON.stringify(payload),
  });
  await throwIfApiError(res);
}

export async function deliverOrder(tenantId: string, storeId: string, orderId: string, token?: string): Promise<void> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}/deliver`, {
    method: "POST",
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
  });
  await throwIfApiError(res);
}

/**
 * DAI-637 — Retry a failed order. Backend moves it from a failure status
 * back to `PaymentPending` so the lifecycle can restart. No body required.
 */
export async function retryFailedOrder(
  tenantId: string,
  storeId: string,
  orderId: string,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}/retry-failure`, {
    method: "POST",
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
  });
  await throwIfApiError(res);
}

/**
 * DAI-637 — Resolve a failed order manually (moved to `Cancelled` with note).
 */
export async function resolveFailedOrder(
  tenantId: string,
  storeId: string,
  orderId: string,
  note: string | null | undefined,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}/resolve-failure`, {
    method: "POST",
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
    body: JSON.stringify(note ? { note } : {}),
  });
  await throwIfApiError(res);
}

export async function getShipment(
  tenantId: string,
  storeId: string,
  orderId: string,
  token?: string
): Promise<{ status: string | null; carrier: string | null; trackingNumber: string | null } | null> {
  const res = await fetch(`${BASE}/orders/${encodeURIComponent(orderId)}/shipment`, {
    credentials: "same-origin",
    headers: orderHeaders(tenantId, storeId, token),
  });
  await throwIfApiError(res);
  const body = (await res.json()) as { shipment?: { status?: string | null; carrier?: string | null; trackingNumber?: string | null } };
  const s = body.shipment;
  if (!s) return null;
  return { status: s.status ?? null, carrier: s.carrier ?? null, trackingNumber: s.trackingNumber ?? null };
}

