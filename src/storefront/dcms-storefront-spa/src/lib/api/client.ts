// DAI-751 / US-4 (T4.3) — storefront fetch interceptor.
// Injects X-Active-Tenant on gateway catalog storefront routes after branch selection.

import { GATEWAY } from "./gateway";

const ACTIVE_TENANT_STORAGE_KEY = "dcms.active-tenant";
const ACTIVE_TENANT_HEADER = "X-Active-Tenant";

export const STOREFRONT_PATH_PREFIX = `${GATEWAY.catalog}/storefront/`;
export const STOREFRONT_BOOTSTRAP_PREFIX = `${GATEWAY.catalog}/storefront/branches`;

export function readActiveTenant(): string | null {
  try {
    return localStorage.getItem(ACTIVE_TENANT_STORAGE_KEY);
  } catch {
    return null;
  }
}

export function writeActiveTenant(tenantId: string): void {
  try {
    localStorage.setItem(ACTIVE_TENANT_STORAGE_KEY, tenantId);
  } catch {
    /* private mode — best effort */
  }
}

export function clearActiveTenant(): void {
  try {
    localStorage.removeItem(ACTIVE_TENANT_STORAGE_KEY);
  } catch {
    /* ignore */
  }
}

interface Envelope<T> {
  data: T | null;
  meta: unknown;
  error: { code: string; message: string } | null;
}

export class StorefrontApiError extends Error {
  constructor(public readonly code: string, message: string, public readonly status: number) {
    super(`${code}: ${message}`);
  }
}

function isStorefrontPath(input: RequestInfo | URL): boolean {
  const url =
    typeof input === "string" ? input :
    input instanceof URL       ? input.pathname :
                                  input.url;
  try {
    const path = url.startsWith("http") ? new URL(url).pathname : url;
    return path.startsWith(STOREFRONT_PATH_PREFIX);
  } catch {
    return false;
  }
}

function isBootstrapPath(input: RequestInfo | URL): boolean {
  const url =
    typeof input === "string" ? input :
    input instanceof URL       ? input.pathname :
                                  input.url;
  try {
    const path = url.startsWith("http") ? new URL(url).pathname : url;
    return path.startsWith(STOREFRONT_BOOTSTRAP_PREFIX);
  } catch {
    return false;
  }
}

export async function storefrontFetch(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers ?? {});
  if (isStorefrontPath(input) && !isBootstrapPath(input)) {
    const tenant = readActiveTenant();
    if (tenant && !headers.has(ACTIVE_TENANT_HEADER))
      headers.set(ACTIVE_TENANT_HEADER, tenant);
  }
  return fetch(input, { ...init, headers });
}

export async function callStorefrontJson<T>(input: RequestInfo | URL, init: RequestInit = {}): Promise<T> {
  const res = await storefrontFetch(input, {
    ...init,
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...(init.headers ?? {}),
    },
  });
  const env = (await res.json().catch(() => null)) as Envelope<T> | null;
  if (!res.ok || !env || env.error) {
    const code = env?.error?.code ?? `http_${res.status}`;
    const message = env?.error?.message ?? res.statusText;
    throw new StorefrontApiError(code, message, res.status);
  }
  if (env.data == null)
    throw new StorefrontApiError("empty_body", "Storefront API returned an empty body.", res.status);
  return env.data;
}

export interface Branch {
  id: string;
  tenantId: string;
  name: string;
  address?: string | null;
  lat?: number | null;
  lng?: number | null;
  isDefault?: boolean;
}

export interface NearestBranch extends Branch {
  distanceKm: number | null;
  fallback: boolean;
}

export function listBranches(): Promise<Branch[]> {
  return callStorefrontJson<Branch[]>(`${GATEWAY.catalog}/storefront/branches`);
}

export function findNearestBranch(lat: number, lng: number, maxKm = 10): Promise<NearestBranch> {
  const qs = new URLSearchParams({ lat: String(lat), lng: String(lng), maxKm: String(maxKm) });
  return callStorefrontJson<NearestBranch>(`${GATEWAY.catalog}/storefront/branches/nearest?${qs}`);
}
