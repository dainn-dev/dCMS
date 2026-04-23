import { useEffect, useRef, useState } from "react";
import { IconChevronDown, IconClose, IconSearch } from "../../../orders/icons";

const labelBase = "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-xs text-on-surface-variant";

// DAI-620: TODO replace with `useProducts(...)` hook when campaign editor accepts tenantId/storeId.
const PRODUCT_OPTIONS: string[] = [];

export type PwpItemMechanicsValue = {
  promotionProducts: string[];
  qualifyingProductsPerSet: string;
  maxPromotionalProductsPerUser: string;
};

function ProductSearchMultiSelect({
  selected,
  onChange,
}: {
  selected: string[];
  onChange: (next: string[]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const filtered = PRODUCT_OPTIONS.filter((o) => o.toLowerCase().includes(search.toLowerCase()));

  function toggle(opt: string) {
    onChange(selected.includes(opt) ? selected.filter((s) => s !== opt) : [...selected, opt]);
  }

  return (
    <div ref={ref} className="relative space-y-1.5">
      <label className={labelBase}>Select promotion products</label>
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((s) => (
            <span
              key={s}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-bold uppercase text-primary"
            >
              {s}
              <button type="button" onClick={() => toggle(s)} className="rounded p-0.5 hover:bg-primary/20 transition-colors" aria-label={`Remove ${s}`}>
                <IconClose className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center gap-2 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs transition-colors hover:border-primary/30 focus:outline-none focus:ring-1 focus:ring-primary"
      >
        <IconSearch className="h-3.5 w-3.5 shrink-0 text-on-surface-variant" />
        <span className={selected.length === 0 ? "flex-1 text-left text-on-surface-variant/60" : "flex-1 text-left text-on-surface"}>
          {selected.length === 0 ? "Search products by name or SKU…" : `${selected.length} product(s) selected`}
        </span>
        <IconChevronDown className={`h-3.5 w-3.5 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute left-0 top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
          <div className="border-b border-outline-variant/10 p-2">
            <input
              autoFocus
              type="text"
              placeholder="Search…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="w-full rounded bg-surface-container-low px-2.5 py-1.5 text-xs outline-none placeholder:text-on-surface-variant/50"
            />
          </div>
          <div className="max-h-52 overflow-y-auto">
            {filtered.length === 0 ? (
              <p className="px-3 py-3 text-center text-xs text-on-surface-variant">No results</p>
            ) : (
              filtered.map((opt) => (
                <label
                  key={opt}
                  className="flex cursor-pointer items-center gap-2.5 px-3 py-2 hover:bg-surface-container transition-colors select-none"
                >
                  <input type="checkbox" className="h-3.5 w-3.5 accent-primary shrink-0" checked={selected.includes(opt)} onChange={() => toggle(opt)} />
                  <span className="text-xs text-on-surface">{opt}</span>
                </label>
              ))
            )}
          </div>
        </div>
      )}
      <p className={hintText}>Promotional items customers may add when qualifying cart rules are met.</p>
    </div>
  );
}

type Props = {
  value: PwpItemMechanicsValue;
  onChange: (next: PwpItemMechanicsValue) => void;
};

export function PwpItemMechanicsSection({ value, onChange }: Props) {
  function patch(p: Partial<PwpItemMechanicsValue>) {
    onChange({ ...value, ...p });
  }

  return (
    <div className="space-y-6">
      <ProductSearchMultiSelect selected={value.promotionProducts} onChange={(promotionProducts) => patch({ promotionProducts })} />
      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <div>
          <label className={labelBase}>No. of qualifying products per set</label>
          <input
            type="number"
            min="1"
            step="1"
            className={inputBase}
            placeholder="e.g. 2"
            value={value.qualifyingProductsPerSet}
            onChange={(e) => patch({ qualifyingProductsPerSet: e.target.value })}
          />
          <p className={hintText}>Minimum qualifying units required to unlock one promotional set.</p>
        </div>
        <div>
          <label className={labelBase}>Maximum no. of promotional products per user</label>
          <input
            type="number"
            min="0"
            step="1"
            className={inputBase}
            placeholder="e.g. 3"
            value={value.maxPromotionalProductsPerUser}
            onChange={(e) => patch({ maxPromotionalProductsPerUser: e.target.value })}
          />
          <p className={hintText}>Cap on promotional SKUs per customer for this campaign.</p>
        </div>
      </div>
    </div>
  );
}
