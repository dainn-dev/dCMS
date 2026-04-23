import { describe, expect, it } from "vitest";

function stripHtml(raw: string): string {
  if (!raw) return "";
  return raw.replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();
}

function csvEscape(v: unknown): string {
  const s = String(v ?? "");
  return `"${s.replace(/"/g, '""')}"`;
}

describe("ProductsPage export helpers", () => {
  it("stripHtml removes tags and collapses whitespace", () => {
    expect(stripHtml("<p>Hello <b>world</b></p>")).toBe("Hello world");
    expect(stripHtml("A<br/>B")).toBe("A B");
  });

  it("csvEscape escapes quotes and wraps in quotes", () => {
    expect(csvEscape('a"b')).toBe("\"a\"\"b\"");
    expect(csvEscape(null)).toBe("\"\"");
  });
});

