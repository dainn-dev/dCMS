/**
 * DAI-613: Fulfillment configuration API client.
 * Routes through dCMS.Gateway → /gateway/v1/fulfillment/tenants/{tenantId}/fulfillment/...
 */

import { GATEWAY } from "./gatewayConfig";
import type {
  CollectionLocation,
  FulfillmentDeliveryMode,
  FulfillmentDynamicField,
  FulfillmentGrouping,
  FulfillmentPredefinedFieldSetting,
  FulfillmentSlot,
  LogisticPartner,
  StockLocation,
} from "../EStoreApp";

const BASE = GATEWAY.fulfillment;

function headers(token?: string): Record<string, string> {
  const h: Record<string, string> = { "Content-Type": "application/json", Accept: "application/json" };
  if (token) h["Authorization"] = `Bearer ${token}`;
  return h;
}

async function checkOk(res: Response): Promise<void> {
  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try {
      const b = await res.json();
      if (b?.error?.message) msg = b.error.message;
    } catch {
      /* ignore */
    }
    throw new Error(msg);
  }
}

/** Normalize date for API (yyyy-MM-dd). */
export function normalizeFulfillmentDate(s: string): string {
  const t = s.trim();
  if (!t) return "";
  const i = t.indexOf("T");
  return i >= 0 ? t.slice(0, i) : t.slice(0, 10);
}

function formatSlotUpdatedAt(iso: string): string {
  try {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso;
    return d.toISOString().slice(0, 16).replace("T", " ");
  } catch {
    return iso;
  }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

type GroupingDto = {
  id: string;
  tenantId: string;
  groupName: string;
  code: string;
  startDate: string;
  endDate: string;
  priority: number;
  active: boolean;
  tenantEnabled: boolean;
  maxPerTenant: number | null;
  deliveryMode: string;
  limitSelectedDistributionCenter: boolean;
  stockLocation: string;
  createdAt: string;
  updatedAt: string;
};

type SlotDto = {
  id: string;
  tenantId: string;
  groupingId: string;
  name: string;
  code: string;
  mode: string;
  startingDate: string;
  endingDate: string;
  price: string;
  updatedAt: string;
};

type CollectionDto = {
  id: string;
  tenantId: string;
  name: string;
  brandCodes: string[];
  address1?: string | null;
  address2?: string | null;
  address3?: string | null;
  postalCode?: string | null;
  country?: string | null;
  geoLat?: string | null;
  geoLng?: string | null;
  desktopImageSrc?: string | null;
  desktopImageName?: string | null;
  mobileImageSrc?: string | null;
  mobileImageName?: string | null;
  active: boolean;
  openingHours?: string | null;
  closingHours?: string | null;
  createdAt: string;
  updatedAt: string;
};

type PartnerDto = {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  enabled: boolean;
  integratedLogistic: boolean;
  createdAt: string;
  updatedAt: string;
};

const DELIVERY: FulfillmentDeliveryMode[] = ["Store Collection", "Local Delivery", "Overseas Delivery"];

function asDeliveryMode(m: string): FulfillmentDeliveryMode {
  return DELIVERY.includes(m as FulfillmentDeliveryMode) ? (m as FulfillmentDeliveryMode) : "Local Delivery";
}

export function groupingFromDto(d: GroupingDto): FulfillmentGrouping {
  return {
    id: d.id,
    groupName: d.groupName,
    code: d.code,
    startDate: d.startDate,
    endDate: d.endDate,
    priority: d.priority,
    active: d.active,
    tenantEnabled: d.tenantEnabled,
    maxPerTenant: d.maxPerTenant ?? "",
    deliveryMode: asDeliveryMode(d.deliveryMode),
    limitSelectedDistributionCenter: d.limitSelectedDistributionCenter,
    stockLocation: d.stockLocation ?? "",
  };
}

export function groupingToPayload(g: Omit<FulfillmentGrouping, "id">): Record<string, unknown> {
  const startDate = normalizeFulfillmentDate(g.startDate);
  let endDate = normalizeFulfillmentDate(g.endDate);
  if (!endDate) endDate = startDate || "2099-12-31";
  const start = startDate || "2026-01-01";
  return {
    groupName: g.groupName,
    code: g.code.trim().toUpperCase(),
    startDate: start,
    endDate: endDate,
    priority: g.priority,
    active: g.active,
    tenantEnabled: g.tenantEnabled,
    maxPerTenant: g.tenantEnabled && g.maxPerTenant !== "" ? g.maxPerTenant : null,
    deliveryMode: g.deliveryMode,
    limitSelectedDistributionCenter: g.limitSelectedDistributionCenter,
    stockLocation: g.limitSelectedDistributionCenter ? g.stockLocation : "",
  };
}

export function slotFromDto(d: SlotDto): FulfillmentSlot {
  return {
    id: d.id,
    groupingId: d.groupingId,
    name: d.name,
    code: d.code,
    mode: asDeliveryMode(d.mode),
    startingDate: d.startingDate,
    endingDate: d.endingDate,
    price: d.price,
    updatedAt: formatSlotUpdatedAt(d.updatedAt),
  };
}

export function slotToPayload(s: {
  name: string;
  code: string;
  mode: FulfillmentDeliveryMode;
  startingDate: string;
  endingDate: string;
  price?: string;
  groupingId?: string;
}): Record<string, unknown> {
  let start = normalizeFulfillmentDate(s.startingDate);
  let end = normalizeFulfillmentDate(s.endingDate);
  if (!start) start = "2026-01-01";
  if (!end) end = start;
  return {
    name: s.name,
    code: s.code.trim().toUpperCase(),
    mode: s.mode,
    startingDate: start,
    endingDate: end,
    price: s.price ?? "",
  };
}

export function collectionFromDto(d: CollectionDto): CollectionLocation {
  return {
    id: d.id,
    name: d.name,
    brandCodes: d.brandCodes ?? [],
    address1: d.address1 ?? undefined,
    address2: d.address2 ?? undefined,
    address3: d.address3 ?? undefined,
    postalCode: d.postalCode ?? undefined,
    country: d.country ?? undefined,
    geoLat: d.geoLat ?? undefined,
    geoLng: d.geoLng ?? undefined,
    desktopImageSrc: d.desktopImageSrc ?? undefined,
    desktopImageName: d.desktopImageName ?? undefined,
    mobileImageSrc: d.mobileImageSrc ?? undefined,
    mobileImageName: d.mobileImageName ?? undefined,
    active: d.active,
    openingHours: d.openingHours ?? undefined,
    closingHours: d.closingHours ?? undefined,
  };
}

export function collectionToPayload(
  c: Omit<CollectionLocation, "id"> & { id?: string }
): Record<string, unknown> {
  return {
    name: c.name,
    brandCodes: c.brandCodes ?? [],
    address1: c.address1 ?? null,
    address2: c.address2 ?? null,
    address3: c.address3 ?? null,
    postalCode: c.postalCode ?? null,
    country: c.country ?? null,
    geoLat: c.geoLat ?? null,
    geoLng: c.geoLng ?? null,
    desktopImageSrc: c.desktopImageSrc ?? null,
    desktopImageName: c.desktopImageName ?? null,
    mobileImageSrc: c.mobileImageSrc ?? null,
    mobileImageName: c.mobileImageName ?? null,
    active: c.active,
    openingHours: c.openingHours ?? null,
    closingHours: c.closingHours ?? null,
  };
}

export function partnerFromDto(d: PartnerDto): LogisticPartner {
  return {
    id: d.id,
    name: d.name,
    code: d.code,
    enabled: d.enabled,
    integratedLogistic: d.integratedLogistic,
  };
}

// ── Groupings ────────────────────────────────────────────────────────────────

export async function fetchFulfillmentGroupings(tenantId: string, token?: string): Promise<FulfillmentGrouping[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings?pageSize=200`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  const items: GroupingDto[] = Array.isArray(body.data) ? body.data : [];
  return items.map(groupingFromDto);
}

export async function createFulfillmentGrouping(
  tenantId: string,
  payload: Omit<FulfillmentGrouping, "id">,
  token?: string
): Promise<FulfillmentGrouping> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(groupingToPayload(payload)),
  });
  await checkOk(res);
  const d = (await res.json()).data as GroupingDto;
  return groupingFromDto(d);
}

export async function updateFulfillmentGrouping(
  tenantId: string,
  id: string,
  payload: Omit<FulfillmentGrouping, "id">,
  token?: string
): Promise<FulfillmentGrouping> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings/${id}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(groupingToPayload(payload)),
  });
  await checkOk(res);
  const d = (await res.json()).data as GroupingDto;
  return groupingFromDto(d);
}

export async function deleteFulfillmentGrouping(tenantId: string, id: string, token?: string): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings/${id}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}

// ── Slots ────────────────────────────────────────────────────────────────────

export async function fetchFulfillmentSlots(
  tenantId: string,
  groupingId: string,
  token?: string
): Promise<FulfillmentSlot[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings/${groupingId}/slots`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  const items: SlotDto[] = Array.isArray(body.data) ? body.data : [];
  return items.map(slotFromDto);
}

export async function createFulfillmentSlot(
  tenantId: string,
  groupingId: string,
  slot: Omit<FulfillmentSlot, "id" | "updatedAt">,
  token?: string
): Promise<FulfillmentSlot> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings/${groupingId}/slots`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(slotToPayload(slot)),
  });
  await checkOk(res);
  const d = (await res.json()).data as SlotDto;
  return slotFromDto(d);
}

export async function updateFulfillmentSlot(
  tenantId: string,
  groupingId: string,
  slotId: string,
  slot: Omit<FulfillmentSlot, "id" | "groupingId" | "updatedAt">,
  token?: string
): Promise<FulfillmentSlot> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings/${groupingId}/slots/${slotId}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(slotToPayload({ ...slot, groupingId })),
  });
  await checkOk(res);
  const d = (await res.json()).data as SlotDto;
  return slotFromDto(d);
}

export async function deleteFulfillmentSlot(
  tenantId: string,
  groupingId: string,
  slotId: string,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/groupings/${groupingId}/slots/${slotId}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}

// ── Collection locations ─────────────────────────────────────────────────────

export async function fetchCollectionLocations(tenantId: string, token?: string): Promise<CollectionLocation[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/collection-locations?pageSize=200`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  const items: CollectionDto[] = Array.isArray(body.data) ? body.data : [];
  return items.map(collectionFromDto);
}

export async function createCollectionLocationApi(
  tenantId: string,
  loc: CollectionLocation,
  token?: string
): Promise<CollectionLocation> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/collection-locations`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(collectionToPayload(loc)),
  });
  await checkOk(res);
  const d = (await res.json()).data as CollectionDto;
  return collectionFromDto(d);
}

export async function updateCollectionLocationApi(
  tenantId: string,
  id: string,
  loc: CollectionLocation,
  token?: string
): Promise<CollectionLocation> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/collection-locations/${id}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(collectionToPayload(loc)),
  });
  await checkOk(res);
  const d = (await res.json()).data as CollectionDto;
  return collectionFromDto(d);
}

export async function deleteCollectionLocationApi(tenantId: string, id: string, token?: string): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/collection-locations/${id}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}

// ── Logistic partners ────────────────────────────────────────────────────────

export async function fetchLogisticPartnersApi(tenantId: string, token?: string): Promise<LogisticPartner[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/logistic-partners?pageSize=200`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  const items: PartnerDto[] = Array.isArray(body.data) ? body.data : [];
  return items.map(partnerFromDto);
}

export async function createLogisticPartnerApi(
  tenantId: string,
  p: Omit<LogisticPartner, "id">,
  token?: string
): Promise<LogisticPartner> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/logistic-partners`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify({
      name: p.name,
      code: p.code.trim().toUpperCase(),
      enabled: p.enabled,
      integratedLogistic: p.integratedLogistic,
    }),
  });
  await checkOk(res);
  const d = (await res.json()).data as PartnerDto;
  return partnerFromDto(d);
}

export async function updateLogisticPartnerApi(
  tenantId: string,
  id: string,
  p: Omit<LogisticPartner, "id">,
  token?: string
): Promise<LogisticPartner> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/logistic-partners/${id}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify({
      name: p.name,
      code: p.code.trim().toUpperCase(),
      enabled: p.enabled,
      integratedLogistic: p.integratedLogistic,
    }),
  });
  await checkOk(res);
  const d = (await res.json()).data as PartnerDto;
  return partnerFromDto(d);
}

export async function deleteLogisticPartnerApi(tenantId: string, id: string, token?: string): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/logistic-partners/${id}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}

// ── Settings (predefined + dynamic + stock locations JSON) ─────────────────

export async function fetchFulfillmentSettings(
  tenantId: string,
  token?: string
): Promise<{
  predefinedFields: FulfillmentPredefinedFieldSetting[];
  dynamicFields: FulfillmentDynamicField[];
  stockLocations: StockLocation[];
}> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/settings`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const data = (await res.json()).data as {
    predefinedFields: unknown;
    dynamicFields: unknown;
    stockLocations: unknown;
  };
  const safeArr = <T>(u: unknown, fallback: T[]): T[] => (Array.isArray(u) ? (u as T[]) : fallback);
  return {
    predefinedFields: safeArr<FulfillmentPredefinedFieldSetting>(data.predefinedFields, []),
    dynamicFields: safeArr<FulfillmentDynamicField>(data.dynamicFields, []),
    stockLocations: safeArr<StockLocation>(data.stockLocations, []),
  };
}

export async function putFulfillmentSettings(
  tenantId: string,
  payload: {
    predefinedFields: FulfillmentPredefinedFieldSetting[];
    dynamicFields: FulfillmentDynamicField[];
    stockLocations: StockLocation[];
  },
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/fulfillment/settings`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify({
      predefinedFields: payload.predefinedFields,
      dynamicFields: payload.dynamicFields,
      stockLocations: payload.stockLocations,
    }),
  });
  await checkOk(res);
}
