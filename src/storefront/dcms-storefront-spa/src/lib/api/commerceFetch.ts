import { readCustomerToken } from "../session/customerSession";

export class CommerceApiError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status: number,
  ) {
    super(`${code}: ${message}`);
  }
}

interface Envelope<T> {
  data: T | null;
  meta: unknown;
  error: { code: string; message: string } | null;
}

export interface CommerceScope {
  tenantId: string;
  storeId: string;
  token?: string | null;
}

export function commerceHeaders(
  scope: CommerceScope,
  extra: Record<string, string> = {},
): HeadersInit {
  const h: Record<string, string> = {
    Accept: "application/json",
    "Content-Type": "application/json",
    "X-Tenant-Id": scope.tenantId,
    "X-Store-Id": scope.storeId,
    ...extra,
  };
  const token = scope.token ?? readCustomerToken();
  if (token) h.Authorization = `Bearer ${token}`;
  return h;
}

export async function commerceFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  return fetch(input, init);
}

export async function callEnvelopeJson<T>(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<{ data: T; meta: unknown }> {
  const res = await commerceFetch(input, init);
  const env = (await res.json().catch(() => null)) as Envelope<T> | null;
  if (!res.ok || !env || env.error) {
    const code = env?.error?.code ?? `http_${res.status}`;
    const message = env?.error?.message ?? res.statusText;
    throw new CommerceApiError(code, message, res.status);
  }
  if (env.data == null)
    throw new CommerceApiError("empty_body", "API returned an empty body.", res.status);
  return { data: env.data, meta: env.meta };
}

export async function callOrderJson<T>(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<T> {
  const res = await commerceFetch(input, init);
  const body = (await res.json().catch(() => null)) as
    | (T & { error?: { code: string; message: string } })
    | null;
  if (!res.ok || !body) {
    const code = body?.error?.code ?? `http_${res.status}`;
    const message = body?.error?.message ?? res.statusText;
    throw new CommerceApiError(code, message, res.status);
  }
  if ("error" in body && body.error)
    throw new CommerceApiError(body.error.code, body.error.message, res.status);
  return body as T;
}
