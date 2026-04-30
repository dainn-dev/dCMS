/**
 * DAI-591: Category management API client.
 * Routes through dCMS.Gateway → /gateway/v1/catalog/tenants/{tenantId}/categories
 * Maps to Catalog API: /api/v1/tenants/{tenantId}/categories
 */

import { GATEWAY } from "./gatewayConfig";
import type { CatNode } from "../pages/CategoriesPage";

const BASE = GATEWAY.catalog;

export type CategoryPayload = {
  name: string;
  slug: string;
  parentId?: number | null;
  sortOrder?: number;
  code?: string;
  active?: boolean;
  publishFrom?: string | null;   // ISO datetime string
  publishUntil?: string | null;  // ISO datetime string
  imageMenuUrl?: string;
  imagePageUrl?: string;
  imageThumbUrl?: string;
  showInNav?: boolean;
  showInBrands?: boolean;
  customNavUrl?: string;
  navSortPriority?: number;
  breakNavColumn?: boolean;
  defaultSort?: string;
  noRecommendations?: boolean;
  metaTitleJson?: string;
  metaKeywordsJson?: string;
  metaDescJson?: string;
  restrictAccess?: boolean;
  accessApp?: string;
  accessMemberType?: string;
  accessMemberTier?: string;
};

type ApiResponse<T> = { data: T; meta: unknown; error: null };

type CategoryDto = {
  id: number;
  tenantId: string;
  parentId: number | null;
  path: string;
  depth: number;
  name: string;
  slug: string;
  sortOrder: number;
  code: string;
  active: boolean;
  publishFrom: string | null;
  publishUntil: string | null;
  imageMenuUrl: string;
  imagePageUrl: string;
  imageThumbUrl: string;
  showInNav: boolean;
  showInBrands: boolean;
  customNavUrl: string;
  navSortPriority: number;
  breakNavColumn: boolean;
  defaultSort: string;
  noRecommendations: boolean;
  metaTitleJson: string;
  metaKeywordsJson: string;
  metaDescJson: string;
  restrictAccess: boolean;
  accessApp: string;
  accessMemberType: string;
  accessMemberTier: string;
  isLeaf: boolean;
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

/** Convert flat DTO list → CatNode tree */
export function buildTree(dtos: CategoryDto[]): CatNode[] {
  const map = new Map<number, CatNode & { _children: CatNode[] }>();
  for (const d of dtos) {
    map.set(d.id, {
      id: String(d.id),
      name: d.name,
      active: d.active,
      publishFrom: d.publishFrom ?? undefined,
      publishUntil: d.publishUntil ?? undefined,
      sortOrder: d.sortOrder,
      _children: [],
      // carry full DTO fields for the edit form
      _dto: d,
    } as any);
  }
  const roots: CatNode[] = [];
  for (const d of dtos) {
    const node = map.get(d.id)!;
    if (d.parentId !== null && map.has(d.parentId)) {
      (map.get(d.parentId)! as any)._children.push(node);
    } else {
      roots.push(node);
    }
  }
  // Promote _children → children (only if non-empty)
  function finalize(node: CatNode & { _children?: CatNode[] }): CatNode {
    const kids = (node as any)._children as CatNode[];
    delete (node as any)._children;
    if (kids?.length) node.children = kids.map(finalize);
    return node;
  }
  return roots.map(finalize);
}

/** GET /api/v1/tenants/{tenantId}/categories — returns flat list, caller calls buildTree */
export async function fetchCategories(
  tenantId: string,
  token?: string
): Promise<CatNode[]> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/categories`, {
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
  const body: ApiResponse<{ items: CategoryDto[] }> = await res.json();
  return buildTree(body.data.items ?? []);
}

/** POST — create, returns the new CatNode (id is numeric string) */
export async function createCategory(
  tenantId: string,
  payload: CategoryPayload,
  token?: string
): Promise<CatNode> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/categories`, {
    method: "POST",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body: ApiResponse<CategoryDto> = await res.json();
  return dtoToNode(body.data);
}

/** PUT — update, returns updated CatNode */
export async function updateCategory(
  tenantId: string,
  id: number,
  payload: CategoryPayload,
  token?: string
): Promise<CatNode> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/categories/${id}`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify(payload),
  });
  await checkOk(res);
  const body: ApiResponse<CategoryDto> = await res.json();
  return dtoToNode(body.data);
}

/** DELETE — no content on success */
export async function deleteCategory(
  tenantId: string,
  id: number,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/categories/${id}`, {
    method: "DELETE",
    credentials: "same-origin",
    headers: headers(token),
  });
  await checkOk(res);
}

/** PUT /{id}/parent — reclassify, returns updated CatNode */
export async function reclassifyCategory(
  tenantId: string,
  id: number,
  newParentId: number | null,
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/categories/${id}/parent`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify({ newParentId }),
  });
  await checkOk(res);
}

/** PUT /sort — reorder siblings */
export async function sortCategories(
  tenantId: string,
  parentId: number | null,
  items: { id: number; sortOrder: number }[],
  token?: string
): Promise<void> {
  const res = await fetch(`${BASE}/tenants/${tenantId}/categories/sort`, {
    method: "PUT",
    credentials: "same-origin",
    headers: headers(token),
    body: JSON.stringify({ parentId, items }),
  });
  await checkOk(res);
}

function dtoToNode(d: CategoryDto): CatNode {
  return {
    id: String(d.id),
    name: d.name,
    active: d.active,
    publishFrom: d.publishFrom ?? undefined,
    publishUntil: d.publishUntil ?? undefined,
    sortOrder: d.sortOrder,
    _dto: d,
  } as CatNode & { _dto: CategoryDto };
}
