import { useEffect, useMemo, useRef, useState } from "react";
import {
  IconAddCircle,
  IconBox,
  IconChevronDown,
  IconChevronRight,
  IconClose,
  IconCloudUpload,
  IconDownload,
  IconEdit,
  IconFilterList,
  IconFirstPage,
  IconImage,
  IconLastPage,
  IconChevronLeft,
  IconLayers,
  IconOpenInNew,
  IconSearch,
  IconVisibility,
} from "../../orders/icons";
import {
  downloadInventoryImportTemplateXlsx,
  downloadProductImportTemplateXlsx,
} from "../exportProductImportTemplates";
import { bulkOperateProducts, fetchAllProductsForExport, fetchProducts, type BulkProductOp, type ProductFilters } from "../api/productsApi";
import { useDebouncedValue } from "../hooks/useDebouncedValue";
import * as XLSX from "xlsx";

const labelFilter = "text-[10px] font-bold text-on-surface-variant uppercase tracking-wider";
const inputFilter =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:ring-1 focus:ring-primary outline-none";

export type ProductListRow = {
  id: string;
  name: string;
  categoryPath: string;
  brand: string;
  upc: string;
  sku: string;
  price: string;
  qty: number;
  eStoreLabel: string;
  eStoreVariant: "live" | "low-stock" | "offline";
  statusLabel: string;
  statusVariant: "active" | "out-of-stock";
  modified: string;
  imageSrc: string;
  imageAlt: string;
};

function eStoreBadgeClasses(variant: ProductListRow["eStoreVariant"]) {
  switch (variant) {
    case "live":
      return "bg-secondary-container/20 text-on-secondary-container";
    case "low-stock":
      return "bg-error-container/40 text-on-error-container";
    case "offline":
      return "bg-on-surface-variant/10 text-on-surface-variant opacity-50";
    default:
      return "bg-outline-variant/20 text-on-surface-variant";
  }
}

function statusBadgeClasses(variant: ProductListRow["statusVariant"]) {
  switch (variant) {
    case "active":
      return "bg-tertiary-container/20 text-on-tertiary-fixed-variant";
    case "out-of-stock":
      return "bg-error-container text-on-error-container";
    default:
      return "bg-outline-variant/20 text-on-surface-variant";
  }
}

function getCompactPages(totalPages: number, current: number): Array<number | "…"> {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1);
  const clamp = (n: number) => Math.min(totalPages, Math.max(1, n));
  const cur = clamp(current);
  const out: Array<number | "…"> = [1];

  const windowStart = Math.max(2, cur - 1);
  const windowEnd = Math.min(totalPages - 1, cur + 1);

  if (windowStart > 2) out.push("…");
  for (let p = windowStart; p <= windowEnd; p++) out.push(p);
  if (windowEnd < totalPages - 1) out.push("…");
  out.push(totalPages);
  return out;
}

type ProductsPageProps = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
  onAddProduct?: () => void;
  onEditProduct?: (row: ProductListRow) => void;
  onImportProduct?: () => void;
  onImageImport?: () => void;
  onInventoryImport?: () => void;
  onAdvancePriceImport?: () => void;
  onCategoryAssignment?: () => void;
};

const FILTER_CATEGORIES = [
  "Timepieces", "Audio", "Footwear", "Photography",
  "Electronics", "Apparel", "Home & Living",
];

const QUICK_ACCESS_OPTIONS: { key: string; label: string; hint: string }[] = [
  { key: "zero-qty",       label: "0 Quantity Product(s)",  hint: "Total Quantity = 0"        },
  { key: "restock",        label: "Re-stock needed",         hint: "Out of stock product(s)"   },
  { key: "sell-on-estore", label: "Sell on eStore",          hint: ""                          },
  { key: "estore-only",    label: "eStore Only",             hint: ""                          },
];

export function ProductsPage({
  tenantId,
  storeId,
  authToken,
  onAddProduct,
  onEditProduct,
  onImportProduct,
  onImageImport,
  onInventoryImport,
  onAdvancePriceImport,
  onCategoryAssignment,
}: ProductsPageProps) {
  // ── Dropdown states ──────────────────────────────────────────
  const [importOpen, setImportOpen] = useState(false);
  const [exportOpen, setExportOpen] = useState(false);
  const [groupOpen, setGroupOpen] = useState(false);

  const importRef = useRef<HTMLDivElement>(null);
  const exportRef = useRef<HTMLDivElement>(null);
  const groupRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handler(e: MouseEvent) {
      const path = e.composedPath();
      if (importRef.current && !path.includes(importRef.current)) setImportOpen(false);
      if (exportRef.current && !path.includes(exportRef.current)) setExportOpen(false);
      if (groupRef.current && !path.includes(groupRef.current)) setGroupOpen(false);
    }
    document.addEventListener("click", handler, true);
    return () => document.removeEventListener("click", handler, true);
  }, []);

  // Export modal state
  const [exportType, setExportType] = useState<"products" | "inventory" | "preview" | "advance-price" | null>(null);
  const [exportFormat, setExportFormat] = useState<"csv" | "excel">("csv");
  const [exportStripHtml, setExportStripHtml] = useState(false);

  const [rows, setRows] = useState<ProductListRow[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [bulkLoading, setBulkLoading] = useState(false);
  const [toast, setToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [confirmArchiveOpen, setConfirmArchiveOpen] = useState(false);
  const [exportLoading, setExportLoading] = useState(false);

  // Filter state (controlled inputs)
  const [upc, setUpc] = useState("");
  const [sku, setSku] = useState("");
  const [name, setName] = useState("");
  const [priceMin, setPriceMin] = useState<number | "">("");
  const [priceMax, setPriceMax] = useState<number | "">("");
  const [qtyMin, setQtyMin] = useState<number | "">("");
  const [qtyMax, setQtyMax] = useState<number | "">("");
  const [brand, setBrand] = useState("all");
  const [category, setCategory] = useState("all");
  const [estoreStatus, setEstoreStatus] = useState<"all" | "live" | "draft" | "inactive">("all");

  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  // Manual search trigger (Search button)
  const [searchNonce, setSearchNonce] = useState(0);

  const debUpc = useDebouncedValue(upc, 300);
  const debSku = useDebouncedValue(sku, 300);
  const debName = useDebouncedValue(name, 300);
  const debPriceMin = useDebouncedValue(priceMin, 300);
  const debPriceMax = useDebouncedValue(priceMax, 300);
  const debQtyMin = useDebouncedValue(qtyMin, 300);
  const debQtyMax = useDebouncedValue(qtyMax, 300);
  const debBrand = useDebouncedValue(brand, 300);
  const debCategory = useDebouncedValue(category, 300);
  const debStatus = useDebouncedValue(estoreStatus, 300);

  function stripHtml(raw: string): string {
    if (!raw) return "";
    // quick + safe for export (not for rendering)
    return raw.replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim();
  }

  function csvEscape(v: unknown): string {
    const s = String(v ?? "");
    return `"${s.replace(/"/g, '""')}"`;
  }

  function downloadBlob(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  // ── doExport ──────────────────────────────────────────────────────────────
  async function doExport() {
    if (!tenantId || !storeId) {
      setToast({ kind: "error", message: "Missing tenantId / storeId for export." });
      return;
    }
    const bom = "\uFEFF";
    setExportLoading(true);
    try {
      const { rows: exportRows, total: exportTotal, limited } = await fetchAllProductsForExport(
        tenantId,
        storeId,
        filters,
        authToken,
        { limit: 5000, pageSize: 200 }
      );

      if (limited) {
        setToast({ kind: "error", message: `Export limited to first 5000 rows (total ${exportTotal}). Please refine filters.` });
      }

      if (!exportType) return;

      const clean = (s: string) => (exportStripHtml ? stripHtml(s) : s);

      if (exportFormat === "csv") {
        let filename = "";
        let csv = "";

        if (exportType === "products") {
          const headers = ["UPC", "SKU", "Product Name", "Category", "Brand", "Retail Price", "Qty", "eStore Status", "Status", "Last Modified"];
          const lines = exportRows.map((r) =>
            [
              clean(r.upc),
              clean(r.sku),
              clean(r.name),
              clean(r.categoryPath),
              clean(r.brand),
              clean(r.price),
              r.qty,
              clean(r.eStoreLabel),
              clean(r.statusLabel),
              clean(r.modified),
            ]
              .map(csvEscape)
              .join(",")
          );
          csv = bom + [headers.join(","), ...lines].join("\n");
          filename = "products-export.csv";
        } else if (exportType === "inventory") {
          const headers = ["UPC", "Product Name", "Store ID", "Store Name", "Store Quantity"];
          const lines = exportRows.map((r) =>
            [clean(r.upc), clean(r.name), "STR-001", "Main Store", r.qty].map(csvEscape).join(",")
          );
          csv = bom + [headers.join(","), ...lines].join("\n");
          filename = "inventory-qty-export.csv";
        } else if (exportType === "preview") {
          const headers = ["UPC", "SKU", "Product Name", "Preview URL"];
          const lines = exportRows.map((r) =>
            [clean(r.upc), clean(r.sku), clean(r.name), `https://preview.dcms.local/products/${r.id}`].map(csvEscape).join(",")
          );
          csv = bom + [headers.join(","), ...lines].join("\n");
          filename = "preview-links-export.csv";
        } else if (exportType === "advance-price") {
          const headers = ["UPC", "SKU", "Product Name", "Advance Price", "Start Date", "End Date"];
          const lines = exportRows.map((r) =>
            [clean(r.upc), clean(r.sku), clean(r.name), "0.00", "", ""].map(csvEscape).join(",")
          );
          csv = bom + [headers.join(","), ...lines].join("\n");
          filename = "advance-price-export.csv";
        }

        if (!csv) return;
        downloadBlob(new Blob([csv], { type: "text/csv;charset=utf-8;" }), filename);
        setToast({ kind: "success", message: `Exported ${exportRows.length} row(s)` });
      } else {
        // XLSX
        let filename = "";
        let sheetName = "";
        let aoa: unknown[][] = [];

        if (exportType === "products") {
          filename = "products-export.xlsx";
          sheetName = "Products";
          aoa = [
            ["UPC", "SKU", "Product Name", "Category", "Brand", "Retail Price", "Qty", "eStore Status", "Status", "Last Modified"],
            ...exportRows.map((r) => [
              clean(r.upc),
              clean(r.sku),
              clean(r.name),
              clean(r.categoryPath),
              clean(r.brand),
              clean(r.price),
              r.qty,
              clean(r.eStoreLabel),
              clean(r.statusLabel),
              clean(r.modified),
            ]),
          ];
        } else if (exportType === "inventory") {
          filename = "inventory-qty-export.xlsx";
          sheetName = "Inventory";
          aoa = [["UPC", "Product Name", "Store ID", "Store Name", "Store Quantity"], ...exportRows.map((r) => [clean(r.upc), clean(r.name), "STR-001", "Main Store", r.qty])];
        } else if (exportType === "preview") {
          filename = "preview-links-export.xlsx";
          sheetName = "Preview";
          aoa = [["UPC", "SKU", "Product Name", "Preview URL"], ...exportRows.map((r) => [clean(r.upc), clean(r.sku), clean(r.name), `https://preview.dcms.local/products/${r.id}`])];
        } else if (exportType === "advance-price") {
          filename = "advance-price-export.xlsx";
          sheetName = "Advance Price";
          aoa = [
            ["UPC", "SKU", "Product Name", "Advance Price", "Start Date", "End Date"],
            ...exportRows.map((r) => [clean(r.upc), clean(r.sku), clean(r.name), "0.00", "", ""]),
          ];
        }

        const ws = XLSX.utils.aoa_to_sheet(aoa);
        const wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, sheetName || "Sheet1");
        const out = XLSX.write(wb, { type: "array", bookType: "xlsx" });
        downloadBlob(new Blob([out], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }), filename);
        setToast({ kind: "success", message: `Exported ${exportRows.length} row(s)` });
      }
    } catch (e: unknown) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Export failed" });
    } finally {
      setExportLoading(false);
      setExportType(null);
    }
  }

  // ── More Filters state ────────────────────────────────────────────────────
  const [moreFiltersOpen, setMoreFiltersOpen] = useState(false);
  // Working state (inside modal, not yet applied)
  const [draftCategories, setDraftCategories] = useState<string[]>([]);
  const [draftQuickAccess, setDraftQuickAccess] = useState<Set<string>>(new Set());
  // Applied state (what the table actually filters on)
  const [activeCategories, setActiveCategories] = useState<string[]>([]);
  const [activeQuickAccess, setActiveQuickAccess] = useState<Set<string>>(new Set());
  const [catSearch, setCatSearch] = useState("");

  const activeCount = activeCategories.length + activeQuickAccess.size;

  function openMoreFilters() {
    // Sync draft from currently applied filters
    setDraftCategories([...activeCategories]);
    setDraftQuickAccess(new Set(activeQuickAccess));
    setCatSearch("");
    setMoreFiltersOpen(true);
  }

  function applyMoreFilters() {
    setActiveCategories([...draftCategories]);
    setActiveQuickAccess(new Set(draftQuickAccess));
    setMoreFiltersOpen(false);
  }

  function resetDraft() {
    setDraftCategories([]);
    setDraftQuickAccess(new Set());
    setCatSearch("");
  }

  function toggleDraftCategory(cat: string) {
    setDraftCategories((prev) =>
      prev.includes(cat) ? prev.filter((c) => c !== cat) : [...prev, cat]
    );
  }

  function toggleDraftQuickAccess(key: string) {
    setDraftQuickAccess((prev) => {
      const next = new Set(prev);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
    });
  }

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const safePage = Math.min(page, totalPages);

  const filters: ProductFilters = useMemo(() => {
    const quick = Array.from(activeQuickAccess) as Exclude<ProductFilters["quickAccess"], undefined>;
    return {
      upc: debUpc.trim() || undefined,
      sku: debSku.trim() || undefined,
      name: debName.trim() || undefined,
      priceMin: typeof debPriceMin === "number" ? debPriceMin : undefined,
      priceMax: typeof debPriceMax === "number" ? debPriceMax : undefined,
      qtyMin: typeof debQtyMin === "number" ? debQtyMin : undefined,
      qtyMax: typeof debQtyMax === "number" ? debQtyMax : undefined,
      brand: debBrand !== "all" ? debBrand : undefined,
      category: debCategory !== "all" ? debCategory : undefined,
      estoreStatus: debStatus !== "all" ? debStatus : undefined,
      quickAccess: quick.length ? quick : undefined,
    };
  }, [
    activeQuickAccess,
    debBrand,
    debCategory,
    debName,
    debPriceMax,
    debPriceMin,
    debQtyMax,
    debQtyMin,
    debSku,
    debStatus,
    debUpc,
  ]);

  useEffect(() => {
    if (!tenantId || !storeId) {
      setRows([]);
      setTotal(0);
      setError("Missing tenantId / storeId for Products API.");
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    fetchProducts(tenantId, storeId, filters, { page: safePage, pageSize }, authToken)
      .then(({ rows, total }) => {
        if (cancelled) return;
        setRows(rows);
        setTotal(total);
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        setRows([]);
        setTotal(0);
        setError(e instanceof Error ? e.message : "Failed to load products");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken, filters, safePage, pageSize, searchNonce]);

  const [selectedIds, setSelectedIds] = useState<Set<string>>(() => new Set());

  const allSelected = rows.length > 0 && rows.every((r) => selectedIds.has(r.id));
  const someSelected = rows.some((r) => selectedIds.has(r.id));

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  function toggleSelectAll(checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const r of rows) {
        if (checked) next.add(r.id);
        else next.delete(r.id);
      }
      return next;
    });
  }

  function toggleRowSelected(id: string, checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  const filteredCatOptions = FILTER_CATEGORIES.filter((c) =>
    c.toLowerCase().includes(catSearch.toLowerCase())
  );

  async function runBulk(op: BulkProductOp) {
    if (!tenantId || !storeId) return;
    if (selectedIds.size === 0) return;
    setBulkLoading(true);
    try {
      const ids = Array.from(selectedIds);
      const { succeeded, failed } = await bulkOperateProducts(tenantId, storeId, ids, op, authToken);
      if (failed > 0) {
        setToast({ kind: "error", message: `${failed} product(s) failed to update.` });
        return; // don't clear selection on partial failure
      }
      setToast({ kind: "success", message: `${succeeded} products updated` });
      setSelectedIds(new Set());
      setSearchNonce((n) => n + 1); // refetch
    } catch (e: unknown) {
      setToast({ kind: "error", message: e instanceof Error ? e.message : "Bulk update failed" });
    } finally {
      setBulkLoading(false);
    }
  }

  return (
    <div
      className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low"
      aria-label="Product manager"
    >
      <header className="relative z-20 flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <nav className="mb-2 flex items-center gap-2 text-[10px] font-medium uppercase tracking-widest text-on-surface-variant">
            <span>Estore</span>
            <IconChevronRight className="h-3 w-3 shrink-0 opacity-70" aria-hidden />
            <span className="font-bold text-primary">Products</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Product Manager</h1>
          <p className="mt-2 text-sm text-on-surface-variant">
            Search, filter, and bulk-manage catalog products across brands and categories.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {/* Import dropdown */}
          <div className="relative" ref={importRef}>
            <button
              type="button"
              className="flex items-center gap-2 rounded bg-surface-container-low px-4 py-2 text-xs font-semibold text-on-surface transition-all hover:bg-surface-container-high"
              onClick={() => { setImportOpen((o) => !o); setExportOpen(false); setGroupOpen(false); }}
            >
              <IconCloudUpload className="h-4 w-4 shrink-0" />
              Import
              <IconChevronDown className="h-3.5 w-3.5 shrink-0 text-on-surface-variant" />
            </button>
            {importOpen && (
              <div
                className="absolute right-0 top-full z-30 mt-1 w-60 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl"
                onMouseDown={(e) => e.stopPropagation()}
              >
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setImportOpen(false); onImportProduct?.(); }}
                >
                  <IconCloudUpload className="h-4 w-4 shrink-0 text-primary" />
                  Products
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setImportOpen(false); onImageImport?.(); }}
                >
                  <IconImage className="h-4 w-4 shrink-0 text-primary" />
                  Product Images
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setImportOpen(false); onInventoryImport?.(); }}
                >
                  <IconBox className="h-4 w-4 shrink-0 text-primary" />
                  Inventory Qty
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setImportOpen(false); onAdvancePriceImport?.(); }}
                >
                  <IconCloudUpload className="h-4 w-4 shrink-0 text-primary" />
                  Advance Price
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setImportOpen(false); onCategoryAssignment?.(); }}
                >
                  <IconLayers className="h-4 w-4 shrink-0 text-primary" />
                  Product Categories
                </button>
              </div>
            )}
          </div>

          {/* Export dropdown */}
          <div className="relative" ref={exportRef}>
            <button
              type="button"
              className="flex items-center gap-2 rounded bg-surface-container-low px-4 py-2 text-xs font-semibold text-on-surface transition-all hover:bg-surface-container-high"
              onClick={() => { setExportOpen((o) => !o); setImportOpen(false); setGroupOpen(false); }}
            >
              <IconDownload className="h-4 w-4 shrink-0" />
              Export
              <IconChevronDown className="h-3.5 w-3.5 shrink-0 text-on-surface-variant" />
            </button>
            {exportOpen && (
              <div
                className="absolute right-0 top-full z-30 mt-1 w-64 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl"
                onMouseDown={(e) => e.stopPropagation()}
              >
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setExportOpen(false); setExportType("products"); }}
                >
                  <IconDownload className="h-4 w-4 shrink-0 text-primary" />
                  Products
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setExportOpen(false); setExportType("inventory"); }}
                >
                  <IconBox className="h-4 w-4 shrink-0 text-primary" />
                  Inventory Qty
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setExportOpen(false); setExportType("advance-price"); }}
                >
                  <IconDownload className="h-4 w-4 shrink-0 text-primary" />
                  Advance Price
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setExportOpen(false); setExportType("preview"); }}
                >
                  <IconOpenInNew className="h-4 w-4 shrink-0 text-primary" />
                  Preview Links
                </button>

                <div className="my-1 border-t border-outline-variant/20" />
                <div className="px-4 pt-1 pb-1 text-[9px] font-bold uppercase tracking-widest text-on-surface-variant/60">Templates</div>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setExportOpen(false); void downloadProductImportTemplateXlsx(); }}
                >
                  <IconCloudUpload className="h-4 w-4 shrink-0 text-primary" />
                  Product Import Template
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setExportOpen(false); void downloadInventoryImportTemplateXlsx(); }}
                >
                  <IconBox className="h-4 w-4 shrink-0 text-primary" />
                  Inventory Import Template
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setExportOpen(false); console.info("[Products] Download Advance Price Import Template"); }}
                >
                  <IconCloudUpload className="h-4 w-4 shrink-0 text-primary" />
                  Advance Price Import Template
                </button>
              </div>
            )}
          </div>

          {/* Group Actions dropdown */}
          <div className="relative" ref={groupRef}>
            <button
              type="button"
              className="flex items-center gap-2 rounded bg-surface-container-low px-4 py-2 text-xs font-semibold text-on-surface transition-all hover:bg-surface-container-high"
              onClick={() => { setGroupOpen((o) => !o); setImportOpen(false); setExportOpen(false); }}
            >
              <IconLayers className="h-4 w-4 shrink-0" />
              Group Actions
              <IconChevronDown className="h-3.5 w-3.5 shrink-0 text-on-surface-variant" />
            </button>
            {groupOpen && (
              <div
                className="absolute right-0 top-full z-30 mt-1 w-60 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl"
                onMouseDown={(e) => e.stopPropagation()}
              >
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); console.info("[Products] Send for Approval", Array.from(selectedIds)); }}
                >
                  <IconCloudUpload className="h-4 w-4 shrink-0 text-primary" />
                  Send for Approval
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); console.info("[Products] Approve", Array.from(selectedIds)); }}
                >
                  <IconAddCircle className="h-4 w-4 shrink-0 text-primary" />
                  Approve
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); console.info("[Products] Reject", Array.from(selectedIds)); }}
                >
                  <IconClose className="h-4 w-4 shrink-0 text-primary" />
                  Reject
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); setConfirmArchiveOpen(true); }}
                >
                  <IconBox className="h-4 w-4 shrink-0 text-primary" />
                  Archive
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); console.info("[Products] Send for Archive", Array.from(selectedIds)); }}
                >
                  <IconCloudUpload className="h-4 w-4 shrink-0 text-primary" />
                  Send for Archive
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); console.info("[Products] Restore", Array.from(selectedIds)); }}
                >
                  <IconOpenInNew className="h-4 w-4 shrink-0 text-primary" />
                  Restore
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); console.info("[Products] Add Category", Array.from(selectedIds)); }}
                >
                  <IconAddCircle className="h-4 w-4 shrink-0 text-primary" />
                  Add Category
                </button>
                <button
                  type="button"
                  className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                  onClick={() => { setGroupOpen(false); console.info("[Products] Change Category", Array.from(selectedIds)); }}
                >
                  <IconEdit className="h-4 w-4 shrink-0 text-primary" />
                  Change Category
                </button>
              </div>
            )}
          </div>

          <button
            type="button"
            className="flex items-center gap-2 rounded bg-primary px-6 py-2 text-xs font-bold text-on-primary shadow-sm transition-all hover:opacity-95"
            onClick={() => onAddProduct?.()}
          >
            <IconAddCircle className="h-4 w-4 shrink-0" />
            Add Product
          </button>
        </div>
      </header>

      <div className="flex-1 space-y-8 p-6 pb-24">
      <section className="rounded-xl bg-surface-container-low p-6">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-5">
          <div className="space-y-1.5">
            <label className={labelFilter}>UPC</label>
            <input
              className={inputFilter}
              placeholder="Enter UPC"
              type="text"
              value={upc}
              onChange={(e) => {
                setUpc(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>SKU</label>
            <input
              className={inputFilter}
              placeholder="Enter SKU"
              type="text"
              value={sku}
              onChange={(e) => {
                setSku(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Product Name</label>
            <input
              className={inputFilter}
              placeholder="Search by name"
              type="text"
              value={name}
              onChange={(e) => {
                setName(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Price (Min/Max)</label>
            <div className="flex gap-2">
              <input
                className={`${inputFilter} w-1/2`}
                placeholder="Min"
                type="number"
                value={priceMin}
                onChange={(e) => {
                  setPriceMin(e.target.value === "" ? "" : Number(e.target.value));
                  setPage(1);
                }}
              />
              <input
                className={`${inputFilter} w-1/2`}
                placeholder="Max"
                type="number"
                value={priceMax}
                onChange={(e) => {
                  setPriceMax(e.target.value === "" ? "" : Number(e.target.value));
                  setPage(1);
                }}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Brand</label>
            <select
              className={`${inputFilter} appearance-none`}
              value={brand}
              onChange={(e) => {
                setBrand(e.target.value);
                setPage(1);
              }}
            >
              <option value="all">All Brands</option>
              <option value="Premium Collection">Premium Collection</option>
              <option value="Eco-Essentials">Eco-Essentials</option>
              <option value="Luxe Goods">Luxe Goods</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Category</label>
            <select
              className={`${inputFilter} appearance-none`}
              value={category}
              onChange={(e) => {
                setCategory(e.target.value);
                setPage(1);
              }}
            >
              <option value="all">All Categories</option>
              <option value="Electronics">Electronics</option>
              <option value="Home & Living">Home &amp; Living</option>
              <option value="Apparel">Apparel</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>eStore Qty</label>
            <div className="flex gap-2">
              <input
                className={`${inputFilter} w-1/2`}
                placeholder="Min"
                type="number"
                value={qtyMin}
                onChange={(e) => {
                  setQtyMin(e.target.value === "" ? "" : Number(e.target.value));
                  setPage(1);
                }}
              />
              <input
                className={`${inputFilter} w-1/2`}
                placeholder="Max"
                type="number"
                value={qtyMax}
                onChange={(e) => {
                  setQtyMax(e.target.value === "" ? "" : Number(e.target.value));
                  setPage(1);
                }}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>Quick Access</label>
            <select className={inputFilter} value="__more" onChange={openMoreFilters}>
              <option value="__more">Use “More Filters”</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <label className={labelFilter}>eStore Status</label>
            <select
              className={inputFilter}
              value={estoreStatus}
              onChange={(e) => {
                setEstoreStatus(e.target.value as typeof estoreStatus);
                setPage(1);
              }}
            >
              <option value="all">All States</option>
              <option value="live">Live</option>
              <option value="draft">Draft</option>
              <option value="inactive">Inactive</option>
            </select>
          </div>
          <div className="flex items-end gap-2 lg:col-span-1">
            <button
              type="button"
              className="h-9 flex-1 rounded-lg bg-primary py-2 text-xs font-bold text-on-primary transition-all hover:opacity-90"
              onClick={() => {
                setPage(1);
                setSearchNonce((n) => n + 1);
              }}
            >
              Search
            </button>
            <button
              type="button"
              className="h-9 rounded-lg bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant transition-all hover:bg-outline-variant/40"
              onClick={() => {
                setUpc("");
                setSku("");
                setName("");
                setPriceMin("");
                setPriceMax("");
                setQtyMin("");
                setQtyMax("");
                setBrand("all");
                setCategory("all");
                setEstoreStatus("all");
                setActiveCategories([]);
                setActiveQuickAccess(new Set());
                setPageSize(25);
                setPage(1);
                setSearchNonce((n) => n + 1);
              }}
            >
              Reset
            </button>
            <button
              type="button"
              className="relative h-9 flex items-center gap-1.5 rounded-lg border border-outline-variant/30 bg-surface-container-lowest px-3 py-2 text-xs font-bold text-on-surface-variant transition-colors hover:bg-surface-container-high"
              onClick={openMoreFilters}
            >
              <IconFilterList className="h-4 w-4 shrink-0" />
              <span className="hidden sm:inline">More Filters</span>
              {activeCount > 0 && (
                <span className="flex h-4 min-w-[1rem] items-center justify-center rounded-full bg-primary px-1 text-[9px] font-bold text-on-primary">
                  {activeCount}
                </span>
              )}
            </button>
          </div>
        </div>
      </section>

      {error && (
        <div className="rounded-xl border border-error/25 bg-error/5 px-5 py-4 text-sm text-error">
          {error}
        </div>
      )}

      <div className="relative overflow-hidden rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-[0_4px_20px_rgba(40,23,22,0.02)]">
        {selectedIds.size > 0 && (
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-outline-variant/10 bg-surface-container px-6 py-3">
            <div className="text-xs font-semibold text-on-surface">
              {selectedIds.size} selected{" "}
              <button
                type="button"
                className="ml-2 text-[11px] font-bold text-primary hover:underline"
                onClick={() => setSelectedIds(new Set())}
              >
                Clear selection
              </button>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                className="rounded-md bg-primary px-4 py-2 text-[11px] font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
                disabled={bulkLoading}
                onClick={() => void runBulk("publish")}
              >
                Publish selected
              </button>
              <button
                type="button"
                className="rounded-md border border-outline-variant/25 bg-surface-container-lowest px-4 py-2 text-[11px] font-bold text-on-surface hover:bg-surface-container-high disabled:opacity-40"
                disabled={bulkLoading}
                onClick={() => void runBulk("hide")}
              >
                Hide selected
              </button>
              <button
                type="button"
                className="rounded-md bg-error px-4 py-2 text-[11px] font-bold text-on-error hover:opacity-90 disabled:opacity-40"
                disabled={bulkLoading}
                onClick={() => setConfirmArchiveOpen(true)}
              >
                Archive selected
              </button>
            </div>
          </div>
        )}

        {bulkLoading && (
          <div className="pointer-events-none absolute inset-0 z-10 flex items-center justify-center bg-black/10 backdrop-blur-[1px]">
            <div className="rounded-full bg-surface-container-lowest px-4 py-2 text-xs font-semibold text-on-surface shadow">
              Updating…
            </div>
          </div>
        )}
        <div className="overflow-x-auto custom-scrollbar">
          <table className="w-full border-collapse text-left">
            <thead>
              <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                <th className="w-10 px-6 py-4 text-center">
                  <input
                    type="checkbox"
                    className="h-3.5 w-3.5 cursor-pointer rounded border-outline-variant accent-primary disabled:cursor-not-allowed disabled:opacity-40"
                    aria-label="Select all"
                    checked={allSelected}
                    disabled={rows.length === 0}
                    ref={(el) => {
                      if (el) el.indeterminate = someSelected && !allSelected;
                    }}
                    onChange={(e) => toggleSelectAll(e.target.checked)}
                  />
                </th>
                <th className="w-20 px-4 py-4">Image</th>
                <th className="px-4 py-4">Product Name</th>
                <th className="px-4 py-4">Brand</th>
                <th className="px-4 py-4">UPC / SKU</th>
                <th className="px-4 py-4 text-right">Price</th>
                <th className="px-4 py-4 text-center">Qty</th>
                <th className="px-4 py-4">eStore</th>
                <th className="px-4 py-4">Status</th>
                <th className="px-4 py-4">Modified</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-outline-variant/10">
              {loading && (
                <tr>
                  <td colSpan={11} className="px-6 py-10 text-center text-sm text-on-surface-variant">
                    Loading…
                  </td>
                </tr>
              )}
              {!loading && rows.length === 0 && (
                <tr>
                  <td colSpan={11} className="px-6 py-10 text-center text-sm text-on-surface-variant">
                    No products match the selected filters.{" "}
                    <button
                      type="button"
                      className="font-bold text-primary hover:underline"
                      onClick={() => {
                        setActiveCategories([]);
                        setActiveQuickAccess(new Set());
                        setPage(1);
                        setSearchNonce((n) => n + 1);
                      }}
                    >
                      Clear filters
                    </button>
                  </td>
                </tr>
              )}
              {!loading && rows.map((row) => (
                <tr key={row.id} className="text-[12px] transition-colors hover:bg-surface-container-low">
                  <td className="px-6 py-4 text-center">
                    <input
                      type="checkbox"
                      className="h-3.5 w-3.5 cursor-pointer rounded border-outline-variant accent-primary"
                      aria-label={`Select ${row.name}`}
                      checked={selectedIds.has(row.id)}
                      onChange={(e) => toggleRowSelected(row.id, e.target.checked)}
                    />
                  </td>
                  <td className="px-4 py-4">
                    <div className="h-10 w-10 overflow-hidden rounded border border-outline-variant/10 bg-surface-container-high">
                      <img className="h-full w-full object-cover" alt={row.imageAlt} src={row.imageSrc} />
                    </div>
                  </td>
                  <td className="px-4 py-4">
                    <p className="font-bold text-on-surface">{row.name}</p>
                    <span className="text-[10px] text-on-surface-variant">{row.categoryPath}</span>
                  </td>
                  <td className="px-4 py-4 font-medium text-on-surface-variant">{row.brand}</td>
                  <td className="px-4 py-4 font-mono text-[10px]">
                    <span className="block">UPC: {row.upc}</span>
                    <span className="block text-primary/60">SKU: {row.sku}</span>
                  </td>
                  <td className="px-4 py-4 text-right font-bold text-on-surface">{row.price}</td>
                  <td className="px-4 py-4 text-center font-medium">{row.qty}</td>
                  <td className="px-4 py-4">
                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-bold uppercase ${eStoreBadgeClasses(row.eStoreVariant)}`}
                    >
                      {row.eStoreLabel}
                    </span>
                  </td>
                  <td className="px-4 py-4">
                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[9px] font-bold uppercase ${statusBadgeClasses(row.statusVariant)}`}
                    >
                      {row.statusLabel}
                    </span>
                  </td>
                  <td className="px-4 py-4 text-on-surface-variant">{row.modified}</td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex justify-end gap-2 text-on-surface-variant">
                      <button
                        type="button"
                        className="transition-colors hover:text-primary"
                        aria-label="Edit"
                        onClick={() => onEditProduct?.(row)}
                      >
                        <IconEdit className="h-5 w-5" />
                      </button>
                      <button
                        type="button"
                        className="transition-colors hover:text-primary"
                        aria-label="View"
                        onClick={() => console.info("[Products] View", row.id)}
                      >
                        <IconVisibility className="h-5 w-5" />
                      </button>
                      <button
                        type="button"
                        className="transition-colors hover:text-primary"
                        aria-label="Open in new"
                        onClick={() => console.info("[Products] Open", row.id)}
                      >
                        <IconOpenInNew className="h-5 w-5" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <footer className="flex flex-col items-center justify-between gap-4 bg-surface-container px-6 py-4 sm:flex-row">
          <div className="flex flex-wrap items-center gap-4 text-[11px] font-medium text-on-surface-variant">
            <span>
              {total === 0
                ? "Showing 0 entries"
                : `Showing ${(safePage - 1) * pageSize + 1} to ${Math.min(safePage * pageSize, total)} of ${total} entries`}
            </span>
            <div className="flex items-center gap-2">
              <label htmlFor="products-page-size">Show</label>
              <select
                id="products-page-size"
                className="rounded border-none bg-surface-container-lowest px-2 py-1 text-[11px] focus:ring-1 focus:ring-primary focus:outline-none"
                value={String(pageSize)}
                onChange={(e) => {
                  const next = Number(e.target.value);
                  setPageSize(Number.isFinite(next) && next > 0 ? next : 25);
                  setPage(1);
                }}
              >
                <option value="10">10</option>
                <option value="25">25</option>
                <option value="50">50</option>
              </select>
            </div>
          </div>
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
            <div className="mx-2 flex items-center gap-1">
              {getCompactPages(totalPages, safePage).map((p, i) =>
                p === "…" ? (
                  <span key={`e-${i}`} className="px-1 text-[11px] text-on-surface-variant">
                    …
                  </span>
                ) : (
                  <button
                    key={p}
                    type="button"
                    className={
                      p === safePage
                        ? "h-8 w-8 rounded bg-primary text-[11px] font-bold text-on-primary"
                        : "h-8 w-8 rounded text-[11px] font-medium text-on-surface hover:bg-surface-container-high"
                    }
                    onClick={() => setPage(p)}
                  >
                    {p}
                  </button>
                )
              )}
            </div>
            <button
              type="button"
              className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high"
              aria-label="Next page"
              disabled={safePage >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              <IconChevronRight className="h-5 w-5" />
            </button>
            <button
              type="button"
              className="rounded-lg p-2 text-on-surface-variant hover:bg-surface-container-high"
              aria-label="Last page"
              disabled={safePage >= totalPages}
              onClick={() => setPage(totalPages)}
            >
              <IconLastPage className="h-5 w-5" />
            </button>
          </div>
        </footer>
      </div>

      {confirmArchiveOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6">
              <h3 className="text-sm font-bold text-on-surface">Archive products</h3>
              <p className="mt-2 text-xs text-on-surface-variant leading-relaxed">
                Archive <strong>{selectedIds.size}</strong> selected product(s)? This is a soft archive.
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setConfirmArchiveOpen(false)}
                disabled={bulkLoading}
              >
                Cancel
              </button>
              <button
                type="button"
                className="rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 disabled:opacity-40"
                onClick={() => {
                  setConfirmArchiveOpen(false);
                  void runBulk("archive");
                }}
                disabled={bulkLoading}
              >
                Archive
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-outline-variant/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <p className={`text-sm font-semibold ${toast.kind === "error" ? "text-error" : "text-on-surface"}`}>{toast.message}</p>
        </div>
      )}

      </div>

      {/* ── More Filters modal ────────────────────────────────────────────── */}
      {moreFiltersOpen && (
        <div
          className="fixed inset-0 z-50 flex items-start justify-center bg-on-surface/40 pt-24 backdrop-blur-sm"
          onClick={() => setMoreFiltersOpen(false)}
        >
          <div
            className="mx-4 w-full max-w-md overflow-hidden rounded-2xl bg-surface-container-lowest shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Header */}
            <div className="flex items-center justify-between border-b border-outline-variant/20 px-6 py-4">
              <div className="flex items-center gap-2">
                <IconFilterList className="h-5 w-5 text-primary shrink-0" />
                <h3 className="text-base font-bold text-on-surface">More Filters</h3>
              </div>
              <button
                type="button"
                className="rounded p-1.5 text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setMoreFiltersOpen(false)}
                aria-label="Close"
              >
                <IconClose className="h-4 w-4" />
              </button>
            </div>

            <div className="max-h-[60vh] overflow-y-auto px-6 py-5 space-y-6">
              {/* Categories */}
              <div>
                <h4 className="mb-3 text-[10px] font-bold uppercase tracking-widest text-primary">Categories</h4>
                <div className="relative mb-2">
                  <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
                  <input
                    type="text"
                    className="w-full rounded-md border border-outline-variant/20 bg-surface py-2 pl-8 pr-3 text-xs focus:outline-none focus:ring-1 focus:ring-primary"
                    placeholder="Type to search categories..."
                    value={catSearch}
                    onChange={(e) => setCatSearch(e.target.value)}
                  />
                </div>
                <div className="space-y-1.5 rounded-md border border-outline-variant/15 bg-surface p-3 max-h-40 overflow-y-auto">
                  {filteredCatOptions.length === 0 ? (
                    <p className="px-1 py-2 text-xs italic text-on-surface-variant">No categories match "{catSearch}"</p>
                  ) : (
                    filteredCatOptions.map((cat) => {
                      const checked = draftCategories.includes(cat);
                      return (
                        <label
                          key={cat}
                          className={`flex cursor-pointer items-center gap-3 rounded px-2 py-2 text-xs transition-colors hover:bg-surface-container-high ${
                            checked ? "font-semibold text-primary" : "text-on-surface"
                          }`}
                        >
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-primary shrink-0"
                            checked={checked}
                            onChange={() => toggleDraftCategory(cat)}
                          />
                          {cat}
                          {checked && (
                            <span className="ml-auto rounded-full bg-primary/10 px-1.5 py-0.5 text-[9px] font-bold text-primary">
                              Selected
                            </span>
                          )}
                        </label>
                      );
                    })
                  )}
                </div>
                {draftCategories.length > 0 && (
                  <p className="mt-1.5 text-[10px] text-primary">
                    {draftCategories.length} categor{draftCategories.length === 1 ? "y" : "ies"} selected
                  </p>
                )}
              </div>

              {/* Quick Access */}
              <div>
                <h4 className="mb-3 text-[10px] font-bold uppercase tracking-widest text-primary">Quick Access</h4>
                <div className="space-y-2">
                  {QUICK_ACCESS_OPTIONS.map(({ key, label, hint }) => {
                    const checked = draftQuickAccess.has(key);
                    return (
                      <label
                        key={key}
                        className={`flex cursor-pointer items-start gap-3 rounded-lg border p-3 transition-colors ${
                          checked
                            ? "border-primary/30 bg-primary/5"
                            : "border-outline-variant/20 hover:bg-surface-container-low"
                        }`}
                      >
                        <input
                          type="checkbox"
                          className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                          checked={checked}
                          onChange={() => toggleDraftQuickAccess(key)}
                        />
                        <div>
                          <p className="text-xs font-semibold text-on-surface">{label}</p>
                          {hint && <p className="text-[10px] text-on-surface-variant">{hint}</p>}
                        </div>
                      </label>
                    );
                  })}
                </div>
              </div>
            </div>

            {/* Footer */}
            <div className="flex items-center justify-between border-t border-outline-variant/20 px-6 py-4">
              <button
                type="button"
                className="text-xs font-bold text-on-surface-variant hover:text-error transition-colors"
                onClick={resetDraft}
              >
                Reset
              </button>
              <div className="flex gap-3">
                <button
                  type="button"
                  className="rounded-md border border-outline-variant/30 px-4 py-2 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                  onClick={() => setMoreFiltersOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="flex items-center gap-2 rounded-md bg-primary px-5 py-2 text-xs font-bold text-on-primary shadow-sm hover:opacity-90 transition-opacity"
                  onClick={applyMoreFilters}
                >
                  <IconSearch className="h-3.5 w-3.5 shrink-0" />
                  Search
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ── Export options modal ──────────────────────────────────────────── */}
      {exportType !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            {/* Header */}
            <div className="flex items-center justify-between border-b border-outline-variant/10 px-6 py-4">
              <h3 className="text-base font-bold text-on-surface">
                {exportType === "products"
                  ? "Export Products"
                  : exportType === "inventory"
                  ? "Export Inventory Qty"
                  : exportType === "preview"
                  ? "Export Preview Links"
                  : "Advance Price Export"}
              </h3>
              <button
                type="button"
                aria-label="Close"
                className="rounded-full p-1.5 text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setExportType(null)}
              >
                <IconClose className="h-4 w-4" />
              </button>
            </div>

            {/* Body */}
            {exportType === "advance-price" ? (
              <div className="px-6 py-6">
                <div className="flex items-center justify-center gap-6">
                  <span className="text-xs font-semibold text-on-surface">File Format:</span>
                  {(["excel", "csv"] as const).map((fmt) => (
                    <label key={fmt} className="flex cursor-pointer items-center gap-2 select-none">
                      <input
                        type="radio"
                        name="exportFormat"
                        value={fmt}
                        checked={exportFormat === fmt}
                        onChange={() => setExportFormat(fmt)}
                        className="accent-primary"
                      />
                      <span className="text-xs font-semibold text-on-surface uppercase">
                        {fmt === "excel" ? "Excel" : "CSV"}
                      </span>
                    </label>
                  ))}
                </div>
              </div>
            ) : (
              <div className="space-y-6 px-6 py-6">
                {/* File format */}
                <div>
                  <p className="mb-3 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
                    File Format
                  </p>
                  <div className="flex gap-6">
                    {(["excel", "csv"] as const).map((fmt) => (
                      <label key={fmt} className="flex cursor-pointer items-center gap-2 select-none">
                        <input
                          type="radio"
                          name="exportFormat"
                          value={fmt}
                          checked={exportFormat === fmt}
                          onChange={() => setExportFormat(fmt)}
                          className="accent-primary"
                        />
                        <span className="text-xs font-semibold text-on-surface">
                          {fmt === "excel" ? "Excel (.xlsx)" : "CSV (.csv)"}
                        </span>
                      </label>
                    ))}
                  </div>
                </div>

                {/* Strip HTML */}
                <label className="flex cursor-pointer items-center gap-3 select-none">
                  <input
                    type="checkbox"
                    checked={exportStripHtml}
                    onChange={(e) => setExportStripHtml(e.target.checked)}
                    className="h-4 w-4 accent-primary"
                  />
                  <span className="text-xs font-semibold text-on-surface">Strip HTML</span>
                  <span className="text-[10px] text-on-surface-variant">
                    Remove HTML tags from exported text fields
                  </span>
                </label>
              </div>
            )}

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setExportType(null)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90"
                onClick={doExport}
              >
                {exportType === "advance-price" ? null : <IconDownload className="h-4 w-4 shrink-0" />}
                OK
              </button>
            </div>
          </div>
        </div>
      )}

      <div className="pointer-events-none fixed bottom-6 right-8 z-10 flex items-center gap-4 rounded-full border border-outline-variant/30 bg-surface/80 px-6 py-2 shadow-xl backdrop-blur-sm">
        <div className="pointer-events-auto flex items-center gap-2">
          <span className="h-2 w-2 animate-pulse rounded-full bg-primary" aria-hidden />
          <span className="text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            Live Sync Active
          </span>
        </div>
        <div className="h-4 w-px bg-outline-variant/30" aria-hidden />
        <p className="pointer-events-auto text-[10px] font-medium text-on-surface-variant">
          Last updated: 2 mins ago
        </p>
      </div>
    </div>
  );
}
