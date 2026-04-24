/**
 * DAI-669: Users API — Umbraco backoffice `DcmsUserController`.
 * Base: /umbraco/dcms/api/users
 * Auth: cookie session (credentials: "include") — no Bearer token needed.
 */

import type { UserRow } from "../pages/access/UsersPage";

export type UserDto = {
  id: number;
  key: string;
  username: string;
  email: string;
  name: string;
  isActive: boolean;
  groups: string[];
  createdAt: string;
  updatedAt: string;
};

export type UserCreatePayload = {
  username: string;
  email: string;
  name: string;
  userGroupAlias: string;
  password: string;
  active?: boolean;
};

export type UserUpdatePayload = {
  name?: string;
  email?: string;
  userGroupAlias?: string;
  active?: boolean;
};

type ApiEnvelope<T, TMeta = unknown> = {
  data: T;
  meta: TMeta | null;
  error: { code?: string; message?: string } | null;
};

type ListMeta = { page: number; pageSize: number; total: number; totalPages: number };

const BASE = "/umbraco/dcms/api/users";

const HDR: HeadersInit = { Accept: "application/json", "Content-Type": "application/json" };

async function checkOk(res: Response): Promise<void> {
  if (!res.ok) {
    let msg = `HTTP ${res.status}`;
    try {
      const body = (await res.json()) as { error?: { message?: string } };
      if (body?.error?.message) msg = body.error.message;
    } catch { /* ignore */ }
    throw new Error(msg);
  }
}

function mapDtoToRow(d: UserDto): UserRow {
  return {
    id: String(d.id),
    username: d.username,
    name: d.name,
    email: d.email,
    role: d.groups?.[0] ?? "—",
    active: d.isActive,
  };
}

// ── List ─────────────────────────────────────────────────────────────────────

export async function listUsers(
  opts?: { page?: number; pageSize?: number; search?: string }
): Promise<{ items: UserRow[]; total: number }> {
  const params = new URLSearchParams();
  if (opts?.page) params.set("page", String(opts.page));
  if (opts?.pageSize) params.set("pageSize", String(opts.pageSize));
  if (opts?.search) params.set("search", opts.search);
  const qs = params.size ? `?${params.toString()}` : "";

  const res = await fetch(`${BASE}${qs}`, { credentials: "include", headers: HDR });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<UserDto[], ListMeta>;
  return {
    items: (body.data ?? []).map(mapDtoToRow),
    total: body.meta?.total ?? (body.data?.length ?? 0),
  };
}

// ── Get single ───────────────────────────────────────────────────────────────

export async function getUser(id: number): Promise<UserDto> {
  const res = await fetch(`${BASE}/${id}`, { credentials: "include", headers: HDR });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<UserDto>;
  return body.data;
}

// ── Create ───────────────────────────────────────────────────────────────────

export async function createUser(payload: UserCreatePayload): Promise<UserRow> {
  const res = await fetch(BASE, {
    method: "POST",
    credentials: "include",
    headers: HDR,
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<UserDto>;
  return mapDtoToRow(body.data);
}

// ── Update ───────────────────────────────────────────────────────────────────

export async function updateUser(id: number, payload: UserUpdatePayload): Promise<UserRow> {
  const res = await fetch(`${BASE}/${id}`, {
    method: "PUT",
    credentials: "include",
    headers: HDR,
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<UserDto>;
  return mapDtoToRow(body.data);
}

// ── Delete ───────────────────────────────────────────────────────────────────

export async function deleteUser(id: number): Promise<void> {
  const res = await fetch(`${BASE}/${id}`, { method: "DELETE", credentials: "include", headers: HDR });
  await checkOk(res);
}

// ── Change password ───────────────────────────────────────────────────────────

export async function changePassword(
  id: number,
  _currentPassword: string,
  newPassword: string
): Promise<void> {
  const res = await fetch(`${BASE}/${id}/change-password`, {
    method: "POST",
    credentials: "include",
    headers: HDR,
    body: JSON.stringify({ newPassword }),
  });
  await checkOk(res);
}
