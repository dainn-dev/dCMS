import { useEffect, useMemo, useRef, useState } from "react";
import {
  IconAccountTree,
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
  IconChevronLeft,
  IconChevronRight,
  IconClose,
  IconCloudUpload,
  IconFirstPage,
  IconFolder,
  IconFolderOpen,
  IconInfo,
  IconLastPage,
  IconSearch,
  IconTag,
  IconUnfoldLess,
  IconUnfoldMore,
} from "../../orders/icons";

// ── Style tokens ─────────────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-all hover:scale-[1.02] active:scale-95 disabled:pointer-events-none disabled:opacity-40";
const btnGhost =
  "text-xs font-bold uppercase tracking-widest text-on-surface-variant px-4 py-2.5 hover:bg-surface-container-high rounded-md transition-colors";

const PAGE_SIZE = 50;

// ── Mock categories (tree, mirrors EditBrandPage hierarchy) ──────────────────
type CategoryTreeNode = { id: string; name: string; children?: CategoryTreeNode[] };

const MOCK_CATEGORY_TREE: CategoryTreeNode[] = [
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

function CategoryTreePicker({
  tree,
  selectedIds,
  onToggle,
  filter,
}: {
  tree: CategoryTreeNode[];
  selectedIds: string[];
  onToggle: (id: string) => void;
  filter: string;
}) {
  const allExpandableIds = useMemo(() => collectExpandableCategoryIds(tree), [tree]);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(allExpandableIds));

  const filterLower = filter.trim().toLowerCase();

  const nodeMatches = (n: CategoryTreeNode): boolean => {
    if (!filterLower) return true;
    if (n.name.toLowerCase().includes(filterLower)) return true;
    return n.children?.some(nodeMatches) ?? false;
  };

  const expandedEffective = filterLower ? new Set(allExpandableIds) : expanded;

  const toggleExpand = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const renderNode = (node: CategoryTreeNode, depth = 0): React.ReactNode => {
    if (!nodeMatches(node)) return null;
    const hasChildren = Boolean(node.children?.length);
    const isOpen = hasChildren && expandedEffective.has(node.id);
    const isChecked = selectedIds.includes(node.id);

    const rowCls = `group flex w-full items-center gap-1 rounded p-1.5 text-[13px] transition-colors cursor-pointer select-none ${
      isChecked
        ? "bg-primary/10 text-primary font-semibold"
        : "text-on-surface-variant hover:bg-surface-container-high"
    }`;

    if (!hasChildren) {
      return (
        <div key={node.id} style={{ marginLeft: depth ? 24 : 0 }}>
          <label className={rowCls}>
            <span className="inline-flex w-5 shrink-0" aria-hidden />
            <input
              type="checkbox"
              checked={isChecked}
              onChange={() => onToggle(node.id)}
              className="h-3.5 w-3.5 shrink-0 accent-primary"
            />
            <IconTag className="h-4 w-4 shrink-0 opacity-80" />
            <span className="truncate">{node.name}</span>
          </label>
        </div>
      );
    }

    return (
      <div key={node.id} className={depth ? "mt-1" : ""}>
        <label className={rowCls}>
          <button
            type="button"
            className="shrink-0 rounded p-0.5 hover:bg-surface-container-high"
            aria-expanded={isOpen}
            onClick={(e) => {
              e.preventDefault();
              toggleExpand(node.id);
            }}
          >
            {isOpen ? (
              <IconChevronDown className="h-4 w-4" />
            ) : (
              <IconChevronRight className="h-4 w-4" />
            )}
          </button>
          <input
            type="checkbox"
            checked={isChecked}
            onChange={() => onToggle(node.id)}
            className="h-3.5 w-3.5 shrink-0 accent-primary"
          />
          <span className="flex min-w-0 flex-1 items-center gap-1.5">
            {isOpen ? (
              <IconFolderOpen className="h-4 w-4 shrink-0 text-primary/80" />
            ) : (
              <IconFolder className="h-4 w-4 shrink-0 text-primary/60" />
            )}
            <span className="truncate">{node.name}</span>
          </span>
        </label>
        {isOpen && node.children && (
          <div className="ml-6 mt-1 space-y-1 border-l border-outline-variant/30 pl-2">
            {node.children.map((child) => renderNode(child, depth + 1))}
          </div>
        )}
      </div>
    );
  };

  const visibleRoots = tree.filter(nodeMatches);

  return (
    <div className="rounded-lg border border-outline-variant/20 bg-surface-container-lowest">
      <div className="flex items-center justify-between border-b border-outline-variant/10 px-3 py-1.5">
        <span className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
          Hierarchy
        </span>
        <div className="flex gap-1">
          <button
            type="button"
            className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high"
            title="Expand all"
            onClick={() => setExpanded(new Set(allExpandableIds))}
          >
            <IconUnfoldMore className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high"
            title="Collapse all"
            onClick={() => setExpanded(new Set())}
          >
            <IconUnfoldLess className="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
      <div className="max-h-64 space-y-1 overflow-y-auto p-2">
        {visibleRoots.length === 0 ? (
          <p className="px-2 py-4 text-center text-[11px] italic text-on-surface-variant">
            No categories match &ldquo;{filter}&rdquo;.
          </p>
        ) : (
          visibleRoots.map((n) => renderNode(n))
        )}
      </div>
    </div>
  );
}

// ── Mock parsed rows (UPC/SKU file upload result) ─────────────────────────────
type AssignRow = {
  id: string;
  upc: string;
  sku: string;
  name: string;
  currentCategories: string[];
};

const MOCK_ASSIGN_ROWS: AssignRow[] = [
  { id: "a1", upc: "400234110", sku: "WT-550-B",  name: "Vantage Series 5 Watch",  currentCategories: ["Timepieces > Luxury"] },
  { id: "a2", upc: "400234111", sku: "AU-102-S",  name: "Echo-Noise Headphones",   currentCategories: ["Audio > Wireless"] },
  { id: "a3", upc: "400234115", sku: "FT-99-R",   name: "SwiftRun Pro Z",          currentCategories: ["Footwear > Athletics"] },
  { id: "a4", upc: "400234120", sku: "CM-2401-X", name: "InstaCam Retro X",        currentCategories: [] },
  { id: "a5", upc: "400234130", sku: "FN-700-B",  name: "NoiseFree Pro Buds",      currentCategories: ["Electronics"] },
];

// ── CategoryPicker (hierarchical, mirrors EditBrandPage) ─────────────────────
function CategoryPicker({
  selected,
  onChange,
}: {
  selected: string[];
  onChange: (next: string[]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  function toggle(id: string) {
    onChange(
      selected.includes(id) ? selected.filter((c) => c !== id) : [...selected, id]
    );
  }

  return (
    <div ref={containerRef} className="relative space-y-2">
      {/* Selected chips */}
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((id) => {
            const name = findCategoryName(MOCK_CATEGORY_TREE, id) ?? id;
            return (
              <span
                key={id}
                className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
              >
                {name}
                <button
                  type="button"
                  aria-label={`Remove ${name}`}
                  className="rounded p-0.5 hover:bg-primary/20 transition-colors"
                  onClick={() => toggle(id)}
                >
                  <IconClose className="h-2.5 w-2.5" />
                </button>
              </span>
            );
          })}
        </div>
      )}

      {/* Trigger */}
      <button
        type="button"
        className="flex w-full items-center justify-between gap-2 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs transition-colors hover:border-primary/30 focus:outline-none focus:ring-1 focus:ring-primary"
        onClick={() => setOpen((o) => !o)}
      >
        <span className={selected.length ? "text-on-surface" : "italic text-on-surface-variant"}>
          {selected.length
            ? `${selected.length} categor${selected.length === 1 ? "y" : "ies"} selected`
            : "Click to choose categories"}
        </span>
        <IconChevronDown
          className={`h-4 w-4 shrink-0 text-outline transition-transform ${open ? "rotate-180" : ""}`}
        />
      </button>

      {/* Dropdown */}
      {open && (
        <div className="absolute left-0 right-0 top-full z-40 mt-1 rounded-lg border border-outline-variant/20 bg-surface shadow-xl p-2">
          <div className="relative mb-2">
            <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
            <input
              autoFocus
              type="text"
              className="w-full rounded-md border border-outline-variant/20 bg-surface-container-lowest py-2 pl-8 pr-3 text-xs focus:outline-none focus:ring-1 focus:ring-primary"
              placeholder="Filter categories..."
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
            />
          </div>
          <CategoryTreePicker
            tree={MOCK_CATEGORY_TREE}
            selectedIds={selected}
            onToggle={toggle}
            filter={filter}
          />
        </div>
      )}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────
type Props = { onNavigateToProducts: () => void };

export function CategoryAssignmentPage({ onNavigateToProducts }: Props) {
  // ── Step 1 ────────────────────────────────────────────────────────────────
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadDone, setUploadDone] = useState(false);
  const [importBy, setImportBy] = useState<"upc" | "sku">("upc");
  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── Step 2 ────────────────────────────────────────────────────────────────
  const [rows, setRows] = useState<AssignRow[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [targetCategories, setTargetCategories] = useState<string[]>([]);
  const [clearOther, setClearOther] = useState(false);
  const [page, setPage] = useState(1);

  // ── Toast ─────────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  // ── Handlers ─────────────────────────────────────────────────────────────
  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    setSelectedFile(e.target.files?.[0] ?? null);
  }

  function handleUpload() {
    if (!selectedFile) return;
    setRows(MOCK_ASSIGN_ROWS);
    setSelectedIds(new Set(MOCK_ASSIGN_ROWS.map((r) => r.id)));
    setUploadDone(true);
  }

  function toggleRow(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  function handleAssign(approve: boolean) {
    const count = selectedIds.size;
    const cats = targetCategories.join(", ");
    const verb = approve ? "assigned and approved" : "assigned";
    setToast(`${count} product${count !== 1 ? "s" : ""} ${verb} to "${cats}" successfully.`);
  }

  function removeSelected() {
    if (selectedIds.size === 0) return;
    setRows((prev) => prev.filter((r) => !selectedIds.has(r.id)));
    setSelectedIds(new Set());
  }

  function clearList() {
    setRows([]);
    setSelectedIds(new Set());
    setPage(1);
  }

  function resetWizard() {
    setSelectedFile(null);
    setUploadDone(false);
    setRows([]);
    setSelectedIds(new Set());
    setTargetCategories([]);
    setClearOther(false);
    setPage(1);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  const canAssign = selectedIds.size > 0 && targetCategories.length > 0;

  const totalRecords = rows.length;
  const totalPages = Math.max(0, Math.ceil(totalRecords / PAGE_SIZE));
  const safePage = Math.min(Math.max(1, page), Math.max(totalPages, 1));
  const visibleRows = rows.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  const allSelected = visibleRows.length > 0 && visibleRows.every((r) => selectedIds.has(r.id));
  const someSelected = visibleRows.some((r) => selectedIds.has(r.id));

  function toggleAll(checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const r of visibleRows) {
        if (checked) next.add(r.id);
        else next.delete(r.id);
      }
      return next;
    });
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-1 min-w-0">
          <nav className="mb-1 flex flex-wrap gap-x-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="text-on-surface-variant/50">/</span>
            <button type="button" className="text-primary hover:underline" onClick={onNavigateToProducts}>
              Products
            </button>
            <span className="text-on-surface-variant/50">/</span>
            <span className="text-primary">Category Assignment</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Category Assignment</h1>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={onNavigateToProducts}
            className="flex items-center gap-2 rounded-md border border-outline-variant/40 px-4 py-2 text-xs font-bold uppercase tracking-widest text-on-surface-variant hover:bg-surface-container-high transition-colors"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back
          </button>
          <button
            type="button"
            className={`${btnPrimary} ${!canAssign ? "pointer-events-none opacity-40" : ""}`}
            disabled={!canAssign}
            onClick={() => handleAssign(false)}
          >
            <IconAccountTree className="h-4 w-4 shrink-0" />
            Assign
          </button>
          <button
            type="button"
            className={`${btnPrimary} ${!canAssign ? "pointer-events-none opacity-40" : ""}`}
            disabled={!canAssign}
            onClick={() => handleAssign(true)}
          >
            <IconCheckCircle className="h-4 w-4 shrink-0" />
            Assign and Approve
          </button>
        </div>
      </header>

      {/* ── Body ────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* ── Section 1: Category Assignment File Upload ─────────────────── */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${uploadDone ? "bg-secondary-container/30 text-secondary" : "bg-primary text-on-primary"}`}>
              {uploadDone ? <IconCheckCircle className="h-4 w-4" /> : "1"}
            </span>
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Category Assignment File Upload</h3>
          </div>

          <div className="space-y-3">
            <label className={labelBase}>SKU/UPC Data File:</label>
            <div className="flex flex-wrap items-center gap-3">
              <div
                className="flex flex-1 min-w-[260px] cursor-pointer items-center gap-4 rounded-lg border-2 border-dashed border-primary/20 bg-primary/5 px-5 py-4 transition-colors hover:border-primary/40 hover:bg-primary/10"
                onClick={() => fileInputRef.current?.click()}
              >
                <IconCloudUpload className="h-8 w-8 shrink-0 text-primary/50" />
                <div className="min-w-0">
                  {selectedFile ? (
                    <>
                      <p className="truncate text-sm font-semibold text-on-surface">{selectedFile.name}</p>
                      <p className="text-[10px] text-on-surface-variant">
                        {(selectedFile.size / 1024).toFixed(1)} KB · Click to change
                      </p>
                    </>
                  ) : (
                    <>
                      <p className="text-sm font-semibold text-primary/80">Choose File</p>
                      <p className="text-[10px] text-on-surface-variant">Accepts .xlsx, .xls, .csv</p>
                    </>
                  )}
                </div>
              </div>
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,.xls,.csv"
                className="hidden"
                onChange={handleFileChange}
              />
              <label className="flex cursor-pointer items-center gap-2 select-none">
                <input
                  type="checkbox"
                  className="h-4 w-4 accent-primary"
                  checked={importBy === "upc"}
                  onChange={(e) => setImportBy(e.target.checked ? "upc" : "sku")}
                />
                <span className="text-xs font-semibold text-on-surface">Import by UPC</span>
              </label>
              <button
                type="button"
                className={`${btnPrimary} ${!selectedFile ? "pointer-events-none opacity-40" : ""}`}
                disabled={!selectedFile}
                onClick={handleUpload}
              >
                <IconCloudUpload className="h-4 w-4 shrink-0" />
                Upload File
              </button>
            </div>
            <div className="flex items-start gap-3 rounded-md border border-outline-variant/10 bg-surface-container-low p-3">
              <IconInfo className="h-4 w-4 shrink-0 text-primary mt-0.5" />
              <p className="text-[11px] text-on-surface-variant leading-relaxed">
                The data file must contain a column matching the selected identifier ({importBy === "upc" ? "UPC" : "SKU"}). Accepted formats: <strong>.xlsx</strong>, <strong>.xls</strong>, <strong>.csv</strong>.
              </p>
            </div>
          </div>
        </section>

        {/* ── Section 2: Category Assignment ─────────────────────────────── */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm">
          <div className="mb-5 flex items-center gap-3">
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">2</span>
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Category Assignment</h3>
          </div>

          <div className="space-y-4">
            <div className="grid grid-cols-1 items-center gap-3 md:grid-cols-[160px_1fr]">
              <label className={`${labelBase} md:mb-0`}>
                Category: <span className="text-error">*</span>
              </label>
              <CategoryPicker
                selected={targetCategories}
                onChange={setTargetCategories}
              />
            </div>
            <div className="grid grid-cols-1 items-center gap-3 md:grid-cols-[160px_1fr]">
              <span />
              <label className="flex cursor-pointer items-center gap-2 select-none">
                <input
                  type="checkbox"
                  className="h-4 w-4 accent-primary"
                  checked={clearOther}
                  onChange={(e) => setClearOther(e.target.checked)}
                />
                <span className="text-xs font-semibold text-on-surface">Clear Other Categories</span>
              </label>
            </div>
          </div>
        </section>

        {/* ── Section 3: Uploaded products list + pagination ─────────────── */}
        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-outline-variant/10 px-6 py-3">
            <p className="text-[11px] font-medium text-on-surface-variant">
              Page {totalPages === 0 ? 0 : safePage} of {totalPages} pages, Each page {PAGE_SIZE}, Total {totalRecords} records found
            </p>
            <div className="flex items-center gap-2">
              <button
                type="button"
                className="flex items-center gap-1.5 rounded-md bg-error px-3 py-1.5 text-[11px] font-bold text-on-error hover:opacity-90 disabled:opacity-40"
                disabled={selectedIds.size === 0}
                onClick={removeSelected}
              >
                <IconClose className="h-3.5 w-3.5 shrink-0" />
                Remove Selected
              </button>
              <button
                type="button"
                className="flex items-center gap-1.5 rounded-md bg-tertiary px-3 py-1.5 text-[11px] font-bold text-on-tertiary hover:opacity-90 disabled:opacity-40"
                disabled={rows.length === 0}
                onClick={clearList}
              >
                Clear
              </button>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="w-10 px-5 py-3 text-center">
                    <input
                      type="checkbox"
                      className="h-3.5 w-3.5 accent-primary"
                      checked={allSelected}
                      ref={(el) => { if (el) el.indeterminate = !allSelected && someSelected; }}
                      onChange={(e) => toggleAll(e.target.checked)}
                      aria-label="Select all products"
                    />
                  </th>
                  <th className="px-4 py-3">UPC</th>
                  <th className="px-4 py-3">SKU</th>
                  <th className="px-4 py-3">Product Name</th>
                  <th className="px-4 py-3">Current Categories</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/10">
                {visibleRows.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-4 py-10 text-center text-sm text-on-surface-variant">
                      No products uploaded yet.
                    </td>
                  </tr>
                ) : (
                  visibleRows.map((row) => {
                    const isSelected = selectedIds.has(row.id);
                    return (
                      <tr
                        key={row.id}
                        className={`cursor-pointer text-xs transition-colors ${isSelected ? "bg-primary/5" : "hover:bg-surface-container-low"}`}
                        onClick={() => toggleRow(row.id)}
                      >
                        <td className="px-5 py-3.5 text-center" onClick={(e) => e.stopPropagation()}>
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-primary"
                            checked={isSelected}
                            onChange={() => toggleRow(row.id)}
                          />
                        </td>
                        <td className="px-4 py-3.5 font-mono text-[10px] text-on-surface-variant">{row.upc}</td>
                        <td className="px-4 py-3.5 font-mono text-[10px] text-on-surface-variant">{row.sku}</td>
                        <td className="px-4 py-3.5 font-semibold text-on-surface">{row.name}</td>
                        <td className="px-4 py-3.5">
                          {row.currentCategories.length > 0 ? (
                            <div className="flex flex-wrap gap-1">
                              {row.currentCategories.map((c) => (
                                <span
                                  key={c}
                                  className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-medium text-on-surface-variant"
                                >
                                  {c}
                                </span>
                              ))}
                            </div>
                          ) : (
                            <span className="italic text-[10px] text-on-surface-variant/50">No categories</span>
                          )}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          <div className="flex flex-col items-center justify-end gap-2 border-t border-outline-variant/10 px-6 py-3">
            <div className="flex items-center gap-1">
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage <= 1}
                aria-label="First page"
                onClick={() => setPage(1)}
              >
                <IconFirstPage className="h-5 w-5" />
              </button>
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage <= 1}
                aria-label="Previous page"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <IconChevronLeft className="h-5 w-5" />
              </button>
              <button type="button" className="h-8 w-8 rounded bg-primary text-[11px] font-bold text-on-primary">
                {Math.max(1, safePage)}
              </button>
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage >= Math.max(1, totalPages)}
                aria-label="Next page"
                onClick={() => setPage((p) => Math.min(Math.max(1, totalPages), p + 1))}
              >
                <IconChevronRight className="h-5 w-5" />
              </button>
              <button
                type="button"
                className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                disabled={safePage >= Math.max(1, totalPages)}
                aria-label="Last page"
                onClick={() => setPage(Math.max(1, totalPages))}
              >
                <IconLastPage className="h-5 w-5" />
              </button>
            </div>
          </div>
        </section>

        <div className="flex justify-end">
          <button type="button" className={btnGhost} onClick={resetWizard}>
            Cancel
          </button>
        </div>
      </div>

      {/* ── Toast ───────────────────────────────────────────────────────── */}
      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
