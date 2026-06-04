import { GATEWAY } from "../../estore/api/gatewayConfig";

const ORDERS_BASE = GATEWAY.orders;
const REPORTS_BASE = GATEWAY.reports;

type ApiEnvelope<TData, TMeta = unknown> = {
  data: TData;
  meta: TMeta | null;
  error: { code?: string; message?: string } | null;
};

function headers(tenantId: string, storeId?: string, token?: string): Record<string, string> {
  const h: Record<string, string> = {
    Accept: "application/json",
    "X-Tenant-Id": tenantId,
  };
  if (storeId) h["X-Store-Id"] = storeId;
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
      // ignore parse error
    }
    throw new Error(errMsg);
  }
}

export type ReportFilters = {
  dateFrom: string;
  dateTo: string;
  storeId?: string;
  brandCode?: string;
  categoryId?: string;
  memberQuery?: string;
  paymentMethod?: string;
  slotId?: string;
  receiptNumber?: string;
  source?: string;
  orderPromoCode?: string;
  itemPromoCode?: string;
  rebatesCode?: string;
  paymentType?: string;
  billingCountry?: string;
  membershipType?: string;
  membershipTier?: string;
};

function dateParams(filters: ReportFilters): URLSearchParams {
  const p = new URLSearchParams();
  p.set("dateFrom", filters.dateFrom);
  p.set("dateTo", filters.dateTo);
  if (filters.storeId && filters.storeId !== "all") p.set("storeId", filters.storeId);
  return p;
}

// --- Transaction reports ---

export type TransactionSummaryRow = {
  paymentMethod: string;
  transactionCount: number;
  totalAmount: number;
  currency: string;
};

export async function fetchTransactionSummary(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
): Promise<TransactionSummaryRow[]> {
  const params = dateParams(filters);
  const res = await fetch(`${ORDERS_BASE}/orders/reports/transactions/summary?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<TransactionSummaryRow[]> = await res.json();
  return body.data ?? [];
}

export type TransactionDetailRow = {
  orderId: string;
  receiptNumber: string | null;
  date: string;
  member: string;
  customerName: string | null;
  customerEmail: string | null;
  store: string;
  status: string;
  amount: number;
  taxAmount: number;
  orderDiscount: number;
  currency: string;
  orderPromoCode: string | null;
  billingCountry: string | null;
  campaigns: string[];
  itemPromoCodes: string[];
  paymentType: string;
};

export async function fetchTransactionDetails(
  tenantId: string,
  filters: ReportFilters,
  pagination?: { cursor?: string; limit?: number },
  token?: string,
): Promise<{ rows: TransactionDetailRow[]; nextCursor: string | null }> {
  const params = dateParams(filters);
  if (filters.memberQuery) params.set("memberQuery", filters.memberQuery);
  if (filters.brandCode && filters.brandCode !== "all") params.set("brandCode", filters.brandCode);
  if (filters.paymentMethod && filters.paymentMethod !== "all") params.set("paymentMethod", filters.paymentMethod);
  if (filters.orderPromoCode) params.set("orderPromoCode", filters.orderPromoCode);
  if (filters.itemPromoCode) params.set("itemPromoCode", filters.itemPromoCode);
  if (filters.paymentType && filters.paymentType !== "all") params.set("paymentType", filters.paymentType);
  if (filters.billingCountry && filters.billingCountry !== "all") params.set("billingCountry", filters.billingCountry);
  if (pagination?.cursor) params.set("cursor", pagination.cursor);
  if (pagination?.limit) params.set("limit", String(pagination.limit));
  const res = await fetch(`${ORDERS_BASE}/orders/reports/transactions/details?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<TransactionDetailRow[], { nextCursor: string | null }> = await res.json();
  return { rows: body.data ?? [], nextCursor: body.meta?.nextCursor ?? null };
}

/**
 * Pages through ALL transaction detail rows via the keyset cursor so the report
 * isn't silently truncated at the first window (the view paginates client-side).
 * Safety-capped to avoid runaway loops on very large date ranges.
 */
export async function fetchAllTransactionDetails(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
  maxRows = 5000,
): Promise<{ rows: TransactionDetailRow[] }> {
  const all: TransactionDetailRow[] = [];
  let cursor: string | undefined;
  for (let i = 0; i < 200; i++) {
    const { rows, nextCursor } = await fetchTransactionDetails(tenantId, filters, { cursor, limit: 100 }, token);
    all.push(...rows);
    if (!nextCursor || rows.length === 0 || all.length >= maxRows) break;
    cursor = nextCursor;
  }
  return { rows: all };
}

export type TransactionsOverviewRow = {
  totalTransactions: number;
  totalAmount: number;
  totalTax: number;
  totalDiscount: number;
  uniqueCustomers: number;
  averageOrderValue: number;
  distinctMonths: number;
  distinctDays: number;
  topDayAmount: number | null;
  topDayTransactions: number | null;
  topDayDate: string | null;
  averageMonthlyAmount: number;
  averageDailyAmount: number;
};

export async function fetchTransactionsOverview(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
): Promise<TransactionsOverviewRow | null> {
  const params = dateParams(filters);
  const res = await fetch(`${ORDERS_BASE}/orders/reports/transactions/overview?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<TransactionsOverviewRow> = await res.json();
  return body.data ?? null;
}

export type EcommercePaymentRow = {
  orderId: string;
  paymentMethod: string;
  amount: number;
  currency: string;
  transactionRef: string;
  date: string;
};

export async function fetchEcommercePayments(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
): Promise<EcommercePaymentRow[]> {
  const params = dateParams(filters);
  if (filters.paymentMethod && filters.paymentMethod !== "all") params.set("paymentMethod", filters.paymentMethod);
  const res = await fetch(`${ORDERS_BASE}/orders/reports/transactions/ecommerce?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<EcommercePaymentRow[]> = await res.json();
  return body.data ?? [];
}

// --- Sales reports ---

export type SalesByCategoryRow = {
  category: string;
  productsCount: number;
  transactions: number;
  unitsSold: number;
  totalSales: number;
  currency: string;
};

export async function fetchSalesByCategory(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
): Promise<SalesByCategoryRow[]> {
  const params = dateParams(filters);
  if (filters.brandCode && filters.brandCode !== "all") params.set("brandCode", filters.brandCode);
  const res = await fetch(`${ORDERS_BASE}/orders/reports/sales/by-category?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<SalesByCategoryRow[]> = await res.json();
  return body.data ?? [];
}

export type SalesByProductRow = {
  productId: string;
  productNameJson: string;
  unitsSold: number;
  totalSales: number;
  currency: string;
};

export async function fetchSalesByProduct(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
): Promise<SalesByProductRow[]> {
  const params = dateParams(filters);
  if (filters.brandCode && filters.brandCode !== "all") params.set("brandCode", filters.brandCode);
  if (filters.categoryId && filters.categoryId !== "all") params.set("categoryId", filters.categoryId);
  const res = await fetch(`${ORDERS_BASE}/orders/reports/sales/by-product?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<SalesByProductRow[]> = await res.json();
  return body.data ?? [];
}

export type SalesByTenantRow = {
  tenantId: string;
  ordersCount: number;
  productsSold: number;
  totalSales: number;
  currency: string;
};

export async function fetchSalesByTenant(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
): Promise<SalesByTenantRow[]> {
  const params = dateParams(filters);
  const res = await fetch(`${ORDERS_BASE}/orders/reports/sales/by-tenant?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, undefined, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<SalesByTenantRow[]> = await res.json();
  return body.data ?? [];
}

// ── Analytics Reports API (DAI-685) ─────────────────────────────────────────

export type SalesGroupBy = "store" | "product" | "category";

export type SalesGroupRow = {
  key: string;
  orders: number | null;
  gross: number;
};

export async function fetchSalesGrouped(
  tenantId: string,
  filters: ReportFilters,
  groupBy: SalesGroupBy,
  token?: string,
): Promise<SalesGroupRow[]> {
  const params = dateParams(filters);
  params.set("groupBy", groupBy);
  const res = await fetch(`${REPORTS_BASE}/sales?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<SalesGroupRow[]> = await res.json();
  return body.data ?? [];
}

export type AbandonCartRow = { cartId: string; createdAt: string };

export async function fetchAbandonCart(
  tenantId: string,
  filters: ReportFilters,
  token?: string,
): Promise<AbandonCartRow[]> {
  const params = dateParams(filters);
  const res = await fetch(`${REPORTS_BASE}/abandon-cart?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<AbandonCartRow[]> = await res.json();
  return body.data ?? [];
}

export type RestockSubscriptionAggRow = { productId: string; subscriptions: number; fulfilled: number };

export async function fetchRestockSubscriptions(
  tenantId: string,
  filters: Pick<ReportFilters, "storeId">,
  token?: string,
): Promise<RestockSubscriptionAggRow[]> {
  const params = new URLSearchParams();
  if (filters.storeId && filters.storeId !== "all") params.set("storeId", filters.storeId);
  const res = await fetch(`${REPORTS_BASE}/restock-subscriptions?${params}`, {
    credentials: "same-origin",
    headers: headers(tenantId, filters.storeId, token),
  });
  await checkOk(res);
  const body: ApiEnvelope<RestockSubscriptionAggRow[]> = await res.json();
  return body.data ?? [];
}
