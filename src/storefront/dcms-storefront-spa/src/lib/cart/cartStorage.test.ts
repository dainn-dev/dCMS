import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  addOrUpdateLine,
  readCart,
  type CartLine,
  writeCart,
} from "./cartStorage";

const memory: Record<string, string> = {};

beforeEach(() => {
  for (const key of Object.keys(memory)) delete memory[key];
  vi.stubGlobal("localStorage", {
    getItem: (key: string) => memory[key] ?? null,
    setItem: (key: string, value: string) => {
      memory[key] = value;
    },
    removeItem: (key: string) => {
      delete memory[key];
    },
  });
});

const line = (variantId: string, qty: number): CartLine => ({
  productId: "p1",
  variantId,
  sku: `sku-${variantId}`,
  name: "Test",
  unitPrice: 100,
  currency: "VND",
  quantity: qty,
  warehouseId: "wh-main",
});

afterEach(() => {
  writeCart("t1", "s1", []);
  writeCart("t2", "s1", []);
});

describe("cartStorage", () => {
  it("merges quantity for the same variant", () => {
    addOrUpdateLine("t1", "s1", line("v1", 1));
    addOrUpdateLine("t1", "s1", line("v1", 2));
    expect(readCart("t1", "s1")[0]?.quantity).toBe(3);
  });

  it("isolates carts per tenant", () => {
    addOrUpdateLine("t1", "s1", line("v1", 1));
    addOrUpdateLine("t2", "s1", line("v2", 5));
    expect(readCart("t1", "s1")).toHaveLength(1);
    expect(readCart("t2", "s1")[0]?.variantId).toBe("v2");
  });
});
