import { locationHashForPageId, parsePageIdFromLocationHash } from "../lib/spaSectionHash";
import type { EStorePageId } from "./layout/EStoreLayout";
import { ESTORE_HASH_PAGE_IDS } from "./layout/EStoreLayout";

const PAGE_SET = new Set<string>(ESTORE_HASH_PAGE_IDS);

export function parseEStorePageFromHash(): EStorePageId | null {
  return parsePageIdFromLocationHash<EStorePageId>(PAGE_SET);
}

export function eStoreHashForPage(id: EStorePageId): string {
  return locationHashForPageId(id);
}
