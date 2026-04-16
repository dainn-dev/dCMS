import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import {
  IconAdd,
  IconCalendarToday,
  IconCheckCircle,
  IconChevronDown,
  IconChevronRight,
  IconCloudUpload,
  IconDelete,
  IconDownload,
  IconEdit,
  IconFormatBold,
  IconFormatItalic,
  IconFormatListBulleted,
  IconFormatListNumbered,
  IconFormatUnderlined,
  IconFolder,
  IconFolderOpen,
  IconImage,
  IconInfo,
  IconLink,
  IconOpenInNew,
  IconSave,
  IconSearch,
  IconSort,
  IconTag,
  IconUnfoldLess,
  IconUnfoldMore,
} from "../../orders/icons";

const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const sectionHeading =
  "text-sm font-bold text-on-surface border-l-4 border-primary pl-3";
const btnFooterGhost =
  "text-on-surface-variant font-bold text-xs uppercase tracking-widest px-6 py-2 hover:bg-surface-container-high rounded-md transition-colors";
const btnFooterPrimary =
  "px-6 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-primary/20 hover:scale-[1.02] active:scale-95 transition-all flex items-center gap-2";

type CatNode = { id: string; name: string; active?: boolean; children?: CatNode[] };
type PageMode =
  | { kind: "edit"; nodeId: string }
  | { kind: "add"; parentId: string | null };
type EditTab = "general" | "images" | "navbar" | "product-page" | "seo";

const TREE: CatNode[] = [
  {
    id: "c1",
    name: "@12%rebate",
    active: true,
    children: [
      { id: "c1a", name: "Sub-category A", active: true },
      { id: "c1b", name: "Sub-category B", active: false },
    ],
  },
  { id: "c2", name: "1-12-REBATE", active: true },
  { id: "c3", name: "Anniversary", active: false },
  { id: "c4", name: "CGCategory", active: true },
  { id: "c5", name: "Electronics", active: true },
  { id: "c6", name: "Furniture", active: true },
];

function collectExpandableIds(nodes: CatNode[], acc: string[] = []): string[] {
  for (const n of nodes) {
    if (n.children?.length) {
      acc.push(n.id);
      collectExpandableIds(n.children, acc);
    }
  }
  return acc;
}

function findNodeName(nodes: CatNode[], id: string): string | undefined {
  for (const n of nodes) {
    if (n.id === id) return n.name;
    if (n.children?.length) {
      const found = findNodeName(n.children, id);
      if (found) return found;
    }
  }
  return undefined;
}

function findParentId(
  nodes: CatNode[],
  targetId: string,
  parentId: string | null = null
): string | null | undefined {
  for (const n of nodes) {
    if (n.id === targetId) return parentId;
    if (n.children?.length) {
      const found = findParentId(n.children, targetId, n.id);
      if (found !== undefined) return found;
    }
  }
  return undefined;
}

// ─── Category Tree Picker ────────────────────────────────────────────────────

function CategoryTreePicker({
  value,
  onChange,
  tree,
  excludeId,
}: {
  value: string | null;
  onChange: (id: string | null) => void;
  tree: CatNode[];
  excludeId?: string;
}) {
  const [open, setOpen] = useState(false);

  const selectedLabel =
    value === null
      ? "No Parent (Top Level)"
      : (findNodeName(tree, value) ?? "Unknown");

  const handleSelect = (id: string | null) => {
    onChange(id);
    setOpen(false);
  };

  const renderPickerNodes = (nodes: CatNode[], depth = 0): ReactNode =>
    nodes
      .filter((n) => n.id !== excludeId)
      .map((n) => (
        <div key={n.id}>
          <button
            type="button"
            className={`flex w-full items-center gap-2 py-1.5 text-xs transition-colors hover:bg-primary/10 ${
              value === n.id
                ? "bg-primary/10 font-semibold text-primary"
                : "text-on-surface-variant"
            }`}
            style={{ paddingLeft: 12 + depth * 16, paddingRight: 12 }}
            onClick={() => handleSelect(n.id)}
          >
            {n.children?.length ? (
              <IconFolder className="h-3.5 w-3.5 shrink-0 text-primary/60" />
            ) : (
              <IconTag className="h-3.5 w-3.5 shrink-0 opacity-60" />
            )}
            <span className="truncate">{n.name}</span>
          </button>
          {n.children?.length ? renderPickerNodes(n.children, depth + 1) : null}
        </div>
      ));

  return (
    <div className="relative">
      {open && (
        <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} />
      )}
      <button
        type="button"
        className={`${inputBase} flex items-center justify-between gap-2 text-left`}
        onClick={() => setOpen((v) => !v)}
      >
        <span className={value === null ? "italic text-on-surface-variant" : "text-on-surface"}>
          {selectedLabel}
        </span>
        <IconChevronDown
          className={`h-4 w-4 shrink-0 text-outline transition-transform ${open ? "rotate-180" : ""}`}
        />
      </button>

      {open && (
        <div className="absolute left-0 top-full z-50 mt-1 max-h-60 w-full min-w-[220px] overflow-y-auto rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
          <button
            type="button"
            className={`flex w-full items-center gap-2 px-3 py-2 text-left text-xs transition-colors hover:bg-primary/10 ${
              value === null
                ? "bg-primary/10 font-semibold text-primary"
                : "italic text-on-surface-variant"
            }`}
            onClick={() => handleSelect(null)}
          >
            <span className="flex h-3.5 w-3.5 shrink-0 items-center justify-center text-[10px] font-bold opacity-50">
              —
            </span>
            No Parent (Top Level)
          </button>
          <div className="border-t border-outline-variant/10" />
          {renderPickerNodes(tree)}
        </div>
      )}
    </div>
  );
}

// ─── CategoriesPage ──────────────────────────────────────────────────────────

export function CategoriesPage() {
  const expandableIds = useMemo(() => collectExpandableIds(TREE), []);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(["c1"]));
  const [mode, setMode] = useState<PageMode>({ kind: "edit", nodeId: "c1" });
  const [filter, setFilter] = useState("");
  const [tab, setTab] = useState<EditTab>("general");
  const [formParentId, setFormParentId] = useState<string | null>(null);

  useEffect(() => {
    if (mode.kind === "add") {
      setFormParentId(mode.parentId);
      setTab("general");
    } else {
      const p = findParentId(TREE, mode.nodeId);
      setFormParentId(p !== undefined ? p : null);
    }
  }, [mode]);

  const expandAll = useCallback(() => setExpanded(new Set(expandableIds)), [expandableIds]);
  const collapseAll = useCallback(() => setExpanded(new Set()), []);

  const toggleExpanded = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const handleAddSubCategory = useCallback((parentId: string) => {
    setMode({ kind: "add", parentId });
    setExpanded((prev) => new Set([...prev, parentId]));
  }, []);

  // ── Tree rendering ──────────────────────────────────────────────────────────

  const filterLower = filter.trim().toLowerCase();
  const nodeMatches = (n: CatNode): boolean => {
    if (!filterLower) return true;
    if (n.name.toLowerCase().includes(filterLower)) return true;
    return n.children?.some(nodeMatches) ?? false;
  };

  const renderTree = (nodes: CatNode[], depth = 0): ReactNode =>
    nodes.map((node) => {
      if (!nodeMatches(node)) return null;

      const hasChildren = Boolean(node.children?.length);
      const isOpen = hasChildren && expanded.has(node.id);
      const isSelected = mode.kind === "edit" && mode.nodeId === node.id;
      const isActive = node.active !== false;

      const rowBase =
        "group flex w-full items-center gap-1 rounded p-1.5 text-[13px] transition-colors cursor-pointer select-none";
      const rowCls = isSelected
        ? `${rowBase} bg-primary/10 text-primary font-semibold`
        : `${rowBase} text-on-surface-variant hover:bg-surface-container-high`;

      const inactiveBadge = !isActive && (
        <span className="shrink-0 rounded-full bg-outline-variant/25 px-1.5 py-px text-[9px] font-bold uppercase tracking-wider text-on-surface-variant">
          Inactive
        </span>
      );

      const actionBtns = (
        <div className="ml-auto hidden shrink-0 items-center gap-0.5 group-hover:flex">
          <button
            type="button"
            title="Add sub-category"
            className="rounded p-0.5 text-on-surface-variant hover:bg-surface-container-high hover:text-primary"
            onClick={(e) => { e.stopPropagation(); handleAddSubCategory(node.id); }}
          >
            <IconAdd className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            title="Edit"
            className="rounded p-0.5 text-on-surface-variant hover:bg-surface-container-high hover:text-primary"
            onClick={(e) => { e.stopPropagation(); setMode({ kind: "edit", nodeId: node.id }); }}
          >
            <IconEdit className="h-3.5 w-3.5" />
          </button>
          <button
            type="button"
            title="Delete"
            className="rounded p-0.5 text-on-surface-variant hover:bg-error/10 hover:text-error"
            onClick={(e) => { e.stopPropagation(); console.info("[Categories] Delete", node.id); }}
          >
            <IconDelete className="h-3.5 w-3.5" />
          </button>
        </div>
      );

      if (!hasChildren) {
        return (
          <div key={node.id} style={{ marginLeft: depth ? 24 : 0 }}>
            <div
              role="button"
              tabIndex={0}
              className={rowCls}
              onClick={() => setMode({ kind: "edit", nodeId: node.id })}
              onKeyDown={(e) => e.key === "Enter" && setMode({ kind: "edit", nodeId: node.id })}
            >
              <span className="inline-flex w-5 shrink-0" aria-hidden />
              <IconTag className={`h-4 w-4 shrink-0 ${!isActive ? "opacity-40" : "opacity-80"}`} />
              <span className={`truncate ${!isActive ? "opacity-50" : ""}`}>{node.name}</span>
              {inactiveBadge}
              {actionBtns}
            </div>
          </div>
        );
      }

      return (
        <div key={node.id} className={depth ? "mt-1" : ""}>
          <div
            role="button"
            tabIndex={0}
            className={rowCls}
            onClick={() => setMode({ kind: "edit", nodeId: node.id })}
            onKeyDown={(e) => e.key === "Enter" && setMode({ kind: "edit", nodeId: node.id })}
          >
            <button
              type="button"
              className="shrink-0 rounded p-0.5 hover:bg-surface-container-high"
              aria-expanded={isOpen}
              onClick={(e) => { e.stopPropagation(); toggleExpanded(node.id); }}
            >
              {isOpen ? (
                <IconChevronDown className="h-4 w-4" />
              ) : (
                <IconChevronRight className="h-4 w-4" />
              )}
            </button>
            <span className="flex min-w-0 flex-1 items-center gap-1.5">
              {isOpen ? (
                <IconFolderOpen
                  className={`h-4 w-4 shrink-0 text-primary/80 ${!isActive ? "opacity-40" : ""}`}
                />
              ) : (
                <IconFolder
                  className={`h-4 w-4 shrink-0 text-primary/60 ${!isActive ? "opacity-40" : ""}`}
                />
              )}
              <span className={`truncate ${!isActive ? "opacity-50" : ""}`}>{node.name}</span>
            </span>
            {inactiveBadge}
            {actionBtns}
          </div>
          {isOpen && node.children && (
            <div className="ml-6 mt-1 space-y-1 border-l border-outline-variant/30 pl-2">
              {renderTree(node.children, depth + 1)}
            </div>
          )}
        </div>
      );
    });

  // ── Derived display values ──────────────────────────────────────────────────

  const [restrictAccess, setRestrictAccess] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);

  const isAddMode = mode.kind === "add";
  const editNodeName =
    mode.kind === "edit" ? (findNodeName(TREE, mode.nodeId) ?? "Category") : "";
  const addParentName =
    isAddMode && mode.parentId ? findNodeName(TREE, mode.parentId) : null;

  const rightPanelTitle = isAddMode
    ? mode.parentId === null
      ? "New Top-Level Category"
      : "New Sub-category"
    : editNodeName;

  const formKey =
    mode.kind === "edit"
      ? mode.nodeId
      : `add-${mode.parentId ?? "root"}`;

  const tabs: { id: EditTab; label: string }[] = [
    { id: "general", label: "General" },
    { id: "images", label: "Images" },
    { id: "navbar", label: "Navbar Settings" },
    { id: "product-page", label: "Product Page Settings" },
    { id: "seo", label: "SEO Settings" },
  ];

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div
      className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low"
      aria-label="Category management"
    >
      {/* Page header */}
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Categories</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Categories</h1>
          <p className="text-sm text-on-surface-variant">
            Organize the catalog hierarchy, merchandising, and storefront navigation for each category.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            className="flex items-center gap-2 rounded-md bg-primary px-3 py-2 text-xs font-semibold text-on-primary shadow-sm transition-colors hover:opacity-90"
            onClick={() => setMode({ kind: "add", parentId: null })}
          >
            <IconAdd className="h-4 w-4 shrink-0" />
            Add Top Level Category
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-md bg-surface-container-high px-3 py-2 text-xs font-semibold text-on-surface-variant transition-colors hover:bg-surface-variant/80"
            onClick={() => console.info("[Categories] Export (placeholder)")}
          >
            <IconDownload className="h-4 w-4 shrink-0" />
            Export Categories
          </button>
        </div>
      </header>

      <div className="flex min-h-0 flex-1 gap-6 p-6">
        {/* ── Left: hierarchy ─────────────────────────────────────────────── */}
        <section className="flex w-72 shrink-0 flex-col overflow-hidden rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
          <div className="border-b border-outline-variant/10 p-4">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-xs font-bold uppercase tracking-wider text-on-surface-variant">
                Hierarchy
              </h2>
              <div className="flex gap-1">
                <button
                  type="button"
                  className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high"
                  title="Expand all"
                  onClick={expandAll}
                >
                  <IconUnfoldMore className="h-4 w-4" />
                </button>
                <button
                  type="button"
                  className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high"
                  title="Collapse all"
                  onClick={collapseAll}
                >
                  <IconUnfoldLess className="h-4 w-4" />
                </button>
              </div>
            </div>
            <div className="relative mb-3">
              <IconSearch className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
              <input
                className={`${inputBase} pl-8 text-on-surface placeholder:text-on-surface-variant`}
                placeholder="Filter categories..."
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
                aria-label="Filter categories"
              />
            </div>
            <button
              type="button"
              className="flex w-full items-center justify-center gap-2 rounded border border-primary/15 bg-primary/5 py-1.5 text-[11px] font-semibold text-primary transition-colors hover:bg-primary/10"
              onClick={() => console.info("[Categories] Sort categories (placeholder)")}
            >
              <IconSort className="h-3.5 w-3.5 shrink-0" />
              Sort Categories
            </button>
          </div>
          <div className="custom-scrollbar flex-1 space-y-1 overflow-y-auto p-4">
            {renderTree(TREE)}
          </div>
        </section>

        {/* ── Right: editor ───────────────────────────────────────────────── */}
        <section className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">

          {/* Panel title bar */}
          <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/20 bg-surface-container px-5 py-3">
            <div className="flex items-center gap-2.5">
              {isAddMode && (
                <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-primary">
                  New
                </span>
              )}
              <div>
                <p className="text-sm font-semibold text-on-surface">{rightPanelTitle}</p>
                {isAddMode && addParentName && (
                  <p className="text-[11px] text-on-surface-variant">
                    Under: <span className="font-medium">{addParentName}</span>
                  </p>
                )}
              </div>
            </div>
            {isAddMode && (
              <button
                type="button"
                className="rounded px-3 py-1 text-xs font-medium text-on-surface-variant transition-colors hover:bg-surface-container-high"
                onClick={() => setMode({ kind: "edit", nodeId: "c1" })}
              >
                Cancel
              </button>
            )}
          </div>

          {/* Tabs */}
          <div className="flex shrink-0 items-center border-b border-outline-variant/20 bg-surface-container px-4">
            <div className="flex gap-6 overflow-x-auto">
              {tabs.map((t) => {
                const active = tab === t.id;
                return (
                  <button
                    key={t.id}
                    type="button"
                    onClick={() => setTab(t.id)}
                    className={`relative shrink-0 py-4 text-[13px] transition-colors ${
                      active
                        ? "font-bold text-primary"
                        : "font-medium text-on-surface-variant hover:text-primary"
                    }`}
                  >
                    {t.label}
                    {active && (
                      <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-primary" aria-hidden />
                    )}
                  </button>
                );
              })}
            </div>
          </div>

          {/* Form body — key forces re-mount (resets uncontrolled inputs) when node/mode changes */}
          <div
            key={formKey}
            className="custom-scrollbar min-h-0 flex-1 overflow-y-auto p-8"
          >
            <div className="mx-auto max-w-4xl space-y-10">

              {/* ── General ── */}
              {tab === "general" && (
                <div className="space-y-6">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <h3 className={sectionHeading}>General Information</h3>
                    <label className="flex cursor-pointer items-center gap-2">
                      <span className="text-xs font-medium text-on-surface-variant">Active:</span>
                      <input
                        type="checkbox"
                        defaultChecked
                        className="h-4 w-4 rounded border-outline-variant text-primary focus:ring-primary/20"
                      />
                      <span className="text-xs font-semibold text-primary">Yes</span>
                    </label>
                  </div>

                  <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                    <div className="space-y-2">
                      <label className={labelBase}>Parent Category</label>
                      <CategoryTreePicker
                        value={formParentId}
                        onChange={setFormParentId}
                        tree={TREE}
                        excludeId={mode.kind === "edit" ? mode.nodeId : undefined}
                      />
                      <p className="text-[10px] text-on-surface-variant">
                        {formParentId === null
                          ? "This category will be a top-level root."
                          : "This category will appear under the selected parent."}
                      </p>
                    </div>
                    <div className="space-y-2">
                      <label className={labelBase}>Internal Identifier</label>
                      <input
                        className={`${inputBase} ${isAddMode ? "bg-surface-container text-on-surface-variant" : ""}`}
                        defaultValue={isAddMode ? "" : "CAT-2023-REBATE-12"}
                        placeholder={isAddMode ? "Auto-generated on save" : ""}
                        readOnly={isAddMode}
                      />
                    </div>
                  </div>

                  <div className="space-y-4">
                    <label className={labelBase}>Category Name (Multi-language)</label>
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      <div className="space-y-1.5">
                        <span className="text-[10px] font-bold uppercase text-outline">English (EN)</span>
                        <input
                          className={inputBase}
                          defaultValue={isAddMode ? "" : editNodeName}
                          placeholder={isAddMode ? "e.g. Men, Women, Kids" : ""}
                        />
                      </div>
                      <div className="space-y-1.5">
                        <span className="text-[10px] font-bold uppercase text-outline">Chinese (ZH)</span>
                        <input
                          className={inputBase}
                          defaultValue={isAddMode ? "" : "@12% 折扣"}
                          placeholder={isAddMode ? "中文名称" : ""}
                        />
                      </div>
                      <div className="space-y-1.5">
                        <span className="text-[10px] font-bold uppercase text-outline">Vietnamese (VN)</span>
                        <input className={inputBase} placeholder="Tên danh mục" />
                      </div>
                      <div className="space-y-1.5">
                        <span className="text-[10px] font-bold uppercase text-outline">Japanese (JA)</span>
                        <input className={inputBase} placeholder="カテゴリ名" />
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {/* ── Images ── */}
              {tab === "images" && (
                <div className="space-y-6">
                  <h3 className={sectionHeading}>Visual Assets</h3>
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-3">

                    {/* Menu Image */}
                    <div className="space-y-3">
                      <label className={labelBase}>Menu Image</label>
                      <p className="text-[11px] text-on-surface-variant">
                        Displayed in the eStore navigation menu.
                      </p>
                      <button
                        type="button"
                        className="flex aspect-video w-full cursor-pointer flex-col items-center justify-center rounded border-2 border-dashed border-outline-variant/40 bg-surface transition-colors hover:border-primary/40"
                        onClick={() => console.info("[Categories] Upload menu image (placeholder)")}
                      >
                        <IconCloudUpload className="h-8 w-8 text-outline" />
                        <span className="mt-2 text-center text-[10px] text-outline">
                          1200 × 400px recommended
                        </span>
                        <span className="mt-1 text-[10px] text-primary/70 underline">Choose File</span>
                      </button>
                    </div>

                    {/* Category Page Image */}
                    <div className="space-y-3">
                      <label className={labelBase}>Category Page Image</label>
                      <p className="text-[11px] text-on-surface-variant">
                        Displayed on the category product listing page.
                      </p>
                      {isAddMode ? (
                        <button
                          type="button"
                          className="flex aspect-video w-full cursor-pointer flex-col items-center justify-center rounded border-2 border-dashed border-outline-variant/40 bg-surface transition-colors hover:border-primary/40"
                          onClick={() =>
                            console.info("[Categories] Upload category page image (placeholder)")
                          }
                        >
                          <IconCloudUpload className="h-8 w-8 text-outline" />
                          <span className="mt-1 text-[10px] text-primary/70 underline">Choose File</span>
                        </button>
                      ) : (
                        <div className="group relative aspect-video overflow-hidden rounded shadow-sm">
                          <img
                            alt=""
                            className="h-full w-full object-cover"
                            src="https://lh3.googleusercontent.com/aida-public/AB6AXuAPJzecG9bliDfIpfU0WQn6f271QaBU3e2T3PLxsf_FCi0WH0MnpiPgUIfZrwgJcMtnCGJhehRS9NeNl9gMQMb3cr0G8gC1dMGLXHgtDE9q_rAxl09PITIVh745aZKiBD37NWVg4B4OLT3_6KFyT0UzPf3nCYvOH5rBmv4Gn9RzOfBe_4W0AJLL8IRu-RAwBZ3BwVJ5tsKCHT7zGQN2gMSi8XyGSXQAsLOHbDqpW4jkYxuBIxWRFM5npA5thm1Bw9VnFmmy5Wmbshc"
                          />
                          <div className="absolute inset-0 flex items-center justify-center gap-2 bg-black/40 opacity-0 transition-opacity group-hover:opacity-100">
                            <button
                              type="button"
                              className="rounded-full bg-white p-1.5 text-on-surface"
                              aria-label="Edit image"
                            >
                              <IconEdit className="h-4 w-4" />
                            </button>
                            <button
                              type="button"
                              className="rounded-full bg-white p-1.5 text-error"
                              aria-label="Remove image"
                            >
                              <IconDelete className="h-4 w-4" />
                            </button>
                          </div>
                        </div>
                      )}
                    </div>

                    {/* Thumbnail Image */}
                    <div className="space-y-3">
                      <label className={labelBase}>Thumbnail Image</label>
                      <p className="text-[11px] text-on-surface-variant">
                        Small image to represent this category.
                      </p>
                      <button
                        type="button"
                        className="flex aspect-square w-32 cursor-pointer flex-col items-center justify-center rounded border-2 border-dashed border-outline-variant/40 bg-surface transition-colors hover:border-primary/40"
                        onClick={() => console.info("[Categories] Upload thumbnail (placeholder)")}
                      >
                        <IconImage className="h-6 w-6 text-outline" />
                        <span className="mt-2 px-2 text-center text-[10px] text-outline">200 × 200px</span>
                        <span className="mt-1 text-[10px] text-primary/70 underline">Choose File</span>
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {/* ── Navbar Settings ── */}
              {tab === "navbar" && (
                <div className="space-y-8">
                  {/* Visibility */}
                  <div className="space-y-4">
                    <h3 className={sectionHeading}>Visibility</h3>
                    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                      <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 bg-surface p-4 transition-colors hover:bg-surface-container-low">
                        <input
                          type="checkbox"
                          defaultChecked
                          className="mt-0.5 h-4 w-4 rounded border-outline-variant accent-primary"
                        />
                        <div>
                          <p className="text-[13px] font-semibold text-on-surface">Show in Navigation Menu</p>
                          <p className="mt-0.5 text-[11px] text-on-surface-variant">
                            Include this category in the top navigation menu. When unchecked, the category is hidden from the eStore menu.
                          </p>
                        </div>
                      </label>
                      <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 bg-surface p-4 transition-colors hover:bg-surface-container-low">
                        <input
                          type="checkbox"
                          className="mt-0.5 h-4 w-4 rounded border-outline-variant accent-primary"
                        />
                        <div>
                          <p className="text-[13px] font-semibold text-on-surface">Show in Brands Directory</p>
                          <p className="mt-0.5 text-[11px] text-on-surface-variant">
                            Display this category in the Brands directory listing. When unchecked, it is hidden from the brand listing page.
                          </p>
                        </div>
                      </label>
                    </div>
                  </div>

                  {/* Publishing Schedule */}
                  <div className="space-y-4">
                    <h3 className={sectionHeading}>Publishing Schedule</h3>
                    <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                      <div className="space-y-2">
                        <label className={labelBase}>
                          <span className="flex items-center gap-1.5">
                            <IconCalendarToday className="h-3.5 w-3.5" />
                            Publish From
                          </span>
                        </label>
                        <input
                          type="datetime-local"
                          className={inputBase}
                          defaultValue=""
                        />
                        <p className="text-[10px] text-on-surface-variant">
                          Date &amp; time the category starts appearing on eStore.
                        </p>
                      </div>
                      <div className="space-y-2">
                        <label className={labelBase}>
                          <span className="flex items-center gap-1.5">
                            <IconCalendarToday className="h-3.5 w-3.5" />
                            Publish Until
                          </span>
                        </label>
                        <input
                          type="datetime-local"
                          className={inputBase}
                          defaultValue=""
                        />
                        <p className="text-[10px] text-on-surface-variant">
                          Date &amp; time the category is removed from eStore. Leave blank for no expiry.
                        </p>
                      </div>
                    </div>
                  </div>

                  {/* Navigation Display */}
                  <div className="space-y-4">
                    <h3 className={sectionHeading}>Navigation Display</h3>
                    <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                      <div className="space-y-2">
                        <label className={labelBase}>Sort Priority</label>
                        <input
                          type="number"
                          className={`${inputBase} w-36`}
                          defaultValue={10}
                          min={1}
                        />
                        <p className="text-[10px] text-on-surface-variant">
                          Priority relative to other categories. Highest priority = 1.
                        </p>
                      </div>
                      <div className="space-y-2">
                        <label className={labelBase}>Custom Navigation URL</label>
                        <input
                          className={inputBase}
                          placeholder="/category/custom-path"
                          defaultValue={isAddMode ? "" : ""}
                        />
                        <p className="text-[10px] text-on-surface-variant">
                          Override the default URL for this category in the navigation menu.
                        </p>
                      </div>
                    </div>
                    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 bg-surface p-4 transition-colors hover:bg-surface-container-low">
                      <input
                        type="checkbox"
                        className="mt-0.5 h-4 w-4 rounded border-outline-variant accent-primary"
                      />
                      <div>
                        <p className="text-[13px] font-semibold text-on-surface">Break Navigation Column</p>
                        <p className="mt-0.5 text-[11px] text-on-surface-variant">
                          Display this category in a new column in the navigation menu.
                        </p>
                      </div>
                    </label>
                  </div>
                </div>
              )}

              {/* ── Product Page Settings ── */}
              {tab === "product-page" && (
                <div className="space-y-8">
                  {/* Product Recommendations */}
                  <div className="space-y-4">
                    <h3 className={sectionHeading}>Product Recommendations</h3>
                    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 bg-surface p-4 transition-colors hover:bg-surface-container-low">
                      <input
                        type="checkbox"
                        className="mt-0.5 h-4 w-4 rounded border-outline-variant accent-primary"
                      />
                      <div>
                        <p className="text-[13px] font-semibold text-on-surface">
                          Do not recommend products in this category
                        </p>
                        <p className="mt-0.5 text-[11px] text-on-surface-variant">
                          Exclude products under this category from automated product recommendations.
                          By default, products are recommended on the Product Details page using the
                          same category as the recommendation signal.
                        </p>
                      </div>
                    </label>
                  </div>

                  {/* Promote Products */}
                  <div className="space-y-4">
                    <h3 className={sectionHeading}>Promote Products</h3>
                    <p className="text-sm text-on-surface-variant">
                      Select specific products to promote and feature within this category.
                    </p>
                    <div className="flex items-center gap-3 rounded-lg border border-outline-variant/15 bg-surface p-4">
                      <div className="flex-1">
                        <p className="text-[13px] font-semibold text-on-surface">Featured Products</p>
                        <p className="text-[11px] text-on-surface-variant">0 products selected</p>
                      </div>
                      <button
                        type="button"
                        className="flex items-center gap-2 rounded-md border border-primary/30 bg-primary/5 px-4 py-2 text-xs font-semibold text-primary transition-colors hover:bg-primary/10"
                        onClick={() => console.info("[Categories] Promote products — View Listing (placeholder)")}
                      >
                        <IconOpenInNew className="h-3.5 w-3.5 shrink-0" />
                        View Listing
                      </button>
                    </div>
                  </div>

                  {/* Default Sort */}
                  <div className="space-y-4">
                    <h3 className={sectionHeading}>Default Sort Order</h3>
                    <div className="max-w-xs space-y-2">
                      <label className={labelBase}>Default Sort Option</label>
                      <select className={inputBase} defaultValue="bestseller">
                        <option value="latest">Latest</option>
                        <option value="price-asc">Price (Low – High)</option>
                        <option value="price-desc">Price (High – Low)</option>
                        <option value="name">Name</option>
                        <option value="bestseller">Best Seller</option>
                        <option value="popularity">Popularity</option>
                      </select>
                    </div>
                  </div>

                  {/* Category Description */}
                  <div className="space-y-4">
                    <h3 className={sectionHeading}>Description</h3>
                    <p className="text-sm text-on-surface-variant">
                      Enter additional details such as a category overview and description shown on
                      the category listing page.
                    </p>
                    <div className="overflow-hidden rounded-lg border border-outline-variant/30">
                      <div className="flex flex-wrap items-center gap-1 border-b border-outline-variant/20 bg-surface-container px-3 py-2">
                        {[
                          <IconFormatBold className="h-4 w-4" />,
                          <IconFormatItalic className="h-4 w-4" />,
                          <IconFormatUnderlined className="h-4 w-4" />,
                        ].map((icon, i) => (
                          <button key={i} type="button" className="rounded p-1 hover:bg-surface-container-lowest">
                            {icon}
                          </button>
                        ))}
                        <span className="mx-1 h-4 w-px bg-outline-variant/30" aria-hidden />
                        {[
                          <IconFormatListBulleted className="h-4 w-4" />,
                          <IconFormatListNumbered className="h-4 w-4" />,
                        ].map((icon, i) => (
                          <button key={i} type="button" className="rounded p-1 hover:bg-surface-container-lowest">
                            {icon}
                          </button>
                        ))}
                        <span className="mx-1 h-4 w-px bg-outline-variant/30" aria-hidden />
                        <button type="button" className="rounded p-1 hover:bg-surface-container-lowest">
                          <IconLink className="h-4 w-4" />
                        </button>
                      </div>
                      <textarea
                        className={`min-h-[140px] ${inputBase} resize-y`}
                        placeholder="Enter category overview and description..."
                        rows={5}
                      />
                    </div>
                  </div>
                </div>
              )}

              {/* ── SEO ── */}
              {tab === "seo" && (
                <div className="space-y-8 pb-6">
                  {/* SEO Metadata */}
                  <div className="space-y-5">
                    <h3 className={sectionHeading}>SEO &amp; Metadata</h3>
                    <div className="space-y-2">
                      <label className={labelBase}>Meta Title</label>
                      <input
                        className={inputBase}
                        defaultValue={isAddMode ? "" : "Shop @12%rebate Category Online | Exclusive Deals"}
                        placeholder={isAddMode ? "e.g. Shop Electronics Online | Best Deals" : ""}
                      />
                      <p className="text-[10px] text-on-surface-variant">
                        Text that appears on the browser's title bar and tab.
                      </p>
                    </div>
                    <div className="space-y-2">
                      <label className={labelBase}>Meta Keywords</label>
                      <input
                        className={inputBase}
                        defaultValue={isAddMode ? "" : "rebate, deals, cashback, enterprise savings"}
                        placeholder="Comma-separated keywords, e.g. electronics, gadgets, tech"
                      />
                      <p className="text-[10px] text-on-surface-variant">
                        Relevant keywords to help search engines improve this category's visibility.
                      </p>
                    </div>
                    <div className="space-y-2">
                      <label className={labelBase}>Meta Description</label>
                      <textarea
                        className={inputBase}
                        rows={3}
                        defaultValue={
                          isAddMode
                            ? ""
                            : "Discover huge savings in our rebate category. Browse high-quality items with guaranteed 12% cash back for enterprise members."
                        }
                        placeholder={isAddMode ? "Describe this category for search engines..." : ""}
                      />
                      <p className="text-[10px] text-on-surface-variant">
                        Brief overview displayed in search result listings.
                      </p>
                    </div>
                  </div>

                  {/* Google Search Preview */}
                  <div className="rounded-lg border border-outline-variant/20 bg-surface-container p-5">
                    <span className="mb-3 block text-[10px] font-bold uppercase tracking-wider text-outline">
                      Google Search Preview
                    </span>
                    <div className="space-y-1">
                      <p className="truncate text-sm font-medium text-blue-700">
                        {isAddMode
                          ? "Category Title | Your Store Name"
                          : "Shop @12%rebate Category Online | Exclusive Deals"}
                      </p>
                      <p className="truncate text-xs text-green-700">
                        {isAddMode
                          ? "https://example.com/category/new-category"
                          : "https://example.com/category/@12-percent-rebate"}
                      </p>
                      <p className="text-xs leading-tight text-on-surface-variant">
                        {isAddMode
                          ? "Your meta description will appear here once filled in above."
                          : "Discover huge savings in our rebate category. Browse high-quality items with guaranteed 12% cash back for enterprise members."}
                      </p>
                    </div>
                  </div>

                  {/* Restrict Access */}
                  <div className="space-y-5">
                    <h3 className={sectionHeading}>Access Control</h3>
                    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 bg-surface p-4 transition-colors hover:bg-surface-container-low">
                      <input
                        type="checkbox"
                        checked={restrictAccess}
                        onChange={(e) => setRestrictAccess(e.target.checked)}
                        className="mt-0.5 h-4 w-4 rounded border-outline-variant accent-primary"
                      />
                      <div>
                        <p className="text-[13px] font-semibold text-on-surface">Restrict Access</p>
                        <p className="mt-0.5 text-[11px] text-on-surface-variant">
                          Restrict visibility of this category to specific applications, member types,
                          and member tiers. When enabled, only the selected audience can see this category.
                        </p>
                      </div>
                    </label>

                    {restrictAccess && (
                      <div className="grid grid-cols-1 gap-5 rounded-lg border border-primary/15 bg-primary/5 p-5 md:grid-cols-3">
                        <div className="space-y-2">
                          <label className={labelBase}>Application</label>
                          <select className={inputBase} defaultValue="">
                            <option value="">All Applications</option>
                            <option value="website">Website</option>
                            <option value="mobile">Mobile App</option>
                          </select>
                          <p className="text-[10px] text-on-surface-variant">
                            Visible only to the selected application channel.
                          </p>
                        </div>
                        <div className="space-y-2">
                          <label className={labelBase}>Member Type</label>
                          <select className={inputBase} defaultValue="">
                            <option value="">All Member Types</option>
                            <option value="retail">Retail</option>
                            <option value="wholesale">Wholesale</option>
                            <option value="enterprise">Enterprise</option>
                          </select>
                          <p className="text-[10px] text-on-surface-variant">
                            Visible only to the selected member type.
                          </p>
                        </div>
                        <div className="space-y-2">
                          <label className={labelBase}>Member Tier</label>
                          <select className={inputBase} defaultValue="">
                            <option value="">All Tiers</option>
                            <option value="bronze">Bronze</option>
                            <option value="silver">Silver</option>
                            <option value="gold">Gold</option>
                            <option value="platinum">Platinum</option>
                          </select>
                          <p className="text-[10px] text-on-surface-variant">
                            Visible only to the selected member tier.
                          </p>
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Footer */}
          <footer className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-outline-variant/20 bg-surface-container-high px-6 py-4">
            <div className="flex items-center gap-1 text-[11px] italic text-on-surface-variant">
              <IconInfo className="h-4 w-4 shrink-0" />
              {isAddMode
                ? "Fill in the details above to create a new category."
                : "Last updated by Admin 2 hours ago"}
            </div>
            <div className="flex gap-3">
              <button
                type="button"
                className={btnFooterGhost}
                onClick={() => {
                  if (isAddMode) {
                    setMode({ kind: "edit", nodeId: "c1" });
                  } else {
                    console.info("[Categories] Discard changes (placeholder)");
                  }
                }}
              >
                {isAddMode ? "Cancel" : "Discard Changes"}
              </button>
              <button
                type="button"
                className={btnFooterPrimary}
                onClick={() => {
                  if (isAddMode) {
                    console.info("[Categories] Create category (placeholder)", { formParentId });
                    setShowSuccessModal(true);
                  } else {
                    console.info("[Categories] Save (placeholder)");
                  }
                }}
              >
                <IconSave className="h-4 w-4" />
                {isAddMode ? "Create Category" : "Save Category"}
              </button>
            </div>
          </footer>
        </section>
      </div>

      {/* ── Success Confirmation Modal ── */}
      {showSuccessModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="success-modal-title"
        >
          <div className="mx-4 w-full max-w-sm rounded-2xl bg-surface p-8 shadow-2xl">
            <div className="flex flex-col items-center gap-5 text-center">
              <div className="flex h-16 w-16 items-center justify-center rounded-full bg-primary/10">
                <IconCheckCircle className="h-8 w-8 text-primary" />
              </div>
              <div className="space-y-1.5">
                <h3
                  id="success-modal-title"
                  className="text-base font-bold text-on-surface"
                >
                  Category Created Successfully!
                </h3>
                <p className="text-sm text-on-surface-variant">
                  The new category has been saved and is now available in the hierarchy.
                  {formParentId === null
                    ? " It was added as a top-level category."
                    : ` It was added under "${findNodeName(TREE, formParentId) ?? "the selected parent"}".`}
                </p>
              </div>
              <div className="flex w-full flex-col gap-2">
                <button
                  type="button"
                  className={btnFooterPrimary + " w-full justify-center"}
                  onClick={() => {
                    setShowSuccessModal(false);
                    setMode({ kind: "edit", nodeId: "c1" });
                  }}
                >
                  Continue Editing
                </button>
                <button
                  type="button"
                  className={btnFooterGhost + " w-full justify-center"}
                  onClick={() => {
                    setShowSuccessModal(false);
                    setMode({ kind: "add", parentId: null });
                  }}
                >
                  Add Another Category
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
