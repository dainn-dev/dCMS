import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type ReactNode,
} from "react";
import { exportCategoriesToXlsx } from "../exportCategoriesXlsx";
import { MultiLangInput, MultiLangTextarea } from "../components/MultiLangField";
import { MultiLangLexicalRichText } from "../components/MultiLangLexicalRichText";
import {
  IconAdd,
  IconCalendarToday,
  IconCheckCircle,
  IconChevronDown,
  IconChevronRight,
  IconChevronUp,
  IconClose,
  IconCloudUpload,
  IconDelete,
  IconDownload,
  IconEdit,
  IconFolder,
  IconFolderOpen,
  IconInfo,
  IconOpenInNew,
  IconSave,
  IconSearch,
  IconSort,
  IconTag,
  IconUnfoldLess,
  IconUnfoldMore,
  IconWarning,
} from "../../orders/icons";
import {
  fetchCategories,
  createCategory,
  updateCategory,
  deleteCategory,
  reclassifyCategory,
  sortCategories,
  type CategoryPayload,
} from "../api/categoriesApi";

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

/** Same mock catalog as `EditCampaignPage` — replace when product listing API exists. */
const MOCK_FEATURED_PRODUCTS = [
  "Vantage Series 5 Watch (WT-550-B)",
  "Echo-Noise Headphones (AU-102-S)",
  "SwiftRun Pro Z (FT-99-R)",
];

function FeaturedProductSearchablePicker({
  label,
  options,
  selected,
  onChange,
  placeholder,
}: {
  label: string;
  options: string[];
  selected: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
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

  const filtered = options.filter((o) => o.toLowerCase().includes(search.toLowerCase()));

  function toggle(opt: string) {
    onChange(selected.includes(opt) ? selected.filter((s) => s !== opt) : [...selected, opt]);
  }

  return (
    <div ref={ref} className="relative space-y-1.5">
      <label className={`${labelBase} mb-1`}>{label}</label>
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((s) => (
            <span
              key={s}
              className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-bold uppercase text-primary"
            >
              {s}
              <button
                type="button"
                onClick={() => toggle(s)}
                className="rounded p-0.5 hover:bg-primary/20 transition-colors"
                aria-label={`Remove ${s}`}
              >
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
          {selected.length === 0 ? (placeholder ?? `Search ${label.toLowerCase()}…`) : `${selected.length} selected`}
        </span>
        <IconChevronDown className={`h-3.5 w-3.5 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute left-0 top-full z-[60] mt-1 w-full overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
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
          <div className="max-h-44 overflow-y-auto">
            {filtered.length === 0 ? (
              <p className="px-3 py-3 text-center text-xs text-on-surface-variant">No results</p>
            ) : (
              filtered.map((opt) => (
                <label
                  key={opt}
                  className="flex cursor-pointer items-center gap-2.5 px-3 py-2 hover:bg-surface-container transition-colors select-none"
                  onMouseDown={(e) => e.stopPropagation()}
                  onClick={(e) => {
                    e.preventDefault();
                    toggle(opt);
                  }}
                >
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 accent-primary shrink-0 pointer-events-none"
                    checked={selected.includes(opt)}
                    readOnly
                  />
                  <span className="text-xs text-on-surface">{opt}</span>
                </label>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export type CatNode = {
  id: string;
  name: string;
  active?: boolean;
  publishFrom?: string;   // ISO date string, e.g. "2024-01-01"
  publishUntil?: string;  // ISO date string — past date = expired
  sortOrder?: number;
  children?: CatNode[];
  /** Full DTO from API — available when loaded from backend. */
  _dto?: Record<string, unknown>;
};
type PageMode =
  | { kind: "edit"; nodeId: string }
  | { kind: "add"; parentId: string | null };
type EditTab = "general" | "images" | "navbar" | "product-page" | "seo";

// ─── Category Name Field ──────────────────────────────────────────────────────

function CategoryNameField({
  isAddMode,
  defaultEnValue,
}: {
  isAddMode: boolean;
  defaultEnValue: string;
}) {
  return (
    <MultiLangInput
      label="Category Name (Multi-language)"
      defaultValues={{
        en: isAddMode ? "" : defaultEnValue,
      }}
      placeholders={{ en: "e.g. Men, Women, Kids", vn: "Tên danh mục", zh: "中文名称", ja: "カテゴリ名" }}
    />
  );
}

const INITIAL_TREE: CatNode[] = [
  {
    id: "c1",
    name: "@12%rebate",
    active: true,
    publishFrom: "2024-01-01",
    publishUntil: "2027-12-31",
    sortOrder: 1,
    children: [
      {
        id: "c1a",
        name: "Sub-category A",
        active: true,
        publishFrom: "2024-01-01",
        publishUntil: "2027-12-31",
        sortOrder: 1,
      },
      {
        id: "c1b",
        name: "Sub-category B",
        active: false,
        publishFrom: "2023-06-01",
        publishUntil: "2024-03-15",  // expired
        sortOrder: 2,
      },
    ],
  },
  {
    id: "c2",
    name: "1-12-REBATE",
    active: true,
    publishFrom: "2023-12-01",
    publishUntil: "2024-02-28",  // expired
    sortOrder: 2,
  },
  {
    id: "c3",
    name: "Anniversary",
    active: false,
    publishFrom: "2023-09-01",
    publishUntil: "2023-12-31",  // expired
    sortOrder: 3,
  },
  {
    id: "c4",
    name: "CGCategory",
    active: true,
    publishFrom: "2024-03-01",
    publishUntil: "2028-06-30",
    sortOrder: 4,
  },
  {
    id: "c5",
    name: "Electronics",
    active: true,
    publishFrom: "2024-01-01",
    publishUntil: "2028-12-31",
    sortOrder: 5,
  },
  {
    id: "c6",
    name: "Furniture",
    active: true,
    publishFrom: "2024-06-01",
    publishUntil: "2027-06-30",
    sortOrder: 6,
  },
];

// ─── Tree mutation helpers ────────────────────────────────────────────────────

function removeNode(
  nodes: CatNode[],
  id: string
): { tree: CatNode[]; removed: CatNode | null } {
  let removed: CatNode | null = null;
  const tree = nodes
    .filter((n) => {
      if (n.id === id) { removed = n; return false; }
      return true;
    })
    .map((n) => {
      if (!n.children?.length) return n;
      const result = removeNode(n.children, id);
      if (result.removed) removed = result.removed;
      return { ...n, children: result.tree };
    });
  return { tree, removed };
}

function insertNode(
  nodes: CatNode[],
  node: CatNode,
  parentId: string | null
): CatNode[] {
  if (parentId === null) return [...nodes, node];
  return nodes.map((n) => {
    if (n.id === parentId)
      return { ...n, children: [...(n.children ?? []), node] };
    if (n.children?.length)
      return { ...n, children: insertNode(n.children, node, parentId) };
    return n;
  });
}

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

function findNodeRef(nodes: CatNode[], id: string): CatNode | null {
  for (const n of nodes) {
    if (n.id === id) return n;
    if (n.children?.length) {
      const f = findNodeRef(n.children, id);
      if (f) return f;
    }
  }
  return null;
}

function getOrderingContext(nodes: CatNode[], parentId: string | null): CatNode[] {
  if (parentId === null) return [...nodes];
  const p = findNodeRef(nodes, parentId);
  return p?.children ? [...p.children] : [];
}

function applySiblingOrder(nodes: CatNode[], parentId: string | null, ordered: CatNode[]): CatNode[] {
  if (parentId === null) return ordered.map((n) => ({ ...n }));
  return nodes.map((n) => {
    if (n.id === parentId) return { ...n, children: ordered.map((c) => ({ ...c })) };
    if (n.children?.length) return { ...n, children: applySiblingOrder(n.children, parentId, ordered) };
    return n;
  });
}

function firstNodeIdInTree(nodes: CatNode[]): string | null {
  if (!nodes.length) return null;
  return nodes[0].id;
}

function collectParentsWithChildren(
  nodes: CatNode[],
  acc: { id: string; name: string }[] = []
): { id: string; name: string }[] {
  for (const n of nodes) {
    if (n.children?.length) {
      acc.push({ id: n.id, name: n.name });
      collectParentsWithChildren(n.children, acc);
    }
  }
  return acc;
}

// ─── Expiry helpers ───────────────────────────────────────────────────────────

const TODAY = new Date();
TODAY.setHours(0, 0, 0, 0);

function isExpired(node: CatNode): boolean {
  if (!node.publishUntil) return false;
  return new Date(node.publishUntil) < TODAY;
}

function countExpiredInTree(nodes: CatNode[]): number {
  return nodes.reduce((acc, n) => {
    const self = isExpired(n) ? 1 : 0;
    const children = countExpiredInTree(n.children ?? []);
    return acc + self + children;
  }, 0);
}

const MAX_CATEGORY_IMG_BYTES = 2 * 1024 * 1024;

const DEFAULT_EDIT_CATEGORY_PAGE_IMG =
  "https://lh3.googleusercontent.com/aida-public/AB6AXuAPJzecG9bliDfIpfU0WQn6f271QaBU3e2T3PLxsf_FCi0WH0MnpiPgUIfZrwgJcMtnCGJhehRS9NeNl9gMQMb3cr0G8gC1dMGLXHgtDE9q_rAxl09PITIVh745aZKiBD37NWVg4B4OLT3_6KFyT0UzPf3nCYvOH5rBmv4Gn9RzOfBe_4W0AJLL8IRu-RAwBZ3BwVJ5tsKCHT7zGQN2gMSi8XyGSXQAsLOHbDqpW4jkYxuBIxWRFM5npA5thm1Bw9VnFmmy5Wmbshc";

function CategoryImageSlot({
  label,
  hint,
  aspectClass,
  value,
  onChange,
  emptyHint,
}: {
  label: string;
  hint: string;
  aspectClass: string;
  value: string;
  onChange: (next: string) => void;
  emptyHint: string;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [err, setErr] = useState("");

  function pick() {
    setErr("");
    inputRef.current?.click();
  }

  function onFile(e: ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = "";
    if (!f) return;
    if (!f.type.startsWith("image/")) {
      setErr("Choose an image file (JPG, PNG, WEBP).");
      return;
    }
    if (f.size > MAX_CATEGORY_IMG_BYTES) {
      setErr("Max file size is 2 MB.");
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === "string") onChange(reader.result);
    };
    reader.readAsDataURL(f);
  }

  return (
    <div className="space-y-3">
      <label className={labelBase}>{label}</label>
      <p className="text-[11px] text-on-surface-variant">{hint}</p>
      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp,image/*"
        className="sr-only"
        onChange={onFile}
      />
      {value ? (
        <div
          className={`group relative ${aspectClass} w-full overflow-hidden rounded border border-outline-variant/20 bg-surface shadow-sm`}
        >
          <img alt="" src={value} className="h-full w-full object-cover" />
          <div className="absolute inset-0 flex items-center justify-center gap-2 bg-black/40 opacity-0 transition-opacity group-hover:opacity-100">
            <button
              type="button"
              className="rounded-full bg-white p-1.5 text-on-surface"
              aria-label="Replace image"
              onClick={pick}
            >
              <IconEdit className="h-4 w-4" />
            </button>
            <button
              type="button"
              className="rounded-full bg-white p-1.5 text-error"
              aria-label="Remove image"
              onClick={() => onChange("")}
            >
              <IconDelete className="h-4 w-4" />
            </button>
          </div>
        </div>
      ) : (
        <button
          type="button"
          className={`flex ${aspectClass} w-full cursor-pointer flex-col items-center justify-center rounded border-2 border-dashed border-outline-variant/40 bg-surface transition-colors hover:border-primary/40`}
          onClick={pick}
        >
          <IconCloudUpload className="h-8 w-8 text-outline" />
          <span className="mt-2 text-center text-[10px] text-outline">{emptyHint}</span>
          <span className="mt-1 text-[10px] text-primary/70 underline">Choose File</span>
        </button>
      )}
      {err ? <p className="text-[10px] text-error">{err}</p> : null}
    </div>
  );
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

export function CategoriesPage({
  tenantId,
  authToken,
}: {
  tenantId?: string;
  authToken?: string;
}) {
  const [treeData, setTreeData] = useState<CatNode[]>(INITIAL_TREE);
  const [apiLoading, setApiLoading] = useState(false);
  const expandableIds = useMemo(() => collectExpandableIds(treeData), [treeData]);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(["c1"]));
  const [mode, setMode] = useState<PageMode>({ kind: "edit", nodeId: "c1" });
  const [filter, setFilter] = useState("");
  const [hideExpired, setHideExpired] = useState(true);
  const [tab, setTab] = useState<EditTab>("general");
  const [formParentId, setFormParentId] = useState<string | null>(null);
  const [originalParentId, setOriginalParentId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ message: string; visible: boolean }>({
    message: "",
    visible: false,
  });

  // ── Load from API on mount (if tenantId provided) ─────────────────────────
  useEffect(() => {
    if (!tenantId) return;
    let cancelled = false;
    setApiLoading(true);
    fetchCategories(tenantId, authToken)
      .then((tree) => { if (!cancelled) { setTreeData(tree); setApiLoading(false); } })
      .catch((err) => {
        if (!cancelled) {
          console.error("[CategoriesPage] fetch failed", err);
          setApiLoading(false);
          showToast("Failed to load categories from server. Showing demo data.");
        }
      });
    return () => { cancelled = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantId, authToken]);

  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [sortModalOpen, setSortModalOpen] = useState(false);
  const [sortParentId, setSortParentId] = useState<string | null>(null);
  const [sortDraftIds, setSortDraftIds] = useState<string[]>([]);
  const [promoteListingOpen, setPromoteListingOpen] = useState(false);
  const [featuredProducts, setFeaturedProducts] = useState<string[]>([]);
  const [featuredProductsDraft, setFeaturedProductsDraft] = useState<string[]>([]);
  const [imgMenuSrc, setImgMenuSrc] = useState("");
  const [imgCategoryPageSrc, setImgCategoryPageSrc] = useState("");
  const [imgThumbSrc, setImgThumbSrc] = useState("");

  // Auto-hide toast after 3 s
  useEffect(() => {
    if (!toast.visible) return;
    const t = setTimeout(() => setToast((p) => ({ ...p, visible: false })), 3000);
    return () => clearTimeout(t);
  }, [toast.visible]);

  const showToast = useCallback((message: string) => {
    setToast({ message, visible: true });
  }, []);

  useEffect(() => {
    if (mode.kind === "add") {
      setFormParentId(mode.parentId);
      setOriginalParentId(mode.parentId);
      setTab("general");
    } else {
      const p = findParentId(treeData, mode.nodeId);
      const pid = p !== undefined ? p : null;
      setFormParentId(pid);
      setOriginalParentId(pid);
    }
  // treeData intentionally excluded — only sync when mode changes
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  const isAddMode = mode.kind === "add";

  // Reclassification — detected when parent picker changes from original
  const isReclassifying =
    !isAddMode && formParentId !== originalParentId;
  const reclassifyFromLabel =
    originalParentId === null
      ? "Top Level"
      : (findNodeName(treeData, originalParentId) ?? "Unknown");
  const reclassifyToLabel =
    formParentId === null
      ? "Top Level"
      : (findNodeName(treeData, formParentId) ?? "Unknown");

  const handleSaveCategory = useCallback(async () => {
    const nodeNumericId = mode.kind === "edit" ? parseInt(mode.nodeId, 10) : NaN;
    const parentNumericId = formParentId ? parseInt(formParentId, 10) : null;

    if (isReclassifying && mode.kind === "edit" && tenantId) {
      // ── Reclassify via API ──────────────────────────────────────────────
      try {
        await reclassifyCategory(tenantId, nodeNumericId, parentNumericId, authToken);
        // Optimistic local tree mutation
        const { tree: treeWithout, removed } = removeNode(treeData, mode.nodeId);
        if (removed) {
          setTreeData(insertNode(treeWithout, removed, formParentId));
          setOriginalParentId(formParentId);
          if (formParentId) setExpanded((prev) => new Set([...prev, formParentId]));
        }
        showToast(
          formParentId === null
            ? `"${findNodeName(treeData, mode.nodeId)}" reclassified as a top-level category.`
            : `"${findNodeName(treeData, mode.nodeId)}" moved under "${reclassifyToLabel}".`
        );
      } catch (err) {
        showToast(`Reclassify failed: ${err instanceof Error ? err.message : "Unknown error"}`);
      }
      return;
    }

    if (isAddMode) {
      if (tenantId) {
        // ── Create via API ────────────────────────────────────────────────
        const slug = `cat-${Date.now()}`;
        const payload: CategoryPayload = {
          name: "New Category",
          slug,
          parentId: formParentId ? parseInt(formParentId, 10) : null,
        };
        try {
          const newNode = await createCategory(tenantId, payload, authToken);
          setTreeData((prev) => insertNode(prev, newNode, formParentId));
          showToast("Category created successfully.");
          setShowSuccessModal(true);
        } catch (err) {
          showToast(`Create failed: ${err instanceof Error ? err.message : "Unknown error"}`);
        }
      } else {
        showToast("Category created successfully.");
        setShowSuccessModal(true);
      }
      return;
    }

    if (tenantId && !isNaN(nodeNumericId)) {
      // ── Update via API ────────────────────────────────────────────────
      const existing = findNodeRef(treeData, mode.nodeId);
      const payload: CategoryPayload = {
        name: existing?.name ?? "Category",
        slug: `cat-${nodeNumericId}`,
        parentId: parentNumericId,
      };
      try {
        await updateCategory(tenantId, nodeNumericId, payload, authToken);
        showToast("Category saved.");
      } catch (err) {
        showToast(`Save failed: ${err instanceof Error ? err.message : "Unknown error"}`);
      }
    } else {
      showToast("Category saved.");
    }
  }, [
    isAddMode, isReclassifying, mode, treeData, formParentId,
    reclassifyToLabel, showToast, tenantId, authToken,
  ]);

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
  const expiredCount = useMemo(() => countExpiredInTree(treeData), [treeData]);

  const nodeMatches = (n: CatNode): boolean => {
    // Hide expired nodes when checkbox is checked
    if (hideExpired && isExpired(n)) return false;
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

      const nodeExpired = isExpired(node);

      const inactiveBadge = !isActive && (
        <span className="shrink-0 rounded-full bg-outline-variant/25 px-1.5 py-px text-[9px] font-bold uppercase tracking-wider text-on-surface-variant group-hover:hidden">
          Inactive
        </span>
      );

      const expiredBadge = nodeExpired && (
        <span className="shrink-0 rounded-full bg-error/10 px-1.5 py-px text-[9px] font-bold uppercase tracking-wider text-error group-hover:hidden">
          Expired
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
            onClick={(e) => {
              e.stopPropagation();
              setDeleteTarget({ id: node.id, name: node.name });
            }}
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
              className={`${rowCls} ${nodeExpired ? "opacity-60" : ""}`}
              onClick={() => setMode({ kind: "edit", nodeId: node.id })}
              onKeyDown={(e) => e.key === "Enter" && setMode({ kind: "edit", nodeId: node.id })}
            >
              <span className="inline-flex w-5 shrink-0" aria-hidden />
              <IconTag className={`h-4 w-4 shrink-0 ${!isActive ? "opacity-40" : "opacity-80"}`} />
              <span className={`truncate ${!isActive ? "opacity-50" : ""}`}>{node.name}</span>
              {expiredBadge}
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
            className={`${rowCls} ${nodeExpired ? "opacity-60" : ""}`}
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
            {expiredBadge}
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

  const editNodeName =
    mode.kind === "edit" ? (findNodeName(treeData, mode.nodeId) ?? "Category") : "";

  type CategoryDtoShape = {
    code?: string;
    active?: boolean;
    publishFrom?: string | null;
    publishUntil?: string | null;
    imageMenuUrl?: string;
    imagePageUrl?: string;
    imageThumbUrl?: string;
    showInNav?: boolean;
    showInBrands?: boolean;
    customNavUrl?: string;
    navSortPriority?: number;
    breakNavColumn?: boolean;
    defaultSort?: string;
    noRecommendations?: boolean;
    metaTitleJson?: string;
    metaKeywordsJson?: string;
    metaDescJson?: string;
    restrictAccess?: boolean;
    accessApp?: string;
    accessMemberType?: string;
    accessMemberTier?: string;
  };
  const editDto: CategoryDtoShape | null = useMemo(() => {
    if (mode.kind !== "edit") return null;
    const ref = findNodeRef(treeData, mode.nodeId);
    return (ref as unknown as { _dto?: CategoryDtoShape } | null)?._dto ?? null;
  }, [mode, treeData]);

  function parseLocaleJson(raw: string | undefined): Record<string, string> {
    if (!raw) return {};
    try {
      const parsed = JSON.parse(raw);
      return parsed && typeof parsed === "object" ? (parsed as Record<string, string>) : {};
    } catch { return {}; }
  }
  const metaTitleMap = useMemo(() => parseLocaleJson(editDto?.metaTitleJson), [editDto]);
  const metaKeywordsMap = useMemo(() => parseLocaleJson(editDto?.metaKeywordsJson), [editDto]);
  const metaDescMap = useMemo(() => parseLocaleJson(editDto?.metaDescJson), [editDto]);

  // Live meta states (drive Google Search Preview while typing)
  const [metaTitleLive, setMetaTitleLive] = useState<Record<string, string>>({});
  const [metaKeywordsLive, setMetaKeywordsLive] = useState<Record<string, string>>({});
  const [metaDescLive, setMetaDescLive] = useState<Record<string, string>>({});

  const editNodeCode = editDto?.code ?? "";

  function toLocalDateTime(iso: string | null | undefined): string {
    if (!iso) return "";
    // <input type="datetime-local"> expects YYYY-MM-DDTHH:mm without timezone
    const d = new Date(iso);
    if (isNaN(d.getTime())) return "";
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
  const addParentName =
    isAddMode && mode.parentId ? findNodeName(treeData, mode.parentId) : null;

  const rightPanelTitle = isAddMode
    ? mode.parentId === null
      ? "New Top-Level Category"
      : "New Sub-category"
    : editNodeName;

  const formKey =
    mode.kind === "edit"
      ? mode.nodeId
      : `add-${mode.parentId ?? "root"}`;

  useEffect(() => {
    setImgMenuSrc(isAddMode ? "" : (editDto?.imageMenuUrl ?? ""));
    setImgThumbSrc(isAddMode ? "" : (editDto?.imageThumbUrl ?? ""));
    setImgCategoryPageSrc(isAddMode ? "" : (editDto?.imagePageUrl ?? DEFAULT_EDIT_CATEGORY_PAGE_IMG));
    setRestrictAccess(isAddMode ? false : (editDto?.restrictAccess ?? false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [formKey, isAddMode]);

  // When switching category / mode, seed live meta state from DTO so preview starts correct.
  useEffect(() => {
    setMetaTitleLive({
      en: isAddMode ? "" : (metaTitleMap.en ?? ""),
      vn: isAddMode ? "" : (metaTitleMap.vn ?? metaTitleMap.vi ?? ""),
      zh: isAddMode ? "" : (metaTitleMap.zh ?? ""),
      ja: isAddMode ? "" : (metaTitleMap.ja ?? ""),
    });
    setMetaKeywordsLive({
      en: isAddMode ? "" : (metaKeywordsMap.en ?? ""),
      vn: isAddMode ? "" : (metaKeywordsMap.vn ?? metaKeywordsMap.vi ?? ""),
      zh: isAddMode ? "" : (metaKeywordsMap.zh ?? ""),
      ja: isAddMode ? "" : (metaKeywordsMap.ja ?? ""),
    });
    setMetaDescLive({
      en: isAddMode ? "" : (metaDescMap.en ?? ""),
      vn: isAddMode ? "" : (metaDescMap.vn ?? metaDescMap.vi ?? ""),
      zh: isAddMode ? "" : (metaDescMap.zh ?? ""),
      ja: isAddMode ? "" : (metaDescMap.ja ?? ""),
    });
  }, [formKey, isAddMode, metaTitleMap, metaKeywordsMap, metaDescMap]);

  useEffect(() => {
    if (!sortModalOpen) return;
    const kids = getOrderingContext(treeData, sortParentId);
    setSortDraftIds(kids.map((c) => c.id));
  }, [sortModalOpen, sortParentId, treeData]);

  const sortParentOptions = useMemo(() => {
    const withKids = collectParentsWithChildren(treeData);
    return [{ id: null as string | null, name: "Top level (roots)" }, ...withKids];
  }, [treeData]);

  async function handleDeleteConfirm() {
    if (!deleteTarget) return;
    const removedId = deleteTarget.id;
    const numericId = parseInt(removedId, 10);

    if (tenantId && !isNaN(numericId)) {
      try {
        await deleteCategory(tenantId, numericId, authToken);
      } catch (err) {
        showToast(`Delete failed: ${err instanceof Error ? err.message : "Unknown error"}`);
        setDeleteTarget(null);
        return;
      }
    }

    const { tree } = removeNode(treeData, removedId);
    setTreeData(tree);
    setDeleteTarget(null);
    showToast("Category deleted.");
    if (mode.kind === "edit" && mode.nodeId === removedId) {
      const nextId = firstNodeIdInTree(tree);
      if (nextId) setMode({ kind: "edit", nodeId: nextId });
      else setMode({ kind: "add", parentId: null });
    }
  }

  function moveSortDraft(index: number, dir: -1 | 1) {
    setSortDraftIds((ids) => {
      const j = index + dir;
      if (j < 0 || j >= ids.length) return ids;
      const copy = [...ids];
      const t = copy[index];
      copy[index] = copy[j];
      copy[j] = t;
      return copy;
    });
  }

  async function applySortOrder() {
    const kids = getOrderingContext(treeData, sortParentId);
    const map = new Map(kids.map((c) => [c.id, c]));
    const ordered = sortDraftIds
      .map((id, i) => {
        const node = map.get(id);
        if (!node) return null;
        return { ...node, sortOrder: i + 1 };
      })
      .filter(Boolean) as CatNode[];

    if (tenantId) {
      try {
        const parentNumericId = sortParentId ? parseInt(sortParentId, 10) : null;
        const items = ordered.map((n) => ({ id: parseInt(n.id, 10), sortOrder: n.sortOrder ?? 0 }));
        await sortCategories(tenantId, parentNumericId, items, authToken);
      } catch (err) {
        showToast(`Sort failed: ${err instanceof Error ? err.message : "Unknown error"}`);
        setSortModalOpen(false);
        return;
      }
    }

    setTreeData(applySiblingOrder(treeData, sortParentId, ordered));
    setSortModalOpen(false);
    showToast("Category order updated.");
  }

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
      className="-m-6 flex h-[calc(100dvh-6rem)] flex-col bg-surface-container-low overflow-hidden"
      aria-label="Category management"
    >
      {/* API loading overlay */}
      {apiLoading && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-surface/60 backdrop-blur-sm">
          <div className="flex flex-col items-center gap-3">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
            <p className="text-xs font-medium text-on-surface-variant">Loading categories…</p>
          </div>
        </div>
      )}
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
            onClick={async () => {
              try {
                await exportCategoriesToXlsx(treeData);
                showToast("Category export downloaded (.xlsx).");
              } catch {
                showToast("Export failed. Try again.");
              }
            }}
          >
            <IconDownload className="h-4 w-4 shrink-0" />
            Export Categories
          </button>
        </div>
      </header>

      <div className="flex min-h-0 flex-1 gap-6 p-6 overflow-hidden">
        {/* ── Left: hierarchy ─────────────────────────────────────────────── */}
        <section className="flex w-72 shrink-0 flex-col overflow-hidden rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm" style={{ minHeight: 0 }}>
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
              onClick={() => {
                let defaultParent: string | null = null;
                if (mode.kind === "edit") {
                  const n = findNodeRef(treeData, mode.nodeId);
                  if (n?.children?.length) defaultParent = mode.nodeId;
                }
                setSortParentId(defaultParent);
                setSortModalOpen(true);
              }}
            >
              <IconSort className="h-3.5 w-3.5 shrink-0" />
              Sort Categories
            </button>

          </div>
          <div className="custom-scrollbar flex-1 space-y-1 overflow-y-auto p-4">
            {renderTree(treeData)}
          </div>

          {/* Hide Expired toggle — pinned at bottom of tree panel */}
          <div className="shrink-0 border-t border-outline-variant/10 px-4 py-2">
            <label className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-[11px] font-medium text-on-surface-variant transition-colors hover:bg-surface-container-high select-none">
              <input
                type="checkbox"
                checked={hideExpired}
                onChange={(e) => setHideExpired(e.target.checked)}
                className="h-3.5 w-3.5 cursor-pointer accent-primary"
                aria-label="Hide expired categories"
              />
              <span>Hide Expired</span>
              {expiredCount > 0 && (
                <span
                  className={`ml-auto rounded-full px-1.5 py-px text-[9px] font-bold tabular-nums ${
                    hideExpired
                      ? "bg-outline-variant/30 text-on-surface-variant"
                      : "bg-error/10 text-error"
                  }`}
                >
                  {expiredCount}
                </span>
              )}
            </label>
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
                        defaultChecked={isAddMode ? true : (editDto?.active ?? true)}
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
                        tree={treeData}
                        excludeId={mode.kind === "edit" ? mode.nodeId : undefined}
                      />
                      {isReclassifying ? (
                        <div className="flex items-start gap-2 rounded-md border border-amber-300/60 bg-amber-50/80 px-3 py-2 dark:border-amber-500/30 dark:bg-amber-900/20">
                          <IconInfo className="mt-0.5 h-3.5 w-3.5 shrink-0 text-amber-600" />
                          <p className="text-[10px] leading-relaxed text-amber-800 dark:text-amber-300">
                            <span className="font-bold">Reclassify:</span>{" "}
                            <span className="font-medium">{reclassifyFromLabel}</span>
                            {" → "}
                            <span className="font-medium">{reclassifyToLabel}</span>
                            {". Save to apply."}
                          </p>
                        </div>
                      ) : (
                        <p className="text-[10px] text-on-surface-variant">
                          {formParentId === null
                            ? "This category will be a top-level root."
                            : "This category will appear under the selected parent."}
                        </p>
                      )}
                    </div>
                    <div className="space-y-2">
                      <label className={labelBase}>Internal Identifier</label>
                      <input
                        className={`${inputBase} ${isAddMode ? "bg-surface-container text-on-surface-variant" : ""}`}
                        defaultValue={isAddMode ? "" : editNodeCode}
                        placeholder={isAddMode ? "Auto-generated on save" : ""}
                        readOnly={isAddMode}
                      />
                    </div>
                  </div>

                  <CategoryNameField
                    isAddMode={isAddMode}
                    defaultEnValue={isAddMode ? "" : editNodeName}
                  />
                </div>
              )}

              {/* ── Images ── */}
              {tab === "images" && (
                <div className="space-y-6">
                  <h3 className={sectionHeading}>Visual Assets</h3>
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
                    <CategoryImageSlot
                      label="Menu Image"
                      hint="Displayed in the eStore navigation menu."
                      aspectClass="aspect-video"
                      value={imgMenuSrc}
                      onChange={setImgMenuSrc}
                      emptyHint="1200 × 400px recommended"
                    />
                    <CategoryImageSlot
                      label="Category Page Image"
                      hint="Displayed on the category product listing page."
                      aspectClass="aspect-video"
                      value={imgCategoryPageSrc}
                      onChange={setImgCategoryPageSrc}
                      emptyHint="Wide banner recommended"
                    />
                    <CategoryImageSlot
                      label="Thumbnail Image"
                      hint="Small image to represent this category."
                      aspectClass="aspect-square max-w-[200px]"
                      value={imgThumbSrc}
                      onChange={setImgThumbSrc}
                      emptyHint="200 × 200px"
                    />
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
                          defaultChecked={isAddMode ? true : (editDto?.showInNav ?? true)}
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
                          defaultChecked={isAddMode ? false : (editDto?.showInBrands ?? false)}
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
                          defaultValue={isAddMode ? "" : toLocalDateTime(editDto?.publishFrom)}
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
                          defaultValue={isAddMode ? "" : toLocalDateTime(editDto?.publishUntil)}
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
                          defaultValue={isAddMode ? 10 : (editDto?.navSortPriority ?? 10)}
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
                          defaultValue={isAddMode ? "" : (editDto?.customNavUrl ?? "")}
                        />
                        <p className="text-[10px] text-on-surface-variant">
                          Override the default URL for this category in the navigation menu.
                        </p>
                      </div>
                    </div>
                    <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/15 bg-surface p-4 transition-colors hover:bg-surface-container-low">
                      <input
                        type="checkbox"
                        defaultChecked={isAddMode ? false : (editDto?.breakNavColumn ?? false)}
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
                        defaultChecked={isAddMode ? false : (editDto?.noRecommendations ?? false)}
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
                        <p className="text-[11px] text-on-surface-variant">
                          {featuredProducts.length === 0
                            ? "No products selected"
                            : `${featuredProducts.length} product${featuredProducts.length !== 1 ? "s" : ""} selected`}
                        </p>
                      </div>
                      <button
                        type="button"
                        className="flex items-center gap-2 rounded-md border border-primary/30 bg-primary/5 px-4 py-2 text-xs font-semibold text-primary transition-colors hover:bg-primary/10"
                        onClick={() => {
                          setFeaturedProductsDraft(featuredProducts);
                          setPromoteListingOpen(true);
                        }}
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
                      <select className={inputBase} defaultValue={isAddMode ? "bestseller" : (editDto?.defaultSort || "bestseller")}>
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
                    <MultiLangLexicalRichText
                      label=""
                      defaultValues={{ en: isAddMode ? "" : "" }}
                      placeholders={{
                        en: "Enter category overview and description...",
                        vn: "Nhập mô tả danh mục...",
                        zh: "输入类别概述和描述...",
                        ja: "カテゴリの概要と説明を入力してください...",
                      }}
                    />
                  </div>
                </div>
              )}

              {/* ── SEO ── */}
              {tab === "seo" && (
                <div className="space-y-8 pb-6">
                  {/* SEO Metadata */}
                  <div className="space-y-5">
                    <h3 className={sectionHeading}>SEO &amp; Metadata</h3>
                    <MultiLangInput
                      label="Meta Title"
                      defaultValues={{
                        en: isAddMode ? "" : (metaTitleMap.en ?? ""),
                        vn: isAddMode ? "" : (metaTitleMap.vn ?? metaTitleMap.vi ?? ""),
                        zh: isAddMode ? "" : (metaTitleMap.zh ?? ""),
                        ja: isAddMode ? "" : (metaTitleMap.ja ?? ""),
                      }}
                      onValuesChange={setMetaTitleLive}
                      placeholders={{
                        en: isAddMode ? "e.g. Shop Electronics Online | Best Deals" : "",
                        vn: isAddMode ? "VD: Mua sắm điện tử | Ưu đãi tốt nhất" : "",
                        zh: "例：网上购物电子产品 | 最优惠",
                        ja: "例：エレクトロニクスをオンラインで購入 | 最高のお得",
                      }}
                      hint="Text that appears on the browser's title bar and tab."
                    />
                    <MultiLangInput
                      label="Meta Keywords"
                      defaultValues={{
                        en: isAddMode ? "" : (metaKeywordsMap.en ?? ""),
                        vn: isAddMode ? "" : (metaKeywordsMap.vn ?? metaKeywordsMap.vi ?? ""),
                        zh: isAddMode ? "" : (metaKeywordsMap.zh ?? ""),
                        ja: isAddMode ? "" : (metaKeywordsMap.ja ?? ""),
                      }}
                      onValuesChange={setMetaKeywordsLive}
                      placeholders={{
                        en: "Comma-separated keywords, e.g. electronics, gadgets, tech",
                        vn: "Từ khóa cách nhau bằng dấu phẩy",
                        zh: "以逗号分隔的关键词",
                        ja: "カンマ区切りのキーワード",
                      }}
                      hint="Relevant keywords to help search engines improve this category's visibility."
                    />
                    <MultiLangTextarea
                      label="Meta Description"
                      rows={3}
                      defaultValues={{
                        en: isAddMode ? "" : (metaDescMap.en ?? ""),
                        vn: isAddMode ? "" : (metaDescMap.vn ?? metaDescMap.vi ?? ""),
                        zh: isAddMode ? "" : (metaDescMap.zh ?? ""),
                        ja: isAddMode ? "" : (metaDescMap.ja ?? ""),
                      }}
                      onValuesChange={setMetaDescLive}
                      placeholders={{
                        en: isAddMode ? "Describe this category for search engines..." : "",
                        vn: isAddMode ? "Mô tả ngắn về danh mục này cho công cụ tìm kiếm..." : "",
                        zh: "为搜索引擎描述此类别...",
                        ja: "検索エンジン向けにこのカテゴリを説明してください...",
                      }}
                      hint="Brief overview displayed in search result listings."
                    />
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
                          : ((metaTitleLive.en ?? "").trim() || `${editNodeName} | Your Store`)}
                      </p>
                      <p className="truncate text-xs text-green-700">
                        {isAddMode
                          ? "https://example.com/category/new-category"
                          : `https://example.com/category/${(findNodeRef(treeData, mode.kind === "edit" ? mode.nodeId : "")?.name ?? editNodeName).toLowerCase().replace(/\s+/g, "-")}`}
                      </p>
                      <p className="text-xs leading-tight text-on-surface-variant">
                        {isAddMode
                          ? "Your meta description will appear here once filled in above."
                          : ((metaDescLive.en ?? "").trim() || "Your meta description will appear here once filled in above.")}
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
                          <select className={inputBase} defaultValue={isAddMode ? "" : (editDto?.accessApp ?? "")}>
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
                          <select className={inputBase} defaultValue={isAddMode ? "" : (editDto?.accessMemberType ?? "")}>
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
                          <select className={inputBase} defaultValue={isAddMode ? "" : (editDto?.accessMemberTier ?? "")}>
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
                    // Reset parent picker to original (discard reclassification)
                    setFormParentId(originalParentId);
                  }
                }}
              >
                {isAddMode ? "Cancel" : "Discard Changes"}
              </button>
              <button
                type="button"
                className={isReclassifying
                  ? "px-6 py-2 bg-amber-600 text-white rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-amber-600/20 hover:scale-[1.02] active:scale-95 transition-all flex items-center gap-2"
                  : btnFooterPrimary}
                onClick={handleSaveCategory}
              >
                <IconSave className="h-4 w-4" />
                {isAddMode ? "Create Category" : isReclassifying ? "Save & Reclassify" : "Save Category"}
              </button>
            </div>
          </footer>
        </section>
      </div>

      {/* ── Delete category confirmation ── */}
      {deleteTarget && (
        <div className="fixed inset-0 z-[55] flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete category</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Delete &ldquo;{deleteTarget.name}&rdquo; and its sub-tree from the hierarchy? This demo only updates
                  local state (no server).
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteTarget(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={handleDeleteConfirm}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Sort categories (same-level reorder) ── */}
      {sortModalOpen && (
        <div
          className="fixed inset-0 z-[55] flex items-center justify-center bg-black/40 backdrop-blur-sm p-4"
          onClick={() => setSortModalOpen(false)}
        >
          <div
            className="max-h-[min(90vh,560px)] w-full max-w-md overflow-hidden rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="border-b border-outline-variant/15 px-5 py-4">
              <h3 className="text-sm font-bold text-on-surface">Sort categories</h3>
              <p className="mt-1 text-[11px] text-on-surface-variant">
                Reorder siblings with the arrows. Applies sort order 1…n within the selected parent.
              </p>
              <label className="mt-3 block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
                Parent level
              </label>
              <select
                className={`${inputBase} mt-1`}
                value={sortParentId ?? ""}
                onChange={(e) => setSortParentId(e.target.value === "" ? null : e.target.value)}
              >
                {sortParentOptions.map((o) => (
                  <option key={o.id === null ? "root" : o.id} value={o.id ?? ""}>
                    {o.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto p-3">
              {sortDraftIds.length === 0 ? (
                <p className="px-2 py-6 text-center text-xs text-on-surface-variant italic">
                  No categories at this level.
                </p>
              ) : (
                <ul className="space-y-1">
                  {sortDraftIds.map((id, index) => (
                    <li
                      key={id}
                      className="flex items-center gap-2 rounded-lg border border-outline-variant/15 bg-surface px-2 py-2"
                    >
                      <span className="min-w-0 flex-1 truncate text-xs font-medium text-on-surface">
                        {findNodeName(treeData, id) ?? id}
                      </span>
                      <button
                        type="button"
                        className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                        disabled={index === 0}
                        aria-label="Move up"
                        onClick={() => moveSortDraft(index, -1)}
                      >
                        <IconChevronUp className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-30"
                        disabled={index === sortDraftIds.length - 1}
                        aria-label="Move down"
                        onClick={() => moveSortDraft(index, 1)}
                      >
                        <IconChevronDown className="h-4 w-4" />
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className="flex justify-end gap-2 border-t border-outline-variant/10 px-4 py-3">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-4 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setSortModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-4 py-2 text-xs font-bold text-on-primary hover:bg-primary-container"
                onClick={applySortOrder}
                disabled={sortDraftIds.length === 0}
              >
                Apply order
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Promote products: product picker (stub) ── */}
      {promoteListingOpen && (
        <div
          className="fixed inset-0 z-[55] flex items-center justify-center bg-black/40 backdrop-blur-sm p-4"
          onClick={() => setPromoteListingOpen(false)}
        >
          <div
            className="max-h-[min(90vh,520px)] w-full max-w-lg overflow-y-auto rounded-2xl border border-outline-variant/20 bg-surface-container-lowest p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <h3 className="text-sm font-bold text-on-surface">Featured products</h3>
                <p className="mt-1 text-xs leading-relaxed text-on-surface-variant">
                  Same product picker pattern as Campaign editor — mock catalog until listing API is available.
                </p>
              </div>
              <button
                type="button"
                aria-label="Close"
                className="shrink-0 rounded-full p-1.5 text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setPromoteListingOpen(false)}
              >
                <IconClose className="h-4 w-4" />
              </button>
            </div>
            <div className="mt-4">
              <FeaturedProductSearchablePicker
                label="Products"
                options={MOCK_FEATURED_PRODUCTS}
                selected={featuredProductsDraft}
                onChange={setFeaturedProductsDraft}
                placeholder="Search products…"
              />
            </div>
            <div className="mt-6 flex justify-end gap-2 border-t border-outline-variant/10 pt-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-4 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setPromoteListingOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-4 py-2 text-xs font-bold text-on-primary hover:bg-primary-container"
                onClick={() => {
                  setFeaturedProducts(featuredProductsDraft);
                  setPromoteListingOpen(false);
                  showToast(
                    featuredProductsDraft.length === 0
                      ? "Featured products cleared."
                      : `${featuredProductsDraft.length} featured product${featuredProductsDraft.length !== 1 ? "s" : ""} saved (local).`,
                  );
                }}
              >
                Apply
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Toast notification ── */}
      <div
        aria-live="polite"
        className={`fixed bottom-6 left-1/2 z-[60] -translate-x-1/2 transition-all duration-300 ${
          toast.visible ? "translate-y-0 opacity-100" : "translate-y-4 opacity-0 pointer-events-none"
        }`}
      >
        <div className="flex items-center gap-3 rounded-xl bg-on-surface px-5 py-3 shadow-2xl">
          <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
          <span className="text-sm font-medium text-surface">{toast.message}</span>
          <button
            type="button"
            aria-label="Dismiss"
            className="ml-2 rounded p-0.5 text-surface/60 hover:text-surface transition-colors"
            onClick={() => setToast((p) => ({ ...p, visible: false }))}
          >
            ✕
          </button>
        </div>
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
                    : ` It was added under "${findNodeName(treeData, formParentId) ?? "the selected parent"}".`}
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
