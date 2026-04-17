import { useEffect, useMemo, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconFilterList,
  IconInfo,
  IconSave,
} from "../../orders/icons";

// ── Types ────────────────────────────────────────────────────────────────────
type Props = { onBack: () => void };

// ── Helper ───────────────────────────────────────────────────────────────────
function parseLines(raw: string): { lines: string[]; blanks: number; duplicates: number } {
  const all = raw.split("\n").map((l) => l.trim());
  const nonEmpty = all.filter(Boolean);
  const unique = new Set(nonEmpty);
  return {
    lines: nonEmpty,
    blanks: all.length - nonEmpty.length,
    duplicates: nonEmpty.length - unique.size,
  };
}

// ── Component ────────────────────────────────────────────────────────────────
export function PromoExclusionListPage({ onBack }: Props) {
  const [raw, setRaw] = useState(
    // Pre-seed with a couple of mock exclusions
    "PROD-001\nPROD-002\nSKU-GIFT-CARD\n"
  );

  const stats = useMemo(() => parseLines(raw), [raw]);

  // ── Save ─────────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  function handleSave() {
    setToast("Exclusion list saved successfully.");
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Promo Codes
          </button>
          <div className="flex items-center gap-3">
            <IconFilterList className="h-6 w-6 shrink-0 text-primary" />
            <div>
              <h2 className="text-2xl font-bold tracking-tight text-on-surface">
                Promotions Exclusion List
              </h2>
              <p className="mt-0.5 text-sm text-on-surface-variant">
                Exclude specific products from all ongoing promotions, even if they qualify.
              </p>
            </div>
          </div>
        </div>

        {/* Save button */}
        <button
          type="button"
          onClick={handleSave}
          className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90"
        >
          <IconSave className="h-4 w-4 shrink-0" />
          Save
        </button>
      </div>

      {/* ── Body ─────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* Info panel */}
        <div className="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
          <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
          <div className="space-y-1 text-xs text-on-surface-variant leading-relaxed">
            <p className="font-semibold text-on-surface">How the Exclusion List works</p>
            <p>
              Any product UPC or SKU entered here will be excluded from all active promotions,
              even if the product meets the promotion criteria.
            </p>
            <p className="font-medium text-on-surface">Only one UPC or SKU per line.</p>
          </div>
        </div>

        {/* Textarea card */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          {/* Section header with stats */}
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 bg-surface-container-low/60 px-6 py-3">
            <h3 className="text-[11px] font-bold uppercase tracking-widest text-on-surface">
              Excluded UPCs / SKUs
            </h3>
            <div className="flex items-center gap-2 ml-auto">
              <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-[9px] font-bold uppercase text-primary">
                {stats.lines.length} entr{stats.lines.length !== 1 ? "ies" : "y"}
              </span>
              {stats.duplicates > 0 && (
                <span className="rounded-full bg-amber-50 px-2.5 py-0.5 text-[9px] font-bold uppercase text-amber-700">
                  {stats.duplicates} duplicate{stats.duplicates !== 1 ? "s" : ""}
                </span>
              )}
              {stats.blanks > 0 && (
                <span className="rounded-full bg-surface-container-high px-2.5 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant">
                  {stats.blanks} blank{stats.blanks !== 1 ? " lines" : " line"}
                </span>
              )}
            </div>
          </div>

          {/* Textarea */}
          <div className="p-6">
            <textarea
              className="w-full min-h-[320px] resize-y rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-4 py-3 font-mono text-xs leading-relaxed text-on-surface outline-none placeholder:text-on-surface-variant/40 focus:ring-1 focus:ring-primary"
              placeholder={"PROD-001\nPROD-002\nSKU-EXAMPLE\n…"}
              value={raw}
              onChange={(e) => setRaw(e.target.value)}
              spellCheck={false}
            />
            <p className="mt-2 text-[10px] text-on-surface-variant">
              Enter one UPC or SKU per line. Blank lines and duplicates will be ignored on save.
            </p>
          </div>

          {/* Preview list (only when entries exist) */}
          {stats.lines.length > 0 && (
            <div className="border-t border-outline-variant/10 px-6 pb-6">
              <p className="mb-3 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
                Preview — {stats.lines.length} unique entr{stats.lines.length !== 1 ? "ies" : "y"}
              </p>
              <div className="flex flex-wrap gap-1.5 max-h-40 overflow-y-auto">
                {[...new Set(stats.lines)].map((entry) => (
                  <span
                    key={entry}
                    className="rounded-full bg-surface-container-high px-3 py-0.5 font-mono text-[10px] text-on-surface-variant"
                  >
                    {entry}
                  </span>
                ))}
              </div>
            </div>
          )}
        </section>

        {/* Save footer */}
        <div className="flex justify-end">
          <button
            type="button"
            onClick={handleSave}
            className="flex items-center gap-2 rounded-lg bg-primary px-6 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95"
          >
            <IconCheckCircle className="h-4 w-4 shrink-0" />
            Save Exclusion List
          </button>
        </div>
      </div>

      {/* ── Toast ────────────────────────────────────────────────────────── */}
      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
