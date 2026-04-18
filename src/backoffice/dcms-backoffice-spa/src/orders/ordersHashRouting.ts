import { locationHashForPageId, parsePageIdFromLocationHash } from "../lib/spaSectionHash";
import type { OrdersPageId } from "./layout/OrdersLayout";
import { ORDERS_HASH_PAGE_IDS } from "./layout/OrdersLayout";

const PAGE_SET = new Set<string>(ORDERS_HASH_PAGE_IDS);

export function parseOrdersPageFromHash(): OrdersPageId | null {
  return parsePageIdFromLocationHash<OrdersPageId>(PAGE_SET);
}

export function ordersHashForPage(id: OrdersPageId): string {
  return locationHashForPageId(id);
}
