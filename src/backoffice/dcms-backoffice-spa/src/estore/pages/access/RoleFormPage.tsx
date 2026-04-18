import { useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconSave,
} from "../../../orders/icons";

// TODO: Replace with real API calls:
//   GET  /umbraco/dcms/api/access/roles/{roleAlias}  (edit mode)
//   POST /umbraco/dcms/api/access/roles               (add mode)
//   PUT  /umbraco/dcms/api/access/roles/{roleAlias}   (edit mode)

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const sectionTitleRow =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2";

export type RoleFormPageProps = {
  mode: "add" | "edit";
  roleAlias?: string;
  onSave?: () => void;
  onCancel?: () => void;
};

export function RoleFormPage({ mode, roleAlias, onSave, onCancel }: RoleFormPageProps) {
  const isAdd = mode === "add";

  const [name, setName] = useState(isAdd ? "" : `Role ${roleAlias ?? ""}`);
  const [description, setDescription] = useState(
    isAdd ? "" : "Sample role description for editing.",
  );
  const [isTenantRole, setIsTenantRole] = useState(false);
  const [enableCacheRefresh, setEnableCacheRefresh] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  function handleSave() {
    if (!name.trim()) return;
    setShowSuccess(true);
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      {/* Top bar */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          {!isAdd && (
            <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
              <span>eStore</span>
              <span className="mx-2">/</span>
              <span>Access</span>
              <span className="mx-2">/</span>
              <button
                type="button"
                className="text-on-surface-variant hover:text-primary transition-colors"
                onClick={onCancel}
              >
                Roles
              </button>
              <span className="mx-2">/</span>
              <span className="text-primary">Edit Role</span>
            </nav>
          )}
          <button
            type="button"
            onClick={onCancel}
            className="flex items-center gap-2 text-primary font-bold text-xs uppercase tracking-tighter mb-1 hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Roles
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? "Create New Role" : `Edit Role: ${name}`}
          </h2>
        </div>
        <div className="flex items-center gap-3">
          <button
            type="button"
            className="rounded-md border border-outline-variant/30 px-5 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={!name.trim()}
            className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
            onClick={handleSave}
          >
            <IconSave className="h-4 w-4 shrink-0" />
            {isAdd ? "Create Role" : "Save Changes"}
          </button>
        </div>
      </div>

      {/* Body */}
      <div className="flex-1 p-6 max-w-2xl">
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="border-b border-outline-variant/10 px-6 py-4">
            <h3 className={sectionTitleRow}>Role Details</h3>
          </div>
          <div className="px-6 py-6 space-y-5">
            <div>
              <label className={labelBase}>
                Role Name <span className="text-error">*</span>
              </label>
              <input
                className={inputBase}
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Brand Manager"
              />
            </div>
            <div>
              <label className={labelBase}>Description</label>
              <textarea
                className={`${inputBase} resize-none`}
                rows={3}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Describe the purpose and responsibilities of this role."
              />
            </div>
            <div className="flex flex-col gap-4 pt-1">
              <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                  checked={isTenantRole}
                  onChange={(e) => setIsTenantRole(e.target.checked)}
                />
                <div>
                  <p className="text-xs font-bold text-on-surface">Tenant Role</p>
                  <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                    When enabled, this role is scoped to a specific tenant. Users assigned this role can only manage resources within their assigned tenant.
                  </p>
                </div>
              </label>
              <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                <input
                  type="checkbox"
                  className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                  checked={enableCacheRefresh}
                  onChange={(e) => setEnableCacheRefresh(e.target.checked)}
                />
                <div>
                  <p className="text-xs font-bold text-on-surface">Enable Website Cache Refresh</p>
                  <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                    Allow members of this role to trigger a website cache refresh from the backoffice.
                  </p>
                </div>
              </label>
            </div>
          </div>
        </section>
      </div>

      {/* Success modal */}
      {showSuccess && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setShowSuccess(false)}
        >
          <div
            className="bg-surface-container-lowest rounded-2xl shadow-2xl p-8 max-w-sm w-full mx-4 text-center"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="w-16 h-16 rounded-full bg-secondary-container/20 flex items-center justify-center mx-auto mb-4">
              <IconCheckCircle className="h-8 w-8 text-secondary" />
            </div>
            <h3 className="text-lg font-bold text-on-surface mb-2">
              {isAdd ? "Role Created!" : "Changes Saved!"}
            </h3>
            <p className="text-sm text-on-surface-variant mb-6">
              {isAdd ? `The role "${name}" has been created.` : `"${name}" has been updated successfully.`}
            </p>
            <div className="flex gap-3 justify-center">
              <button
                type="button"
                className="px-5 py-2 rounded-md border border-outline-variant/30 text-on-surface-variant font-bold text-xs uppercase tracking-widest hover:bg-surface-container-high transition-colors"
                onClick={() => { setShowSuccess(false); onSave?.(); }}
              >
                Back to Roles
              </button>
              <button
                type="button"
                className="px-5 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest hover:bg-primary-container transition-colors"
                onClick={() => setShowSuccess(false)}
              >
                {isAdd ? "Add Another" : "Continue Editing"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
