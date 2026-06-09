import { afterEach, describe, expect, it, vi } from "vitest";
import { buildProductsSearchUrl, searchProducts } from "./catalogApi";

describe("catalogApi", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("buildProductsSearchUrl includes tenant, store, and query", () => {
    const url = buildProductsSearchUrl({
      tenantId: "aeon-bt",
      storeId: "s1",
      q: "milk",
      pageSize: 10,
      cursor: "abc",
    });
    expect(url).toContain("/gateway/v1/catalog/products?");
    expect(url).toContain("tenantId=aeon-bt");
    expect(url).toContain("storeId=s1");
    expect(url).toContain("q=milk");
    expect(url).toContain("pageSize=10");
    expect(url).toContain("cursor=abc");
  });

  it("searchProducts calls fetch and unwraps envelope", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({
          data: [{ id: "1", name: "A", slug: "a", minBasePrice: { amount: 1, currency: "VND" }, hasInStockVariant: true }],
          meta: { nextCursor: null },
          error: null,
        }),
        { status: 200 },
      ),
    );

    const result = await searchProducts({ tenantId: "aeon-bt", storeId: "s1" });
    expect(fetchMock).toHaveBeenCalled();
    expect(result.items).toHaveLength(1);
    expect(result.items[0]?.slug).toBe("a");
  });
});
