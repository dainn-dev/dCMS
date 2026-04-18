import type { ReactNode } from "react";
import { IconFilterList } from "../../orders/icons";

const inputClass =
  "rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs text-on-surface focus:ring-1 focus:ring-primary outline-none";

type Props = {
  children: ReactNode;
  onSearch: () => void;
  onReset: () => void;
  searchDisabled?: boolean;
};

export function ReportFilterPanel({ children, onSearch, onReset, searchDisabled }: Props) {
  return (
    <section
      className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-5 shadow-sm"
      aria-label="Report filters"
    >
      <div className="mb-4 flex items-center gap-2 border-b border-outline-variant/10 pb-3">
        <IconFilterList className="h-4 w-4 text-primary" aria-hidden />
        <h2 className="text-xs font-bold uppercase tracking-wider text-on-surface">Filters</h2>
      </div>
      <div className="flex flex-wrap items-end gap-4">{children}</div>
      <div className="mt-5 flex flex-wrap gap-2 border-t border-outline-variant/10 pt-4">
        <button
          type="button"
          disabled={searchDisabled}
          className="rounded-lg bg-primary px-4 py-2 text-xs font-bold text-on-primary shadow-sm hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
          onClick={onSearch}
        >
          Search
        </button>
        <button
          type="button"
          className="rounded-lg border border-outline-variant/30 bg-white px-4 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
          onClick={onReset}
        >
          Reset
        </button>
      </div>
    </section>
  );
}

export function ReportFilterField({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor?: string;
  children: ReactNode;
}) {
  return (
    <div className="flex min-w-[140px] flex-col gap-1">
      <label htmlFor={htmlFor} className="text-xs font-bold uppercase tracking-wider text-on-surface-variant">
        {label}
      </label>
      {children}
    </div>
  );
}

export { inputClass };
