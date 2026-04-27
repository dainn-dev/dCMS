import { GATEWAY } from "./gatewayConfig";

const BASE = GATEWAY.notifications;

type ApiEnvelope<TData, TMeta = unknown> = {
  data: TData;
  meta: TMeta | null;
  error: { code?: string; message?: string } | null;
};

function headers(tenantId: string, storeId?: string, token?: string): Record<string, string> {
  const h: Record<string, string> = {
    Accept: "application/json",
    "Content-Type": "application/json",
    "X-Tenant-Id": tenantId,
  };
  if (storeId) h["X-Store-Id"] = storeId;
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
      // ignore
    }
    throw new Error(errMsg);
  }
}

export type TemplateRow = {
  id: string;
  tenantId: string | null;
  key: string;
  locale: string;
  channel: "email" | "sms" | "print" | "admin";
  subject: string | null;
  body: string;
  modelVersion: number;
  updatedAt: string;
  updatedBy: string | null;
};

export async function listTemplates(tenantId: string, storeId?: string, token?: string): Promise<TemplateRow[]> {
  const res = await fetch(`${BASE}/templates`, { credentials: "same-origin", headers: headers(tenantId, storeId, token) });
  await checkOk(res);
  const body: ApiEnvelope<TemplateRow[]> = await res.json();
  return body.data ?? [];
}

export async function putTemplate(
  tenantId: string,
  storeId: string | undefined,
  input: {
    id?: string;
    key: string;
    locale: string;
    channel: TemplateRow["channel"];
    subject?: string | null;
    body: string;
    modelVersion?: number;
  },
  token?: string,
): Promise<void> {
  const res = await fetch(`${BASE}/templates`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(tenantId, storeId, token),
    body: JSON.stringify(input),
  });
  await checkOk(res);
}

export async function previewTemplate(
  tenantId: string,
  storeId: string | undefined,
  payload: unknown,
  token?: string,
): Promise<{ subject: string; body: unknown }> {
  const res = await fetch(`${BASE}/templates/preview`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(tenantId, storeId, token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body: ApiEnvelope<{ subject: string; body: unknown }> = await res.json();
  return body.data ?? { subject: "(preview)", body: payload };
}

