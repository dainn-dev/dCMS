import { useEffect, useMemo, useRef, useState } from "react";
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
import type { QuantityLimitRow } from "./ProductQuantityLimitSettingsPage";

const fieldBase =
  "h-9 w-full rounded border border-outline-variant/30 bg-surface-container-lowest px-3 text-xs text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container";

type Props = {
  row?: QuantityLimitRow;
  onBack: () => void;
  onSave: (row: QuantityLimitRow) => void;
};

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
];

function findCategoryName(tree: CategoryTreeNode[], id: string): string | undefined {
  for (const n of tree) {
    if (n.id === id) return n.name;
    if (n.children?.length) {
      const found = findCategoryName(n.children, id);
      if (found) return found;
    }
  }
  return undefined;
}

function collectExpandableCategoryIds(tree: CategoryTreeNode[], acc: string[] = []): string[] {
  for (const n of tree) {
    if (n.children?.length) {
      acc.push(n.id);
      collectExpandableCategoryIds(n.children, acc);
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
  const label = value.length === 0 ? placeholder : `${value.length} selected`;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        className={`${fieldBase} flex items-center justify-between text-left`}
        onClick={() => setOpen((o) => !o)}
      >
        <span className={value.length ? "truncate font-semibold" : "truncate text-on-surface-variant"}>{label || "Click to choose categories"}</span>
        <IconChevronDown className={`h-4 w-4 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute left-0 right-0 top-full z-50 mt-1 rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
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
      <input
        ref={ref}
        type="date"
        className={`${fieldBase} rounded-r-none`}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      <button type="button" className="h-9 border-y border-outline-variant/30 px-3 text-on-surface-variant" onClick={() => onChange("")}>×</button>
      <button type="button" className="h-9 rounded-r border border-outline-variant/30 px-3 text-on-surface-variant" onClick={openPicker}>
        <IconCalendarToday className="h-4 w-4" />
      </button>
    </div>
  );
}

export function AdvanceQuantityLimitSettingsPage({ row, onBack, onSave }: Props) {
  const [name, setName] = useState(row?.name ?? "");
  const [startDate, setStartDate] = useState(row?.startDate ?? "");
  const [endDate, setEndDate] = useState(row?.endDate ?? "");
  const [limitType, setLimitType] = useState<"Per Cart" | "Per User">(
    row?.limitType === "Per User" ? "Per User" : "Per Cart"
  );
  const [perProduct, setPerProduct] = useState(false);
  const [brand, setBrand] = useState("");
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);
  const [product, setProduct] = useState("");
  const [membershipType, setMembershipType] = useState("");
  const [membershipTier, setMembershipTier] = useState("");
  const [quantityLimit, setQuantityLimit] = useState("");

  function save() {
    onSave({
      id: row?.id ?? `q-${Date.now().toString(36)}`,
      name,
      limitType,
      startDate,
      endDate,
      modifiedBy: row?.modifiedBy ?? "",
    });
  }

  return (
    <div className="-m-6 min-h-[calc(100dvh-6rem)] bg-surface-container-low text-on-surface" aria-label="Advance quantity limit settings">
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Product Quantity Limit Settings
          </button>
          <h2 className="flex flex-wrap items-center gap-3 text-2xl font-bold tracking-tight text-on-surface">
            {row ? "Edit Advance Quantity Limit Settings" : "Add Advance Quantity Limit Settings"}
          </h2>
        </div>
        <button type="button" className={btnPrimary} onClick={save}>
          <IconSave className="h-4 w-4 shrink-0" />
          Save and Approve
        </button>
      </div>

      <main className="space-y-3 p-5">
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
                <label className="inline-flex items-center"><input type="radio" className="accent-primary" checked={limitType === "Per Cart"} onChange={() => setLimitType("Per Cart")} />Per Cart</label>
                <label className="inline-flex items-center"><input type="radio" className="accent-primary" checked={limitType === "Per User"} onChange={() => setLimitType("Per User")} />Per User</label>
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
              <select className={fieldBase} value={brand} onChange={(e) => setBrand(e.target.value)}>
                <option value=""></option>
                <option value="Premium Collection">Premium Collection</option>
                <option value="Eco-Essentials">Eco-Essentials</option>
                <option value="Luxe Goods">Luxe Goods</option>
              </select>
              <span className="text-center text-error">⊗</span>
            </div>
            <div className="grid max-w-[1320px] grid-cols-[540px_730px_24px] items-start gap-x-8">
              <label className="pt-2 text-sm text-on-surface-variant">Category</label>
              <CategoryTreeSelect value={selectedCategories} onChange={setSelectedCategories} />
              <span className="pt-2 text-center text-error">⊗</span>
            </div>
            <div className="grid max-w-[1320px] grid-cols-[540px_730px_24px] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Product</label>
              <div className="relative">
                <input className={`${fieldBase} pr-9`} placeholder="Enter Product UPC/Name" value={product} onChange={(e) => setProduct(e.target.value)} />
                <IconSearch className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant" />
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
              <label className="text-sm text-on-surface-variant">Quantity Limit*</label>
              <input className={fieldBase} value={quantityLimit} onChange={(e) => setQuantityLimit(e.target.value)} />
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}
