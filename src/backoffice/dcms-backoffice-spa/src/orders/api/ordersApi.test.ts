import { beforeEach, describe, expect, it, vi } from "vitest";
import { cancelOrder, fetchOrders, getOrder } from "./ordersApi";

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

describe("ordersApi", () => {
  it("fetchOrders() uses cursor pagination and tenant/store headers", async () => {
    mockFetchOnce({
      items: [
        {
          orderId: "550e8400-e29b-41d4-a716-446655440000",
          customerId: "cust-1",
          status: "Created",
          totalAmount: 123.45,
          currency: "USD",
          createdAt: "2026-01-01T00:00:00Z",
          lineCount: 2,
        },
      ],
      nextCursor: "next",
    });

    const out = await fetchOrders("TEN", "STORE", { customerId: "cust-1", status: "Created" }, { cursor: "c1", limit: 50 }, "tok");
    expect(out.rows.length).toBe(1);
    expect(out.nextCursor).toBe("next");

    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/gateway/v1/orders/orders?");
    expect(String(call[0])).toContain("customerId=cust-1");
    expect(String(call[0])).toContain("status=Created");
    expect(String(call[0])).toContain("cursor=c1");
    expect(String(call[0])).toContain("limit=50");
    expect(call[1]?.headers?.["X-Tenant-Id"]).toBe("TEN");
    expect(call[1]?.headers?.["X-Store-Id"]).toBe("STORE");
    expect(call[1]?.headers?.Authorization).toBe("Bearer tok");
  });

  it("getOrder() maps detail dto into Order (addresses present)", async () => {
    mockFetchOnce({
      orderId: "550e8400-e29b-41d4-a716-446655440000",
      tenantId: "TEN",
      storeId: "STORE",
      customerId: "cust-1",
      status: "Delivered",
      total: { amount: 10, currency: "USD" },
      paymentIntentId: null,
      createdAt: "2026-01-02T00:00:00Z",
      shippingAddress: { line1: "A", line2: null, city: null, region: null, postalCode: "1", countryCode: "US" },
      shipment: { status: null, carrier: null, trackingNumber: null },
      lines: [],
    });

    const o = await getOrder("TEN", "STORE", "550e8400-e29b-41d4-a716-446655440000", "tok");
    expect(o.orderId).toBe("550e8400-e29b-41d4-a716-446655440000");
    expect(o.status).toBe("Delivered");
    expect(o.shippingAddressLine1).toBe("A");
    expect(o.shippingPostalCode).toBe("1");
  });

  it("cancelOrder() POSTs reason body", async () => {
    mockFetchOnce({}, true, 200);
    await cancelOrder("TEN", "STORE", "550e8400-e29b-41d4-a716-446655440000", "customer_request", "tok");
    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(call[1]?.method).toBe("POST");
    expect(String(call[0])).toContain("/cancel");
    expect(String(call[1]?.body)).toContain("customer_request");
  });
});

