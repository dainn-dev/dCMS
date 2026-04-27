import { useEffect, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
  IconSave,
} from "../../../orders/icons";
import { createUser, getUser, updateUser } from "../../api/usersApi";
import { fetchRoles, type RoleRow } from "../../api/rolesApi";

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const sectionTitleRow =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2";

type UserFormPageProps = {
  mode: "add" | "edit";
  userId?: string;
  onSave?: () => void;
  onCancel?: () => void;
  authToken?: string;
};

type PasswordError = { password?: string; confirm?: string };

function validatePassword(password: string, confirm: string): PasswordError {
  const errs: PasswordError = {};
  if (password.length < 8) {
    errs.password = "Password must be at least 8 characters.";
  } else if (!/[A-Z]/.test(password)) {
    errs.password = "Password must include at least one uppercase letter.";
  } else if (!/[a-z]/.test(password)) {
    errs.password = "Password must include at least one lowercase letter.";
  } else if (!/[0-9]/.test(password)) {
    errs.password = "Password must include at least one digit.";
  }
  if (password !== confirm) {
    errs.confirm = "Passwords do not match.";
  }
  return errs;
}

export function UserFormPage({ mode, userId, onSave, onCancel, authToken }: UserFormPageProps) {
  const isAdd = mode === "add";

  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [roleAlias, setRoleAlias] = useState("");
  const [roles, setRoles] = useState<RoleRow[]>([]);
  const [active, setActive] = useState(true);
  const [passwordNeverExpires, setPasswordNeverExpires] = useState(false);
  const [passwordErrors, setPasswordErrors] = useState<PasswordError>({});
  const [saving, setSaving] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  // Send notifications
  const [sendNotifOpen, setSendNotifOpen] = useState(false);
  const [sendSubscribers, setSendSubscribers] = useState(false);
  const [sendProductApproval, setSendProductApproval] = useState(false);
  const [sendPromotionApproval, setSendPromotionApproval] = useState(false);
  const [sendContentApproval, setSendContentApproval] = useState(false);
  const [sendCampaignApproval, setSendCampaignApproval] = useState(false);

  // Receive notifications
  const [recvNotifOpen, setRecvNotifOpen] = useState(false);
  const [recvProductApproval, setRecvProductApproval] = useState(false);
  const [recvPromotionApproval, setRecvPromotionApproval] = useState(false);
  const [recvContentApproval, setRecvContentApproval] = useState(false);
  const [recvCampaignApproval, setRecvCampaignApproval] = useState(false);
  const [recvFulfillmentUpdate, setRecvFulfillmentUpdate] = useState(false);
  const [tenantInput, setTenantInput] = useState("");
  const [tenants, setTenants] = useState<string[]>([]);
  const [showSuccess, setShowSuccess] = useState(false);

  useEffect(() => {
    let cancelled = false;
    const loadRoles = fetchRoles(authToken).then((rows) => {
      if (!cancelled) setRoles(rows);
      return rows;
    });

    if (isAdd || !userId) {
      loadRoles.then((rows) => {
        if (!cancelled && rows.length > 0 && !roleAlias) setRoleAlias(rows[0].alias);
      });
      return () => { cancelled = true; };
    }

    Promise.all([loadRoles, getUser(Number(userId))])
      .then(([rows, dto]) => {
        if (cancelled) return;
        setUsername(dto.username);
        setEmail(dto.email);
        setFullName(dto.name);
        const userGroup = dto.groups?.[0] ?? "";
        setRoleAlias(userGroup || (rows.length > 0 ? rows[0].alias : ""));
        setActive(dto.isActive);
      })
      .catch(() => { /* prefill silently fails → user sees empty form */ });
    return () => { cancelled = true; };
  }, [isAdd, userId, authToken]);

  function addTenant() {
    const v = tenantInput.trim();
    if (v && !tenants.includes(v)) {
      setTenants((prev) => [...prev, v]);
    }
    setTenantInput("");
  }

  function removeTenant(t: string) {
    setTenants((prev) => prev.filter((x) => x !== t));
  }

  function handleSave() {
    if (isAdd) {
      const errs = validatePassword(password, confirmPassword);
      setPasswordErrors(errs);
      if (Object.keys(errs).length > 0) return;
    }
    setSaving(true);
    setApiError(null);
    const call = isAdd
      ? createUser({ username, email, name: fullName, userGroupAlias: roleAlias, password })
      : updateUser(Number(userId), { name: fullName, email, userGroupAlias: roleAlias, active });
    call
      .then(() => setShowSuccess(true))
      .catch((e: unknown) => setApiError(e instanceof Error ? e.message : "Save failed"))
      .finally(() => setSaving(false));
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      {/* Top bar */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button
            type="button"
            onClick={onCancel}
            className="flex items-center gap-2 text-primary font-bold text-xs uppercase tracking-tighter mb-1 hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Users
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? "Add New User" : `Edit User: ${fullName}`}
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
            className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90"
            onClick={handleSave}
          >
            <IconSave className="h-4 w-4 shrink-0" />
            {isAdd ? "Create User" : "Save Changes"}
          </button>
        </div>
      </div>

      {/* Body */}
      <div className="flex-1 p-6 space-y-6 max-w-3xl">

        {/* General */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="border-b border-outline-variant/10 px-6 py-4">
            <h3 className={sectionTitleRow}>General</h3>
          </div>
          <div className="px-6 py-6 grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className={labelBase}>
                Username <span className="text-error">*</span>
              </label>
              <input
                className={`${inputBase} ${!isAdd ? "bg-surface-container-high text-on-surface-variant cursor-not-allowed" : ""}`}
                value={username}
                onChange={(e) => isAdd && setUsername(e.target.value)}
                readOnly={!isAdd}
                placeholder="e.g. john.doe"
              />
              {!isAdd && (
                <p className="mt-1 text-[10px] text-on-surface-variant italic">Username cannot be changed after creation.</p>
              )}
            </div>
            <div>
              <label className={labelBase}>
                Email <span className="text-error">*</span>
              </label>
              <input
                className={inputBase}
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="e.g. john.doe@company.com"
              />
            </div>
            <div className="md:col-span-2">
              <label className={labelBase}>
                Full Name <span className="text-error">*</span>
              </label>
              <input
                className={inputBase}
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder="e.g. John Doe"
              />
            </div>
            {isAdd && (
              <>
                <div>
                  <label className={labelBase}>
                    Password <span className="text-error">*</span>
                  </label>
                  <input
                    className={`${inputBase} ${passwordErrors.password ? "border-error ring-1 ring-error" : ""}`}
                    type="password"
                    value={password}
                    onChange={(e) => {
                      setPassword(e.target.value);
                      if (passwordErrors.password) setPasswordErrors((p) => ({ ...p, password: undefined }));
                    }}
                    placeholder="Min 8 chars, upper+lower+digit"
                  />
                  {passwordErrors.password && (
                    <p className="mt-1 text-[10px] text-error">{passwordErrors.password}</p>
                  )}
                </div>
                <div>
                  <label className={labelBase}>
                    Confirm Password <span className="text-error">*</span>
                  </label>
                  <input
                    className={`${inputBase} ${passwordErrors.confirm ? "border-error ring-1 ring-error" : ""}`}
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => {
                      setConfirmPassword(e.target.value);
                      if (passwordErrors.confirm) setPasswordErrors((p) => ({ ...p, confirm: undefined }));
                    }}
                    placeholder="Re-enter password"
                  />
                  {passwordErrors.confirm && (
                    <p className="mt-1 text-[10px] text-error">{passwordErrors.confirm}</p>
                  )}
                </div>
              </>
            )}
            <div>
              <label className={labelBase}>
                Role <span className="text-error">*</span>
              </label>
              <select
                className={inputBase}
                value={roleAlias}
                onChange={(e) => setRoleAlias(e.target.value)}
              >
                {roles.length === 0 && (
                  <option value="">Loading roles…</option>
                )}
                {roles.map((r) => (
                  <option key={r.alias} value={r.alias}>{r.name}</option>
                ))}
              </select>
            </div>
            <div className="flex flex-col gap-3 justify-center">
              <label className="flex cursor-pointer items-center gap-3 select-none">
                <input
                  type="checkbox"
                  className="h-4 w-4 accent-primary"
                  checked={active}
                  onChange={(e) => setActive(e.target.checked)}
                />
                <span className="text-xs font-bold text-on-surface">Active</span>
              </label>
              <label className="flex cursor-pointer items-center gap-3 select-none">
                <input
                  type="checkbox"
                  className="h-4 w-4 accent-primary"
                  checked={passwordNeverExpires}
                  onChange={(e) => setPasswordNeverExpires(e.target.checked)}
                />
                <span className="text-xs font-bold text-on-surface">Password Never Expires</span>
              </label>
            </div>
          </div>
        </section>

        {/* Send Notifications (collapsible) */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <button
            type="button"
            className="flex w-full items-center justify-between border-b border-outline-variant/10 px-6 py-4 hover:bg-surface-container transition-colors"
            onClick={() => setSendNotifOpen((v) => !v)}
          >
            <h3 className="text-sm font-bold uppercase tracking-widest text-primary">Send Notifications</h3>
            <IconChevronDown
              className={`h-4 w-4 text-on-surface-variant transition-transform duration-200 ${sendNotifOpen ? "" : "-rotate-90"}`}
            />
          </button>
          {sendNotifOpen && (
            <div className="px-6 py-5 grid grid-cols-1 sm:grid-cols-2 gap-3">
              {[
                { label: "Subscribers", value: sendSubscribers, onChange: setSendSubscribers },
                { label: "Product Approval", value: sendProductApproval, onChange: setSendProductApproval },
                { label: "Promotion Approval", value: sendPromotionApproval, onChange: setSendPromotionApproval },
                { label: "Content Approval", value: sendContentApproval, onChange: setSendContentApproval },
                { label: "Campaign Approval", value: sendCampaignApproval, onChange: setSendCampaignApproval },
              ].map(({ label, value, onChange }) => (
                <label key={label} className="flex cursor-pointer items-center gap-3 select-none">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-primary"
                    checked={value}
                    onChange={(e) => onChange(e.target.checked)}
                  />
                  <span className="text-xs text-on-surface">{label}</span>
                </label>
              ))}
            </div>
          )}
        </section>

        {/* Receive Notifications (collapsible) */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <button
            type="button"
            className="flex w-full items-center justify-between border-b border-outline-variant/10 px-6 py-4 hover:bg-surface-container transition-colors"
            onClick={() => setRecvNotifOpen((v) => !v)}
          >
            <h3 className="text-sm font-bold uppercase tracking-widest text-primary">Receive Notifications</h3>
            <IconChevronDown
              className={`h-4 w-4 text-on-surface-variant transition-transform duration-200 ${recvNotifOpen ? "" : "-rotate-90"}`}
            />
          </button>
          {recvNotifOpen && (
            <div className="px-6 py-5 space-y-5">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {[
                  { label: "Product Approval", value: recvProductApproval, onChange: setRecvProductApproval },
                  { label: "Promotion Approval", value: recvPromotionApproval, onChange: setRecvPromotionApproval },
                  { label: "Content Approval", value: recvContentApproval, onChange: setRecvContentApproval },
                  { label: "Campaign Approval", value: recvCampaignApproval, onChange: setRecvCampaignApproval },
                  { label: "Fulfillment Option Update", value: recvFulfillmentUpdate, onChange: setRecvFulfillmentUpdate },
                ].map(({ label, value, onChange }) => (
                  <label key={label} className="flex cursor-pointer items-center gap-3 select-none">
                    <input
                      type="checkbox"
                      className="h-4 w-4 accent-primary"
                      checked={value}
                      onChange={(e) => onChange(e.target.checked)}
                    />
                    <span className="text-xs text-on-surface">{label}</span>
                  </label>
                ))}
              </div>

              {/* Tenants multi-select */}
              <div>
                <label className={labelBase}>Tenants</label>
                {tenants.length > 0 && (
                  <div className="flex flex-wrap gap-1.5 mb-2">
                    {tenants.map((t) => (
                      <span
                        key={t}
                        className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
                      >
                        {t}
                        <button
                          type="button"
                          aria-label={`Remove ${t}`}
                          className="rounded p-0.5 hover:bg-primary/20 transition-colors text-primary"
                          onClick={() => removeTenant(t)}
                        >
                          ✕
                        </button>
                      </span>
                    ))}
                  </div>
                )}
                <div className="flex gap-2">
                  <input
                    className={inputBase}
                    value={tenantInput}
                    onChange={(e) => setTenantInput(e.target.value)}
                    onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addTenant(); } }}
                    placeholder="Type tenant name and press Enter or Add"
                  />
                  <button
                    type="button"
                    className="shrink-0 rounded-md border border-outline-variant/30 px-4 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                    onClick={addTenant}
                  >
                    Add
                  </button>
                </div>
              </div>
            </div>
          )}
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
              {isAdd ? "User Created!" : "Changes Saved!"}
            </h3>
            <p className="text-sm text-on-surface-variant mb-6">
              {isAdd
                ? "The new user account has been created successfully."
                : `"${fullName}" has been successfully updated.`}
            </p>
            <div className="flex gap-3 justify-center">
              <button
                type="button"
                className="px-5 py-2 rounded-md border border-outline-variant/30 text-on-surface-variant font-bold text-xs uppercase tracking-widest hover:bg-surface-container-high transition-colors"
                onClick={() => { setShowSuccess(false); onSave?.(); }}
              >
                Back to Users
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
