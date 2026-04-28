import { describe, expect, it, vi, beforeEach } from "vitest";
import {
  fetchTransactionSummary,
  fetchTransactionDetails,
  fetchEcommercePayments,
  fetchSalesByCategory,
  fetchSalesByProduct,
  fetchSalesByTenant,
  fetchSalesGrouped,
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

// ── fetchSalesGrouped (DAI-685 analytics) ───────────────────────────────────

describe("fetchSalesGrouped", () => {
  it("hits analytics /sales with groupBy=store", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesGrouped("t1", { ...filters, storeId: "s1" }, "store", "tok");
    expect(lastFetchUrl()).toContain("/reports/sales");
    expect(lastFetchUrl()).toContain("groupBy=store");
    expect(lastFetchUrl()).toContain("dateFrom=2026-04-01");
    expect(lastFetchHeaders()["X-Tenant-Id"]).toBe("t1");
    expect(lastFetchHeaders()["X-Store-Id"]).toBe("s1");
    expect(lastFetchHeaders()["Authorization"]).toBe("Bearer tok");
  });

  it("supports groupBy=product", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesGrouped("t1", filters, "product");
    expect(lastFetchUrl()).toContain("groupBy=product");
  });

  it("supports groupBy=category", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchSalesGrouped("t1", filters, "category");
    expect(lastFetchUrl()).toContain("groupBy=category");
  });

  it("returns rows from envelope", async () => {
    mockFetchOnce({
      data: [{ key: "s1", orders: 10, gross: 1000 }],
      meta: null,
      error: null,
    });
    const result = await fetchSalesGrouped("t1", filters, "store");
    expect(result).toHaveLength(1);
    expect(result[0].key).toBe("s1");
    expect(result[0].gross).toBe(1000);
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
  it("omits storeId param when 'all'", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchRestockSubscriptions("t1", { storeId: "all" });
    expect(lastFetchUrl()).toContain("/reports/restock-subscriptions");
    expect(lastFetchUrl()).not.toContain("storeId=");
  });

  it("includes storeId param when set", async () => {
    mockFetchOnce({ data: [], meta: null, error: null });
    await fetchRestockSubscriptions("t1", { storeId: "s7" }, "tok");
    expect(lastFetchUrl()).toContain("storeId=s7");
    expect(lastFetchHeaders()["X-Tenant-Id"]).toBe("t1");
    expect(lastFetchHeaders()["X-Store-Id"]).toBe("s7");
    expect(lastFetchHeaders()["Authorization"]).toBe("Bearer tok");
  });

  it("returns aggregated subscription rows", async () => {
    mockFetchOnce({
      data: [{ productId: "p1", subscriptions: 12, fulfilled: 5 }],
      meta: null,
      error: null,
    });
    const result = await fetchRestockSubscriptions("t1", { storeId: "all" });
    expect(result).toHaveLength(1);
    expect(result[0].productId).toBe("p1");
    expect(result[0].subscriptions).toBe(12);
  });
});
