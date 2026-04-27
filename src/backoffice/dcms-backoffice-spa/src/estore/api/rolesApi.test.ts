import { describe, expect, it, vi, beforeEach } from "vitest";
import {
  fetchRoles,
  createRole,
  updateRole,
  deleteRole,
  fetchRolePermissions,
  upsertModulePermissions,
  fetchRoleUmbracoPermissions,
  updateRoleUmbracoPermissions,
  fetchRoleGranularPermissions,
  updateRoleGranularPermissions,
  validateRoleAlias,
  roleDtoToRow,
} from "./rolesApi";

function mockFetchOnce(json: unknown, ok = true, status = 200) {
  globalThis.fetch = vi.fn(async () => {
    return {
      ok,
      status,
      json: async () => json,
    } as unknown as Response;
  }) as unknown as typeof fetch;
}

function lastFetchCall() {
  return (globalThis.fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0];
}

beforeEach(() => {
  vi.restoreAllMocks();
});

// ── roleDtoToRow ────────────────────────────────────────────────────────────

describe("roleDtoToRow", () => {
  it("maps RoleDto to RoleRow", () => {
    const row = roleDtoToRow({
      alias: "editor",
      name: "Editor",
      icon: "icon-users",
      allowedSections: ["content", "media"],
      memberCount: 5,
      isTenantRole: true,
      description: "Edits content",
    });
    expect(row).toEqual({
      alias: "editor",
      name: "Editor",
      description: "Edits content",
      isTenantRole: true,
      memberCount: 5,
    });
  });
});

// ── validateRoleAlias ───────────────────────────────────────────────────────

describe("validateRoleAlias", () => {
  it("returns null for valid alias", () => {
    expect(validateRoleAlias("brand-manager")).toBeNull();
    expect(validateRoleAlias("a")).toBeNull();
    expect(validateRoleAlias("role_123")).toBeNull();
  });

  it("rejects empty alias", () => {
    expect(validateRoleAlias("")).toBe("Alias is required.");
    expect(validateRoleAlias("   ")).toBe("Alias is required.");
  });

  it("rejects invalid characters", () => {
    expect(validateRoleAlias("Brand Manager")).not.toBeNull();
    expect(validateRoleAlias("123-start")).not.toBeNull();
    expect(validateRoleAlias("-dash")).not.toBeNull();
  });
});

// ── CRUD: fetchRoles / createRole / updateRole / deleteRole ─────────────────

describe("fetchRoles", () => {
  it("fetches roles and maps to RoleRow[]", async () => {
    mockFetchOnce({
      data: [
        {
          alias: "editor",
          name: "Editor",
          icon: "icon-users",
          allowedSections: ["content"],
          memberCount: 3,
          isTenantRole: false,
          description: "desc",
        },
      ],
      meta: null,
      error: null,
    });

    const rows = await fetchRoles("tok");
    expect(rows).toHaveLength(1);
    expect(rows[0].alias).toBe("editor");
    expect(rows[0].memberCount).toBe(3);

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles");
    expect(call[1]?.headers?.Authorization).toBe("Bearer tok");
  });
});

describe("createRole", () => {
  it("POSTs role payload", async () => {
    mockFetchOnce({
      data: { alias: "new-role", name: "New Role", icon: "icon-users", allowedSections: [], memberCount: 0, isTenantRole: false, description: "" },
      meta: null,
      error: null,
    });

    const row = await createRole({ name: "New Role", alias: "new-role" }, "tok");
    expect(row.alias).toBe("new-role");

    const call = lastFetchCall();
    expect(call[1]?.method).toBe("POST");
    expect(call[1]?.body).toContain("\"alias\":\"new-role\"");
  });
});

describe("updateRole", () => {
  it("PUTs update payload to alias endpoint", async () => {
    mockFetchOnce({
      data: { alias: "editor", name: "Senior Editor", icon: "icon-users", allowedSections: [], memberCount: 2, isTenantRole: true, description: "updated" },
      meta: null,
      error: null,
    });

    const row = await updateRole("editor", { name: "Senior Editor", isTenantRole: true }, "tok");
    expect(row.name).toBe("Senior Editor");
    expect(row.isTenantRole).toBe(true);

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/editor");
    expect(call[1]?.method).toBe("PUT");
  });
});

describe("deleteRole", () => {
  it("DELETEs role", async () => {
    mockFetchOnce({ data: { deleted: true }, meta: null, error: null });

    await deleteRole("old-role", "tok");

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/old-role");
    expect(call[1]?.method).toBe("DELETE");
  });

  it("throws on HTTP error", async () => {
    mockFetchOnce({ error: { message: "Cannot delete" } }, false, 409);
    await expect(deleteRole("active-role", "tok")).rejects.toThrow("Cannot delete");
  });
});

// ── Module permissions ──────────────────────────────────────────────────────

describe("fetchRolePermissions", () => {
  it("fetches module permissions for a role", async () => {
    mockFetchOnce({
      data: [
        { role_alias: "editor", module: "catalog", action: "read", granted: true },
        { role_alias: "editor", module: "catalog", action: "write", granted: false },
      ],
      meta: null,
      error: null,
    });

    const rows = await fetchRolePermissions("editor", "tok");
    expect(rows).toHaveLength(2);
    expect(rows[0].module).toBe("catalog");
    expect(rows[0].granted).toBe(true);
    expect(rows[1].granted).toBe(false);

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/editor/permissions");
  });
});

describe("upsertModulePermissions", () => {
  it("PUTs actions for a module", async () => {
    mockFetchOnce({ data: { updated: 2 }, meta: null, error: null });

    await upsertModulePermissions(
      "editor",
      "catalog",
      [
        { action: "read", granted: true },
        { action: "write", granted: false },
      ],
      "tok",
    );

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/editor/permissions/catalog");
    expect(call[1]?.method).toBe("PUT");
    const body = JSON.parse(call[1]?.body as string);
    expect(body.actions).toHaveLength(2);
    expect(body.actions[0]).toEqual({ action: "read", granted: true });
  });
});

// ── DAI-671: Umbraco section permissions ────────────────────────────────────

describe("fetchRoleUmbracoPermissions", () => {
  it("fetches sections + start nodes", async () => {
    mockFetchOnce({
      data: {
        sections: ["content", "media"],
        contentStartNodeKey: "aaa-bbb-ccc",
        mediaStartNodeKey: null,
        isProtected: false,
      },
      meta: null,
      error: null,
    });

    const perms = await fetchRoleUmbracoPermissions("editor", "tok");
    expect(perms.sections).toEqual(["content", "media"]);
    expect(perms.contentStartNodeKey).toBe("aaa-bbb-ccc");
    expect(perms.mediaStartNodeKey).toBeNull();
    expect(perms.isProtected).toBe(false);

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/editor/umbraco-permissions");
  });

  it("maps empty/missing fields safely", async () => {
    mockFetchOnce({ data: {}, meta: null, error: null });

    const perms = await fetchRoleUmbracoPermissions("viewer");
    expect(perms.sections).toEqual([]);
    expect(perms.contentStartNodeKey).toBeNull();
    expect(perms.mediaStartNodeKey).toBeNull();
    expect(perms.isProtected).toBe(false);
  });

  it("maps isProtected for admin groups", async () => {
    mockFetchOnce({
      data: { sections: ["content", "media"], contentStartNodeKey: null, mediaStartNodeKey: null, isProtected: true },
      meta: null,
      error: null,
    });

    const perms = await fetchRoleUmbracoPermissions("admin");
    expect(perms.isProtected).toBe(true);
  });
});

describe("updateRoleUmbracoPermissions", () => {
  it("PUTs sections and start-node keys", async () => {
    mockFetchOnce({
      data: {
        sections: ["content"],
        contentStartNodeKey: "node-123",
        mediaStartNodeKey: null,
        isProtected: false,
      },
      meta: null,
      error: null,
    });

    const result = await updateRoleUmbracoPermissions(
      "editor",
      {
        sections: ["content"],
        contentStartNodeKey: "node-123",
        mediaStartNodeKey: null,
      },
      "tok",
    );

    expect(result.sections).toEqual(["content"]);
    expect(result.contentStartNodeKey).toBe("node-123");

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/editor/umbraco-permissions");
    expect(call[1]?.method).toBe("PUT");

    const body = JSON.parse(call[1]?.body as string);
    expect(body.sections).toEqual(["content"]);
    expect(body.contentStartNodeKey).toBe("node-123");
    expect(body.mediaStartNodeKey).toBeNull();
  });

  it("sends null for omitted start-node keys", async () => {
    mockFetchOnce({
      data: { sections: ["media"], contentStartNodeKey: null, mediaStartNodeKey: null, isProtected: false },
      meta: null,
      error: null,
    });

    await updateRoleUmbracoPermissions("editor", { sections: ["media"] }, "tok");

    const call = lastFetchCall();
    const body = JSON.parse(call[1]?.body as string);
    expect(body.contentStartNodeKey).toBeNull();
    expect(body.mediaStartNodeKey).toBeNull();
  });

  it("throws on HTTP 403 for protected groups", async () => {
    mockFetchOnce({ error: { message: "HTTP 403" } }, false, 403);
    await expect(
      updateRoleUmbracoPermissions("admin", { sections: ["content"] }, "tok"),
    ).rejects.toThrow();
  });
});

// ── DAI-671 follow-up: granular per-content-node permissions ────────────────

describe("fetchRoleGranularPermissions", () => {
  it("fetches content nodes and available permissions", async () => {
    mockFetchOnce({
      data: {
        contentNodes: [
          { nodeKey: "guid-1", permissions: ["Umb.Document.Read", "Umb.Document.Update"] },
          { nodeKey: "guid-2", permissions: ["Umb.Document.Read"] },
        ],
        availablePermissions: [
          { alias: "Umb.Document.Read", label: "Browse", category: "content" },
          { alias: "Umb.Document.Update", label: "Update", category: "content" },
        ],
        isProtected: false,
      },
      meta: null,
      error: null,
    });

    const result = await fetchRoleGranularPermissions("editor", "tok");
    expect(result.contentNodes).toHaveLength(2);
    expect(result.contentNodes[0].nodeKey).toBe("guid-1");
    expect(result.contentNodes[0].permissions).toEqual(["Umb.Document.Read", "Umb.Document.Update"]);
    expect(result.availablePermissions).toHaveLength(2);
    expect(result.availablePermissions[0].alias).toBe("Umb.Document.Read");
    expect(result.availablePermissions[0].category).toBe("content");
    expect(result.isProtected).toBe(false);

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/editor/granular-permissions");
  });

  it("filters out nodes with empty keys", async () => {
    mockFetchOnce({
      data: {
        contentNodes: [
          { nodeKey: "", permissions: ["Umb.Document.Read"] },
          { nodeKey: "valid-guid", permissions: [] },
        ],
        availablePermissions: [],
        isProtected: false,
      },
      meta: null,
      error: null,
    });

    const result = await fetchRoleGranularPermissions("editor");
    expect(result.contentNodes).toHaveLength(1);
    expect(result.contentNodes[0].nodeKey).toBe("valid-guid");
  });

  it("handles empty/missing data gracefully", async () => {
    mockFetchOnce({ data: {}, meta: null, error: null });

    const result = await fetchRoleGranularPermissions("viewer");
    expect(result.contentNodes).toEqual([]);
    expect(result.availablePermissions).toEqual([]);
    expect(result.isProtected).toBe(false);
  });
});

describe("updateRoleGranularPermissions", () => {
  it("PUTs content node grants", async () => {
    mockFetchOnce({
      data: {
        contentNodes: [
          { nodeKey: "guid-1", permissions: ["Umb.Document.Read", "Umb.Document.Create"] },
        ],
        availablePermissions: [
          { alias: "Umb.Document.Read", label: "Browse", category: "content" },
          { alias: "Umb.Document.Create", label: "Create", category: "content" },
        ],
        isProtected: false,
      },
      meta: null,
      error: null,
    });

    const result = await updateRoleGranularPermissions(
      "editor",
      {
        contentNodes: [
          { nodeKey: "guid-1", permissions: ["Umb.Document.Read", "Umb.Document.Create"] },
        ],
      },
      "tok",
    );

    expect(result.contentNodes).toHaveLength(1);
    expect(result.contentNodes[0].permissions).toContain("Umb.Document.Create");

    const call = lastFetchCall();
    expect(String(call[0])).toBe("/umbraco/dcms/api/roles/editor/granular-permissions");
    expect(call[1]?.method).toBe("PUT");

    const body = JSON.parse(call[1]?.body as string);
    expect(body.contentNodes).toHaveLength(1);
    expect(body.contentNodes[0].nodeKey).toBe("guid-1");
    expect(body.contentNodes[0].permissions).toEqual(["Umb.Document.Read", "Umb.Document.Create"]);
  });

  it("sends empty contentNodes to clear all", async () => {
    mockFetchOnce({
      data: { contentNodes: [], availablePermissions: [], isProtected: false },
      meta: null,
      error: null,
    });

    await updateRoleGranularPermissions("editor", { contentNodes: [] }, "tok");

    const call = lastFetchCall();
    const body = JSON.parse(call[1]?.body as string);
    expect(body.contentNodes).toEqual([]);
  });

  it("throws on HTTP 403 for protected groups", async () => {
    mockFetchOnce({ error: { message: "HTTP 403" } }, false, 403);
    await expect(
      updateRoleGranularPermissions(
        "admin",
        { contentNodes: [{ nodeKey: "guid-1", permissions: ["Umb.Document.Read"] }] },
        "tok",
      ),
    ).rejects.toThrow();
  });
});
