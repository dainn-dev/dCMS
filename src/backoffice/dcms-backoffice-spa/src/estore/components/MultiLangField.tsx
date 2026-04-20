import { useState } from "react";
import { useLanguages } from "../LanguageContext";
import type { UmbracoLanguage } from "../useUmbracoLanguages";

// ── Style tokens ──────────────────────────────────────────────────────────────
export const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
export const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";

// ── Helpers ───────────────────────────────────────────────────────────────────

/** "en-US" → "en", "zh-CN" → "zh", "vi-VN" → "vi" */
export function langKey(isoCode: string): string {
  return isoCode.split("-")[0].toLowerCase();
}

/** "en-US" → "EN", "zh-CN" → "ZH" */
function langPill(isoCode: string): string {
  return isoCode.split("-")[0].toUpperCase();
}

// ── Internal: pill switcher anchored to top-right of sibling input/textarea ──

export function LangSwitcher({
  languages,
  activeIso,
  values,
  onSwitch,
}: {
  languages: UmbracoLanguage[];
  activeIso: string;
  values: Record<string, string>;
  onSwitch: (isoCode: string) => void;
}) {
  if (languages.length === 0) return null;

  return (
    <div className="absolute right-0 top-0 -translate-y-full flex items-center gap-px rounded-t border border-b-0 border-outline-variant/20 bg-surface-container overflow-hidden z-10">
      {languages.map((lang) => {
        const isActive = activeIso === lang.isoCode;
        const hasValue = (values[langKey(lang.isoCode)] ?? "").trim().length > 0;
        return (
          <button
            key={lang.isoCode}
            type="button"
            title={lang.name}
            onClick={() => onSwitch(lang.isoCode)}
            className={`relative flex items-center gap-0.5 px-[0.2rem] text-[9px] font-bold uppercase tracking-wide transition-all ${
              isActive
                ? "bg-primary text-on-primary"
                : "text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface"
            }`}
          >
            {langPill(lang.isoCode)}
            {hasValue && !isActive && (
              <span className="h-1 w-1 rounded-full bg-primary/70 shrink-0" />
            )}
          </button>
        );
      })}
    </div>
  );
}

// ── Shared hook: loads languages once and exposes active iso + values state ───

export function useLangField(
  defaultValues?: Record<string, string>,
  onValuesChange?: (values: Record<string, string>) => void
) {
  const { languages, loading } = useLanguages();

  // While loading, treat as empty so switcher stays hidden
  const resolvedLanguages = loading ? [] : languages;

  const defaultIso =
    resolvedLanguages.find((l) => l.isDefault)?.isoCode ??
    resolvedLanguages[0]?.isoCode ??
    "en-US";

  const [activeIso, setActiveIso] = useState<string>("");

  // Resolve active iso: prefer explicit user selection, then Umbraco default
  const resolvedIso = activeIso || defaultIso;

  const [values, setValues] = useState<Record<string, string>>(
    defaultValues ?? {}
  );

  const setValue = (isoCode: string, text: string) => {
    setValues((prev) => {
      const next = { ...prev, [langKey(isoCode)]: text };
      onValuesChange?.(next);
      return next;
    });
  };

  const currentValue = values[langKey(resolvedIso)] ?? "";

  return { languages: resolvedLanguages, resolvedIso, setActiveIso, values, currentValue, setValue };
}

// ── Multi-language <input> ────────────────────────────────────────────────────

export function MultiLangInput({
  label,
  requiredMark,
  defaultValues,
  onValuesChange,
  onBlur,
  placeholders,
  hint,
  inputClassName,
}: {
  label: string;
  /** Renders a red asterisk after the label (e.g. required fields). */
  requiredMark?: boolean;
  defaultValues?: Record<string, string>;
  /** Called whenever any locale value changes (e.g. mirror state for save/export). */
  onValuesChange?: (values: Record<string, string>) => void;
  /** Called when the active locale input loses focus; receives all locale values. */
  onBlur?: (values: Record<string, string>) => void;
  placeholders?: Record<string, string>;
  hint?: string;
  inputClassName?: string;
}) {
  const { languages, resolvedIso, setActiveIso, values, currentValue, setValue } =
    useLangField(defaultValues, onValuesChange);

  const placeholder =
    placeholders?.[langKey(resolvedIso)] ??
    placeholders?.[resolvedIso] ??
    "";

  return (
    <div className="space-y-1.5">
      {label ? (
        <label className={labelBase}>
          {label}
          {requiredMark ? (
            <>
              {" "}
              <span className="text-error">*</span>
            </>
          ) : null}
        </label>
      ) : null}
      <div className="relative">
        <LangSwitcher
          languages={languages}
          activeIso={resolvedIso}
          values={values}
          onSwitch={setActiveIso}
        />
        <input
          key={resolvedIso}
          className={`${inputBase} ${languages.length > 0 ? "rounded-tr-none" : ""} ${inputClassName ?? ""}`}
          value={currentValue}
          onChange={(e) => setValue(resolvedIso, e.target.value)}
          onBlur={(e) =>
            onBlur?.({ ...values, [langKey(resolvedIso)]: e.currentTarget.value })
          }
          placeholder={placeholder}
        />
      </div>
      {hint && <p className="text-[10px] text-on-surface-variant">{hint}</p>}
    </div>
  );
}

// ── Multi-language <textarea> ─────────────────────────────────────────────────

export function MultiLangTextarea({
  label,
  rows,
  defaultValues,
  placeholders,
  hint,
  className,
}: {
  label: string;
  rows?: number;
  defaultValues?: Record<string, string>;
  placeholders?: Record<string, string>;
  hint?: string;
  className?: string;
}) {
  const { languages, resolvedIso, setActiveIso, values, currentValue, setValue } =
    useLangField(defaultValues);

  const placeholder =
    placeholders?.[langKey(resolvedIso)] ??
    placeholders?.[resolvedIso] ??
    "";

  return (
    <div className="space-y-1.5">
      {label && <label className={labelBase}>{label}</label>}
      <div className="relative">
        <LangSwitcher
          languages={languages}
          activeIso={resolvedIso}
          values={values}
          onSwitch={setActiveIso}
        />
        <textarea
          key={resolvedIso}
          rows={rows ?? 3}
          className={`${inputBase} ${languages.length > 0 ? "rounded-tr-none" : ""} resize-y ${className ?? ""}`}
          value={currentValue}
          onChange={(e) => setValue(resolvedIso, e.target.value)}
          placeholder={placeholder}
        />
      </div>
      {hint && <p className="text-[10px] text-on-surface-variant">{hint}</p>}
    </div>
  );
}
