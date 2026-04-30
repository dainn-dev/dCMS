// DAI-752 (US-5) — minimal client for dCMS.Identity.Api.
// Auth tokens are stored in sessionStorage so a browser tab is the natural session boundary.

const TOKEN_KEY = "dcms.session.token";
const CLAIMS_KEY = "dcms.session.claims";
const IMPERSONATION_KEY = "dcms.session.impersonation";

const IDENTITY_BASE = "/api/v1/identity";
const ADMIN_BASE = "/api/v1/admin";

export type Role =
  | "SuperAdmin"
  | "ClientAdmin"
  | "TenantAdmin"
  | "ChainAdmin"
  | "BrandManager"
  | "StoreManager"
  | "StoreStaff"
  | "CustomerSupport"
  | "Customer";

export interface SessionClaims {
  userId: string;
  displayName?: string;
  email?: string;
  role: Role;
  tenantId?: string | null;
  clientId?: string;
  impersonatedBy?: string | null;
  originalRole?: Role | null;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  role: Role;
  tenantId: string | null;
  displayName: string;
  userId: string;
}

export interface StartImpersonationResponse {
  accessToken: string;
  expiresAt: string;
  jti: string;
  targetTenantId: string;
}

interface Envelope<T> {
  data: T | null;
  meta: unknown;
  error: { code: string; message: string } | null;
}

async function callJson<T>(path: string, init: RequestInit): Promise<T> {
  const res = await fetch(path, {
    credentials: "same-origin",
    ...init,
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
      ...(init.headers ?? {}),
    },
  });
  const body = (await res.json().catch(() => null)) as Envelope<T> | null;
  if (!res.ok || !body || body.error) {
    const code = body?.error?.code ?? `http_${res.status}`;
    const message = body?.error?.message ?? res.statusText;
    throw Object.assign(new Error(`${code}: ${message}`), { code });
  }
  if (body.data == null)
    throw new Error("identity: empty response body");
  return body.data;
}

function authHeader(): Record<string, string> {
  const token = sessionStorage.getItem(TOKEN_KEY);
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  const data = await callJson<LoginResponse>(`${IDENTITY_BASE}/login`, {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  sessionStorage.setItem(TOKEN_KEY, data.accessToken);
  sessionStorage.removeItem(IMPERSONATION_KEY);
  // Eagerly hydrate claims so consumers don't need a second round-trip.
  const claims = await fetchMe();
  sessionStorage.setItem(CLAIMS_KEY, JSON.stringify(claims));
  return data;
}

export async function fetchMe(): Promise<SessionClaims> {
  return callJson<SessionClaims>(`${IDENTITY_BASE}/me`, { method: "GET", headers: authHeader() });
}

export async function startImpersonation(targetTenantId: string, reason: string, assumedRole?: Role): Promise<StartImpersonationResponse> {
  const data = await callJson<StartImpersonationResponse>(`${ADMIN_BASE}/impersonate`, {
    method: "POST",
    headers: authHeader(),
    body: JSON.stringify({ targetTenantId, reason, assumedRole }),
  });
  // Stash the original-session pieces so we can restore on end-impersonation.
  const previous = sessionStorage.getItem(TOKEN_KEY);
  const previousClaims = sessionStorage.getItem(CLAIMS_KEY);
  if (previous && previousClaims)
    sessionStorage.setItem(IMPERSONATION_KEY, JSON.stringify({ previousToken: previous, previousClaims, jti: data.jti }));
  sessionStorage.setItem(TOKEN_KEY, data.accessToken);
  const claims = await fetchMe();
  sessionStorage.setItem(CLAIMS_KEY, JSON.stringify(claims));
  return data;
}

export async function endImpersonation(): Promise<void> {
  const stash = sessionStorage.getItem(IMPERSONATION_KEY);
  if (!stash) return;
  const { previousToken, previousClaims, jti } = JSON.parse(stash) as { previousToken: string; previousClaims: string; jti: string };
  // Use the impersonation token (current) to call end — server only requires the jti.
  try {
    await callJson<{ ended: boolean; jti: string }>(`${ADMIN_BASE}/impersonate:end`, {
      method: "POST",
      headers: authHeader(),
      body: JSON.stringify({ jti }),
    });
  } finally {
    sessionStorage.setItem(TOKEN_KEY, previousToken);
    sessionStorage.setItem(CLAIMS_KEY, previousClaims);
    sessionStorage.removeItem(IMPERSONATION_KEY);
  }
}

export function readClaims(): SessionClaims | null {
  const raw = sessionStorage.getItem(CLAIMS_KEY);
  if (!raw) return null;
  try { return JSON.parse(raw) as SessionClaims; } catch { return null; }
}

export function isImpersonating(): boolean {
  return sessionStorage.getItem(IMPERSONATION_KEY) !== null;
}

export function getToken(): string | null {
  return sessionStorage.getItem(TOKEN_KEY);
}

export function logout() {
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(CLAIMS_KEY);
  sessionStorage.removeItem(IMPERSONATION_KEY);
}
