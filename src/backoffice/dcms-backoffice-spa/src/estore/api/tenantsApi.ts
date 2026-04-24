/**
 * DAI-669: Tenants API — Umbraco backoffice `DcmsTenantController` (DAI-668 `dcms_tenants`).
 * Base: /umbraco/dcms/api/tenants
 */

export type TenantRow = {
  id: string;
  code: string;
  name: string;
  contactName: string;
  contactEmail: string;
  brandCount: number;
  active: boolean;
};

export type TenantUpsertPayload = {
  code: string;
  name: string;
  contactName?: string;
  contactEmail?: string;
  brandCount?: number;
  active?: boolean;
};

type ApiEnvelope<T> = {
  data: T;
  meta: unknown;
  error: { code?: string; message?: string } | null;
};

const BASE = "/umbraco/dcms/api/tenants";

function headers(token?: string): HeadersInit {
  const h: Record<string, string> = { Accept: "application/json", "Content-Type": "application/json" };
  if (token) h.Authorization = `Bearer ${token}`;
  return h;
}

async function checkOk(res: Response): Promise<void> {
  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try {
      const body = (await res.json()) as { error?: { message?: string } };
      if (body?.error?.message) msg = body.error.message;
    } catch {
      /* ignore */
    }
    throw new Error(msg);
  }
}

function mapTenant(raw: Record<string, unknown>): TenantRow {
  return {
    id: String(raw.id ?? ""),
    code: String(raw.code ?? ""),
    name: String(raw.name ?? ""),
    contactName: String(raw.contactName ?? ""),
    contactEmail: String(raw.contactEmail ?? ""),
    brandCount: typeof raw.brandCount === "number" ? raw.brandCount : Number(raw.brandCount) || 0,
    active: Boolean(raw.active),
  };
}

export async function fetchTenants(token?: string): Promise<TenantRow[]> {
  const res = await fetch(BASE, { credentials: "include", headers: headers(token) });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>[]>;
  const data = body.data ?? [];
  return data.map((row) => mapTenant(row));
}

export async function createTenant(payload: TenantUpsertPayload, token?: string): Promise<TenantRow> {
  const res = await fetch(BASE, {
    method: "POST",
    credentials: "include",
    headers: headers(token),
    body: JSON.stringify({
      code: payload.code,
      name: payload.name,
      contactName: payload.contactName ?? "",
      contactEmail: payload.contactEmail ?? "",
      brandCount: payload.brandCount ?? 0,
    }),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return mapTenant(body.data as Record<string, unknown>);
}

export async function updateTenant(id: string, payload: TenantUpsertPayload, token?: string): Promise<TenantRow> {
  const res = await fetch(`${BASE}/${encodeURIComponent(id)}`, {
    method: "PUT",
    credentials: "include",
    headers: headers(token),
    body: JSON.stringify({
      code: payload.code,
      name: payload.name,
      contactName: payload.contactName ?? "",
      contactEmail: payload.contactEmail ?? "",
      brandCount: payload.brandCount ?? 0,
      active: payload.active ?? true,
    }),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return mapTenant(body.data as Record<string, unknown>);
}

/** Soft-deactivate tenant (`active = 0`) — matches controller DELETE. */
export async function archiveTenant(id: string, token?: string): Promise<void> {
  const res = await fetch(`${BASE}/${encodeURIComponent(id)}`, {
    method: "DELETE",
    credentials: "include",
    headers: headers(token),
  });
  await checkOk(res);
}

/** Client-side validation aligned with `DcmsTenantController` / C# code rules. */
export const TENANT_CODE_PATTERN = /^[A-Za-z0-9][A-Za-z0-9_-]{0,18}$/;

export function validateTenantCode(code: string): string | null {
  const t = code.trim();
  if (!t) return "Code is required.";
  if (!TENANT_CODE_PATTERN.test(t)) return "Code must be 1–20 characters: letters, digits, underscore or hyphen.";
  return null;
}
