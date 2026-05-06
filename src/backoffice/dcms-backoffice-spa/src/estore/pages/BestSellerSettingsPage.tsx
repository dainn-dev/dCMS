import { useEffect, useMemo, useRef, useState } from "react";
import {
  IconCheckCircle,
  IconChevronDown,
  IconChevronRight,
  IconClose,
  IconFolder,
  IconFolderOpen,
  IconHistory,
  IconSave,
  IconSearch,
  IconTag,
  IconUnfoldLess,
  IconUnfoldMore,
} from "../../orders/icons";
import type { ProductListRow } from "./ProductsPage";

const fieldBase =
  "h-9 w-full rounded border border-outline-variant/30 bg-surface-container-lowest px-3 text-xs text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container";
const btnSecondary =
  "flex items-center gap-2 rounded-md border border-outline-variant/30 px-4 py-2 text-xs font-semibold text-on-surface-variant transition-colors hover:bg-surface-container-high";

type CategoryTreeNode = { id: string; name: string; children?: CategoryTreeNode[] };

const CATEGORY_TREE: CategoryTreeNode[] = [
  {
    id: "c1",
    name: "@12%rebate",
    children: [
      { id: "c1a", name: "Sub-category A" },
      { id: "c1b", name: "Sub-category B" },
    ],
  },
  { id: "c2", name: "1-12-REBATE" },
  { id: "c3", name: "Anniversary" },
  { id: "c4", name: "CGCategory" },
  {
    id: "c5",
    name: "Electronics",
    children: [
      { id: "c5a", name: "Phones & Tablets" },
      { id: "c5b", name: "Laptops & Computers" },
      { id: "c5c", name: "Audio & Headphones" },
    ],
  },
  { id: "c6", name: "Furniture" },
  {
    id: "c7",
    name: "Audio",
    children: [{ id: "c7a", name: "Wireless" }],
  },
  {
    id: "c8",
    name: "Timepieces",
    children: [{ id: "c8a", name: "Luxury" }],
  },
  {
    id: "c9",
    name: "Footwear",
    children: [{ id: "c9a", name: "Athletics" }],
  },
  {
    id: "c10",
    name: "Cameras",
    children: [{ id: "c10a", name: "Instant" }],
  },
];

function findCategoryName(tree: CategoryTreeNode[], id: string): string | undefined {
  for (const node of tree) {
    if (node.id === id) return node.name;
    if (node.children?.length) {
      const found = findCategoryName(node.children, id);
      if (found) return found;
    }
  }
  return undefined;
}

function collectExpandableCategoryIds(tree: CategoryTreeNode[], acc: string[] = []): string[] {
  for (const node of tree) {
    if (node.children?.length) {
      acc.push(node.id);
      collectExpandableCategoryIds(node.children, acc);
    }
  }
  return acc;
}

function CategoryTreeSelect({
  value,
  onChange,
  placeholder = "",
}: {
  value: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState("");
  const allExpandableIds = useMemo(() => collectExpandableCategoryIds(CATEGORY_TREE), []);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(allExpandableIds));
  const ref = useRef<HTMLDivElement>(null);

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

  const nodeMatches = (node: CategoryTreeNode): boolean => {
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

  function renderNode(node: CategoryTreeNode, depth = 0): React.ReactNode {
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
            <button
              type="button"
              className="shrink-0 rounded p-0.5 hover:bg-surface-container-high"
              aria-expanded={isOpen}
              onClick={(e) => {
                e.preventDefault();
                toggleExpand(node.id);
              }}
            >
              {isOpen ? <IconChevronDown className="h-4 w-4" /> : <IconChevronRight className="h-4 w-4" />}
            </button>
          ) : (
            <span className="inline-flex w-5 shrink-0" aria-hidden />
          )}
          <input
            type="checkbox"
            className="h-3.5 w-3.5 shrink-0 accent-primary"
            checked={checked}
            onChange={() => toggle(node.id)}
          />
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

  const visibleRoots = CATEGORY_TREE.filter(nodeMatches);
  const label = value.length === 0 ? placeholder : value.length === CATEGORY_TREE.length ? "All selected" : `${value.length} selected`;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        className={`${fieldBase} flex items-center justify-between text-left`}
        onClick={() => setOpen((o) => !o)}
      >
        <span className={value.length ? "truncate font-semibold" : "truncate text-on-surface-variant"}>{label}</span>
        <IconChevronDown className={`h-4 w-4 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute left-0 right-0 top-full z-40 mt-1 rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
          <div className="p-2">
            {value.length > 0 && (
              <div className="mb-2 flex flex-wrap gap-1.5">
                {value.map((id) => {
                  const name = findCategoryName(CATEGORY_TREE, id) ?? id;
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
              <input
                autoFocus
                className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest py-2 pl-8 pr-3 text-xs outline-none focus:ring-1 focus:ring-primary"
                placeholder="Filter categories..."
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              />
            </div>
            <div className="rounded-lg border border-outline-variant/20 bg-surface-container-lowest">
              <div className="flex items-center justify-between border-b border-outline-variant/10 px-3 py-1.5">
                <span className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">Hierarchy</span>
                <div className="flex gap-1">
                  <button type="button" className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high" title="Expand all" onClick={() => setExpanded(new Set(allExpandableIds))}>
                    <IconUnfoldMore className="h-3.5 w-3.5" />
                  </button>
                  <button type="button" className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high" title="Collapse all" onClick={() => setExpanded(new Set())}>
                    <IconUnfoldLess className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
              <div className="max-h-64 space-y-1 overflow-y-auto p-2">
                {visibleRoots.length === 0 ? (
                  <p className="px-2 py-4 text-center text-[11px] italic text-on-surface-variant">No categories match “{filter}”.</p>
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

type Props = {
  rows: ProductListRow[];
  bestSellerById: Record<string, boolean>;
  onBestSellerChange: (next: Record<string, boolean>) => void;
  onNavigateToProducts: () => void;
};

export function BestSellerSettingsPage({ rows, bestSellerById, onBestSellerChange, onNavigateToProducts }: Props) {
  const [displayList, setDisplayList] = useState(true);
  const [genderBased, setGenderBased] = useState(true);
  const [popularityDuration, setPopularityDuration] = useState("30");
  const [recommendationLogic, setRecommendationLogic] = useState("sales-quantity");
  const [maxItems, setMaxItems] = useState("4");
  const [whitelistedCategories, setWhitelistedCategories] = useState<string[]>(CATEGORY_TREE.map((c) => c.id));
  const [blacklistedCategories, setBlacklistedCategories] = useState<string[]>([]);
  const [whitelistedBrands, setWhitelistedBrands] = useState("all");
  const [blacklistedBrands, setBlacklistedBrands] = useState("");
  const [includedProducts, setIncludedProducts] = useState("");
  const [excludedProducts, setExcludedProducts] = useState("");
  const [toast, setToast] = useState(false);

  const draft = useMemo(() => {
    const limit = Number(maxItems) || 0;
    return rows.reduce<Record<string, boolean>>((acc, row, index) => {
      acc[row.id] = displayList && index < limit;
      return acc;
    }, {});
  }, [displayList, maxItems, rows]);

  const dirty = useMemo(() => JSON.stringify(draft) !== JSON.stringify(bestSellerById), [draft, bestSellerById]);

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
    <div className="-m-6 min-h-[calc(100dvh-6rem)] bg-surface-container-low text-on-surface" aria-label="Best seller settings">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="min-w-0 space-y-1">
          <nav className="mb-1 flex flex-wrap gap-x-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="text-on-surface-variant/50">/</span>
            <button type="button" className="text-primary hover:underline" onClick={onNavigateToProducts}>
              Products
            </button>
            <span className="text-on-surface-variant/50">/</span>
            <span className="text-primary">Best Seller</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Best Seller</h1>
        </div>
        <div className="flex items-center gap-3">
          <button type="button" className={btnSecondary} onClick={() => setToast(true)}>
            <IconHistory className="h-4 w-4 shrink-0" />
            Show Change History
          </button>
          <button type="button" className={btnPrimary} onClick={handleSave}>
            <IconSave className="h-4 w-4 shrink-0" />
            Save
          </button>
        </div>
      </header>

      <main className="p-5">
        <section className="overflow-hidden rounded border border-outline-variant/25 bg-surface-container-lowest">
          <div className="border-b border-outline-variant/25 bg-surface-container px-5 py-3 text-xs text-on-surface-variant">
            General Settings
          </div>

          <div className="space-y-4 px-12 py-7">
            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Display Best Sellers List</label>
              <input
                type="checkbox"
                className="h-4 w-4 rounded border-outline-variant accent-primary"
                checked={displayList}
                onChange={(e) => setDisplayList(e.target.checked)}
              />
              <span />
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Popularity Duration</label>
              <select className={fieldBase} value={popularityDuration} onChange={(e) => setPopularityDuration(e.target.value)}>
                <option value="7">7 days</option>
                <option value="14">14 days</option>
                <option value="30">30 days</option>
                <option value="60">60 days</option>
                <option value="90">90 days</option>
              </select>
              <span />
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Gender Based Recommendation</label>
              <input
                type="checkbox"
                className="h-4 w-4 rounded border-outline-variant accent-primary"
                checked={genderBased}
                onChange={(e) => setGenderBased(e.target.checked)}
              />
              <span />
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Recommendation Logic</label>
              <select className={fieldBase} value={recommendationLogic} onChange={(e) => setRecommendationLogic(e.target.value)}>
                <option value="sales-quantity">Sales Quantity</option>
                <option value="sales-amount">Sales Amount</option>
                <option value="views">Product Views</option>
                <option value="manual">Manual Selection</option>
              </select>
              <span />
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Max Number of Items to Display</label>
              <select className={fieldBase} value={maxItems} onChange={(e) => setMaxItems(e.target.value)}>
                <option value="4">4</option>
                <option value="8">8</option>
                <option value="12">12</option>
                <option value="16">16</option>
              </select>
              <span />
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Whitelisted Categories</label>
              <CategoryTreeSelect
                value={whitelistedCategories}
                onChange={setWhitelistedCategories}
                placeholder="All selected"
              />
              <span className="text-center text-error">⊗</span>
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Blacklisted Categories</label>
              <CategoryTreeSelect
                value={blacklistedCategories}
                onChange={setBlacklistedCategories}
                placeholder=""
              />
              <span className="text-center text-error">⊗</span>
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Whitelisted Brands</label>
              <select className={fieldBase} value={whitelistedBrands} onChange={(e) => setWhitelistedBrands(e.target.value)}>
                <option value="all">All selected</option>
                <option value="Premium Collection">Premium Collection</option>
                <option value="Eco-Essentials">Eco-Essentials</option>
                <option value="Luxe Goods">Luxe Goods</option>
              </select>
              <span className="text-center text-error">⊗</span>
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Blacklisted Brands</label>
              <select className={fieldBase} value={blacklistedBrands} onChange={(e) => setBlacklistedBrands(e.target.value)}>
                <option value=""></option>
                <option value="Premium Collection">Premium Collection</option>
                <option value="Eco-Essentials">Eco-Essentials</option>
                <option value="Luxe Goods">Luxe Goods</option>
              </select>
              <span className="text-center text-error">⊗</span>
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Included Products</label>
              <div className="relative">
                <input
                  className={`${fieldBase} pr-9`}
                  placeholder="Enter Product UPC/Name"
                  value={includedProducts}
                  onChange={(e) => setIncludedProducts(e.target.value)}
                />
                <IconSearch className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant" />
              </div>
              <span />
            </div>

            <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Excluded Products</label>
              <div className="relative">
                <input
                  className={`${fieldBase} pr-9`}
                  placeholder="Enter Product UPC/Name"
                  value={excludedProducts}
                  onChange={(e) => setExcludedProducts(e.target.value)}
                />
                <IconSearch className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant" />
              </div>
              <span />
            </div>
          </div>
        </section>
      </main>

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <span className="text-sm font-semibold text-on-surface">Best seller settings saved.</span>
        </div>
      )}
    </div>
  );
}
