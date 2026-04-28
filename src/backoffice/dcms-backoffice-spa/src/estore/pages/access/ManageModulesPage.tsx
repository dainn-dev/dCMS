import { useEffect, useState } from "react";
import {
  IconAdminPanel,
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
} from "../../../orders/icons";
import {
  fetchRoleGranularPermissions,
  fetchRolePermissions,
  fetchRoleUmbracoPermissions,
  updateRoleGranularPermissions,
  updateRoleUmbracoPermissions,
  upsertModulePermissions,
  type ContentActionDescriptor,
  type PermissionRow,
  type RoleContentNodeGrant,
  type RoleGranularPermissions,
  type RoleUmbracoPermissions,
} from "../../api/rolesApi";
import { UmbracoTreePicker } from "../../components/UmbracoTreePicker";

type ActionPermission = {
  action: string;
  label: string;
  granted: boolean;
};

type ModulePermission = {
  module: string;
  moduleLabel: string;
  actions: ActionPermission[];
};

/** Canonical module/action definitions — all granted=false as baseline; API overlay applies actual grants. */
const CANONICAL_MODULES: ModulePermission[] = [
  {
    module: "user-management",
    moduleLabel: "User Management",
    actions: [
      { action: "create", label: "Create", granted: false },
      { action: "read",   label: "Read",   granted: false },
      { action: "update", label: "Update", granted: false },
      { action: "delete", label: "Delete", granted: false },
    ],
  },
  {
    module: "role-management",
    moduleLabel: "Role Management",
    actions: [
      { action: "read",   label: "Read",   granted: false },
      { action: "update", label: "Update", granted: false },
      { action: "delete", label: "Delete", granted: false },
      { action: "export", label: "Export", granted: false },
    ],
  },
  {
    module: "brand-management",
    moduleLabel: "Brand Management",
    actions: [
      { action: "create", label: "Create", granted: false },
      { action: "read",   label: "Read",   granted: false },
      { action: "update", label: "Update", granted: false },
      { action: "delete", label: "Delete", granted: false },
    ],
  },
  {
    module: "campaign-management",
    moduleLabel: "Campaign Management",
    actions: [
      { action: "create",  label: "Create",  granted: false },
      { action: "read",    label: "Read",    granted: false },
      { action: "update",  label: "Update",  granted: false },
      { action: "delete",  label: "Delete",  granted: false },
      { action: "publish", label: "Publish", granted: false },
      { action: "archive", label: "Archive", granted: false },
    ],
  },
  {
    module: "product-management",
    moduleLabel: "Product Management",
    actions: [
      { action: "create",  label: "Create",  granted: false },
      { action: "read",    label: "Read",    granted: false },
      { action: "update",  label: "Update",  granted: false },
      { action: "delete",  label: "Delete",  granted: false },
      { action: "approve", label: "Approve", granted: false },
      { action: "import",  label: "Import",  granted: false },
      { action: "export",  label: "Export",  granted: false },
    ],
  },
  {
    module: "order-processing",
    moduleLabel: "Order Processing",
    actions: [
      { action: "read",   label: "Read",   granted: false },
      { action: "update", label: "Update", granted: false },
      { action: "export", label: "Export", granted: false },
    ],
  },
  {
    module: "dashboard",
    moduleLabel: "Dashboard",
    actions: [
      { action: "read", label: "Read", granted: false },
    ],
  },
];

function mergePermissions(template: ModulePermission[], rows: PermissionRow[]): ModulePermission[] {
  const lookup = new Map<string, boolean>();
  for (const r of rows) {
    lookup.set(`${r.module}\0${r.action}`, r.granted);
  }
  return template.map((m) => ({
    ...m,
    actions: m.actions.map((a) => {
      const key = `${m.module}\0${a.action}`;
      return lookup.has(key) ? { ...a, granted: lookup.get(key)! } : a;
    }),
  }));
}

type ManageModulesPageProps = {
  roleAlias: string;
  roleName?: string;
  onBack?: () => void;
  authToken?: string;
};

export function ManageModulesPage({ roleAlias, roleName = "Role", onBack, authToken }: ManageModulesPageProps) {
  const [modules, setModules] = useState<ModulePermission[]>(CANONICAL_MODULES);
  const [expandedModules, setExpandedModules] = useState<Set<string>>(() => new Set());
  const [saved, setSaved] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // ── DAI-671: built-in Umbraco section state (Content + Media) ───────────────
  const [umbracoExpanded, setUmbracoExpanded] = useState(true);
  const [umbracoPerms, setUmbracoPerms] = useState<RoleUmbracoPermissions>({
    sections: [],
    contentStartNodeKey: null,
    mediaStartNodeKey: null,
    isProtected: false,
  });
  const [umbracoDirty, setUmbracoDirty] = useState(false);

  // ── DAI-671 follow-up: granular per-content-node permissions ────────────────
  const [granularExpanded, setGranularExpanded] = useState(false);
  const [granular, setGranular] = useState<RoleGranularPermissions>({
    contentNodes: [],
    availablePermissions: [],
    isProtected: false,
  });
  const [granularDirty, setGranularDirty] = useState(false);
  /** Stages a new node being added (picker holds a Guid here until user clicks "+ Add"). */
  const [pendingNodeKey, setPendingNodeKey] = useState("");

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(null);
    Promise.all([
      fetchRolePermissions(roleAlias, authToken),
      fetchRoleUmbracoPermissions(roleAlias, authToken).catch(() => null),
      fetchRoleGranularPermissions(roleAlias, authToken).catch(() => null),
    ])
      .then(([rows, umb, gran]) => {
        if (cancelled) return;
        setModules(mergePermissions(CANONICAL_MODULES, rows));
        if (umb) setUmbracoPerms(umb);
        if (gran) setGranular(gran);
      })
      .catch((e: unknown) => {
        if (!cancelled) setLoadError(e instanceof Error ? e.message : "Failed to load permissions");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [roleAlias, authToken]);

  function toggleUmbracoSection(section: "content" | "media", granted: boolean) {
    setUmbracoPerms((prev) => {
      const set = new Set(prev.sections);
      if (granted) set.add(section);
      else set.delete(section);
      return { ...prev, sections: Array.from(set) };
    });
    setUmbracoDirty(true);
    setSaved(false);
  }

  function setUmbracoStartNodeKey(kind: "content" | "media", value: string) {
    const trimmed = value.trim();
    setUmbracoPerms((prev) => ({
      ...prev,
      [kind === "content" ? "contentStartNodeKey" : "mediaStartNodeKey"]:
        trimmed.length > 0 ? trimmed : null,
    }));
    setUmbracoDirty(true);
    setSaved(false);
  }

  function addGranularNode() {
    const key = pendingNodeKey.trim();
    if (!key) return;
    if (granular.contentNodes.some((n) => n.nodeKey === key)) {
      setPendingNodeKey("");
      return;
    }
    setGranular((prev) => ({
      ...prev,
      contentNodes: [...prev.contentNodes, { nodeKey: key, permissions: [] }],
    }));
    setPendingNodeKey("");
    setGranularDirty(true);
    setSaved(false);
  }

  function removeGranularNode(nodeKey: string) {
    setGranular((prev) => ({
      ...prev,
      contentNodes: prev.contentNodes.filter((n) => n.nodeKey !== nodeKey),
    }));
    setGranularDirty(true);
    setSaved(false);
  }

  function toggleGranularPermission(nodeKey: string, alias: string, granted: boolean) {
    setGranular((prev) => ({
      ...prev,
      contentNodes: prev.contentNodes.map((n) => {
        if (n.nodeKey !== nodeKey) return n;
        const set = new Set(n.permissions);
        if (granted) set.add(alias);
        else set.delete(alias);
        return { ...n, permissions: Array.from(set) };
      }),
    }));
    setGranularDirty(true);
    setSaved(false);
  }

  function toggleExpand(module: string) {
    setExpandedModules((prev) => {
      const next = new Set(prev);
      if (next.has(module)) next.delete(module);
      else next.add(module);
      return next;
    });
  }

  function toggleAction(moduleKey: string, action: string, granted: boolean) {
    setModules((prev) =>
      prev.map((m) =>
        m.module === moduleKey
          ? {
              ...m,
              actions: m.actions.map((a) =>
                a.action === action ? { ...a, granted } : a,
              ),
            }
          : m,
      ),
    );
    setDirty(true);
    setSaved(false);
  }

  async function handleSave() {
    setSaving(true);
    setSaveError(null);
    try {
      for (const mod of modules) {
        await upsertModulePermissions(
          roleAlias,
          mod.module,
          mod.actions.map((a) => ({ action: a.action, granted: a.granted })),
          authToken,
        );
      }
      if (umbracoDirty && !umbracoPerms.isProtected) {
        const updated = await updateRoleUmbracoPermissions(
          roleAlias,
          {
            sections: umbracoPerms.sections,
            contentStartNodeKey: umbracoPerms.contentStartNodeKey,
            mediaStartNodeKey: umbracoPerms.mediaStartNodeKey,
          },
          authToken,
        );
        setUmbracoPerms(updated);
        setUmbracoDirty(false);
      }
      if (granularDirty && !granular.isProtected) {
        const updated = await updateRoleGranularPermissions(
          roleAlias,
          { contentNodes: granular.contentNodes },
          authToken,
        );
        setGranular(updated);
        setGranularDirty(false);
      }
      setSaved(true);
      setDirty(false);
    } catch (e: unknown) {
      setSaveError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  const grantedCount = modules.reduce(
    (sum, m) => sum + m.actions.filter((a) => a.granted).length,
    0,
  );
  const totalCount = modules.reduce((sum, m) => sum + m.actions.length, 0);

  return (
    <div className="-m-6 relative flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      {(loading || saving) && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-surface/60 backdrop-blur-sm">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
        </div>
      )}
      {/* Top bar */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="flex items-center gap-2 text-primary font-bold text-xs uppercase tracking-tighter mb-1 hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Roles
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">
            Manage Modules: <span className="text-primary">{roleName}</span>
          </h2>
          <p className="mt-1 text-sm text-on-surface-variant">
            Configure module access rights for this role.{" "}
            <span className="font-semibold text-on-surface">{grantedCount}</span> of{" "}
            <span className="font-semibold text-on-surface">{totalCount}</span> permissions granted.
          </p>
          {loadError && (
            <p className="mt-2 rounded-lg border border-error/30 bg-error/10 px-3 py-2 text-xs font-medium text-error" role="alert">
              {loadError}
            </p>
          )}
        </div>
        <div className="flex items-center gap-3">
          <button
            type="button"
            className="rounded-md border border-outline-variant/30 px-5 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
            onClick={onBack}
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={(!dirty && !umbracoDirty && !granularDirty) || !!loadError}
            className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
            onClick={() => void handleSave()}
          >
            <IconCheckCircle className="h-4 w-4 shrink-0" />
            Save Access Rights
          </button>
        </div>
      </div>

      {/* Modules list */}
      <div className="flex-1 p-6 space-y-2 max-w-3xl">
        {saveError && (
          <div className="flex items-center gap-3 rounded-lg border border-error/30 bg-error/10 px-5 py-3 mb-4" role="alert">
            <p className="text-xs font-semibold text-error">{saveError}</p>
          </div>
        )}
        {saved && (
          <div className="flex items-center gap-3 rounded-lg border border-primary/20 bg-primary/5 px-5 py-3 mb-4">
            <IconCheckCircle className="h-5 w-5 shrink-0 text-primary" />
            <p className="text-xs font-semibold text-primary">Access rights saved successfully.</p>
          </div>
        )}

        {/* DAI-671: Umbraco built-in sections (Content + Media). The default Users section is hidden,
            so we surface Content/Media access here as part of role management. */}
        <div className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div
            className="flex items-center justify-between gap-4 px-5 py-4 cursor-pointer hover:bg-surface-container transition-colors"
            onClick={() => setUmbracoExpanded((v) => !v)}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                setUmbracoExpanded((v) => !v);
              }
            }}
            aria-expanded={umbracoExpanded}
          >
            <div className="flex items-center gap-3 min-w-0">
              <IconAdminPanel className="h-5 w-5 shrink-0 text-on-surface-variant" />
              <span className="text-sm font-semibold text-on-surface truncate">
                Umbraco Sections (built-in)
              </span>
              {umbracoPerms.isProtected && (
                <span className="inline-flex items-center rounded-full bg-error/10 px-2 py-0.5 text-[9px] font-bold uppercase text-error">
                  Protected
                </span>
              )}
            </div>
            <div className="flex items-center gap-3 shrink-0">
              <span className="text-[10px] font-bold text-on-surface-variant">
                {umbracoPerms.sections.length} / 2 granted
              </span>
              <IconChevronDown
                className={`h-4 w-4 text-on-surface-variant transition-transform duration-200 ${umbracoExpanded ? "" : "-rotate-90"}`}
              />
            </div>
          </div>

          {umbracoExpanded && (
            <div className="border-t border-outline-variant/10 px-5 py-4 space-y-4">
              {umbracoPerms.isProtected && (
                <p className="text-[11px] text-error">
                  This role is a built-in protected group. Its Umbraco section access cannot be edited from here.
                </p>
              )}
              <div className="flex flex-wrap gap-x-8 gap-y-3">
                {(["content", "media"] as const).map((section) => {
                  const granted = umbracoPerms.sections.some(
                    (s) => s.toLowerCase() === section,
                  );
                  return (
                    <label
                      key={section}
                      className="flex cursor-pointer items-center gap-2.5 select-none"
                    >
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-primary disabled:cursor-not-allowed"
                        checked={granted}
                        disabled={umbracoPerms.isProtected}
                        onChange={(e) => toggleUmbracoSection(section, e.target.checked)}
                      />
                      <span
                        className={`text-xs font-medium capitalize ${
                          granted ? "text-on-surface" : "text-on-surface-variant"
                        }`}
                      >
                        {section}
                      </span>
                      {granted && (
                        <span className="inline-flex items-center rounded-full bg-primary/10 px-1.5 py-0 text-[9px] font-bold uppercase text-primary">
                          ✓
                        </span>
                      )}
                    </label>
                  );
                })}
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="flex flex-col gap-1.5">
                  <span className="text-[11px] font-bold uppercase tracking-widest text-on-surface-variant">
                    Content Start Node
                  </span>
                  <UmbracoTreePicker
                    kind="content"
                    value={umbracoPerms.contentStartNodeKey ?? ""}
                    disabled={umbracoPerms.isProtected}
                    onChange={(v) => setUmbracoStartNodeKey("content", v)}
                  />
                  <span className="text-[10px] text-on-surface-variant">
                    Restrict the role's Content tree to this node. Leave empty for full Content tree.
                  </span>
                </div>
                <div className="flex flex-col gap-1.5">
                  <span className="text-[11px] font-bold uppercase tracking-widest text-on-surface-variant">
                    Media Start Node
                  </span>
                  <UmbracoTreePicker
                    kind="media"
                    value={umbracoPerms.mediaStartNodeKey ?? ""}
                    disabled={umbracoPerms.isProtected}
                    onChange={(v) => setUmbracoStartNodeKey("media", v)}
                  />
                  <span className="text-[10px] text-on-surface-variant">
                    Restrict the role's Media tree to this node. Leave empty for full Media tree.
                  </span>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* DAI-671 follow-up: granular per-content-node permissions. Stock Umbraco
            v16 only ships DocumentGranularPermission — Media nodes can't be granular
            (they only have section access + start node above). */}
        <div className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div
            className="flex items-center justify-between gap-4 px-5 py-4 cursor-pointer hover:bg-surface-container transition-colors"
            onClick={() => setGranularExpanded((v) => !v)}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                setGranularExpanded((v) => !v);
              }
            }}
            aria-expanded={granularExpanded}
          >
            <div className="flex items-center gap-3 min-w-0">
              <IconAdminPanel className="h-5 w-5 shrink-0 text-on-surface-variant" />
              <span className="text-sm font-semibold text-on-surface truncate">
                Per-Content-Node Permissions
              </span>
              {granular.isProtected && (
                <span className="inline-flex items-center rounded-full bg-error/10 px-2 py-0.5 text-[9px] font-bold uppercase text-error">
                  Protected
                </span>
              )}
            </div>
            <div className="flex items-center gap-3 shrink-0">
              <span className="text-[10px] font-bold text-on-surface-variant">
                {granular.contentNodes.length} node{granular.contentNodes.length === 1 ? "" : "s"}
              </span>
              <IconChevronDown
                className={`h-4 w-4 text-on-surface-variant transition-transform duration-200 ${granularExpanded ? "" : "-rotate-90"}`}
              />
            </div>
          </div>

          {granularExpanded && (
            <div className="border-t border-outline-variant/10 px-5 py-4 space-y-4">
              {granular.isProtected && (
                <p className="text-[11px] text-error">
                  This role is a built-in protected group. Granular permissions cannot be edited from here.
                </p>
              )}

              {!granular.isProtected && (
                <div className="flex items-end gap-3">
                  <div className="flex-1">
                    <span className="block text-[11px] font-bold uppercase tracking-widest text-on-surface-variant mb-1.5">
                      Add a content node
                    </span>
                    <UmbracoTreePicker
                      kind="content"
                      value={pendingNodeKey}
                      onChange={setPendingNodeKey}
                    />
                  </div>
                  <button
                    type="button"
                    onClick={addGranularNode}
                    disabled={!pendingNodeKey.trim()}
                    className="rounded-md bg-primary px-4 py-2 text-xs font-bold uppercase tracking-widest text-on-primary disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    + Add
                  </button>
                </div>
              )}

              {granular.contentNodes.length === 0 ? (
                <p className="text-[11px] text-on-surface-variant italic">
                  No node-level permissions assigned. Pick a content node above to grant per-node verbs.
                </p>
              ) : (
                <ul className="space-y-3">
                  {granular.contentNodes.map((node) => (
                    <li
                      key={node.nodeKey}
                      className="rounded-lg border border-outline-variant/15 bg-surface px-4 py-3"
                    >
                      <div className="flex items-center justify-between gap-3 mb-2">
                        <code className="text-[11px] font-mono text-on-surface-variant truncate">
                          {node.nodeKey}
                        </code>
                        {!granular.isProtected && (
                          <button
                            type="button"
                            onClick={() => removeGranularNode(node.nodeKey)}
                            className="text-[10px] font-bold uppercase text-error hover:underline"
                          >
                            Remove
                          </button>
                        )}
                      </div>
                      <GranularPermissionGrid
                        node={node}
                        available={granular.availablePermissions}
                        disabled={granular.isProtected}
                        onToggle={(alias, granted) => toggleGranularPermission(node.nodeKey, alias, granted)}
                      />
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>

        {modules.map((mod) => {
          const isExpanded = expandedModules.has(mod.module);
          const grantedInModule = mod.actions.filter((a) => a.granted).length;

          return (
            <div
              key={mod.module}
              className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden"
            >
              {/* Module row */}
              <div
                className="flex items-center justify-between gap-4 px-5 py-4 cursor-pointer hover:bg-surface-container transition-colors"
                onClick={() => toggleExpand(mod.module)}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); toggleExpand(mod.module); } }}
                aria-expanded={isExpanded}
              >
                <div className="flex items-center gap-3 min-w-0">
                  <IconAdminPanel className="h-5 w-5 shrink-0 text-on-surface-variant" />
                  <span className="text-sm font-semibold text-on-surface truncate">{mod.moduleLabel}</span>
                </div>
                <div className="flex items-center gap-3 shrink-0">
                  <span className="text-[10px] font-bold text-on-surface-variant">
                    {grantedInModule} / {mod.actions.length} granted
                  </span>
                  <IconChevronDown
                    className={`h-4 w-4 text-on-surface-variant transition-transform duration-200 ${isExpanded ? "" : "-rotate-90"}`}
                  />
                </div>
              </div>

              {/* Inline permission checkboxes */}
              {isExpanded && (
                <div className="border-t border-outline-variant/10 px-5 py-4">
                  <div className="flex flex-wrap gap-x-8 gap-y-3">
                    {mod.actions.map((action) => (
                      <label
                        key={action.action}
                        className="flex cursor-pointer items-center gap-2.5 select-none"
                      >
                        <input
                          type="checkbox"
                          className="h-4 w-4 accent-primary"
                          checked={action.granted}
                          onChange={(e) => toggleAction(mod.module, action.action, e.target.checked)}
                        />
                        <span
                          className={`text-xs font-medium ${
                            action.granted ? "text-on-surface" : "text-on-surface-variant"
                          }`}
                        >
                          {action.label}
                        </span>
                        {action.granted && (
                          <span className="inline-flex items-center rounded-full bg-primary/10 px-1.5 py-0 text-[9px] font-bold uppercase text-primary">
                            ✓
                          </span>
                        )}
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

/**
 * Render the verb checkboxes for one content node, grouped by Umbraco's permission category
 * (content / structure / administration / other) so the grid stays scannable as the verb
 * count grows. Categories come from the server (`availablePermissions[].category`) which
 * mirrors `Constants.Conventions.PermissionCategories`.
 */
function GranularPermissionGrid({
  node,
  available,
  disabled,
  onToggle,
}: {
  node: RoleContentNodeGrant;
  available: ContentActionDescriptor[];
  disabled: boolean;
  onToggle: (alias: string, granted: boolean) => void;
}) {
  const granted = new Set(node.permissions);
  const byCategory = new Map<string, ContentActionDescriptor[]>();
  for (const perm of available) {
    const list = byCategory.get(perm.category) ?? [];
    list.push(perm);
    byCategory.set(perm.category, list);
  }
  const categories = Array.from(byCategory.keys());

  return (
    <div className="space-y-3">
      {categories.map((cat) => (
        <div key={cat}>
          <p className="text-[9px] font-bold uppercase tracking-widest text-on-surface-variant mb-1.5">
            {cat}
          </p>
          <div className="flex flex-wrap gap-x-6 gap-y-2">
            {byCategory.get(cat)!.map((perm) => {
              const isGranted = granted.has(perm.alias);
              return (
                <label
                  key={perm.alias}
                  className="flex cursor-pointer items-center gap-2 select-none"
                  title={perm.alias}
                >
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-primary disabled:cursor-not-allowed"
                    checked={isGranted}
                    disabled={disabled}
                    onChange={(e) => onToggle(perm.alias, e.target.checked)}
                  />
                  <span
                    className={`text-[11px] font-medium ${
                      isGranted ? "text-on-surface" : "text-on-surface-variant"
                    }`}
                  >
                    {perm.label}
                  </span>
                </label>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
