import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import {
  fetchSalesReport,
  fetchSalesByProduct,
  fetchTransactionsOverview,
  type SalesByProductRow,
  type SalesDimension,
  type SalesOverview,
  type SalesReportRow,
} from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";
import { ReportFilterField, inputClass } from "../shared/ReportFilterPanel";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

// Unified row across all three BRD dimensions; per-dimension columns pick the fields they show.
type UiRow = {
  id: string;
  key: string;
  name: string | null;
  upc: string | null;
  sku: string | null;
  productsCount: number | null;
  transactions: number | null;
  unitsSold: number;
  gross: number;
  currency: string;
};

// BRD §1 Category, §2 Brand, §3 Product.
const DIMENSION_OPTIONS: { value: SalesDimension; label: string }[] = [
  { value: "category", label: "Category" },
  { value: "brand", label: "Brand" },
  { value: "product", label: "Product" },
];

// Defensive: a report formatter must never hard-crash the whole reports SPA on a nullish/NaN value
// (e.g. an API shape mismatch). Coerce to a number first.
const money = (n: number) =>
  Number(n ?? 0).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const int = (n: number) => Number(n ?? 0).toLocaleString("en-US");
// Columns the analytics projection doesn't carry yet (BRD §1/§2 "Current No. of Products",
// "Total Transactions", §3 UPC/SKU/Product Name) come back null and render as "—".
const dashInt = (n: number | null) => (n == null ? "—" : int(n));
const dashTxt = (s: string | null) => (s == null || s === "" ? "—" : s);

// OrderItems.ProductName is jsonb — a plain JSON string ("Water Bottle #33") or a localized map.
const parseProductName = (json: string): string => {
  try {
    const v = JSON.parse(json);
    if (typeof v === "string") return v;
    if (v && typeof v === "object") return String((v as Record<string, unknown>).en ?? Object.values(v)[0] ?? json);
    return String(v ?? json);
  } catch {
    return json;
  }
};

const numCell = (v: number) => <span className="tabular-nums text-xs">{int(v)}</span>;
const moneyCell = (v: number) => <span className="tabular-nums text-xs font-semibold">{money(v)}</span>;
const dashIntCell = (v: number | null) => <span className="tabular-nums text-xs">{dashInt(v)}</span>;

export function SalesReportPage({ tenantId, storeId, authToken }: Props) {
  const [dimension, setDimension] = useState<SalesDimension>("category");
  // BRD §3 Overview Section — only populated for the Sales by Product report.
  const [overview, setOverview] = useState<SalesOverview | null>(null);

  const columns: ColumnDef<UiRow>[] = useMemo(() => {
    const nameCol = (header: string): ColumnDef<UiRow> => ({
      accessorKey: "name",
      header,
      cell: ({ row }) => (
        <span className="text-xs font-semibold text-on-surface">{dashTxt(row.original.name ?? row.original.key)}</span>
      ),
    });
    const productsCountCol: ColumnDef<UiRow> = {
      accessorKey: "productsCount",
      header: "Current No. of Products",
      cell: ({ row }) => dashIntCell(row.original.productsCount),
    };
    const transactionsCol: ColumnDef<UiRow> = {
      accessorKey: "transactions",
      header: "Total Transactions",
      cell: ({ row }) => dashIntCell(row.original.transactions),
    };

    if (dimension === "category") {
      return [
        nameCol("Category"),
        productsCountCol,
        transactionsCol,
        { accessorKey: "unitsSold", header: "Products Sold", cell: ({ row }) => numCell(row.original.unitsSold) },
        { accessorKey: "gross", header: "Total Category Sales", cell: ({ row }) => moneyCell(row.original.gross) },
      ];
    }
    if (dimension === "brand") {
      return [
        nameCol("Brand"),
        productsCountCol,
        transactionsCol,
        { accessorKey: "unitsSold", header: "Total Products Sold", cell: ({ row }) => numCell(row.original.unitsSold) },
        { accessorKey: "gross", header: "Total Brand Sales", cell: ({ row }) => moneyCell(row.original.gross) },
      ];
    }
    // product
    return [
      {
        accessorKey: "upc",
        header: "UPC",
        cell: ({ row }) => <span className="font-mono text-xs">{dashTxt(row.original.upc)}</span>,
      },
      {
        accessorKey: "sku",
        header: "SKU",
        cell: ({ row }) => <span className="font-mono text-xs">{dashTxt(row.original.sku)}</span>,
      },
      {
        accessorKey: "name",
        header: "Product Name",
        cell: ({ row }) => (
          <span className="text-xs font-semibold text-on-surface">{dashTxt(row.original.name ?? row.original.key)}</span>
        ),
      },
      { accessorKey: "unitsSold", header: "Total Products Sold", cell: ({ row }) => numCell(row.original.unitsSold) },
      { accessorKey: "gross", header: "Total Product Sales", cell: ({ row }) => moneyCell(row.original.gross) },
    ];
  }, [dimension]);

  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) {
        setOverview(null);
        return [];
      }
      const apiFilters = {
        dateFrom: filters.dateFrom,
        dateTo: filters.dateTo,
        storeId: filters.storeScope !== "all" ? filters.storeScope : storeId ?? "all",
      };

      // BRD §3 Sales by Product — sourced from the Order DB so it carries real product names and
      // excludes cancelled/refunded orders (Common BR03/BR04). Category/Brand stay on the analytics
      // projection: it's the only source with a real category dimension (order items carry no
      // category/brand, and VariantSnapshot is empty so UPC/SKU remain "—").
      if (dimension === "product") {
        const [products, ov] = await Promise.all([
          fetchSalesByProduct(tenantId, apiFilters, authToken),
          fetchTransactionsOverview(tenantId, apiFilters, authToken),
        ]);
        const rows: UiRow[] = products.map((p: SalesByProductRow, i) => ({
          id: `${p.productId}-${i}`,
          key: p.productId,
          name: parseProductName(p.productNameJson),
          upc: null,
          sku: null,
          productsCount: null,
          transactions: null,
          unitsSold: p.unitsSold,
          gross: p.totalSales,
          currency: p.currency,
        }));
        // BRD §3 Overview (BR05: values match the sum of displayed rows). Total Sales Transactions is
        // the period order count from the (cancelled-excluded) transactions overview.
        setOverview({
          totalProducts: rows.length,
          totalSales: rows.reduce((s, r) => s + r.gross, 0),
          totalSalesTransactions: ov?.totalTransactions ?? 0,
          totalProductsSold: rows.reduce((s, r) => s + r.unitsSold, 0),
          currency: rows[0]?.currency ?? "",
        });
        return rows;
      }

      const { rows } = await fetchSalesReport(tenantId, apiFilters, dimension, authToken);
      // Overview is a Product-report concern (BRD §3); clear it for Category/Brand.
      setOverview(null);
      return rows.map((r: SalesReportRow, i) => ({ ...r, id: `${r.key}-${i}` }));
    },
    [tenantId, storeId, authToken, dimension],
  );

  const headerExtras = (
    <select
      className={inputClass}
      value={dimension}
      onChange={(e) => setDimension(e.target.value as SalesDimension)}
      aria-label="Report dimension"
    >
      {DIMENSION_OPTIONS.map((o) => (
        <option key={o.value} value={o.value}>
          Sales by: {o.label}
        </option>
      ))}
    </select>
  );

  // BRD §3 Overview Section cards (BR05: values match the sum of displayed rows).
  const overviewContent =
    dimension === "product" && overview ? (
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        {[
          { label: "Total Products", value: int(overview.totalProducts) },
          { label: "Total Sales", value: money(overview.totalSales) },
          { label: "Total Sales Transactions", value: int(overview.totalSalesTransactions) },
          { label: "Total Products Sold", value: int(overview.totalProductsSold) },
        ].map((m) => (
          <div key={m.label} className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-4 shadow-sm">
            <p className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">{m.label}</p>
            <p className="mt-1 font-headline text-lg font-bold tabular-nums text-on-surface">{m.value}</p>
          </div>
        ))}
      </div>
    ) : undefined;

  // Per-dimension export headers/rows (BRD Common Export — respects active filters, matches on-screen values).
  const exportConfig = useMemo(() => {
    if (dimension === "category") {
      return {
        headers: ["Category", "Current No. of Products", "Total Transactions", "Products Sold", "Total Category Sales"],
        toRow: (r: UiRow) => [
          r.name ?? r.key,
          r.productsCount == null ? "" : String(r.productsCount),
          r.transactions == null ? "" : String(r.transactions),
          String(r.unitsSold),
          money(r.gross),
        ],
      };
    }
    if (dimension === "brand") {
      return {
        headers: ["Brand", "Current No. of Products", "Total Transactions", "Total Products Sold", "Total Brand Sales"],
        toRow: (r: UiRow) => [
          r.name ?? r.key,
          r.productsCount == null ? "" : String(r.productsCount),
          r.transactions == null ? "" : String(r.transactions),
          String(r.unitsSold),
          money(r.gross),
        ],
      };
    }
    return {
      headers: ["UPC", "SKU", "Product Name", "Total Products Sold", "Total Product Sales"],
      toRow: (r: UiRow) => [r.upc ?? "", r.sku ?? "", r.name ?? r.key, String(r.unitsSold), money(r.gross)],
    };
  }, [dimension]);

  return (
    <ReportView<UiRow>
      breadcrumb="Sales"
      title="Sales report"
      description="Sales performance by Category, Brand or Product over the receipt-date range (Sales Reports BRD). Sales by Product reads the Order DB (real product names, cancelled/refunded orders excluded); Category/Brand use the analytics projection. Columns marked “—” aren’t carried by the source yet."
      exportSheetName="Sales"
      exportFilename={`sales-${dimension}.xlsx`}
      columns={columns}
      exportHeaders={exportConfig.headers}
      toExportRow={exportConfig.toRow}
      loadData={loadData}
      headerExtras={headerExtras}
      overviewContent={overviewContent}
      onReset={() => setOverview(null)}
      emptyMessage={
        dimension === "brand"
          ? "Sales by Brand ships ahead of its analytics projection — no brand dimension is populated yet (BRD §2)."
          : "No sales match the current filters. Adjust the receipt-date range and click Search."
      }
    />
  );
}

// Re-export so legacy callers don't break (file used to export ReportFilterField as a value).
export { ReportFilterField };
