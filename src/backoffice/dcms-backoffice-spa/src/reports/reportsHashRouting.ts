import { locationHashForPageId, parsePageIdFromLocationHash } from "../lib/spaSectionHash";
import type { ReportsPageId } from "./ReportsLayout";
import { REPORTS_HASH_PAGE_IDS } from "./ReportsLayout";

const PAGE_SET = new Set<string>(REPORTS_HASH_PAGE_IDS);

export function parseReportsPageFromHash(): ReportsPageId | null {
  return parsePageIdFromLocationHash<ReportsPageId>(PAGE_SET);
}

export function reportsHashForPage(id: ReportsPageId): string {
  return locationHashForPageId(id);
}
