import { describe, expect, it } from "vitest";
import { parseJsonMap, resolveStoreScope } from "./storeContext";

describe("storeContext", () => {
  it("parseJsonMap returns fallback on invalid JSON", () => {
    expect(parseJsonMap("not-json", { a: "s1" })).toEqual({ a: "s1" });
  });

  it("parseJsonMap parses valid map", () => {
    expect(parseJsonMap('{"aeon-bt":"s2"}', { "aeon-bt": "s1" })).toEqual({ "aeon-bt": "s2" });
  });

  it("resolveStoreScope uses defaults for known tenant", () => {
    const scope = resolveStoreScope("aeon-bt");
    expect(scope.tenantId).toBe("aeon-bt");
    expect(scope.storeId).toBe("s1");
    expect(scope.warehouseId).toBeTruthy();
  });
});
