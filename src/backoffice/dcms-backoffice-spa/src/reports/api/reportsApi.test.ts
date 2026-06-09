import { describe, expect, it, vi, beforeEach } from "vitest";
import {
  fetchTransactionSummary,
  fetchTransactionDetails,
  fetchEcommercePayments,
  fetchSalesByCategory,
  fetchSalesByProduct,
  fetchSalesByTenant,
  fetchSalesReport,
  fetchAbandonCart,
  fetchRestockSubscriptions,
} from "./reportsApi";

function mockFetchOnce(json: unknown, ok = true, status = 200) {
  globalThis.fetch = vi.fn(async () => {
    return {
      ok,
      status,
      json: async () => json,
    } as unknown as Response;
  }) as unknown as typeof fetch;
}

function lastFetchCall() {
  return (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
}

function lastFetchUrl(): string {
  return lastFetchCall()[0] as string;
}

function lastFetchHeaders(): Record<string, string> {
  return (lastFetchCall()[1] as RequestInit).headers as Record<string, string>;
}

beforeEach(() => {
  vi.restoreAllMocks();
});

const filters = { dateFrom: "2026-04-01", dateTo: "2026-04-18" };

// ── fetchTransactionSummary ──────────────────────────────────────────────────

describe("fetchTransactionSummary", () => {
  it("calls correct URL with tenant header", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchTransactionSummary("t1", filters, "tok123");
    expect(lastFetchUrl()).toContain("/reports/transactions/summary");
    expect(lastFetchUrl()).toContain("dateFrom=2026-04-01");
    expect(lastFetchUrl()).toContain("dateTo=2026-04-18");
    expect(lastFetchHeaders()["X-Tenant-Id"]).toBe("t1");
    expect(lastFetchHeaders()["Authorization"]).toBe("Bearer tok123");
  });

  it("returns data from envelope", async () => {
    mockFetchOnce({
      data: [{ paymentMethod: "Visa", transactionCount: 10, totalAmount: 500, currency: "SGD" }],
      meta: null,
      error: null,
    });
    const result = await fetchTransactionSummary("t1", filters);
    expect(result).toHaveLength(1);
    expect(result[0].paymentMethod).toBe("Visa");
  });

  it("throws on HTTP error", async () => {
    mockFetchOnce({ error: { message: "bad request" } }, false, 400);
    await expect(fetchTransactionSummary("t1", filters)).rejects.toThrow("bad request");
  });
});

// ── fetchTransactionDetails ──────────────────────────────────────────────────

describe("fetchTransactionDetails", () => {
  it("includes pagination params in URL", async () => {
    mockFetchOnce({ data: [], meta: { nextCursor: null }, error: null });
    await fetchTransactionDetails("t1", { ...filters, memberQuery: "alpha" }, { cursor: "abc", limit: 25 });
    expect(lastFetchUrl()).toContain("cursor=abc");
    expect(lastFetchUrl()).toContain("limit=25");
    expect(lastFetchUrl()).toContain("memberQuery=alpha");
  });

  it("returns rows and nextCursor", async () => {
    mockFetchOnce({
      data: [{ orderId: "o1", date: "2026-04-02", member: "a@b.c", store: "s1", status: "Confirmed", amount: 100, currency: "SGD" }],
      meta: { nextCursor: "nc1" },
      error: null,
    });
    const result = await fetchTransactionDetails("t1", filters);
    expect(result.rows).toHaveLength(1);
    expect(result.nextCursor).toBe("nc1");
  });
});

// ── fetchEcommercePayments ──────────────────────────────────────────────────

describe("fetchEcommercePayments", () => {
  it("passes paymentMethod filter", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchEcommercePayments("t1", { ...filters, paymentMethod: "Visa" });
    expect(lastFetchUrl()).toContain("paymentMethod=Visa");
  });

  it("skips paymentMethod when 'all'", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchEcommercePayments("t1", { ...filters, paymentMethod: "all" });
    expect(lastFetchUrl()).not.toContain("paymentMethod=");
  });
});

// ── fetchSalesByCategory ────────────────────────────────────────────────────

describe("fetchSalesByCategory", () => {
  it("calls by-category endpoint with brand filter", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesByCategory("t1", { ...filters, brandCode: "CAS-7721" });
    expect(lastFetchUrl()).toContain("/reports/sales/by-category");
    expect(lastFetchUrl()).toContain("brandCode=CAS-7721");
  });

  it("returns typed data", async () => {
    mockFetchOnce({
      data: [{ category: "Dairy", productsCount: 12, transactions: 890, unitsSold: 2400, totalSales: 48200, currency: "SGD" }],
      meta: null,
      error: null,
    });
    const result = await fetchSalesByCategory("t1", filters);
    expect(result).toHaveLength(1);
    expect(result[0].category).toBe("Dairy");
  });
});

// ── fetchSalesByProduct ─────────────────────────────────────────────────────

describe("fetchSalesByProduct", () => {
  it("passes category filter", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesByProduct("t1", { ...filters, categoryId: "cat1", brandCode: "B1" });
    expect(lastFetchUrl()).toContain("categoryId=cat1");
    expect(lastFetchUrl()).toContain("brandCode=B1");
  });

  it("throws on server error", async () => {
    mockFetchOnce({ error: { message: "Internal error" } }, false, 500);
    await expect(fetchSalesByProduct("t1", filters)).rejects.toThrow("Internal error");
  });
});

// ── fetchSalesByTenant ──────────────────────────────────────────────────────

describe("fetchSalesByTenant", () => {
  it("calls by-tenant endpoint without store header", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesByTenant("t1", filters, "tok");
    expect(lastFetchUrl()).toContain("/reports/sales/by-tenant");
    expect(lastFetchHeaders()["Authorization"]).toBe("Bearer tok");
    expect(lastFetchHeaders()["X-Store-Id"]).toBeUndefined();
  });

  it("returns tenant rows", async () => {
    mockFetchOnce({
      data: [{ tenantId: "t1", ordersCount: 100, productsSold: 500, totalSales: 50000, currency: "SGD" }],
      meta: null,
      error: null,
    });
    const result = await fetchSalesByTenant("t1", filters);
    expect(result).toHaveLength(1);
    expect(result[0].tenantId).toBe("t1");
  });
});

// ── fetchSalesReport (Sales Reports BRD §1–§3, analytics) ───────────────────

describe("fetchSalesReport", () => {
  it("hits analytics /sales with groupBy=category and tenant/store headers", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesReport("t1", { ...filters, storeId: "s1" }, "category", "tok");
    expect(lastFetchUrl()).toContain("/reports/sales");
    expect(lastFetchUrl()).toContain("groupBy=category");
    expect(lastFetchUrl()).toContain("dateFrom=2026-04-01");
    expect(lastFetchHeaders()["X-Tenant-Id"]).toBe("t1");
    expect(lastFetchHeaders()["X-Store-Id"]).toBe("s1");
    expect(lastFetchHeaders()["Authorization"]).toBe("Bearer tok");
  });

  it("supports the brand and product dimensions", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesReport("t1", filters, "brand");
    expect(lastFetchUrl()).toContain("groupBy=brand");

    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesReport("t1", filters, "product");
    expect(lastFetchUrl()).toContain("groupBy=product");
  });

  it("returns BRD-shaped rows from the envelope", async () => {
    mockFetchOnce({
      data: [{ key: "cat-1", name: null, upc: null, sku: null, productsCount: null, transactions: null, unitsSold: 24, gross: 1000, currency: "SGD" }],
      meta: null,
      error: null,
    });
    const { rows, overview } = await fetchSalesReport("t1", filters, "category");
    expect(rows).toHaveLength(1);
    expect(rows[0].key).toBe("cat-1");
    expect(rows[0].unitsSold).toBe(24);
    expect(rows[0].gross).toBe(1000);
    expect(overview).toBeNull();
  });

  it("surfaces the product overview from meta", async () => {
    mockFetchOnce({
      data: [],
      meta: { overview: { totalProducts: 3, totalSales: 500, totalSalesTransactions: 12, totalProductsSold: 40, currency: "SGD" } },
      error: null,
    });
    const { overview } = await fetchSalesReport("t1", filters, "product");
    expect(overview).not.toBeNull();
    expect(overview?.totalProducts).toBe(3);
    expect(overview?.totalSalesTransactions).toBe(12);
  });
});

// ── fetchAbandonCart (DAI-685 analytics) ────────────────────────────────────

describe("fetchAbandonCart", () => {
  it("calls /abandon-cart with date range and tenant header", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchAbandonCart("t1", filters, "tok");
    expect(lastFetchUrl()).toContain("/reports/abandon-cart");
    expect(lastFetchUrl()).toContain("dateFrom=2026-04-01");
    expect(lastFetchUrl()).toContain("dateTo=2026-04-18");
    expect(lastFetchHeaders()["X-Tenant-Id"]).toBe("t1");
    expect(lastFetchHeaders()["Authorization"]).toBe("Bearer tok");
  });

  it("returns abandon-cart rows", async () => {
    mockFetchOnce({
      data: [{ cartId: "c1", createdAt: "2026-04-02T10:00:00Z" }],
      meta: null,
      error: null,
    });
    const result = await fetchAbandonCart("t1", filters);
    expect(result).toHaveLength(1);
    expect(result[0].cartId).toBe("c1");
  });

  it("throws on HTTP error", async () => {
    mockFetchOnce({ error: { message: "boom" } }, false, 500);
    await expect(fetchAbandonCart("t1", filters)).rejects.toThrow("boom");
  });
});

// ── fetchRestockSubscriptions (DAI-685 analytics) ───────────────────────────

describe("fetchRestockSubscriptions", () => {
  const range = { dateFrom: "2026-04-01", dateTo: "2026-04-30", storeId: "all" };

  it("sends the required date range and omits storeId when 'all'", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchRestockSubscriptions("t1", range, {});
    expect(lastFetchUrl()).toContain("/reports/restock-subscriptions");
    expect(lastFetchUrl()).toContain("dateFrom=2026-04-01");
    expect(lastFetchUrl()).toContain("dateTo=2026-04-30");
    expect(lastFetchUrl()).not.toContain("storeId=");
  });

  it("includes storeId + optional BR02 filters when set", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchRestockSubscriptions(
      "t1",
      { ...range, storeId: "s7" },
      { upc: "012", sku: "SKU-9", productName: "widget", email: "a@x.com" },
      "tok",
    );
    expect(lastFetchUrl()).toContain("storeId=s7");
    expect(lastFetchUrl()).toContain("upc=012");
    expect(lastFetchUrl()).toContain("sku=SKU-9");
    expect(lastFetchUrl()).toContain("productName=widget");
    expect(lastFetchUrl()).toContain("email=a%40x.com");
    expect(lastFetchHeaders()["X-Tenant-Id"]).toBe("t1");
    expect(lastFetchHeaders()["X-Store-Id"]).toBe("s7");
    expect(lastFetchHeaders()["Authorization"]).toBe("Bearer tok");
  });

  it("returns per-subscription detail rows", async () => {
    mockFetchOnce({
      data: [
        {
          productId: "p1",
          upc: "0001",
          sku: "SKU-1",
          productName: "Widget",
          email: "a@x.com",
          subscriptionDate: "2026-04-10T08:00:00Z",
          restockNotificationSentOn: null,
        },
      ],
      meta: null,
      error: null,
    });
    const result = await fetchRestockSubscriptions("t1", range, {});
    expect(result).toHaveLength(1);
    expect(result[0].productName).toBe("Widget");
    expect(result[0].email).toBe("a@x.com");
    expect(result[0].restockNotificationSentOn).toBeNull();
  });
});
