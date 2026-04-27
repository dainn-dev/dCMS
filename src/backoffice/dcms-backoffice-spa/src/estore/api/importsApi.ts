/**
 * DAI-684 / DAI-708 — bulk import API client.
 * Routes through dCMS.Gateway → /gateway/v1/catalog/tenants/{tenantId}/imports
 */

import { GATEWAY } from "./gatewayConfig";

const BASE = GATEWAY.catalog;

export type ImportType = "products" | "product-images" | "inventory" | "promo-codes";

export type ImportStatus =
  | "Pending"
  | "Running"
  | "Completed"
  | "Failed"
  | "PartiallyCompleted";

export type ImportRowError = {
  rowIndex: number;
  key: string;
  message: string;
};

export type ImportJob = {
  id: string;
  type: ImportType;
  status: ImportStatus;
  total: number | null;
  processed: number;
  errors: ImportRowError[];
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
};

async function parseEnvelope<T>(r: Response): Promise<T> {
  const j = (await r.json()) as { data?: T; error?: { message?: string } };
  if (!r.ok) throw new Error(j?.error?.message ?? `HTTP ${r.status}`);
  return j.data as T;
}

export async function uploadImport(
  tenantId: string,
  type: ImportType,
  file: File,
  signal?: AbortSignal,
): Promise<{ jobId: string; statusUrl: string; type: ImportType; status: ImportStatus }> {
  const form = new FormData();
  form.append("type", type);
  form.append("file", file, file.name);
  const res = await fetch(`${BASE}/tenants/${tenantId}/imports`, {
    method: "POST",
    credentials: "include",
    body: form,
    signal,
  });
  return parseEnvelope(res);
}

export async function getImportJob(
  tenantId: string,
  jobId: string,
  signal?: AbortSignal,
): Promise<ImportJob> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/imports/${jobId}`, {
    credentials: "include",
    signal,
  });
  return parseEnvelope(res);
}

export async function listImportJobs(
  tenantId: string,
  take = 50,
  signal?: AbortSignal,
): Promise<{ items: ImportJob[] }> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/imports?take=${take}`, {
    credentials: "include",
    signal,
  });
  return parseEnvelope(res);
}

export function isTerminal(status: ImportStatus): boolean {
  return status === "Completed" || status === "Failed" || status === "PartiallyCompleted";
}
