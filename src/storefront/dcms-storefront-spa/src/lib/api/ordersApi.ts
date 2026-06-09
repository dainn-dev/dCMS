import { GATEWAY } from "./gateway";
import { callOrderJson, commerceHeaders, type CommerceScope } from "./commerceFetch";
import type { CartLine } from "../cart/cartStorage";

export interface CreateOrderPayload {
  customerId: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  shippingAddress: {
    line1: string;
    line2?: string;
    city: string;
    region?: string;
    postalCode: string;
    countryCode: string;
  };
  lines: CartLine[];
}

export interface CreateOrderResult {
  orderId: string;
  status: string;
  paymentUrl: string | null;
}

export interface OrderListItem {
  orderId: string;
  customerId: string;
  customerName?: string | null;
  status: string;
  totalAmount: number;
  currency: string;
  createdAt: string;
  lineCount: number;
}

export interface OrderDetail {
  orderId: string;
  tenantId: string;
  storeId: string;
  customerId: string;
  customerName?: string | null;
  customerEmail?: string | null;
  customerPhone?: string | null;
  status: string;
  total: { amount: number; currency: string };
  createdAt: string;
  shippingAddress?: {
    line1?: string;
    line2?: string;
    city?: string;
    region?: string;
    postalCode?: string;
    countryCode?: string;
  };
  lines?: Array<{
    lineId: string;
    productId: string;
    variantId: string;
    quantity: number;
    unitPrice?: { amount: number; currency: string };
    productNameSnapshot?: string;
  }>;
}

export interface OrderPaymentView {
  orderId: string;
  total: { amount: number; currency: string };
  status?: string;
  components: Array<{
    type: string;
    amount: number;
    externalRef?: string | null;
    state: string;
  }>;
}

function idempotencyKey(): string {
  try {
    if (typeof crypto !== "undefined" && crypto.randomUUID) return crypto.randomUUID();
  } catch {
    /* fall through */
  }
  return `idem-${Date.now()}`;
}

function toCreateBody(payload: CreateOrderPayload) {
  const currency = payload.lines[0]?.currency ?? "VND";
  return {
    customerId: payload.customerId,
    customerName: payload.customerName,
    customerEmail: payload.customerEmail,
    customerPhone: payload.customerPhone,
    shippingAddress: payload.shippingAddress,
    lines: payload.lines.map(l => ({
      productId: l.productId,
      variantId: l.variantId,
      warehouseId: l.warehouseId,
      quantity: l.quantity,
      unitPrice: { amount: l.unitPrice, currency },
      productNameSnapshot: l.name,
      variantSnapshot: { sku: l.sku },
    })),
  };
}

export async function createOrder(
  scope: CommerceScope,
  payload: CreateOrderPayload,
): Promise<CreateOrderResult> {
  return callOrderJson<CreateOrderResult>(`${GATEWAY.orders}/orders`, {
    method: "POST",
    headers: {
      ...commerceHeaders(scope),
      "Idempotency-Key": idempotencyKey(),
    } as Record<string, string>,
    body: JSON.stringify(toCreateBody(payload)),
  });
}

export async function listOrders(
  scope: CommerceScope,
  cursor?: string | null,
): Promise<{ items: OrderListItem[]; nextCursor: string | null }> {
  const qs = new URLSearchParams();
  if (cursor) qs.set("cursor", cursor);
  const suffix = qs.toString() ? `?${qs}` : "";
  return callOrderJson<{ items: OrderListItem[]; nextCursor: string | null }>(
    `${GATEWAY.orders}/orders${suffix}`,
    { headers: commerceHeaders(scope) },
  );
}

export async function getOrder(scope: CommerceScope, orderId: string): Promise<OrderDetail> {
  return callOrderJson<OrderDetail>(`${GATEWAY.orders}/orders/${encodeURIComponent(orderId)}`, {
    headers: commerceHeaders(scope),
  });
}

export async function getOrderPayment(
  scope: CommerceScope,
  orderId: string,
): Promise<OrderPaymentView> {
  return callOrderJson<OrderPaymentView>(
    `${GATEWAY.orders}/orders/${encodeURIComponent(orderId)}/payment`,
    { headers: commerceHeaders(scope) },
  );
}

export function stubPaymentUrl(orderId: string, externalRef?: string | null): string | null {
  if (!externalRef) return null;
  if (externalRef === `pi_stub_${orderId}` || externalRef.startsWith("pi_stub_"))
    return `https://checkout.local/pay/${orderId}`;
  return null;
}
