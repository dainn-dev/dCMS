/**
 * Shared hash helpers for dcms SPAs under `/umbraco/section/...`.
 * Convention: `#<pageId>`. Legacy: `#dcmsEstore/<pageId>`, `#/<pageId>`.
 */
export function parsePageIdFromLocationHash<T extends string>(validIds: ReadonlySet<string>): T | null {
  const h = window.location.hash;
  let rest = "";
  if (h.startsWith("#dcmsEstore")) {
    rest = h.replace(/^#dcmsEstore\/?/, "");
  } else {
    const raw = h.replace(/^#/, "").trim();
    rest = raw.startsWith("/") ? raw.slice(1) : raw;
  }
  if (!rest) return null;
  const parts = rest.split("/").filter(Boolean);
  const id = parts[parts.length - 1] ?? "";
  return validIds.has(id) ? (id as T) : null;
}

export function locationHashForPageId<T extends string>(id: T): string {
  return `#${id}`;
}
