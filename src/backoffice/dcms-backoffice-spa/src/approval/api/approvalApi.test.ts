import { describe, expect, it, vi, beforeEach } from "vitest";
import {
  approveCampaign,
  approveProduct,
  approvePromoCode,
  fetchPendingCampaigns,
  fetchPendingProducts,
  fetchPendingPromoCodes,
  rejectCampaign,
  rejectProduct,
  rejectPromoCode,
} from "./approvalApi";

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

describe("approvalApi — products", () => {
  it("fetchPendingProducts maps envelope + query params", async () => {
    mockFetchOnce({
      data: {
        items: [
          {
            id: "p1",
            name: "N",
            brandName: "B",
            categoryPath: "C",
            submittedByUserId: "u1",
            submittedAt: "2026-04-01T00:00:00Z",
            status: "pending_approval",
          },
        ],
        nextCursor: "cur2",
        total: 1,
      },
      meta: null,
      error: null,
    });

    const out = await fetchPendingProducts("TEN", "STORE", { limit: 25, cursor: "c0" }, "tok");
    expect(out.items[0].id).toBe("p1");
    expect(out.items[0].productName).toBe("N");
    expect(out.items[0].status).toBe("pending");
    expect(out.nextCursor).toBe("cur2");
    expect(out.total).toBe(1);

    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/gateway/v1/catalog/tenants/TEN/stores/STORE/products/pending-approvals?");
    expect(String(call[0])).toContain("limit=25");
    expect(String(call[0])).toContain("cursor=c0");
    expect(call[1]?.headers?.Authorization).toBe("Bearer tok");
  });

  it("approveProduct POSTs approve", async () => {
    mockFetchOnce({ data: { id: "p1" }, meta: null, error: null });
    await approveProduct("TEN", "STORE", "p1", "tok");
    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/products/p1/approve");
    expect(call[1]?.method).toBe("POST");
    expect(call[1]?.body).toBe("{}");
  });

  it("rejectProduct POSTs comment", async () => {
    mockFetchOnce({ data: { id: "p1" }, meta: null, error: null });
    await rejectProduct("TEN", "STORE", "p1", "no thanks", "tok");
    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/products/p1/reject");
    expect(call[1]?.body).toContain("no thanks");
  });
});

describe("approvalApi — campaigns", () => {
  it("fetchPendingCampaigns uses status=pending_approval", async () => {
    mockFetchOnce({
      data: [
        {
          id: "c1",
          code: "CMP",
          nameJson: '{"en":"Camp"}',
          editorKind: "product-discount",
          workflowState: "pending_approval",
          channel: "Email",
          startDate: null,
          endDate: null,
          budget: "",
          audience: "",
          conversions: 0,
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-02T00:00:00Z",
        },
      ],
      meta: { total: 1, page: 1, pageSize: 50 },
      error: null,
    });

    const out = await fetchPendingCampaigns("TEN", { page: 1, pageSize: 50 }, "tok");
    expect(out.total).toBe(1);
    expect(out.items[0].campaignCode).toBe("CMP");
    expect(out.items[0].status).toBe("pending");

    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/gateway/v1/promotions/tenants/TEN/campaigns?");
    expect(String(call[0])).toContain("status=pending_approval");
  });

  it("approveCampaign + rejectCampaign", async () => {
    mockFetchOnce({ data: {}, meta: null, error: null });
    await approveCampaign("TEN", "c1", "tok");
    let call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/campaigns/c1/approve");

    mockFetchOnce({ data: {}, meta: null, error: null });
    await rejectCampaign("TEN", "c1", "bad", "tok");
    call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/campaigns/c1/reject");
    expect(call[1]?.body).toContain("bad");
  });
});

describe("approvalApi — promo codes", () => {
  it("fetchPendingPromoCodes uses status=pending_approval", async () => {
    mockFetchOnce({
      data: [
        {
          id: "pc1",
          code: "SAVE10",
          nameJson: '{"en":"Save"}',
          discountType: "percentage",
          discountValue: "10",
          workflowState: "pending_approval",
          submittedBy: "u",
          submittedDate: "2026-03-01T00:00:00Z",
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-02T00:00:00Z",
        },
      ],
      meta: { total: 1, page: 1, pageSize: 50 },
      error: null,
    });

    const out = await fetchPendingPromoCodes("TEN", {}, "tok");
    expect(out.items[0].promoCode).toBe("SAVE10");
    expect(out.items[0].status).toBe("pending");

    const call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/gateway/v1/promotions/tenants/TEN/promo-codes?");
    expect(String(call[0])).toContain("status=pending_approval");
  });

  it("approvePromoCode + rejectPromoCode", async () => {
    mockFetchOnce({ data: {}, meta: null, error: null });
    await approvePromoCode("TEN", "pc1", "tok");
    let call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/promo-codes/pc1/approve");

    mockFetchOnce({ data: {}, meta: null, error: null });
    await rejectPromoCode("TEN", "pc1", "nope", "tok");
    call = (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(String(call[0])).toContain("/promo-codes/pc1/reject");
  });
});
