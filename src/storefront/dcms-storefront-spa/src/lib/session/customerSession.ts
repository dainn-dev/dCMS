const TOKEN_KEY = "dcms.customer.token";
const EXPIRES_KEY = "dcms.customer.expires";
const DISPLAY_KEY = "dcms.customer.displayName";

export interface CustomerSessionSnapshot {
  token: string;
  expiresAt: string;
  displayName: string;
  customerId: string;
  tenantId: string | null;
  storeId: string | null;
}

function safeGet(key: string): string | null {
  try {
    return sessionStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeSet(key: string, value: string): void {
  try {
    sessionStorage.setItem(key, value);
  } catch {
    /* private mode */
  }
}

function safeRemove(key: string): void {
  try {
    sessionStorage.removeItem(key);
  } catch {
    /* ignore */
  }
}

export function readCustomerToken(): string | null {
  const token = safeGet(TOKEN_KEY);
  const expires = safeGet(EXPIRES_KEY);
  if (!token || !expires) return null;
  if (Date.parse(expires) <= Date.now()) {
    clearCustomerSession();
    return null;
  }
  return token;
}

export function readCustomerSession(): CustomerSessionSnapshot | null {
  const token = readCustomerToken();
  if (!token) return null;
  const claims = decodeJwtPayload(token);
  const sub = typeof claims.sub === "string" ? claims.sub : null;
  if (!sub) return null;
  return {
    token,
    expiresAt: safeGet(EXPIRES_KEY) ?? "",
    displayName: safeGet(DISPLAY_KEY) ?? "",
    customerId: sub,
    tenantId: typeof claims.tenant_id === "string" ? claims.tenant_id : null,
    storeId: typeof claims.store_id === "string" ? claims.store_id : null,
  };
}

export function writeCustomerSession(
  token: string,
  expiresAt: string,
  displayName: string,
): void {
  safeSet(TOKEN_KEY, token);
  safeSet(EXPIRES_KEY, expiresAt);
  safeSet(DISPLAY_KEY, displayName);
}

export function clearCustomerSession(): void {
  safeRemove(TOKEN_KEY);
  safeRemove(EXPIRES_KEY);
  safeRemove(DISPLAY_KEY);
}

function decodeJwtPayload(token: string): Record<string, unknown> {
  const parts = token.split(".");
  if (parts.length < 2) return {};
  try {
    const json = atob(parts[1].replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return {};
  }
}
