import { FormEvent, useCallback, useEffect, useState } from "react";
import { ProductCard } from "../components/ProductCard";
import { searchProducts, type ProductListItem } from "../lib/api/catalogApi";
import { useBranch } from "../lib/branch/BranchProvider";
import { useOptionalStoreScope } from "../lib/commerce/StoreContextProvider";

export function ProductListPage() {
  const { bootstrap } = useBranch();
  const scope = useOptionalStoreScope();
  const [q, setQ] = useState("");
  const [query, setQuery] = useState("");
  const [items, setItems] = useState<ProductListItem[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (append: boolean, searchQ: string, pageCursor: string | null) => {
      if (!scope) return;
      setLoading(true);
      setError(null);
      try {
        const result = await searchProducts({
          tenantId: scope.tenantId,
          storeId: scope.storeId,
          q: searchQ || undefined,
          pageSize: 20,
          cursor: pageCursor,
        });
        setItems(prev => (append ? [...prev, ...result.items] : result.items));
        setNextCursor(result.meta.nextCursor ?? null);
      } catch (e) {
        setError((e as Error).message);
      } finally {
        setLoading(false);
      }
    },
    [scope?.tenantId, scope?.storeId],
  );

  useEffect(() => {
    if (!scope) return;
    void load(false, query, null);
  }, [load, query, scope]);

  if (bootstrap === "resolving" || !scope) {
    return <p className="sf-skeleton" role="status">Preparing your store…</p>;
  }

  function onSearch(e: FormEvent) {
    e.preventDefault();
    setQuery(q.trim());
  }

  return (
    <section className="sf-page">
      <header className="sf-page__header">
        <h1>Products</h1>
        <form className="sf-search" onSubmit={onSearch}>
          <label htmlFor="product-search">Search</label>
          <input
            id="product-search"
            type="search"
            value={q}
            onChange={e => setQ(e.target.value)}
            placeholder="Search products"
          />
          <button type="submit" className="sf-btn">Search</button>
        </form>
      </header>

      {loading && items.length === 0 && <p className="sf-skeleton" role="status">Loading products…</p>}
      {error && <p className="sf-alert" role="alert">{error}</p>}
      {!loading && !error && items.length === 0 && (
        <p className="sf-empty" role="status">No products found.</p>
      )}

      <div className="sf-product-grid">
        {items.map(p => <ProductCard key={p.id} product={p} />)}
      </div>

      {nextCursor && (
        <button
          type="button"
          className="sf-btn sf-btn--secondary"
          disabled={loading}
          onClick={() => void load(true, query, nextCursor)}
        >
          {loading ? "Loading…" : "Load more"}
        </button>
      )}
    </section>
  );
}
