import { useEffect, useMemo, useState } from "react";
import { IconCheckCircle, IconSave, IconSearch } from "../../orders/icons";
import type { ProductListRow } from "./ProductsPage";

const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const btnPrimary =
  "px-4 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-primary/20 hover:bg-primary-container transition-colors flex items-center gap-2";

type Props = {
  rows: ProductListRow[];
  bestSellerById: Record<string, boolean>;
  onBestSellerChange: (next: Record<string, boolean>) => void;
  onNavigateToProducts: () => void;
};

export function BestSellerSettingsPage({
  rows,
  bestSellerById,
  onBestSellerChange,
  onNavigateToProducts,
}: Props) {
  const [draft, setDraft] = useState<Record<string, boolean>>(bestSellerById);
  const [filter, setFilter] = useState("");
  const [toast, setToast] = useState(false);

  useEffect(() => {
    setDraft(bestSellerById);
  }, [bestSellerById]);

  const dirty = useMemo(() => JSON.stringify(draft) !== JSON.stringify(bestSellerById), [draft, bestSellerById]);

  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (r) => r.name.toLowerCase().includes(q) || r.upc.toLowerCase().includes(q) || r.sku.toLowerCase().includes(q)
    );
  }, [rows, filter]);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(false), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  function handleSave() {
    onBestSellerChange({ ...draft });
    setToast(true);
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Best seller settings">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-start md:justify-between">
        <div className="space-y-2 min-w-0">
          <nav className="mb-1 flex flex-wrap gap-x-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="text-on-surface-variant/50">/</span>
            <button type="button" className="text-primary hover:underline" onClick={onNavigateToProducts}>
              Products
            </button>
            <span className="text-on-surface-variant/50">/</span>
            <span className="text-primary">Best Seller Settings</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Best Seller Settings</h1>
          <p className="text-sm text-on-surface-variant max-w-2xl">
            Flag products to highlight as best sellers on the storefront. Demo: flags persist in{" "}
            <span className="font-mono text-[11px]">localStorage</span> only.
          </p>
        </div>
        <button
          type="button"
          disabled={!dirty}
          className={btnPrimary + " disabled:opacity-40 disabled:pointer-events-none shrink-0"}
          onClick={handleSave}
        >
          <IconSave className="h-4 w-4 shrink-0" />
          Save
        </button>
      </header>

      <div className="flex-1 space-y-4 p-6">
        <div className="relative max-w-md">
          <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
          <input
            className={`${inputBase} pl-8`}
            placeholder="Search by product name or UPC…"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
            aria-label="Filter best seller list"
          />
        </div>

        <div className="overflow-x-auto rounded-xl border border-outline-variant/15 bg-surface-container-lowest shadow-sm">
          <table className="w-full min-w-[520px] border-collapse text-left text-xs">
            <thead>
              <tr className="border-b border-outline-variant/20 bg-surface-container-low text-on-surface-variant uppercase tracking-wider">
                <th className="px-4 py-3 font-bold w-24">Best seller</th>
                <th className="px-4 py-3 font-bold">Product</th>
                <th className="px-4 py-3 font-bold w-32">UPC</th>
                <th className="px-4 py-3 font-bold w-28">SKU</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((row) => (
                <tr key={row.id} className="border-b border-outline-variant/10 hover:bg-surface-container-low/40">
                  <td className="px-4 py-3">
                    <label className="inline-flex cursor-pointer items-center gap-2">
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-primary"
                        checked={Boolean(draft[row.id])}
                        onChange={(e) =>
                          setDraft((d) => ({
                            ...d,
                            [row.id]: e.target.checked,
                          }))
                        }
                      />
                      <span className="text-on-surface-variant">{draft[row.id] ? "On" : "Off"}</span>
                    </label>
                  </td>
                  <td className="px-4 py-3 font-medium text-on-surface">{row.name}</td>
                  <td className="px-4 py-3 font-mono text-on-surface-variant">{row.upc}</td>
                  <td className="px-4 py-3 font-mono text-on-surface-variant">{row.sku}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {filtered.length === 0 && (
            <p className="px-4 py-10 text-center text-sm text-on-surface-variant italic">No products match this search.</p>
          )}
        </div>
      </div>

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-2 rounded-xl bg-on-surface px-5 py-3 shadow-2xl">
          <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
          <span className="text-sm font-medium text-surface">Best seller settings saved.</span>
        </div>
      )}
    </div>
  );
}
