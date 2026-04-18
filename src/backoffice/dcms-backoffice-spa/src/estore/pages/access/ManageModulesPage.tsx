import { useState } from "react";
import {
  IconAdminPanel,
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
} from "../../../orders/icons";

// TODO: Replace with real API calls:
//   GET /umbraco/dcms/api/access/roles/{roleAlias}/modules
//   PUT /umbraco/dcms/api/access/roles/{roleAlias}/modules

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

const DEMO_MODULES: ModulePermission[] = [
  {
    module: "user-management",
    moduleLabel: "User Management",
    actions: [
      { action: "create", label: "Create", granted: false },
      { action: "read", label: "Read", granted: true },
      { action: "update", label: "Update", granted: false },
      { action: "delete", label: "Delete", granted: false },
    ],
  },
  {
    module: "role-management",
    moduleLabel: "Role Management",
    actions: [
      { action: "read", label: "Read", granted: true },
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
      { action: "read", label: "Read", granted: true },
      { action: "update", label: "Update", granted: true },
      { action: "delete", label: "Delete", granted: false },
    ],
  },
  {
    module: "campaign-management",
    moduleLabel: "Campaign Management",
    actions: [
      { action: "create", label: "Create", granted: false },
      { action: "read", label: "Read", granted: true },
      { action: "update", label: "Update", granted: true },
      { action: "delete", label: "Delete", granted: false },
      { action: "publish", label: "Publish", granted: false },
      { action: "archive", label: "Archive", granted: false },
    ],
  },
  {
    module: "product-management",
    moduleLabel: "Product Management",
    actions: [
      { action: "create", label: "Create", granted: false },
      { action: "read", label: "Read", granted: true },
      { action: "update", label: "Update", granted: true },
      { action: "delete", label: "Delete", granted: false },
      { action: "approve", label: "Approve", granted: false },
      { action: "import", label: "Import", granted: false },
      { action: "export", label: "Export", granted: false },
    ],
  },
  {
    module: "order-processing",
    moduleLabel: "Order Processing",
    actions: [
      { action: "read", label: "Read", granted: true },
      { action: "update", label: "Update", granted: false },
      { action: "export", label: "Export", granted: false },
    ],
  },
  {
    module: "dashboard",
    moduleLabel: "Dashboard",
    actions: [
      { action: "read", label: "Read", granted: true },
    ],
  },
];

type ManageModulesPageProps = {
  roleAlias: string;
  roleName?: string;
  onBack?: () => void;
};

export function ManageModulesPage({ roleAlias: _roleAlias, roleName = "Role", onBack }: ManageModulesPageProps) {
  const [modules, setModules] = useState<ModulePermission[]>(DEMO_MODULES);
  const [expandedModules, setExpandedModules] = useState<Set<string>>(() => new Set());
  const [saved, setSaved] = useState(false);
  const [dirty, setDirty] = useState(false);

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

  function handleSave() {
    // TODO: PUT /umbraco/dcms/api/access/roles/{_roleAlias}/modules
    setSaved(true);
    setDirty(false);
  }

  const grantedCount = modules.reduce(
    (sum, m) => sum + m.actions.filter((a) => a.granted).length,
    0,
  );
  const totalCount = modules.reduce((sum, m) => sum + m.actions.length, 0);

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      {/* Top bar */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span>Access</span>
            <span className="mx-2">/</span>
            <button
              type="button"
              className="text-on-surface-variant hover:text-primary transition-colors"
              onClick={onBack}
            >
              Roles
            </button>
            <span className="mx-2">/</span>
            <span className="text-on-surface-variant">{roleName}</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Manage Modules</span>
          </nav>
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
            disabled={!dirty}
            className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
            onClick={handleSave}
          >
            <IconCheckCircle className="h-4 w-4 shrink-0" />
            Save Access Rights
          </button>
        </div>
      </div>

      {/* Modules list */}
      <div className="flex-1 p-6 space-y-2 max-w-3xl">
        {saved && (
          <div className="flex items-center gap-3 rounded-lg border border-primary/20 bg-primary/5 px-5 py-3 mb-4">
            <IconCheckCircle className="h-5 w-5 shrink-0 text-primary" />
            <p className="text-xs font-semibold text-primary">Access rights saved successfully.</p>
          </div>
        )}

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
