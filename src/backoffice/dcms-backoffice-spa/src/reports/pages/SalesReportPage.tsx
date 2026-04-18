import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useEffect, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconDownload } from "../../orders/icons";
import { exportReportRowsToXlsx } from "../shared/exportReportRowsToXlsx";
import { ReportFilterField, ReportFilterPanel, inputClass } from "../shared/ReportFilterPanel";
import { useReportExportState } from "../shared/useReportExport";

type SalesTab = "category" | "brand" | "product" | "tenant";

const STORE_OPTIONS = [
  { value: "all", label: "All stores" },
  { value: "sg-flagship", label: "SG — Flagship" },
  { value: "my-central", label: "MY — Central" },
];

const BRAND_OPTIONS = [
  { value: "all", label: "All brands" },
  { value: "CAS-7721", label: "Luxe Heritage" },
  { value: "VEL-4490", label: "Velocity Tech" },
  { value: "AUR-5501", label: "Aura Essentials" },
];

const CATEGORY_OPTIONS = [
  { value: "all", label: "All categories" },
  { value: "Dairy", label: "Dairy" },
  { value: "Beverages", label: "Beverages" },
  { value: "Home", label: "Home" },
  { value: "Pantry", label: "Pantry" },
];

const TAB_META: Record<SalesTab, { title: string; spec: string }> = {
  category: { title: "Sales by category", spec: "7.1.4" },
  brand: { title: "Sales by brand", spec: "7.1.5" },
  product: { title: "Sales by product", spec: "7.1.6" },
  tenant: { title: "Sales by tenant", spec: "7.1.10" },
};

type CategoryRow = {
  id: string;
  category: string;
  productsCount: number;
  transactions: number;
  unitsSold: number;
  totalSales: string;
};

type BrandRow = {
  id: string;
  brand: string;
  brandCode: string;
  productsCount: number;
  transactions: number;
  unitsSold: number;
  totalSales: string;
};

type ProductRow = {
  id: string;
  upc: string;
  sku: string;
  productName: string;
  category: string;
  brandCode: string;
  unitsSold: number;
  totalSales: string;
};

type TenantRow = {
  id: string;
  tenantName: string;
  ordersCount: number;
  productsSold: number;
  totalSales: string;
};

const MOCK_CATEGORY: CategoryRow[] = [
  { id: "c1", category: "Dairy", productsCount: 12, transactions: 890, unitsSold: 2400, totalSales: "48,200.00" },
  { id: "c2", category: "Beverages", productsCount: 28, transactions: 1204, unitsSold: 5100, totalSales: "62,880.50" },
  { id: "c3", category: "Home", productsCount: 9, transactions: 310, unitsSold: 620, totalSales: "9,310.00" },
  { id: "c4", category: "Pantry", productsCount: 44, transactions: 2100, unitsSold: 8800, totalSales: "71,040.25" },
];

const MOCK_BRAND: BrandRow[] = [
  { id: "b1", brand: "Luxe Heritage Group", brandCode: "CAS-7721", productsCount: 56, transactions: 3200, unitsSold: 9200, totalSales: "112,400.00" },
  { id: "b2", brand: "Velocity Tech Systems", brandCode: "VEL-4490", productsCount: 34, transactions: 1800, unitsSold: 4100, totalSales: "58,920.00" },
  { id: "b3", brand: "Aura Essentials", brandCode: "AUR-5501", productsCount: 22, transactions: 980, unitsSold: 2100, totalSales: "24,180.00" },
];

const MOCK_PRODUCT: ProductRow[] = [
  {
    id: "p1",
    upc: "0885002400123",
    sku: "SKU-MILK-2L",
    productName: "Organic whole milk 2L",
    category: "Dairy",
    brandCode: "CAS-7721",
    unitsSold: 420,
    totalSales: "3,402.00",
  },
  {
    id: "p2",
    upc: "0885002400456",
    sku: "SKU-BEAN-500",
    productName: "Dark roast beans 500g",
    category: "Beverages",
    brandCode: "VEL-4490",
    unitsSold: 188,
    totalSales: "5,640.00",
  },
  {
    id: "p3",
    upc: "0885002400789",
    sku: "SKU-TOTE-01",
    productName: "Recycled tote bag",
    category: "Home",
    brandCode: "AUR-5501",
    unitsSold: 96,
    totalSales: "1,152.00",
  },
  {
    id: "p4",
    upc: "0885002400999",
    sku: "SKU-PASTA-400",
    productName: "Gluten-free pasta 400g",
    category: "Pantry",
    brandCode: "CAS-7721",
    unitsSold: 1200,
    totalSales: "4,800.00",
  },
];

const MOCK_TENANT: TenantRow[] = [
  { id: "t1", tenantName: "Demo Supermarket (SG)", ordersCount: 12840, productsSold: 48200, totalSales: "1,842,900.00" },
  { id: "t2", tenantName: "Demo Supermarket (MY)", ordersCount: 8420, productsSold: 29100, totalSales: "1,120,400.00" },
  { id: "t3", tenantName: "Partner Outlet — North", ordersCount: 2100, productsSold: 8800, totalSales: "298,200.00" },
];

function parseSalesAmount(s: string): number {
  return parseFloat(String(s).replace(/,/g, "")) || 0;
}

function sortByTotalSalesDesc<T extends { totalSales: string }>(rows: T[]): T[] {
  return [...rows].sort((a, b) => parseSalesAmount(b.totalSales) - parseSalesAmount(a.totalSales));
}

const categoryColumns: ColumnDef<CategoryRow>[] = [
  { accessorKey: "category", header: "Category", cell: ({ row }) => <span className="text-xs font-bold">{row.getValue("category")}</span> },
  {
    accessorKey: "productsCount",
    header: "Products count",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("productsCount")}</span>,
  },
  {
    accessorKey: "transactions",
    header: "Transactions",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("transactions")}</span>,
  },
  { accessorKey: "unitsSold", header: "Units sold", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("unitsSold")}</span> },
  { accessorKey: "totalSales", header: "Total sales", cell: ({ row }) => <span className="tabular-nums text-xs font-semibold">{row.getValue("totalSales")}</span> },
];

const brandColumns: ColumnDef<BrandRow>[] = [
  { accessorKey: "brand", header: "Brand", cell: ({ row }) => <span className="text-xs font-bold">{row.getValue("brand")}</span> },
  {
    accessorKey: "productsCount",
    header: "Products count",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("productsCount")}</span>,
  },
  {
    accessorKey: "transactions",
    header: "Transactions",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("transactions")}</span>,
  },
  { accessorKey: "unitsSold", header: "Units sold", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("unitsSold")}</span> },
  { accessorKey: "totalSales", header: "Total sales", cell: ({ row }) => <span className="tabular-nums text-xs font-semibold">{row.getValue("totalSales")}</span> },
];

const productColumns: ColumnDef<ProductRow>[] = [
  { accessorKey: "upc", header: "UPC", cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("upc")}</span> },
  { accessorKey: "sku", header: "SKU", cell: ({ row }) => <span className="font-mono text-xs font-semibold">{row.getValue("sku")}</span> },
  {
    accessorKey: "productName",
    header: "Product name",
    cell: ({ row }) => <span className="text-xs font-medium text-on-surface">{row.getValue("productName")}</span>,
  },
  { accessorKey: "unitsSold", header: "Units sold", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("unitsSold")}</span> },
  { accessorKey: "totalSales", header: "Total sales", cell: ({ row }) => <span className="tabular-nums text-xs font-semibold">{row.getValue("totalSales")}</span> },
];

const tenantColumns: ColumnDef<TenantRow>[] = [
  { accessorKey: "tenantName", header: "Tenant name", cell: ({ row }) => <span className="text-xs font-bold">{row.getValue("tenantName")}</span> },
  {
    accessorKey: "ordersCount",
    header: "Orders count",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("ordersCount")}</span>,
  },
  {
    accessorKey: "productsSold",
    header: "Products sold",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("productsSold")}</span>,
  },
  { accessorKey: "totalSales", header: "Total sales", cell: ({ row }) => <span className="tabular-nums text-xs font-semibold">{row.getValue("totalSales")}</span> },
];

function scaleRows<T extends { transactions?: number; unitsSold?: number; totalSales: string; productsCount?: number }>(
  rows: T[],
  factor: number
): T[] {
  if (factor >= 0.99) return rows;
  return rows.map((r) => ({
    ...r,
    ...(r.productsCount !== undefined ? { productsCount: Math.max(1, Math.floor(r.productsCount * factor)) } : {}),
    ...(r.transactions !== undefined ? { transactions: Math.max(1, Math.floor((r.transactions as number) * factor)) } : {}),
    ...(r.unitsSold !== undefined ? { unitsSold: Math.max(1, Math.floor((r.unitsSold as number) * factor)) } : {}),
    totalSales: (parseSalesAmount(r.totalSales) * factor).toLocaleString("en-US", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }),
  }));
}

function applyCategoryFilters(rows: CategoryRow[], store: string, brand: string): CategoryRow[] {
  const f = store === "all" ? 1 : 0.55;
  let out = scaleRows(rows, f);
  if (brand !== "all") {
    out = out.map((r) => ({
      ...r,
      productsCount: Math.max(1, Math.floor(r.productsCount * 0.42)),
      transactions: Math.max(1, Math.floor(r.transactions * 0.42)),
      unitsSold: Math.max(1, Math.floor(r.unitsSold * 0.42)),
      totalSales: (parseSalesAmount(r.totalSales) * 0.42).toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }),
    }));
  }
  return sortByTotalSalesDesc(out);
}

function applyBrandFilters(rows: BrandRow[], store: string): BrandRow[] {
  const f = store === "all" ? 1 : 0.58;
  return sortByTotalSalesDesc(scaleRows(rows, f));
}

function applyProductFilters(rows: ProductRow[], store: string, brand: string, category: string): ProductRow[] {
  let out = [...rows];
  if (brand !== "all") out = out.filter((r) => r.brandCode === brand);
  if (category !== "all") out = out.filter((r) => r.category === category);
  const f = store === "all" ? 1 : 0.52;
  out = scaleRows(out, f);
  return sortByTotalSalesDesc(out);
}

const DEMO_CHAIN_ADMIN_KEY = "dcms.demoSalesTenantAdmin";

function readDemoChainAdmin(): boolean {
  try {
    const v = localStorage.getItem(DEMO_CHAIN_ADMIN_KEY);
    if (v === null) return true;
    return v === "1";
  } catch {
    return true;
  }
}

export function SalesReportPage() {
  const [tab, setTab] = useState<SalesTab>("category");
  const [dateFrom, setDateFrom] = useState("2026-04-01");
  const [dateTo, setDateTo] = useState("2026-04-18");
  const [storeScope, setStoreScope] = useState("all");
  const [brandScope, setBrandScope] = useState("all");
  const [categoryScope, setCategoryScope] = useState("all");
  const [demoChainAdmin, setDemoChainAdmin] = useState(() => readDemoChainAdmin());

  const [categoryRows, setCategoryRows] = useState<CategoryRow[]>([]);
  const [brandRows, setBrandRows] = useState<BrandRow[]>([]);
  const [productRows, setProductRows] = useState<ProductRow[]>([]);
  const [tenantRows, setTenantRows] = useState<TenantRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  const activeRows =
    tab === "category" ? categoryRows : tab === "brand" ? brandRows : tab === "product" ? productRows : tenantRows;
  const { exportDisabled } = useReportExportState(loading, activeRows.length);

  const persistDemoAdmin = useCallback((on: boolean) => {
    setDemoChainAdmin(on);
    try {
      localStorage.setItem(DEMO_CHAIN_ADMIN_KEY, on ? "1" : "0");
    } catch {
      /* ignore */
    }
  }, []);

  useEffect(() => {
    if (tab === "tenant" && !demoChainAdmin) setTenantRows([]);
  }, [demoChainAdmin, tab]);

  const handleSearch = useCallback(async () => {
    if (dateFrom > dateTo) {
      setCategoryRows([]);
      setBrandRows([]);
      setProductRows([]);
      setTenantRows([]);
      setHasSearched(true);
      return;
    }
    setLoading(true);
    setHasSearched(true);
    await new Promise((r) => setTimeout(r, 480));

    setCategoryRows(applyCategoryFilters([...MOCK_CATEGORY], storeScope, brandScope));
    setBrandRows(applyBrandFilters([...MOCK_BRAND], storeScope));
    setProductRows(applyProductFilters([...MOCK_PRODUCT], storeScope, brandScope, categoryScope));
    setTenantRows(demoChainAdmin ? sortByTotalSalesDesc([...MOCK_TENANT]) : []);

    setLoading(false);
  }, [brandScope, categoryScope, dateFrom, dateTo, demoChainAdmin, storeScope]);

  const handleReset = useCallback(() => {
    setDateFrom("2026-04-01");
    setDateTo("2026-04-18");
    setStoreScope("all");
    setBrandScope("all");
    setCategoryScope("all");
    setCategoryRows([]);
    setBrandRows([]);
    setProductRows([]);
    setTenantRows([]);
    setHasSearched(false);
  }, []);

  const handleExport = useCallback(async () => {
    if (tab === "category" && categoryRows.length) {
      await exportReportRowsToXlsx(
        "SalesByCategory",
        "sales-by-category-7-1-4.xlsx",
        ["Category", "Products count", "Transactions", "Units sold", "Total sales"],
        categoryRows.map((r) => [r.category, String(r.productsCount), String(r.transactions), String(r.unitsSold), r.totalSales])
      );
    } else if (tab === "brand" && brandRows.length) {
      await exportReportRowsToXlsx(
        "SalesByBrand",
        "sales-by-brand-7-1-5.xlsx",
        ["Brand", "Products count", "Transactions", "Units sold", "Total sales"],
        brandRows.map((r) => [r.brand, String(r.productsCount), String(r.transactions), String(r.unitsSold), r.totalSales])
      );
    } else if (tab === "product" && productRows.length) {
      await exportReportRowsToXlsx(
        "SalesByProduct",
        "sales-by-product-7-1-6.xlsx",
        ["UPC", "SKU", "Product name", "Units sold", "Total sales"],
        productRows.map((r) => [r.upc, r.sku, r.productName, String(r.unitsSold), r.totalSales])
      );
    } else if (tab === "tenant" && tenantRows.length) {
      await exportReportRowsToXlsx(
        "SalesByTenant",
        "sales-by-tenant-7-1-10.xlsx",
        ["Tenant name", "Orders count", "Products sold", "Total sales"],
        tenantRows.map((r) => [r.tenantName, String(r.ordersCount), String(r.productsSold), r.totalSales])
      );
    }
  }, [brandRows, categoryRows, productRows, tab, tenantRows]);

  const emptyMessage =
    dateFrom > dateTo
      ? "Invalid date range: From is after To."
      : tab === "tenant" && !demoChainAdmin
        ? "Sales by tenant is available to Chain Admin / Super Admin only. Enable the demo toggle below to preview."
        : "No rows match the current filters. Adjust filters and click Search.";

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Sales reports">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>Reports</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Sales</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Sales reports</h1>
          <p className="max-w-3xl text-sm text-on-surface-variant">
            Category, brand, product, and cross-tenant sales (demo aggregates). Default row order is total sales descending after each search.
          </p>
        </div>
        <button
          type="button"
          disabled={exportDisabled}
          className="flex items-center gap-2 self-start rounded-lg border border-outline-variant/30 bg-white px-4 py-2 text-xs font-bold text-on-surface shadow-sm hover:bg-surface-container-high disabled:pointer-events-none disabled:opacity-40 md:self-center"
          onClick={() => void handleExport()}
        >
          <IconDownload className="h-4 w-4 shrink-0 text-secondary" />
          Export to Excel
        </button>
      </header>

      <div className="border-b border-outline-variant/10 bg-surface px-6">
        <div className="flex flex-wrap gap-1" role="tablist" aria-label="Sales report type">
          {(Object.keys(TAB_META) as SalesTab[]).map((id) => {
            const { title, spec } = TAB_META[id];
            const active = tab === id;
            return (
              <button
                key={id}
                type="button"
                role="tab"
                aria-selected={active}
                className={`border-b-2 px-4 py-3 text-xs font-bold transition-colors ${
                  active
                    ? "border-primary text-primary"
                    : "border-transparent text-on-surface-variant hover:text-on-surface"
                }`}
                onClick={() => setTab(id)}
              >
                {title}
                <span className="ml-1.5 font-medium opacity-70">({spec})</span>
              </button>
            );
          })}
        </div>
      </div>

      <div className="w-full flex-1 space-y-6 p-6">
        <ReportFilterPanel onSearch={() => void handleSearch()} onReset={handleReset} searchDisabled={loading}>
          <ReportFilterField label="From" htmlFor="sl-from">
            <input id="sl-from" type="date" className={inputClass} value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
          </ReportFilterField>
          <ReportFilterField label="To" htmlFor="sl-to">
            <input id="sl-to" type="date" className={inputClass} value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
          </ReportFilterField>
          {tab !== "tenant" && (
            <ReportFilterField label="Store" htmlFor="sl-store">
              <select id="sl-store" className={inputClass} value={storeScope} onChange={(e) => setStoreScope(e.target.value)}>
                {STORE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </ReportFilterField>
          )}
          {(tab === "category" || tab === "product") && (
            <ReportFilterField label="Brand" htmlFor="sl-brand">
              <select id="sl-brand" className={inputClass} value={brandScope} onChange={(e) => setBrandScope(e.target.value)}>
                {BRAND_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </ReportFilterField>
          )}
          {tab === "product" && (
            <ReportFilterField label="Category" htmlFor="sl-cat">
              <select id="sl-cat" className={inputClass} value={categoryScope} onChange={(e) => setCategoryScope(e.target.value)}>
                {CATEGORY_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </ReportFilterField>
          )}
          {tab === "tenant" && (
            <div className="flex min-w-[220px] flex-col gap-2 rounded-lg border border-primary/20 bg-primary/5 px-3 py-2">
              <label className="flex cursor-pointer items-center gap-2 text-xs font-semibold text-on-surface">
                <input
                  type="checkbox"
                  className="h-3.5 w-3.5 accent-primary"
                  checked={demoChainAdmin}
                  onChange={(e) => persistDemoAdmin(e.target.checked)}
                />
                Demo: act as Chain Admin / Super Admin
              </label>
              <p className="text-xs leading-snug text-on-surface-variant">
                Production hides this report unless the signed-in user has the role. This toggle simulates RBAC for UI review.
              </p>
            </div>
          )}
        </ReportFilterPanel>

        {loading && (
          <div
            className="flex items-center justify-center rounded-xl border border-outline-variant/20 bg-surface-container-lowest py-16 text-sm font-medium text-on-surface-variant"
            role="status"
            aria-live="polite"
          >
            Loading report…
          </div>
        )}

        {!loading && hasSearched && activeRows.length === 0 && (
          <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-outline-variant/30 bg-surface-container-lowest py-16 px-6 text-center">
            <p className="text-sm font-semibold text-on-surface">No results</p>
            <p className="mt-2 max-w-md text-xs text-on-surface-variant">{emptyMessage}</p>
          </div>
        )}

        {!loading && categoryRows.length > 0 && tab === "category" && (
          <DataTable columns={categoryColumns} data={categoryRows} getRowId={(r) => r.id} globalFilterPlaceholder="Search categories…" />
        )}
        {!loading && brandRows.length > 0 && tab === "brand" && (
          <DataTable columns={brandColumns} data={brandRows} getRowId={(r) => r.id} globalFilterPlaceholder="Search brands…" />
        )}
        {!loading && productRows.length > 0 && tab === "product" && (
          <DataTable columns={productColumns} data={productRows} getRowId={(r) => r.id} globalFilterPlaceholder="Search SKU, UPC, product…" />
        )}
        {!loading && tenantRows.length > 0 && tab === "tenant" && (
          <DataTable columns={tenantColumns} data={tenantRows} getRowId={(r) => r.id} globalFilterPlaceholder="Search tenants…" />
        )}

        {!loading && !hasSearched && (
          <p className="rounded-xl border border-outline-variant/15 bg-surface-container-low/50 px-4 py-3 text-center text-xs text-on-surface-variant">
            Choose filters and click <span className="font-bold text-on-surface">Search</span> to load{" "}
            <span className="font-semibold text-on-surface">{TAB_META[tab].title}</span>.
          </p>
        )}
      </div>
    </div>
  );
}
