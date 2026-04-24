import { useState } from "react";
import { IconCheckCircle, IconClose, IconKey, IconWarning } from "../../../orders/icons";
import { changePassword } from "../../api/usersApi";

type ChangePasswordModalProps = {
  userId: string;
  userName: string;
  onSave?: () => void;
  onClose?: () => void;
};

const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";

type PasswordErrors = { newPassword?: string; confirm?: string };

function validate(newPassword: string, confirm: string): PasswordErrors {
  const errs: PasswordErrors = {};
  if (newPassword.length < 8) {
    errs.newPassword = "At least 8 characters required.";
  } else if (!/[A-Z]/.test(newPassword)) {
    errs.newPassword = "Must include an uppercase letter.";
  } else if (!/[a-z]/.test(newPassword)) {
    errs.newPassword = "Must include a lowercase letter.";
  } else if (!/[0-9]/.test(newPassword)) {
    errs.newPassword = "Must include a digit.";
  }
  if (confirm.length > 0 && newPassword !== confirm) {
    errs.confirm = "Passwords do not match.";
  }
  return errs;
}

export function ChangePasswordModal({ userId, userName, onSave, onClose }: ChangePasswordModalProps) {
  const [newPassword, setNewPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [errors, setErrors] = useState<PasswordErrors>({});
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  function handleSave() {
    const errs = validate(newPassword, confirm);
    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      return;
    }
    setSaving(true);
    setApiError(null);
    changePassword(Number(userId), "", newPassword)
      .then(() => setSaved(true))
      .catch((e: unknown) => setApiError(e instanceof Error ? e.message : "Failed to change password"))
      .finally(() => setSaving(false));
  }

  if (saved) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
        <div className="w-[380px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl p-8 text-center">
          <div className="w-16 h-16 rounded-full bg-secondary-container/20 flex items-center justify-center mx-auto mb-4">
            <IconCheckCircle className="h-8 w-8 text-secondary" />
          </div>
          <h3 className="text-base font-bold text-on-surface mb-2">Password Updated</h3>
          <p className="text-xs text-on-surface-variant mb-6">
            The password for <span className="font-semibold text-on-surface">{userName}</span> has been changed successfully.
          </p>
          <button
            type="button"
            className="px-6 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest hover:bg-primary-container transition-colors"
            onClick={() => { onSave?.(); onClose?.(); }}
          >
            Done
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
      <div className="w-[460px] max-w-[calc(100vw-2rem)] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-start justify-between gap-4 border-b border-outline-variant/10 px-6 py-5">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/10">
              <IconKey className="h-5 w-5 text-primary" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-on-surface">Change Password</h3>
              <p className="text-xs text-on-surface-variant">For user: <span className="font-semibold text-on-surface">{userName}</span></p>
            </div>
          </div>
          <button
            type="button"
            className="rounded p-1.5 hover:bg-surface-container transition-colors"
            aria-label="Close"
            onClick={onClose}
          >
            <IconClose className="h-4 w-4 text-on-surface-variant" />
          </button>
        </div>

        {/* Body */}
        <div className="px-6 py-6 space-y-4">
          <div>
            <label className={labelBase}>
              New Password <span className="text-error">*</span>
            </label>
            <input
              type="password"
              className={`${inputBase} ${errors.newPassword ? "border-error ring-1 ring-error" : ""}`}
              value={newPassword}
              onChange={(e) => {
                setNewPassword(e.target.value);
                if (errors.newPassword) setErrors((p) => ({ ...p, newPassword: undefined }));
              }}
              placeholder="Min 8 chars, upper+lower+digit"
            />
            {errors.newPassword && (
              <p className="mt-1 flex items-center gap-1 text-[10px] text-error">
                <IconWarning className="h-3 w-3 shrink-0" />
                {errors.newPassword}
              </p>
            )}
          </div>
          <div>
            <label className={labelBase}>
              Confirm New Password <span className="text-error">*</span>
            </label>
            <input
              type="password"
              className={`${inputBase} ${errors.confirm ? "border-error ring-1 ring-error" : ""}`}
              value={confirm}
              onChange={(e) => {
                setConfirm(e.target.value);
                if (errors.confirm) setErrors((p) => ({ ...p, confirm: undefined }));
              }}
              placeholder="Re-enter new password"
            />
            {errors.confirm && (
              <p className="mt-1 flex items-center gap-1 text-[10px] text-error">
                <IconWarning className="h-3 w-3 shrink-0" />
                {errors.confirm}
              </p>
            )}
          </div>
          <p className="text-[10px] text-on-surface-variant leading-relaxed">
            Password requirements: minimum 8 characters, at least one uppercase letter, one lowercase letter, and one digit.
          </p>
          {apiError && (
            <p className="rounded border border-error/30 bg-error/5 px-3 py-2 text-[11px] text-error">{apiError}</p>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
          <button
            type="button"
            className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
            onClick={onClose}
          >
            Cancel
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
            disabled={!newPassword || !confirm || saving}
            onClick={handleSave}
          >
            <IconCheckCircle className="h-4 w-4 shrink-0" />
            Save Password
          </button>
        </div>
      </div>
    </div>
  );
}
