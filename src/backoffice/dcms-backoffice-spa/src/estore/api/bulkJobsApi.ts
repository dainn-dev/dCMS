/**
 * Async bulk jobs (catalog CSV import, order export) — DAI-684. Same-origin; uses Umbraco backoffice cookie.
 * Base: /umbraco/dcms/api/bulk-jobs
 */

const BASE = "/umbraco/dcms/api/bulk-jobs";

export type BulkJobRow = {
  id: string;
  tenantId: string;
  storeId: string | null;
  jobKind: string;
  requestedByUserId: number;
  hangfireJobId: string | null;
  status: string;
  progressProcessed: number;
  progressTotal: number;
  progressPercent: number;
  inputBlobRef: string | null;
  outputBlobRef: string | null;
  errorMessage: string | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  cancelRequestedAt: string | null;
};

async function parse<T>(r: Response): Promise<T> {
  const j = (await r.json()) as { data?: T; error?: { message?: string } };
  if (!r.ok) {
    const msg = j.error?.message ?? r.statusText;
    throw new Error(msg);
  }
  return j.data as T;
}

export async function listBulkJobs(limit = 50): Promise<BulkJobRow[]> {
  const r = await fetch(`${BASE}?limit=${encodeURIComponent(String(limit))}`, { credentials: "include" });
  return parse<BulkJobRow[]>(r);
}

export async function startCatalogImport(file: File, storeId?: string): Promise<{ jobId: string; hangfireJobId: string }> {
  const fd = new FormData();
  fd.append("file", file, file.name);
  if (storeId) fd.append("storeId", storeId);
  const r = await fetch(`${BASE}/catalog-import`, { method: "POST", body: fd, credentials: "include" });
  return parse<{ jobId: string; hangfireJobId: string }>(r);
}

export async function startOrdersExport(body: { dateFrom: string; dateTo: string; storeId?: string }): Promise<{
  jobId: string;
  hangfireJobId: string;
}> {
  const r = await fetch(`${BASE}/orders-export`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      dateFrom: body.dateFrom,
      dateTo: body.dateTo,
      storeId: body.storeId ?? "",
    }),
    credentials: "include",
  });
  return parse<{ jobId: string; hangfireJobId: string }>(r);
}

export async function cancelBulkJob(id: string): Promise<void> {
  const r = await fetch(`${BASE}/${encodeURIComponent(id)}/cancel`, { method: "POST", credentials: "include" });
  await parse<unknown>(r);
}

export async function retryBulkJob(id: string): Promise<{ jobId: string; hangfireJobId: string }> {
  const r = await fetch(`${BASE}/${encodeURIComponent(id)}/retry`, { method: "POST", credentials: "include" });
  return parse<{ jobId: string; hangfireJobId: string }>(r);
}

export function downloadBulkExportUrl(id: string): string {
  return `${BASE}/${encodeURIComponent(id)}/download`;
}
