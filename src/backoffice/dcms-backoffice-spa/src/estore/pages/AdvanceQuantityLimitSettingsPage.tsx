import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCalendarToday,
  IconChevronDown,
  IconChevronRight,
  IconClose,
  IconFolder,
  IconFolderOpen,
  IconSave,
  IconSearch,
  IconTag,
  IconUnfoldLess,
  IconUnfoldMore,
} from "../../orders/icons";
import { fetchBrands } from "../api/brandsApi";
import { fetchCategories } from "../api/categoriesApi";
import { fetchProducts } from "../api/productsApi";
import {
  createQuantityLimitRule,
  updateQuantityLimitRule,
  type QuantityLimitRule,
} from "../api/quantityLimitSettingsApi";
import type { CatNode } from "./CategoriesPage";

const fieldBase =
  "h-9 w-full rounded border border-outline-variant/30 bg-surface-container-lowest px-3 text-xs text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container disabled:opacity-40";

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
  rule?: QuantityLimitRule;
  onBack: () => void;
  onSave: (rule: QuantityLimitRule) => void;
};

function findCategoryName(tree: CatNode[], id: string): string | undefined {
  for (const node of tree) {
    if (node.id === id) return node.name;
    if (node.children?.length) {
      const found = findCategoryName(node.children, id);
      if (found) return found;
    }
  }
  return undefined;
}

function collectExpandableCategoryIds(tree: CatNode[], acc: string[] = []): string[] {
  for (const node of tree) {
    if (node.children?.length) {
      acc.push(node.id);
      collectExpandableCategoryIds(node.children, acc);
    }
  }
  return acc;
}

function CategoryTreeSelect({
  tree,
  value,
  onChange,
  placeholder = "",
}: {
  tree: CatNode[];
  value: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState("");
  const allExpandableIds = useMemo(() => collectExpandableCategoryIds(tree), [tree]);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(allExpandableIds));
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setExpanded(new Set(allExpandableIds));
  }, [allExpandableIds]);

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  const filterLower = filter.trim().toLowerCase();
  const selected = new Set(value);
  const expandedEffective = filterLower ? new Set(allExpandableIds) : expanded;

  const nodeMatches = (node: CatNode): boolean => {
    if (!filterLower) return true;
    if (node.name.toLowerCase().includes(filterLower)) return true;
    return node.children?.some(nodeMatches) ?? false;
  };

  function toggle(id: string) {
    onChange(value.includes(id) ? value.filter((x) => x !== id) : [...value, id]);
  }

  function toggleExpand(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function renderNode(node: CatNode, depth = 0): React.ReactNode {
    if (!nodeMatches(node)) return null;
    const hasChildren = Boolean(node.children?.length);
    const isOpen = hasChildren && expandedEffective.has(node.id);
    const checked = selected.has(node.id);

    return (
      <div key={node.id} className={depth ? "mt-1" : ""} style={{ marginLeft: depth ? 20 : 0 }}>
        <label
          className={`flex cursor-pointer select-none items-center gap-1 rounded p-1.5 text-[13px] transition-colors ${
            checked ? "bg-primary/10 font-semibold text-primary" : "text-on-surface-variant hover:bg-surface-container-high"
          }`}
        >
          {hasChildren ? (
            <button type="button" className="shrink-0 rounded p-0.5 hover:bg-surface-container-high" aria-expanded={isOpen} onClick={(e) => { e.preventDefault(); toggleExpand(node.id); }}>
              {isOpen ? <IconChevronDown className="h-4 w-4" /> : <IconChevronRight className="h-4 w-4" />}
            </button>
          ) : (
            <span className="inline-flex w-5 shrink-0" aria-hidden />
          )}
          <input type="checkbox" className="h-3.5 w-3.5 shrink-0 accent-primary" checked={checked} onChange={() => toggle(node.id)} />
          {hasChildren ? (
            isOpen ? <IconFolderOpen className="h-4 w-4 shrink-0 text-primary/80" /> : <IconFolder className="h-4 w-4 shrink-0 text-primary/60" />
          ) : (
            <IconTag className="h-4 w-4 shrink-0 opacity-80" />
          )}
          <span className="truncate">{node.name}</span>
        </label>
        {hasChildren && isOpen && node.children && (
          <div className="ml-5 mt-1 space-y-1 border-l border-outline-variant/30 pl-2">
            {node.children.map((child) => renderNode(child, depth + 1))}
          </div>
        )}
      </div>
    );
  }

  const visibleRoots = tree.filter(nodeMatches);
  const label = value.length === 0 ? placeholder : `${value.length} selected`;

  return (
    <div ref={ref} className="relative">
      <button type="button" className={`${fieldBase} flex items-center justify-between text-left`} onClick={() => setOpen((o) => !o)}>
        <span className={value.length ? "truncate font-semibold" : "truncate text-on-surface-variant"}>{label || "Click to choose categories"}</span>
        <IconChevronDown className={`h-4 w-4 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute left-0 right-0 top-full z-50 mt-1 rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
          <div className="p-2">
            {value.length > 0 && (
              <div className="mb-2 flex flex-wrap gap-1.5">
                {value.map((id) => {
                  const name = findCategoryName(tree, id) ?? id;
                  return (
                    <span key={id} className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-bold text-primary">
                      {name}
                      <button type="button" className="rounded p-0.5 hover:bg-primary/20" onClick={() => toggle(id)} aria-label={`Remove ${name}`}>
                        <IconClose className="h-2.5 w-2.5" />
                      </button>
                    </span>
                  );
                })}
              </div>
            )}
            <div className="relative mb-2">
              <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
              <input autoFocus className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest py-2 pl-8 pr-3 text-xs outline-none focus:ring-1 focus:ring-primary" placeholder="Filter categories..." value={filter} onChange={(e) => setFilter(e.target.value)} />
            </div>
            <div className="rounded-lg border border-outline-variant/20 bg-surface-container-lowest">
              <div className="flex items-center justify-between border-b border-outline-variant/10 px-3 py-1.5">
                <span className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Hierarchy</span>
                <div className="flex gap-1">
                  <button type="button" className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high" title="Expand all" onClick={() => setExpanded(new Set(allExpandableIds))}><IconUnfoldMore className="h-3.5 w-3.5" /></button>
                  <button type="button" className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high" title="Collapse all" onClick={() => setExpanded(new Set())}><IconUnfoldLess className="h-3.5 w-3.5" /></button>
                </div>
              </div>
              <div className="max-h-64 space-y-1 overflow-y-auto p-2">
                {visibleRoots.length === 0 ? (
                  <p className="px-2 py-4 text-center text-[11px] italic text-on-surface-variant">No categories match.</p>
                ) : (
                  visibleRoots.map((node) => renderNode(node))
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function DateInput({ value, onChange }: { value: string; onChange: (next: string) => void }) {
  const ref = useRef<HTMLInputElement>(null);
  function openPicker() {
    const el = ref.current;
    if (!el) return;
    if (typeof el.showPicker === "function") el.showPicker();
    else el.focus();
  }
  return (
    <div className="flex w-[280px]">
      <input ref={ref} type="date" className={`${fieldBase} rounded-r-none`} value={value} onChange={(e) => onChange(e.target.value)} />
      <button type="button" className="h-9 border-y border-outline-variant/30 px-3 text-on-surface-variant" onClick={() => onChange("")}>×</button>
      <button type="button" className="h-9 rounded-r border border-outline-variant/30 px-3 text-on-surface-variant" onClick={openPicker}>
        <IconCalendarToday className="h-4 w-4" />
      </button>
    </div>
  );
}

export function AdvanceQuantityLimitSettingsPage({ tenantId, storeId, authToken, rule, onBack, onSave }: Props) {
  const apiReady = Boolean(tenantId && storeId);

  const [name, setName] = useState(rule?.name ?? "");
  const [startDate, setStartDate] = useState(rule?.startDate ?? "");
  const [endDate, setEndDate] = useState(rule?.endDate ?? "");
  const [limitType, setLimitType] = useState<"per_cart" | "per_user">(rule?.limitType ?? "per_cart");
  const [perProduct, setPerProduct] = useState(rule?.perProduct ?? false);
  const [brandId, setBrandId] = useState(rule?.brandId ?? "");
  const [categoryUi, setCategoryUi] = useState<string[]>(rule?.categoryIds.map(String) ?? []);
  const [productId, setProductId] = useState(rule?.productId ?? "");
  const [productLabel, setProductLabel] = useState("");
  const [productQuery, setProductQuery] = useState("");
  const [productSuggestions, setProductSuggestions] = useState<{ id: string; name: string }[]>([]);
  const [membershipType, setMembershipType] = useState(rule?.membershipType ?? "");
  const [membershipTier, setMembershipTier] = useState(rule?.membershipTier ?? "");
  const [quantityLimit, setQuantityLimit] = useState(rule ? String(rule.quantityLimit) : "");
  const [categoryTree, setCategoryTree] = useState<CatNode[]>([]);
  const [brands, setBrands] = useState<{ code: string; name: string }[]>([]);
  const [loading, setLoading] = useState(apiReady);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!apiReady) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    void (async () => {
      try {
        const [cats, brandRes] = await Promise.all([
          fetchCategories(tenantId!, authToken),
          fetchBrands(tenantId!, { pageSize: 200 }, authToken),
        ]);
        if (cancelled) return;
        setCategoryTree(cats);
        setBrands(brandRes.rows.map((b) => ({ code: b.code, name: b.name })));
        if (rule?.productId) {
          const { rows } = await fetchProducts(tenantId!, storeId!, undefined, { page: 1, pageSize: 100 }, authToken);
          const match = rows.find((r) => r.id === rule.productId);
          if (match) setProductLabel(match.name);
        }
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : "Failed to load form data.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [apiReady, tenantId, storeId, authToken, rule?.productId]);

  const searchProducts = useCallback(async () => {
    const q = productQuery.trim();
    if (!apiReady || q.length < 2) {
      setProductSuggestions([]);
      return;
    }
    try {
      const { rows } = await fetchProducts(tenantId!, storeId!, { name: q }, { page: 1, pageSize: 10 }, authToken);
      setProductSuggestions(rows.map((r) => ({ id: r.id, name: r.name })));
    } catch {
      setProductSuggestions([]);
    }
  }, [apiReady, productQuery, tenantId, storeId, authToken]);

  useEffect(() => {
    const t = setTimeout(() => void searchProducts(), 300);
    return () => clearTimeout(t);
  }, [searchProducts]);

  async function save() {
    const trimmedName = name.trim();
    if (!trimmedName) {
      setError("Name is required.");
      return;
    }
    if (!startDate) {
      setError("Start date is required.");
      return;
    }
    const qty = Number(quantityLimit);
    if (!Number.isFinite(qty) || qty <= 0) {
      setError("Quantity limit must be a positive number.");
      return;
    }

    const payload = {
      name: trimmedName,
      limitType,
      perProduct,
      quantityLimit: qty,
      startDate,
      endDate: endDate || null,
      brandId: brandId || null,
      categoryIds: categoryUi.map((x) => Number(x)).filter((n) => n > 0),
      productId: productId || null,
      membershipType: membershipType || null,
      membershipTier: membershipTier || null,
    };

    if (!apiReady) {
      onSave({
        id: rule?.id ?? `local-${Date.now()}`,
        modifiedBy: rule?.modifiedBy ?? "",
        updatedAt: null,
        ...payload,
      });
      return;
    }

    setSaving(true);
    setError(null);
    try {
      const saved = rule?.id
        ? await updateQuantityLimitRule(tenantId!, storeId!, rule.id, payload, authToken)
        : await createQuantityLimitRule(tenantId!, storeId!, payload, authToken);
      onSave(saved);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save rule.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="-m-6 min-h-[calc(100dvh-6rem)] bg-surface-container-low text-on-surface" aria-label="Advance quantity limit settings">
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button type="button" onClick={onBack} className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80">
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Product Quantity Limit Settings
          </button>
          <h2 className="flex flex-wrap items-center gap-3 text-2xl font-bold tracking-tight text-on-surface">
            {rule ? "Edit Advance Quantity Limit Settings" : "Add Advance Quantity Limit Settings"}
          </h2>
        </div>
        <button type="button" className={btnPrimary} disabled={saving || loading} onClick={() => void save()}>
          <IconSave className="h-4 w-4 shrink-0" />
          {saving ? "Saving…" : "Save and Approve"}
        </button>
      </div>

      <main className="space-y-3 p-5">
        {!apiReady && (
          <p className="rounded-md border border-outline-variant/20 bg-surface px-4 py-3 text-xs text-on-surface-variant">
            Demo mode — rule changes are kept in memory only.
          </p>
        )}
        {error ? (
          <p className="rounded-md border border-error/30 bg-error/10 px-4 py-3 text-xs text-on-error-container">{error}</p>
        ) : null}
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-on-surface-variant">Loading…</p>
        ) : (
          <>
            <section className="overflow-hidden rounded border border-outline-variant/25 bg-surface-container-lowest">
              <div className="border-b border-outline-variant/25 bg-surface-container px-5 py-3 text-xs text-on-surface-variant">General Information</div>
              <div className="space-y-5 px-9 py-5">
                <div className="grid max-w-[1320px] grid-cols-[540px_730px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Name *</label>
                  <input className={fieldBase} value={name} onChange={(e) => setName(e.target.value)} />
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Start Date *</label>
                  <DateInput value={startDate} onChange={setStartDate} />
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">End Date</label>
                  <DateInput value={endDate} onChange={setEndDate} />
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Limit Type *</label>
                  <div className="flex items-center gap-3 text-sm text-on-surface-variant">
                    <label className="inline-flex items-center gap-1"><input type="radio" className="accent-primary" checked={limitType === "per_cart"} onChange={() => setLimitType("per_cart")} />Per Cart</label>
                    <label className="inline-flex items-center gap-1"><input type="radio" className="accent-primary" checked={limitType === "per_user"} onChange={() => setLimitType("per_user")} />Per User</label>
                  </div>
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Per Product</label>
                  <input type="checkbox" className="h-4 w-4 accent-primary" checked={perProduct} onChange={(e) => setPerProduct(e.target.checked)} />
                </div>
              </div>
            </section>

            <section className="relative rounded border border-outline-variant/25 bg-surface-container-lowest">
              <div className="border-b border-outline-variant/25 bg-surface-container px-5 py-3 text-xs text-on-surface-variant">Qualifiers</div>
              <div className="space-y-4 px-9 py-5">
                <div className="grid max-w-[1320px] grid-cols-[540px_730px_24px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Brand</label>
                  <select className={fieldBase} value={brandId} onChange={(e) => setBrandId(e.target.value)}>
                    <option value=""></option>
                    {brands.map((b) => (
                      <option key={b.code} value={b.code}>{b.name}</option>
                    ))}
                  </select>
                  <span className="text-center text-error">⊗</span>
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px_24px] items-start gap-x-8">
                  <label className="pt-2 text-sm text-on-surface-variant">Category</label>
                  <CategoryTreeSelect tree={categoryTree} value={categoryUi} onChange={setCategoryUi} />
                  <span className="pt-2 text-center text-error">⊗</span>
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px_24px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Product</label>
                  <div className="relative space-y-1">
                    {productId && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-bold text-primary">
                        {productLabel || productId}
                        <button type="button" className="rounded p-0.5 hover:bg-primary/20" onClick={() => { setProductId(""); setProductLabel(""); setProductQuery(""); }} aria-label="Clear product">
                          <IconClose className="h-2.5 w-2.5" />
                        </button>
                      </span>
                    )}
                    {!productId && (
                      <>
                        <input className={`${fieldBase} pr-9`} placeholder="Search product by name (min 2 chars)…" value={productQuery} onChange={(e) => setProductQuery(e.target.value)} />
                        <IconSearch className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant" />
                        {productSuggestions.length > 0 && (
                          <ul className="absolute left-0 right-0 top-full z-30 mt-1 max-h-40 overflow-y-auto rounded-md border border-outline-variant/20 bg-surface shadow-lg">
                            {productSuggestions.map((s) => (
                              <li key={s.id}>
                                <button type="button" className="w-full px-3 py-2 text-left text-xs hover:bg-surface-container-high" onClick={() => { setProductId(s.id); setProductLabel(s.name); setProductQuery(""); setProductSuggestions([]); }}>
                                  {s.name}
                                </button>
                              </li>
                            ))}
                          </ul>
                        )}
                      </>
                    )}
                  </div>
                  <span />
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px_24px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Membership Type</label>
                  <select className={fieldBase} value={membershipType} onChange={(e) => setMembershipType(e.target.value)}>
                    <option value=""></option>
                    <option value="Standard">Standard</option>
                    <option value="VIP">VIP</option>
                  </select>
                  <span className="text-center text-error">⊗</span>
                </div>
                <div className="grid max-w-[1320px] grid-cols-[540px_730px_24px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Membership Tier</label>
                  <select className={fieldBase} value={membershipTier} onChange={(e) => setMembershipTier(e.target.value)}>
                    <option value=""></option>
                    <option value="Silver">Silver</option>
                    <option value="Gold">Gold</option>
                    <option value="Platinum">Platinum</option>
                  </select>
                  <span className="text-center text-error">⊗</span>
                </div>
              </div>
            </section>

            <section className="overflow-hidden rounded border border-outline-variant/25 bg-surface-container-lowest">
              <div className="border-b border-outline-variant/25 bg-surface-container px-5 py-3 text-xs text-on-surface-variant">Settings</div>
              <div className="px-9 py-5">
                <div className="grid max-w-[1320px] grid-cols-[540px_350px] items-center gap-x-8">
                  <label className="text-sm text-on-surface-variant">Quantity Limit *</label>
                  <input className={fieldBase} value={quantityLimit} onChange={(e) => setQuantityLimit(e.target.value)} />
                </div>
              </div>
            </section>
          </>
        )}
      </main>
    </div>
  );
}
