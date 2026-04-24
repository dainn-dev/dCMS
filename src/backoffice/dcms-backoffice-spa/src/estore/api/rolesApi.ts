/**
 * DAI-670: Roles API — Umbraco backoffice `DcmsRoleController` (user groups + `dcms_roles_meta` + module permissions).
 * Base: /umbraco/dcms/api/roles
 */

export type RoleRow = {
  alias: string;
  name: string;
  description: string;
  isTenantRole: boolean;
  memberCount: number;
};

export type RoleDto = {
  alias: string;
  name: string;
  icon: string;
  allowedSections: string[];
  memberCount: number;
  isTenantRole: boolean;
  description: string;
};

export type RoleCreatePayload = {
  name: string;
  alias: string;
  icon?: string;
  isTenantRole?: boolean;
  description?: string;
};

export type RoleUpdatePayload = {
  name?: string;
  icon?: string;
  isTenantRole?: boolean;
  description?: string;
};

export type PermissionRow = {
  roleAlias: string;
  module: string;
  action: string;
  granted: boolean;
};

type ApiEnvelope<T> = {
  data: T;
  meta: unknown;
  error: { code?: string; message?: string } | null;
};

const BASE = "/umbraco/dcms/api/roles";

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

function mapRoleDto(raw: Record<string, unknown>): RoleDto {
  const sections = raw.allowedSections;
  return {
    alias: String(raw.alias ?? ""),
    name: String(raw.name ?? ""),
    icon: String(raw.icon ?? ""),
    allowedSections: Array.isArray(sections) ? sections.map(String) : [],
    memberCount: typeof raw.memberCount === "number" ? raw.memberCount : Number(raw.memberCount) || 0,
    isTenantRole: Boolean(raw.isTenantRole),
    description: String(raw.description ?? ""),
  };
}

export function roleDtoToRow(d: RoleDto): RoleRow {
  return {
    alias: d.alias,
    name: d.name,
    description: d.description,
    isTenantRole: d.isTenantRole,
    memberCount: d.memberCount,
  };
}

export async function fetchRoles(token?: string): Promise<RoleRow[]> {
  const res = await fetch(BASE, { credentials: "include", headers: headers(token) });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>[]>;
  const data = body.data ?? [];
  return data.map((row) => roleDtoToRow(mapRoleDto(row)));
}

export async function createRole(payload: RoleCreatePayload, token?: string): Promise<RoleRow> {
  const res = await fetch(BASE, {
    method: "POST",
    credentials: "include",
    headers: headers(token),
    body: JSON.stringify({
      name: payload.name,
      alias: payload.alias,
      icon: payload.icon ?? "icon-users",
      isTenantRole: payload.isTenantRole ?? false,
      description: payload.description ?? "",
    }),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return roleDtoToRow(mapRoleDto(body.data as Record<string, unknown>));
}

export async function updateRole(alias: string, payload: RoleUpdatePayload, token?: string): Promise<RoleRow> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}`, {
    method: "PUT",
    credentials: "include",
    headers: headers(token),
    body: JSON.stringify({
      name: payload.name,
      icon: payload.icon,
      isTenantRole: payload.isTenantRole,
      description: payload.description,
    }),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return roleDtoToRow(mapRoleDto(body.data as Record<string, unknown>));
}

export async function deleteRole(alias: string, token?: string): Promise<void> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}`, {
    method: "DELETE",
    credentials: "include",
    headers: headers(token),
  });
  await checkOk(res);
}

export async function fetchRolePermissions(alias: string, token?: string): Promise<PermissionRow[]> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}/permissions`, {
    credentials: "include",
    headers: headers(token),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>[]>;
  const data = body.data ?? [];
  return data.map((r) => ({
    roleAlias: String(r.roleAlias ?? r.RoleAlias ?? ""),
    module: String(r.module ?? r.Module ?? ""),
    action: String(r.action ?? r.Action ?? ""),
    granted: Boolean(r.granted ?? r.Granted),
  }));
}

export async function upsertModulePermissions(
  alias: string,
  module: string,
  actions: { action: string; granted: boolean }[],
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}/permissions/${encodeURIComponent(module)}`, {
    method: "PUT",
    credentials: "include",
    headers: headers(token),
    body: JSON.stringify({ actions }),
  });
  await checkOk(res);
}

/** Alias rules aligned with typical Umbraco user-group alias (lowercase slug). */
export const ROLE_ALIAS_PATTERN = /^[a-z][a-z0-9_-]{0,62}$/;

export function validateRoleAlias(alias: string): string | null {
  const t = alias.trim().toLowerCase();
  if (!t) return "Alias is required.";
  if (!ROLE_ALIAS_PATTERN.test(t)) return "Use 1–63 chars: lowercase letters, digits, hyphen or underscore; must start with a letter.";
  return null;
}
