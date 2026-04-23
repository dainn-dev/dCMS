import { describe, expect, it, vi, beforeEach } from "vitest";
import { bulkUpdateProducts, createProduct, fetchProducts, getProduct } from "./productsApi";

function mockFetchOnce(json: unknown, ok = true, status = 200) {
  globalThis.fetch = vi.fn(async () => {
    return {
      ok,
      status,
      json: async () => json,
    } as unknown as Response;
  }) as unknown as typeof fetch;
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe("productsApi", () => {
  it("fetchProducts() hits gateway catalog with query params", async () => {
    mockFetchOnce({
      data: [
        {
          id: "p1",
          name: "fallback",
          nameByLocale: { vi: "Tên", en: "Name" },
          minBasePrice: { amount: 12345, currency: "USD" },
          hasInStockVariant: true,
          slug: "slug",
        },
      ],
      meta: { totalCount: 7, pageSize: 10, nextCursor: null },
      error: null,
    });

    const out = await fetchProducts("TEN", "STORE", { name: "abc", sku: "S1" }, { page: 2, pageSize: 10 }, "tok");
    expect(out.total).toBe(7);
    expect(out.rows[0].id).toBe("p1");
    expect(out.rows[0].name).toBe("Tên");

    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/gateway/v1/catalog/tenants/TEN/stores/STORE/products?");
    expect(String(call[0])).toContain("q=abc+S1");
    expect(String(call[0])).toContain("pageSize=10");
    expect(String(call[0])).toContain("cursor=10");
    expect(call[1]?.headers?.Authorization).toBe("Bearer tok");
  });

  it("getProduct() parses envelope", async () => {
    mockFetchOnce({
      data: {
        id: "p1",
        tenantId: "TEN",
        storeId: "STORE",
        categoryId: 1,
        nameJson: "{\"vi\":\"Tên\"}",
        descriptionJson: "{}",
        slug: "slug",
        status: "draft",
        salesCount30d: 0,
        createdAt: "2026-01-01T00:00:00Z",
        updatedAt: "2026-01-01T00:00:00Z",
      },
      meta: null,
      error: null,
    });

    const p = await getProduct("TEN", "STORE", "p1", "tok");
    expect(p.id).toBe("p1");
    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/gateway/v1/catalog/tenants/TEN/stores/STORE/products/p1");
  });

  it("createProduct() POSTs payload", async () => {
    mockFetchOnce({ data: { id: "p1", slug: "s", status: "draft" }, meta: null, error: null });

    const res = await createProduct(
      "TEN",
      "STORE",
      { categoryId: 1, nameJson: "{\"vi\":\"Tên\"}", descriptionJson: "{}", slug: "s" },
      "tok"
    );
    expect(res.id).toBe("p1");
    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(call[1]?.method).toBe("POST");
    expect(call[1]?.headers?.Authorization).toBe("Bearer tok");
    expect(call[1]?.body).toContain("\"slug\":\"s\"");
  });

  it("bulkUpdateProducts() PUTs bulk payload", async () => {
    mockFetchOnce({ data: { succeeded: 2, failed: 1 }, meta: null, error: null });

    const res = await bulkUpdateProducts(
      "TEN",
      "STORE",
      {
        items: [
          { productId: "p1", categoryId: 1, nameJson: "{}", descriptionJson: "{}", slug: "s1" },
          { productId: "p2", categoryId: 1, nameJson: "{}", descriptionJson: "{}", slug: "s2" },
        ],
      },
      "tok"
    );
    expect(res.succeeded).toBe(2);
    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/products/bulk");
    expect(call[1]?.method).toBe("PUT");
  });
});

