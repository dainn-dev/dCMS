/**
 * DAI-578 / DAI-584: Brand CRUD API client.
 * Routes through dCMS.Gateway (YARP) — /gateway/v1/catalog/...
 *
 * Auth: passes Bearer token when provided (sourced from Umbraco auth context).
 * Falls back gracefully when the API is unreachable (dev without backend).
 */

import type { BrandListRow } from "../brands-columns";
import { GATEWAY } from "./gatewayConfig";

const CATALOG_BASE = GATEWAY.catalog;

export type BrandPayload = {
  name: string;
  active: boolean;
  imageUrl?: string;
  imageAlt?: string;
  /** Raw JSON object string — additional fields from BrandConfigPage. */
  additionalInfo?: string;
};

type BrandDto = {
  tenantId: string;
  code: string;
  name: string;
  active: boolean;
  imageUrl: string;
  imageAlt: string;
  additionalInfo: string;
  createdAt: string;
  updatedAt: string;
};

type ApiMeta = { total: number; page: number; pageSize: number };
type ApiResponse<T> = { data: T; meta: ApiMeta | null; error: null };

function toRow(dto: BrandDto): BrandListRow {
  return {
    code:     dto.code,
    name:     dto.name,
    active:   dto.active,
    imageSrc: dto.imageUrl ?? "",
    imageAlt: dto.imageAlt ?? "",
    additionalInfo: dto.additionalInfo ?? "{}",
  };
}

function headers(token?: string): Record<string, string> {
  const h: Record<string, string> = {
    "Content-Type": "application/json",
    Accept: "application/json",
  };
  if (token) h["Authorization"] = `Bearer ${token}`;
  return h;
}

async function checkOk(res: Response): Promise<void> {
  if (!res.ok) {
    let errMsg = `HTTP ${res.status}`;
    try {
      const body = await res.json();
      if (body?.error?.message) errMsg = body.error.message;
    } catch {
      // ignore parse error
    }
    throw new Error(errMsg);
  }
}

// ── List ─────────────────────────────────────────────────────────────────────

export async function fetchBrands(
  tenantId: string,
  options?: { active?: boolean; search?: string; page?: number; pageSize?: number },
  token?: string
): Promise<{ rows: BrandListRow[]; total: number }> {
  const params = new URLSearchParams();
  if (options?.active !== undefined) params.set("active", String(options.active));
  if (options?.search) params.set("search", options.search);
  if (options?.page) params.set("page", String(options.page));
  if (options?.pageSize) params.set("pageSize", String(options.pageSize));

  const qs = params.toString() ? `?${params.toString()}` : "";
  const res = await fetch(
    `${CATALOG_BASE}/tenants/${tenantId}/brands${qs}`,
    { credentials: "same-origin", headers: headers(token) }
  );
  await checkOk(res);
  const body: ApiResponse<BrandDto[]> = await res.json();
  return {
    rows:  (body.data ?? []).map(toRow),
    total: body.meta?.total ?? 0,
  };
}

// ── Create ────────────────────────────────────────────────────────────────────

export async function createBrand(
  tenantId: string,
  code: string,
  payload: BrandPayload,
  token?: string
): Promise<BrandListRow> {
  const res = await fetch(
    `${CATALOG_BASE}/tenants/${tenantId}/brands`,
    {
      method: "POST",
      credentials: "same-origin",
      headers: headers(token),
      body: JSON.stringify({ code, ...payload }),
    }
  );
  await checkOk(res);
  const body: ApiResponse<BrandDto> = await res.json();
  return toRow(body.data);
}

// ── Update ────────────────────────────────────────────────────────────────────

export async function updateBrand(
  tenantId: string,
  code: string,
  payload: BrandPayload,
  token?: string
): Promise<BrandListRow> {
  const res = await fetch(
    `${CATALOG_BASE}/tenants/${tenantId}/brands/${encodeURIComponent(code)}`,
    {
      method: "PUT",
      credentials: "same-origin",
      headers: headers(token),
      body: JSON.stringify(payload),
    }
  );
  await checkOk(res);
  const body: ApiResponse<BrandDto> = await res.json();
  return toRow(body.data);
}

// ── Delete ────────────────────────────────────────────────────────────────────

export async function deleteBrand(
  tenantId: string,
  code: string,
  token?: string
): Promise<void> {
  const res = await fetch(
    `${CATALOG_BASE}/tenants/${tenantId}/brands/${encodeURIComponent(code)}`,
    {
      method: "DELETE",
      credentials: "same-origin",
      headers: headers(token),
    }
  );
  await checkOk(res);
}
