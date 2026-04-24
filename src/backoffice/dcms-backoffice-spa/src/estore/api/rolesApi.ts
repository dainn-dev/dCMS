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

// ── DAI-671: built-in Umbraco section + start-node permissions ───────────────

/**
 * Built-in Umbraco section permissions for a role. Server enforces a whitelist:
 * only "content" and "media" are managed here. Tenants/Users have dCMS UI;
 * Settings/Members/etc are out of scope.
 */
export type RoleUmbracoPermissions = {
  /** Subset of {"content","media"} that the role currently has access to. */
  sections: string[];
  /** Start-node key for Content tree (null = root). */
  contentStartNodeKey: string | null;
  /** Start-node key for Media tree (null = root). */
  mediaStartNodeKey: string | null;
  /** True if the role is a built-in protected group (Admins / Sensitive data) and cannot be edited. */
  isProtected: boolean;
};

export type RoleUmbracoPermissionsPayload = {
  sections: string[];
  contentStartNodeKey?: string | null;
  mediaStartNodeKey?: string | null;
};

function mapUmbracoPermissions(raw: Record<string, unknown>): RoleUmbracoPermissions {
  const sections = raw.sections;
  const contentKey = raw.contentStartNodeKey;
  const mediaKey = raw.mediaStartNodeKey;
  return {
    sections: Array.isArray(sections) ? sections.map(String) : [],
    contentStartNodeKey: typeof contentKey === "string" && contentKey.length > 0 ? contentKey : null,
    mediaStartNodeKey: typeof mediaKey === "string" && mediaKey.length > 0 ? mediaKey : null,
    isProtected: Boolean(raw.isProtected),
  };
}

export async function fetchRoleUmbracoPermissions(
  alias: string,
  token?: string
): Promise<RoleUmbracoPermissions> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}/umbraco-permissions`, {
    credentials: "include",
    headers: headers(token),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return mapUmbracoPermissions(body.data ?? {});
}

export async function updateRoleUmbracoPermissions(
  alias: string,
  payload: RoleUmbracoPermissionsPayload,
  token?: string
): Promise<RoleUmbracoPermissions> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}/umbraco-permissions`, {
    method: "PUT",
    credentials: "include",
    headers: headers(token),
    body: JSON.stringify({
      sections: payload.sections,
      contentStartNodeKey: payload.contentStartNodeKey ?? null,
      mediaStartNodeKey: payload.mediaStartNodeKey ?? null,
    }),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return mapUmbracoPermissions(body.data ?? {});
}

// ── DAI-671 follow-up: granular per-content-node permissions ─────────────────

/** A single permission verb available for assignment on a content node. */
export type ContentActionDescriptor = {
  /** Verb identifier stored in `DocumentGranularPermission.Permission` (e.g. "Umb.Document.Read"). */
  alias: string;
  /** Human-readable label (e.g. "Browse"). */
  label: string;
  /** UI grouping bucket: "content" | "structure" | "administration" | "other". */
  category: string;
};

/** A single content node with the verbs currently granted to the role on that node. */
export type RoleContentNodeGrant = {
  nodeKey: string;
  permissions: string[];
};

export type RoleGranularPermissions = {
  contentNodes: RoleContentNodeGrant[];
  availablePermissions: ContentActionDescriptor[];
  /** True if the role is a built-in protected group (Admins / Sensitive data). */
  isProtected: boolean;
};

export type RoleGranularPermissionsPayload = {
  contentNodes: RoleContentNodeGrant[];
};

function mapGranularPermissions(raw: Record<string, unknown>): RoleGranularPermissions {
  const nodesRaw = Array.isArray(raw.contentNodes) ? raw.contentNodes : [];
  const availRaw = Array.isArray(raw.availablePermissions) ? raw.availablePermissions : [];
  return {
    contentNodes: nodesRaw.map((n) => {
      const node = n as Record<string, unknown>;
      const perms = Array.isArray(node.permissions) ? node.permissions.map(String) : [];
      return { nodeKey: String(node.nodeKey ?? ""), permissions: perms };
    }).filter((n) => n.nodeKey.length > 0),
    availablePermissions: availRaw.map((d) => {
      const desc = d as Record<string, unknown>;
      return {
        alias: String(desc.alias ?? desc.Item1 ?? ""),
        label: String(desc.label ?? desc.Item2 ?? ""),
        category: String(desc.category ?? desc.Item3 ?? "other"),
      };
    }).filter((d) => d.alias.length > 0),
    isProtected: Boolean(raw.isProtected),
  };
}

export async function fetchRoleGranularPermissions(
  alias: string,
  token?: string
): Promise<RoleGranularPermissions> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}/granular-permissions`, {
    credentials: "include",
    headers: headers(token),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return mapGranularPermissions(body.data ?? {});
}

export async function updateRoleGranularPermissions(
  alias: string,
  payload: RoleGranularPermissionsPayload,
  token?: string
): Promise<RoleGranularPermissions> {
  const res = await fetch(`${BASE}/${encodeURIComponent(alias)}/granular-permissions`, {
    method: "PUT",
    credentials: "include",
    headers: headers(token),
    body: JSON.stringify({
      contentNodes: payload.contentNodes.map((n) => ({
        nodeKey: n.nodeKey,
        permissions: n.permissions,
      })),
    }),
  });
  await checkOk(res);
  const body = (await res.json()) as ApiEnvelope<Record<string, unknown>>;
  return mapGranularPermissions(body.data ?? {});
}

/** Alias rules aligned with typical Umbraco user-group alias (lowercase slug). */
export const ROLE_ALIAS_PATTERN = /^[a-z][a-z0-9_-]{0,62}$/;

export function validateRoleAlias(alias: string): string | null {
  const t = alias.trim().toLowerCase();
  if (!t) return "Alias is required.";
  if (!ROLE_ALIAS_PATTERN.test(t)) return "Use 1–63 chars: lowercase letters, digits, hyphen or underscore; must start with a letter.";
  return null;
}
