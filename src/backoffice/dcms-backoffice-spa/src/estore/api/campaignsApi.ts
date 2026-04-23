/**
 * DAI-603: Campaign management API client.
 * All calls route through dCMS.Gateway → /gateway/v1/catalog/tenants/{tenantId}/campaigns
 */

import { GATEWAY } from "./gatewayConfig";
import type { CampaignListRow, CampaignStatus, CampaignChannel, CampaignEditorKind } from "../campaigns-columns";

const BASE = GATEWAY.promotions;

export type CampaignPayload = {
  code: string;
  nameJson?: string;
  editorKind?: string;
  channel?: string;
  startDate?: string | null;
  endDate?: string | null;
  activeDaysJson?: string;
  activeMonthsJson?: string;
  qualifiersJson?: string;
  mechanicsJson?: string;
  promotionDetailsJson?: string;
  budget?: string;
  audience?: string;
};

export type HistoryEntry = {
  id: number;
  campaignId: string;
  actorUserId: string;
  fromState: string;
  toState: string;
  comment: string;
  createdAt: string;
};

type CampaignDto = {
  id: string;
  tenantId: string;
  code: string;
  nameJson: string;
  editorKind: string;
  workflowState: string;
  channel: string;
  startDate: string | null;
  endDate: string | null;
  budget: string;
  audience: string;
  conversions: number;
  createdAt: string;
  updatedAt: string;
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

function parseName(nameJson: string): string {
  try { return (JSON.parse(nameJson) as Record<string, string>).en ?? Object.values(JSON.parse(nameJson))[0] ?? ""; }
  catch { return nameJson; }
}

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  try { return new Date(iso).toLocaleDateString("en-US", { month: "short", day: "2-digit", year: "numeric" }); }
  catch { return iso; }
}

function dtoToRow(d: CampaignDto): CampaignListRow {
  const validStatuses: CampaignStatus[] = ["draft", "scheduled", "active", "paused", "ended"];
  // Map API workflow states → SPA CampaignStatus
  const stateMap: Record<string, CampaignStatus> = {
    draft: "draft", pending_approval: "draft", approved: "scheduled",
    active: "active", deactivated: "paused", archived: "ended", rejected: "draft",
  };
  const status: CampaignStatus = stateMap[d.workflowState] ?? "draft";
  const validChannels: CampaignChannel[] = ["Email", "SMS", "Push", "Web"];
  const channel: CampaignChannel = validChannels.includes(d.channel as CampaignChannel)
    ? (d.channel as CampaignChannel) : "Email";

  return {
    id: d.id,
    name: parseName(d.nameJson) || d.code,
    code: d.code,
    editorKind: d.editorKind as CampaignEditorKind,
    status,
    channel,
    startDate: formatDate(d.startDate),
    endDate: formatDate(d.endDate),
    audience: d.audience || "—",
    budget: d.budget || "—",
    conversions: d.conversions,
    // carry raw workflowState for EditCampaignPage
    _workflowState: d.workflowState,
  } as CampaignListRow & { _workflowState: string };
}

// ── CRUD ──────────────────────────────────────────────────────────────────────

export async function fetchCampaigns(
  tenantId: string,
  opts?: { status?: string; channel?: string; search?: string },
  token?: string
): Promise<CampaignListRow[]> {
  const params = new URLSearchParams({ pageSize: "200" });
  if (opts?.status)  params.set("status",  opts.status);
  if (opts?.channel) params.set("channel", opts.channel);
  if (opts?.search)  params.set("search",  opts.search);

  const res = await fetch(`${BASE}/tenants/${tenantId}/campaigns?${params}`, {
    credentials: "same-origin", headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  const items: CampaignDto[] = Array.isArray(body.data) ? body.data : [];
  return items.map(dtoToRow);
}

export async function getCampaign(tenantId: string, id: string, token?: string): Promise<CampaignDto> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/campaigns/${id}`, {
    credentials: "same-origin", headers: headers(token),
  });
  await checkOk(res);
  return (await res.json()).data as CampaignDto;
}

export async function createCampaign(
  tenantId: string, payload: CampaignPayload, token?: string
): Promise<CampaignListRow> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/campaigns`, {
    method: "POST", credentials: "same-origin", headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  return dtoToRow((await res.json()).data as CampaignDto);
}

export async function updateCampaign(
  tenantId: string, id: string, payload: CampaignPayload, token?: string
): Promise<CampaignListRow> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/campaigns/${id}`, {
    method: "PUT", credentials: "same-origin", headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  return dtoToRow((await res.json()).data as CampaignDto);
}

export async function deleteCampaign(tenantId: string, id: string, token?: string): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/campaigns/${id}`, {
    method: "DELETE", credentials: "same-origin", headers: headers(token),
  });
  await checkOk(res);
}

// ── Workflow ──────────────────────────────────────────────────────────────────

async function doTransition(
  tenantId: string, id: string, action: string, comment?: string, token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/campaigns/${id}/${action}`, {
    method: "POST", credentials: "same-origin", headers: headers(token),
    body: JSON.stringify({ comment: comment ?? "" }),
  });
  await checkOk(res);
}

export const submitCampaign   = (t: string, id: string, c?: string, tok?: string) => doTransition(t, id, "submit",   c, tok);
export const approveCampaign  = (t: string, id: string, c?: string, tok?: string) => doTransition(t, id, "approve",  c, tok);
export const rejectCampaign   = (t: string, id: string, c?: string, tok?: string) => doTransition(t, id, "reject",   c, tok);
export const activateCampaign = (t: string, id: string, c?: string, tok?: string) => doTransition(t, id, "activate", c, tok);
export const pauseCampaign    = (t: string, id: string, c?: string, tok?: string) => doTransition(t, id, "pause",    c, tok);
export const archiveCampaign  = (t: string, id: string, c?: string, tok?: string) => doTransition(t, id, "archive",  c, tok);

export async function fetchCampaignHistory(
  tenantId: string, id: string, token?: string
): Promise<HistoryEntry[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/campaigns/${id}/history`, {
    credentials: "same-origin", headers: headers(token),
  });
  await checkOk(res);
  const body = await res.json();
  return Array.isArray(body.data) ? (body.data as HistoryEntry[]) : [];
}
