/**
 * DAI-597: Attribute management API client.
 * All calls route through dCMS.Gateway → /gateway/v1/catalog/tenants/{tenantId}/attributes
 */

import { GATEWAY } from "./gatewayConfig";
import type { AttributeListRow, AttributeType } from "../attributes-columns";

const BASE = GATEWAY.catalog;

export type AttributeValueRow = {
  id: number;
  attributeId: number;
  name: string;
  code: string;
  colorHex: string;
  imageUrl: string;
  sortOrder: number;
  createdAt: string;
};

export type AttributePayload = {
  name: string;
  code: string;
  type?: AttributeType;
  required?: boolean;
  description?: string;
  sortOrder?: number;
};

export type ValuePayload = {
  name: string;
  code?: string;
  colorHex?: string;
  imageUrl?: string;
  sortOrder?: number;
};

type AttributeDto = {
  id: number;
  tenantId: string;
  name: string;
  code: string;
  type: string;
  required: boolean;
  description: string;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
  values?: AttributeValueRow[];
};

function headers(token?: string): Record<string, string> {
  const h: Record<string, string> = { "Content-Type": "application/json", Accept: "application/json" };
  if (token) h["Authorization"] = `Bearer ${token}`;
  return h;
}

async function checkOk(res: Response): Promise<void> {
  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try { const b = await res.json(); if (b?.error?.message) msg = b.error.message; } catch {}
    throw new Error(msg);
  }
}

function dtoToRow(dto: AttributeDto, seq: string): AttributeListRow {
  const validTypes: AttributeType[] = ["TEXT", "COLOR", "IMAGE", "SELECT", "BOOLEAN"];
  const type = validTypes.includes(dto.type as AttributeType)
    ? (dto.type as AttributeType)
    : "TEXT";
  return { seq, name: dto.name, code: dto.code, type, required: dto.required, id: dto.id };
}

/** GET /attributes?page=1&pageSize=200 */
export async function fetchAttributes(
  tenantId: string,
  token?: string
): Promise<AttributeListRow[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes?pageSize=200`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  const items: AttributeDto[] = Array.isArray(body.data) ? body.data : [];
  return items.map((d, i) => dtoToRow(d, String(i + 1).padStart(2, "0")));
}

/** POST /attributes → 201 */
export async function createAttribute(
  tenantId: string,
  payload: AttributePayload,
  token?: string
): Promise<AttributeListRow> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body = await res.json();
  return dtoToRow(body.data as AttributeDto, "00");
}

/** PUT /attributes/{id} */
export async function updateAttribute(
  tenantId: string,
  id: number,
  payload: AttributePayload,
  token?: string
): Promise<AttributeListRow> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes/${id}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body = await res.json();
  return dtoToRow(body.data as AttributeDto, "00");
}

/** DELETE /attributes/{id} */
export async function deleteAttribute(
  tenantId: string,
  id: number,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes/${id}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}

/** GET /attributes/{id}/values */
export async function fetchAttributeValues(
  tenantId: string,
  id: number,
  token?: string
): Promise<AttributeValueRow[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes/${id}/values`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  return Array.isArray(body.data) ? (body.data as AttributeValueRow[]) : [];
}

/** POST /attributes/{id}/values → 201 */
export async function createAttributeValue(
  tenantId: string,
  id: number,
  payload: ValuePayload,
  token?: string
): Promise<AttributeValueRow> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes/${id}/values`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body = await res.json();
  return body.data as AttributeValueRow;
}

/** PUT /attributes/{id}/values/{valueId} */
export async function updateAttributeValue(
  tenantId: string,
  id: number,
  valueId: number,
  payload: ValuePayload,
  token?: string
): Promise<AttributeValueRow> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes/${id}/values/${valueId}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body = await res.json();
  return body.data as AttributeValueRow;
}

/** DELETE /attributes/{id}/values/{valueId} */
export async function deleteAttributeValue(
  tenantId: string,
  id: number,
  valueId: number,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/attributes/${id}/values/${valueId}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}
