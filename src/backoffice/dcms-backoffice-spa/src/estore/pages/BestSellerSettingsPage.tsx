import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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
import {
  DEFAULT_BEST_SELLER_SETTINGS,
  fetchBestSellerHistory,
  fetchBestSellerSettings,
  saveBestSellerSettings,
  type BestSellerHistoryEntry,
  type BestSellerSettings,
} from "../api/bestSellerSettingsApi";
import { fetchBrands } from "../api/brandsApi";
import { fetchCategories } from "../api/categoriesApi";
import { fetchProducts } from "../api/productsApi";
import type { CatNode } from "./CategoriesPage";

const fieldBase =
  "h-9 w-full rounded border border-outline-variant/30 bg-surface-container-lowest px-3 text-xs text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container disabled:opacity-40";
const btnSecondary =
  "flex items-center gap-2 rounded-md border border-outline-variant/30 px-4 py-2 text-xs font-semibold text-on-surface-variant transition-colors hover:bg-surface-container-high";

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

function collectAllCategoryIds(tree: CatNode[], acc: string[] = []): string[] {
  for (const node of tree) {
    acc.push(node.id);
    if (node.children?.length) collectAllCategoryIds(node.children, acc);
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

  const visibleRoots = tree.filter(nodeMatches);
  const allIds = useMemo(() => collectAllCategoryIds(tree), [tree]);
  const label =
    value.length === 0 || value.length >= allIds.length
      ? placeholder || "All selected"
      : `${value.length} selected`;

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

/** Search products and add/remove by id. */
function ProductIdPicker({
  label,
  selectedIds,
  nameById,
  onChange,
  tenantId,
  storeId,
  authToken,
  excludeIds,
}: {
  label: string;
  selectedIds: string[];
  nameById: Record<string, string>;
  onChange: (ids: string[], namePatch?: Record<string, string>) => void;
  tenantId: string;
  storeId: string;
  authToken?: string;
  excludeIds?: string[];
}) {
  const [query, setQuery] = useState("");
  const [suggestions, setSuggestions] = useState<{ id: string; name: string }[]>([]);
  const [searching, setSearching] = useState(false);

  const search = useCallback(async () => {
    const q = query.trim();
    if (q.length < 2) {
      setSuggestions([]);
      return;
    }
    setSearching(true);
    try {
      const { rows } = await fetchProducts(tenantId, storeId, { name: q }, { page: 1, pageSize: 10 }, authToken);
      const exclude = new Set(excludeIds ?? []);
      setSuggestions(rows.filter((r) => !exclude.has(r.id)).map((r) => ({ id: r.id, name: r.name })));
    } catch {
      setSuggestions([]);
    } finally {
      setSearching(false);
    }
  }, [query, tenantId, storeId, authToken, excludeIds]);

  useEffect(() => {
    const t = setTimeout(() => void search(), 300);
    return () => clearTimeout(t);
  }, [search]);

  function add(id: string, name: string) {
    if (!selectedIds.includes(id)) {
      onChange([...selectedIds, id], { [id]: name });
    }
    setQuery("");
    setSuggestions([]);
  }

  function remove(id: string) {
    onChange(selectedIds.filter((x) => x !== id));
  }

  return (
    <div className="space-y-2">
      <label className="text-sm text-on-surface-variant">{label}</label>
      {selectedIds.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selectedIds.map((id) => (
            <span key={id} className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-bold text-primary">
              {nameById[id] ?? id}
              <button type="button" className="rounded p-0.5 hover:bg-primary/20" onClick={() => remove(id)} aria-label="Remove">
                <IconClose className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}
      <div className="relative">
        <input
          className={`${fieldBase} pr-9`}
          placeholder="Search product by name (min 2 chars)…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <IconSearch className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant" />
        {suggestions.length > 0 && (
          <ul className="absolute left-0 right-0 top-full z-30 mt-1 max-h-40 overflow-y-auto rounded-md border border-outline-variant/20 bg-surface shadow-lg">
            {suggestions.map((s) => (
              <li key={s.id}>
                <button
                  type="button"
                  className="w-full px-3 py-2 text-left text-xs hover:bg-surface-container-high"
                  onClick={() => add(s.id, s.name)}
                >
                  {s.name}
                </button>
              </li>
            ))}
          </ul>
        )}
        {searching && query.trim().length >= 2 && (
          <p className="mt-1 text-[10px] text-on-surface-variant">Searching…</p>
        )}
      </div>
    </div>
  );
}

/** Ordered manual best-seller list (rank = array order). */
function ManualProductPicker({
  selectedIds,
  nameById,
  onChange,
  tenantId,
  storeId,
  authToken,
  excludeIds,
  maxItems,
}: {
  selectedIds: string[];
  nameById: Record<string, string>;
  onChange: (ids: string[], namePatch?: Record<string, string>) => void;
  tenantId: string;
  storeId: string;
  authToken?: string;
  excludeIds?: string[];
  maxItems: number;
}) {
  const [query, setQuery] = useState("");
  const [suggestions, setSuggestions] = useState<{ id: string; name: string }[]>([]);
  const [searching, setSearching] = useState(false);

  const search = useCallback(async () => {
    const q = query.trim();
    if (q.length < 2) {
      setSuggestions([]);
      return;
    }
    setSearching(true);
    try {
      const { rows } = await fetchProducts(tenantId, storeId, { name: q }, { page: 1, pageSize: 10 }, authToken);
      const exclude = new Set([...(excludeIds ?? []), ...selectedIds]);
      setSuggestions(rows.filter((r) => !exclude.has(r.id)).map((r) => ({ id: r.id, name: r.name })));
    } catch {
      setSuggestions([]);
    } finally {
      setSearching(false);
    }
  }, [query, tenantId, storeId, authToken, excludeIds, selectedIds]);

  useEffect(() => {
    const t = setTimeout(() => void search(), 300);
    return () => clearTimeout(t);
  }, [search]);

  function add(id: string, name: string) {
    if (selectedIds.length >= maxItems) return;
    if (!selectedIds.includes(id)) onChange([...selectedIds, id], { [id]: name });
    setQuery("");
    setSuggestions([]);
  }

  function remove(id: string) {
    onChange(selectedIds.filter((x) => x !== id));
  }

  function move(id: string, dir: -1 | 1) {
    const idx = selectedIds.indexOf(id);
    if (idx < 0) return;
    const next = idx + dir;
    if (next < 0 || next >= selectedIds.length) return;
    const copy = [...selectedIds];
    [copy[idx], copy[next]] = [copy[next], copy[idx]];
    onChange(copy);
  }

  return (
    <div className="space-y-2">
      <label className="text-sm text-on-surface-variant">
        Manual Best Sellers <span className="text-on-surface-variant/70">({selectedIds.length}/{maxItems})</span>
      </label>
      {selectedIds.length > 0 && (
        <ol className="space-y-1 rounded-md border border-outline-variant/20 bg-surface-container-lowest p-2">
          {selectedIds.map((id, rank) => (
            <li key={id} className="flex items-center gap-2 rounded px-2 py-1.5 text-xs hover:bg-surface-container-high">
              <span className="w-5 shrink-0 font-bold text-on-surface-variant">{rank + 1}.</span>
              <span className="min-w-0 flex-1 truncate font-medium">{nameById[id] ?? id}</span>
              <div className="flex shrink-0 gap-0.5">
                <button type="button" className="rounded px-1.5 py-0.5 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30" disabled={rank === 0} onClick={() => move(id, -1)} aria-label="Move up">↑</button>
                <button type="button" className="rounded px-1.5 py-0.5 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30" disabled={rank === selectedIds.length - 1} onClick={() => move(id, 1)} aria-label="Move down">↓</button>
                <button type="button" className="rounded p-0.5 text-error hover:bg-error/10" onClick={() => remove(id)} aria-label="Remove"><IconClose className="h-3 w-3" /></button>
              </div>
            </li>
          ))}
        </ol>
      )}
      {selectedIds.length < maxItems && (
        <div className="relative">
          <input
            className={`${fieldBase} pr-9`}
            placeholder="Search product to add (min 2 chars)…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          <IconSearch className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant" />
          {suggestions.length > 0 && (
            <ul className="absolute left-0 right-0 top-full z-30 mt-1 max-h-40 overflow-y-auto rounded-md border border-outline-variant/20 bg-surface shadow-lg">
              {suggestions.map((s) => (
                <li key={s.id}>
                  <button type="button" className="w-full px-3 py-2 text-left text-xs hover:bg-surface-container-high" onClick={() => add(s.id, s.name)}>
                    {s.name}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {searching && query.trim().length >= 2 && <p className="mt-1 text-[10px] text-on-surface-variant">Searching…</p>}
        </div>
      )}
      {selectedIds.length === 0 && (
        <p className="text-[11px] italic text-on-surface-variant">Add at least one product when using manual selection.</p>
      )}
    </div>
  );
}

function ChangeHistoryModal({
  open,
  onClose,
  entries,
  loading,
  error,
}: {
  open: boolean;
  onClose: () => void;
  entries: BestSellerHistoryEntry[];
  loading: boolean;
  error: string | null;
}) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-scrim/40 p-4" role="dialog" aria-modal="true" aria-label="Best seller change history">
      <div className="flex max-h-[80vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg border border-outline-variant/20 bg-surface shadow-2xl">
        <div className="flex items-center justify-between border-b border-outline-variant/15 px-5 py-4">
          <h2 className="font-headline text-lg font-bold">Change History</h2>
          <button type="button" className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high" onClick={onClose} aria-label="Close">
            <IconClose className="h-5 w-5" />
          </button>
        </div>
        <div className="overflow-y-auto p-5">
          {loading ? <p className="text-sm text-on-surface-variant">Loading history…</p> : null}
          {error ? <p className="rounded-md border border-error/30 bg-error/10 px-3 py-2 text-xs text-on-error-container">{error}</p> : null}
          {!loading && !error && entries.length === 0 ? (
            <p className="text-sm text-on-surface-variant">No saved changes yet.</p>
          ) : null}
          {!loading && entries.length > 0 ? (
            <ul className="space-y-3">
              {entries.map((e) => (
                <li key={e.id} className="rounded-md border border-outline-variant/20 bg-surface-container-lowest px-4 py-3 text-xs">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-semibold text-on-surface">{new Date(e.createdAt).toLocaleString()}</span>
                    <span className="text-on-surface-variant">{e.userId} · {e.userRole}</span>
                  </div>
                  {e.settings ? (
                    <p className="mt-2 text-on-surface-variant">
                      Logic: <strong className="text-on-surface">{e.settings.recommendationLogic}</strong>
                      {" · "}Max items: <strong className="text-on-surface">{e.settings.maxItems}</strong>
                      {" · "}Duration: <strong className="text-on-surface">{e.settings.popularityDurationDays}d</strong>
                      {e.settings.recommendationLogic === "manual" ? (
                        <>{" · "}Manual: <strong className="text-on-surface">{e.settings.manualProductIds.length}</strong></>
                      ) : null}
                    </p>
                  ) : (
                    <p className="mt-2 italic text-on-surface-variant">Settings snapshot unavailable.</p>
                  )}
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      </div>
    </div>
  );
}

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
  onNavigateToProducts: () => void;
};

export function BestSellerSettingsPage({ tenantId, storeId, authToken, onNavigateToProducts }: Props) {
  const apiReady = Boolean(tenantId && storeId);

  const [settings, setSettings] = useState<BestSellerSettings>(DEFAULT_BEST_SELLER_SETTINGS);
  const [savedSettings, setSavedSettings] = useState<BestSellerSettings>(DEFAULT_BEST_SELLER_SETTINGS);
  const [categoryTree, setCategoryTree] = useState<CatNode[]>([]);
  const [brands, setBrands] = useState<{ code: string; name: string }[]>([]);
  const [productNames, setProductNames] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(apiReady);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [historyEntries, setHistoryEntries] = useState<BestSellerHistoryEntry[]>([]);

  const dirty = useMemo(() => JSON.stringify(settings) !== JSON.stringify(savedSettings), [settings, savedSettings]);

  const allCategoryIdStrings = useMemo(() => collectAllCategoryIds(categoryTree), [categoryTree]);

  // Whitelist UI: empty array = all categories selected
  const whitelistCategoryUi = useMemo(() => {
    if (settings.whitelistedCategoryIds.length === 0) return allCategoryIdStrings;
    return settings.whitelistedCategoryIds.map(String);
  }, [settings.whitelistedCategoryIds, allCategoryIdStrings]);

  const blacklistCategoryUi = settings.blacklistedCategoryIds.map(String);

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(false), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  useEffect(() => {
    if (!apiReady) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    void (async () => {
      try {
        const [cats, brandRes, cfg] = await Promise.all([
          fetchCategories(tenantId!, authToken),
          fetchBrands(tenantId!, { pageSize: 200 }, authToken),
          fetchBestSellerSettings(tenantId!, storeId!, authToken),
        ]);
        if (cancelled) return;
        setCategoryTree(cats);
        setBrands(brandRes.rows.map((b) => ({ code: b.code, name: b.name })));
        setSettings(cfg.settings);
        setSavedSettings(cfg.settings);

        const ids = [
          ...cfg.settings.includedProductIds,
          ...cfg.settings.excludedProductIds,
          ...cfg.settings.manualProductIds,
        ];
        if (ids.length) {
          const { rows } = await fetchProducts(tenantId!, storeId!, undefined, { page: 1, pageSize: 100 }, authToken);
          const names: Record<string, string> = {};
          for (const r of rows) names[r.id] = r.name;
          setProductNames(names);
        }
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : "Failed to load best seller settings.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [apiReady, tenantId, storeId, authToken]);

  function patchSettings(patch: Partial<BestSellerSettings>) {
    setSettings((prev) => ({ ...prev, ...patch }));
  }

  function setWhitelistCategories(ids: string[]) {
    const all = allCategoryIdStrings;
    // All selected → store as empty (meaning no filter)
    if (ids.length === 0 || ids.length >= all.length) {
      patchSettings({ whitelistedCategoryIds: [] });
    } else {
      patchSettings({ whitelistedCategoryIds: ids.map((x) => Number(x)).filter((n) => n > 0) });
    }
  }

  function setBlacklistCategories(ids: string[]) {
    patchSettings({ blacklistedCategoryIds: ids.map((x) => Number(x)).filter((n) => n > 0) });
  }

  async function handleShowHistory() {
    if (!apiReady) return;
    setHistoryOpen(true);
    setHistoryLoading(true);
    setHistoryError(null);
    try {
      const entries = await fetchBestSellerHistory(tenantId!, storeId!, authToken);
      setHistoryEntries(entries);
    } catch (e) {
      setHistoryError(e instanceof Error ? e.message : "Failed to load change history.");
      setHistoryEntries([]);
    } finally {
      setHistoryLoading(false);
    }
  }

  async function handleSave() {
    if (!apiReady) {
      setToast(true);
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const saved = await saveBestSellerSettings(tenantId!, storeId!, settings, authToken);
      setSettings(saved);
      setSavedSettings(saved);
      setToast(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save settings.");
    } finally {
      setSaving(false);
    }
  }

  const whitelistBrandUi = settings.whitelistedBrandIds.length === 0 ? "all" : settings.whitelistedBrandIds[0] ?? "all";
  const blacklistBrandUi = settings.blacklistedBrandIds[0] ?? "";

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
          <button type="button" className={btnSecondary} disabled={!apiReady || loading} onClick={() => void handleShowHistory()}>
            <IconHistory className="h-4 w-4 shrink-0" />
            Show Change History
          </button>
          <button type="button" className={btnPrimary} disabled={!dirty || saving || loading} onClick={() => void handleSave()}>
            <IconSave className="h-4 w-4 shrink-0" />
            {saving ? "Saving…" : "Save"}
          </button>
        </div>
      </header>

      <main className="p-5">
        {!apiReady && (
          <p className="mb-4 rounded-md border border-outline-variant/20 bg-surface px-4 py-3 text-xs text-on-surface-variant">
            Demo mode — connect to a store context to persist settings to the catalog API.
          </p>
        )}
        {error ? (
          <p className="mb-4 rounded-md border border-error/30 bg-error/10 px-4 py-3 text-xs text-on-error-container">{error}</p>
        ) : null}
        {loading ? (
          <p className="px-6 py-12 text-center text-sm text-on-surface-variant">Loading best seller settings…</p>
        ) : (
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
                  checked={settings.displayList}
                  onChange={(e) => patchSettings({ displayList: e.target.checked })}
                />
                <span />
              </div>

              <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Popularity Duration</label>
                <select
                  className={fieldBase}
                  value={String(settings.popularityDurationDays)}
                  onChange={(e) => patchSettings({ popularityDurationDays: Number(e.target.value) })}
                >
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
                  checked={settings.genderBased}
                  onChange={(e) => patchSettings({ genderBased: e.target.checked })}
                />
                <span />
              </div>

              <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Recommendation Logic</label>
                <select
                  className={fieldBase}
                  value={settings.recommendationLogic}
                  onChange={(e) =>
                    patchSettings({
                      recommendationLogic: e.target.value as BestSellerSettings["recommendationLogic"],
                    })
                  }
                >
                  <option value="sales-quantity">Sales Quantity</option>
                  <option value="sales-amount">Sales Amount</option>
                  <option value="views">Product Views</option>
                  <option value="manual">Manual Selection</option>
                </select>
                <span />
              </div>

              <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Max Number of Items to Display</label>
                <select
                  className={fieldBase}
                  value={String(settings.maxItems)}
                  onChange={(e) => patchSettings({ maxItems: Number(e.target.value) })}
                >
                  <option value="4">4</option>
                  <option value="8">8</option>
                  <option value="12">12</option>
                  <option value="16">16</option>
                </select>
                <span />
              </div>

              {settings.recommendationLogic === "manual" && apiReady ? (
                <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-start gap-x-8">
                  <label className="text-sm text-on-surface-variant">Manual Selection</label>
                  <ManualProductPicker
                    selectedIds={settings.manualProductIds}
                    nameById={productNames}
                    excludeIds={settings.excludedProductIds}
                    maxItems={settings.maxItems}
                    tenantId={tenantId!}
                    storeId={storeId!}
                    authToken={authToken}
                    onChange={(ids, namePatch) => {
                      if (namePatch) setProductNames((prev) => ({ ...prev, ...namePatch }));
                      patchSettings({ manualProductIds: ids });
                    }}
                  />
                  <span />
                </div>
              ) : null}

              <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Whitelisted Categories</label>
                <CategoryTreeSelect
                  tree={categoryTree}
                  value={whitelistCategoryUi}
                  onChange={setWhitelistCategories}
                  placeholder="All selected"
                />
                <span className="text-center text-error">⊗</span>
              </div>

              <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Blacklisted Categories</label>
                <CategoryTreeSelect tree={categoryTree} value={blacklistCategoryUi} onChange={setBlacklistCategories} placeholder="" />
                <span className="text-center text-error">⊗</span>
              </div>

              <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Whitelisted Brands</label>
                <select
                  className={fieldBase}
                  value={whitelistBrandUi}
                  onChange={(e) => {
                    const v = e.target.value;
                    patchSettings({ whitelistedBrandIds: v === "all" ? [] : [v] });
                  }}
                >
                  <option value="all">All selected</option>
                  {brands.map((b) => (
                    <option key={b.code} value={b.code}>
                      {b.name}
                    </option>
                  ))}
                </select>
                <span className="text-center text-error">⊗</span>
              </div>

              <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Blacklisted Brands</label>
                <select
                  className={fieldBase}
                  value={blacklistBrandUi}
                  onChange={(e) => {
                    const v = e.target.value;
                    patchSettings({ blacklistedBrandIds: v ? [v] : [] });
                  }}
                >
                  <option value=""></option>
                  {brands.map((b) => (
                    <option key={b.code} value={b.code}>
                      {b.name}
                    </option>
                  ))}
                </select>
                <span className="text-center text-error">⊗</span>
              </div>

              {apiReady ? (
                <>
                  <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-start gap-x-8">
                    <span />
                    <ProductIdPicker
                      label="Included Products"
                      selectedIds={settings.includedProductIds}
                      nameById={productNames}
                      excludeIds={settings.excludedProductIds}
                      tenantId={tenantId!}
                      storeId={storeId!}
                      authToken={authToken}
                      onChange={(ids, namePatch) => {
                        if (namePatch) setProductNames((prev) => ({ ...prev, ...namePatch }));
                        patchSettings({ includedProductIds: ids });
                      }}
                    />
                    <span />
                  </div>
                  <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-start gap-x-8">
                    <span />
                    <ProductIdPicker
                      label="Excluded Products"
                      selectedIds={settings.excludedProductIds}
                      nameById={productNames}
                      excludeIds={settings.includedProductIds}
                      tenantId={tenantId!}
                      storeId={storeId!}
                      authToken={authToken}
                      onChange={(ids, namePatch) => {
                        if (namePatch) setProductNames((prev) => ({ ...prev, ...namePatch }));
                        patchSettings({ excludedProductIds: ids });
                      }}
                    />
                    <span />
                  </div>
                </>
              ) : (
                <>
                  <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                    <label className="text-sm text-on-surface-variant">Included Products</label>
                    <input className={fieldBase} disabled placeholder="Requires store context" />
                    <span />
                  </div>
                  <div className="grid max-w-[1320px] grid-cols-[260px_720px_24px] items-center gap-x-8">
                    <label className="text-sm text-on-surface-variant">Excluded Products</label>
                    <input className={fieldBase} disabled placeholder="Requires store context" />
                    <span />
                  </div>
                </>
              )}
            </div>
          </section>
        )}
      </main>

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <span className="text-sm font-semibold text-on-surface">
            {apiReady ? "Best seller settings saved." : "Settings saved locally (demo mode)."}
          </span>
        </div>
      )}

      <ChangeHistoryModal
        open={historyOpen}
        onClose={() => setHistoryOpen(false)}
        entries={historyEntries}
        loading={historyLoading}
        error={historyError}
      />
    </div>
  );
}
