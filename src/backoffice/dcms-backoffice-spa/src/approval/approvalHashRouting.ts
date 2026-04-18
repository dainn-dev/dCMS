import { locationHashForPageId, parsePageIdFromLocationHash } from "../lib/spaSectionHash";
import type { ApprovalPageId } from "./layout/ApprovalLayout";
import { APPROVAL_HASH_PAGE_IDS } from "./layout/ApprovalLayout";

const PAGE_SET = new Set<string>(APPROVAL_HASH_PAGE_IDS);

export function parseApprovalPageFromHash(): ApprovalPageId | null {
  return parsePageIdFromLocationHash<ApprovalPageId>(PAGE_SET);
}

export function approvalHashForPage(id: ApprovalPageId): string {
  return locationHashForPageId(id);
}
