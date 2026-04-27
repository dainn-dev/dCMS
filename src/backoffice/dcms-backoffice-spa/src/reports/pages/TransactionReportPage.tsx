import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconDownload } from "../../orders/icons";
import { exportReportRowsToXlsx } from "../shared/exportReportRowsToXlsx";
import { ReportFilterField, ReportFilterPanel, inputClass } from "../shared/ReportFilterPanel";
import { useReportExportState } from "../shared/useReportExport";
import {
  fetchTransactionSummary,
  fetchTransactionDetails,
  fetchEcommercePayments,
  type TransactionSummaryRow as ApiSummaryRow,
  type TransactionDetailRow as ApiDetailRow,
  type EcommercePaymentRow as ApiEcommerceRow,
} from "../api/reportsApi";

type ReportTab = "summary" | "details" | "ecommerce";

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

const PAYMENT_METHOD_OPTIONS = [
  { value: "all", label: "All methods" },
  { value: "Visa", label: "Visa" },
  { value: "Mastercard", label: "Mastercard" },
  { value: "PayNow", label: "PayNow" },
  { value: "PayPal", label: "PayPal" },
];

type SummaryRow = {
  id: string;
  paymentMethod: string;
  transactionCount: number;
  totalAmount: string;
};

type DetailsRow = {
  id: string;
  orderId: string;
  date: string;
  member: string;
  store: string;
  brandCode: string;
  paymentMethod: string;
  amount: string;
  status: string;
};

type EcommerceRow = {
  id: string;
  orderId: string;
  paymentMethod: string;
  amount: string;
  transactionRef: string;
  date: string;
};

function filterSummaryByBrand(rows: SummaryRow[], brand: string): SummaryRow[] {
  return brand === "all" ? rows : rows;
}

const MOCK_SUMMARY: SummaryRow[] = [
  { id: "s1", paymentMethod: "Visa", transactionCount: 842, totalAmount: "128,940.50" },
  { id: "s2", paymentMethod: "Mastercard", transactionCount: 310, totalAmount: "44,120.00" },
  { id: "s3", paymentMethod: "PayNow", transactionCount: 156, totalAmount: "19,880.25" },
  { id: "s4", paymentMethod: "PayPal", transactionCount: 88, totalAmount: "12,305.00" },
];

const MOCK_DETAILS: DetailsRow[] = [
  {
    id: "d1",
    orderId: "ORD-10021",
    date: "2026-04-02",
    member: "member-alpha@example.com",
    store: "SG — Flagship",
    brandCode: "CAS-7721",
    paymentMethod: "Visa",
    amount: "128.40",
    status: "Completed",
  },
  {
    id: "d2",
    orderId: "ORD-10022",
    date: "2026-04-03",
    member: "beta.user@example.com",
    store: "SG — Flagship",
    brandCode: "VEL-4490",
    paymentMethod: "PayNow",
    amount: "59.00",
    status: "Completed",
  },
  {
    id: "d3",
    orderId: "ORD-10028",
    date: "2026-04-05",
    member: "member-alpha@example.com",
    store: "MY — Central",
    brandCode: "AUR-5501",
    paymentMethod: "Mastercard",
    amount: "902.15",
    status: "Pending payment",
  },
  {
    id: "d4",
    orderId: "ORD-10031",
    date: "2026-04-06",
    member: "gamma@example.com",
    store: "MY — Central",
    brandCode: "CAS-7721",
    paymentMethod: "PayPal",
    amount: "45.00",
    status: "Completed",
  },
];

/** Same order can appear twice when customer uses two payment methods (7.1.3). */
const MOCK_ECOMMERCE: EcommerceRow[] = [
  {
    id: "e1",
    orderId: "ORD-10400",
    paymentMethod: "Visa",
    amount: "80.00",
    transactionRef: "TXN-V-88421",
    date: "2026-04-04 11:02",
  },
  {
    id: "e2",
    orderId: "ORD-10400",
    paymentMethod: "PayNow",
    amount: "40.00",
    transactionRef: "TXN-PN-88422",
    date: "2026-04-04 11:03",
  },
  {
    id: "e3",
    orderId: "ORD-10405",
    paymentMethod: "Mastercard",
    amount: "210.50",
    transactionRef: "TXN-M-88430",
    date: "2026-04-05 09:15",
  },
  {
    id: "e4",
    orderId: "ORD-10408",
    paymentMethod: "PayPal",
    amount: "33.00",
    transactionRef: "TXN-PP-88441",
    date: "2026-04-06 16:40",
  },
];

const summaryColumns: ColumnDef<SummaryRow>[] = [
  {
    accessorKey: "paymentMethod",
    header: "Payment method",
    cell: ({ row }) => <span className="text-xs font-bold text-on-surface">{row.getValue("paymentMethod")}</span>,
  },
  {
    accessorKey: "transactionCount",
    header: "Number of transactions",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("transactionCount")}</span>,
  },
  {
    accessorKey: "totalAmount",
    header: "Total amount",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("totalAmount")}</span>,
  },
];

const detailsColumns: ColumnDef<DetailsRow>[] = [
  {
    accessorKey: "orderId",
    header: "Order ID",
    cell: ({ row }) => <span className="font-mono text-xs font-bold">{row.getValue("orderId")}</span>,
  },
  { accessorKey: "date", header: "Date", cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("date")}</span> },
  { accessorKey: "member", header: "Member", cell: ({ row }) => <span className="text-xs">{row.getValue("member")}</span> },
  { accessorKey: "store", header: "Store", cell: ({ row }) => <span className="text-xs">{row.getValue("store")}</span> },
  { accessorKey: "brandCode", header: "Brand", cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("brandCode")}</span> },
  { accessorKey: "paymentMethod", header: "Payment", cell: ({ row }) => <span className="text-xs">{row.getValue("paymentMethod")}</span> },
  { accessorKey: "amount", header: "Amount", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("amount")}</span> },
  { accessorKey: "status", header: "Status", cell: ({ row }) => <span className="text-xs">{row.getValue("status")}</span> },
];

const ecommerceColumns: ColumnDef<EcommerceRow>[] = [
  {
    accessorKey: "orderId",
    header: "Order ID",
    cell: ({ row }) => <span className="font-mono text-xs font-bold">{row.getValue("orderId")}</span>,
  },
  { accessorKey: "paymentMethod", header: "Payment method", cell: ({ row }) => <span className="text-xs">{row.getValue("paymentMethod")}</span> },
  { accessorKey: "amount", header: "Amount", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("amount")}</span> },
  {
    accessorKey: "transactionRef",
    header: "Transaction ref",
    cell: ({ row }) => <span className="font-mono text-xs text-on-surface-variant">{row.getValue("transactionRef")}</span>,
  },
  { accessorKey: "date", header: "Date", cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("date")}</span> },
];

const TAB_LABELS: Record<ReportTab, { title: string }> = {
  summary: { title: "Transaction summary" },
  details: { title: "Transaction details" },
  ecommerce: { title: "Ecommerce payments" },
};

function filterByBrand<T extends { brandCode?: string }>(rows: T[], brand: string): T[] {
  if (brand === "all") return rows;
  return rows.filter((r) => r.brandCode === brand);
}

function filterDetails(rows: DetailsRow[], memberQuery: string, brand: string, store: string): DetailsRow[] {
  let out = filterByBrand(rows, brand);
  if (store !== "all") {
    const label = STORE_OPTIONS.find((o) => o.value === store)?.label;
    if (label) out = out.filter((r) => r.store === label);
  }
  const q = memberQuery.trim().toLowerCase();
  if (q) out = out.filter((r) => r.member.toLowerCase().includes(q));
  return out;
}

function filterEcommerce(rows: EcommerceRow[], method: string): EcommerceRow[] {
  if (method === "all") return rows;
  return rows.filter((r) => r.paymentMethod === method);
}

function applySummaryDemoFilters(rows: SummaryRow[], store: string, brand: string): SummaryRow[] {
  const storeFactor = store === "all" ? 1 : 0.52;
  let out = rows.map((r) => ({
    ...r,
    transactionCount: Math.max(1, Math.floor(r.transactionCount * storeFactor)),
    totalAmount: (parseFloat(r.totalAmount.replace(/,/g, "")) * storeFactor).toLocaleString("en-US", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }),
  }));
  if (brand !== "all") {
    out = out.map((r) => ({
      ...r,
      transactionCount: Math.max(1, Math.floor(r.transactionCount * 0.38)),
      totalAmount: (parseFloat(String(r.totalAmount).replace(/,/g, "")) * 0.38).toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }),
    }));
  }
  return out;
}

type TransactionReportPageProps = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

function formatAmount(n: number): string {
  return n.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function mapApiSummary(r: ApiSummaryRow): SummaryRow {
  return {
    id: `${r.paymentMethod}-${r.currency}`,
    paymentMethod: r.paymentMethod,
    transactionCount: r.transactionCount,
    totalAmount: formatAmount(r.totalAmount),
  };
}

function mapApiDetail(r: ApiDetailRow): DetailsRow {
  return {
    id: r.orderId,
    orderId: r.orderId.substring(0, 12).toUpperCase(),
    date: r.date.substring(0, 10),
    member: r.member,
    store: r.store,
    brandCode: "",
    paymentMethod: "",
    amount: formatAmount(r.amount),
    status: r.status,
  };
}

function mapApiEcommerce(r: ApiEcommerceRow): EcommerceRow {
  return {
    id: `${r.orderId}-${r.transactionRef}`,
    orderId: r.orderId.substring(0, 12).toUpperCase(),
    paymentMethod: r.paymentMethod,
    amount: formatAmount(r.amount),
    transactionRef: r.transactionRef,
    date: r.date.substring(0, 16).replace("T", " "),
  };
}

export function TransactionReportPage({ tenantId, storeId, authToken }: TransactionReportPageProps) {
  const [tab, setTab] = useState<ReportTab>("summary");
  const [dateFrom, setDateFrom] = useState("2026-04-01");
  const [dateTo, setDateTo] = useState("2026-04-18");
  const [storeScope, setStoreScope] = useState("all");
  const [brandScope, setBrandScope] = useState("all");
  const [memberQuery, setMemberQuery] = useState("");
  const [paymentMethodFilter, setPaymentMethodFilter] = useState("all");

  const [summaryRows, setSummaryRows] = useState<SummaryRow[]>([]);
  const [detailsRows, setDetailsRows] = useState<DetailsRow[]>([]);
  const [ecommerceRows, setEcommerceRows] = useState<EcommerceRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  const activeRows = tab === "summary" ? summaryRows : tab === "details" ? detailsRows : ecommerceRows;
  const { exportDisabled } = useReportExportState(loading, activeRows.length);

  const summaryMetrics = useMemo(() => {
    if (summaryRows.length === 0) return null;
    const totalCount = summaryRows.reduce((a, r) => a + r.transactionCount, 0);
    const totalAmt = summaryRows.reduce((a, r) => a + parseFloat(String(r.totalAmount).replace(/,/g, "")), 0);
    return {
      totalCount,
      totalAmount: totalAmt.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
      methodCount: summaryRows.length,
    };
  }, [summaryRows]);

  const [error, setError] = useState<string | null>(null);

  const handleSearch = useCallback(async () => {
    if (dateFrom > dateTo) {
      setSummaryRows([]);
      setDetailsRows([]);
      setEcommerceRows([]);
      setHasSearched(true);
      return;
    }
    setLoading(true);
    setHasSearched(true);
    setError(null);

    if (tenantId) {
      const filters = { dateFrom, dateTo, storeId: storeScope !== "all" ? storeScope : storeId, brandCode: brandScope, memberQuery, paymentMethod: paymentMethodFilter };
      try {
        const [summary, details, ecommerce] = await Promise.all([
          fetchTransactionSummary(tenantId, filters, authToken),
          fetchTransactionDetails(tenantId, filters, { limit: 100 }, authToken),
          fetchEcommercePayments(tenantId, filters, authToken),
        ]);
        setSummaryRows(summary.map(mapApiSummary));
        setDetailsRows(details.rows.map(mapApiDetail));
        setEcommerceRows(ecommerce.map(mapApiEcommerce));
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load report data");
        setSummaryRows([]);
        setDetailsRows([]);
        setEcommerceRows([]);
      }
    } else {
      await new Promise((r) => setTimeout(r, 500));
      setSummaryRows(filterSummaryByBrand([...MOCK_SUMMARY], brandScope));
      setDetailsRows(filterDetails([...MOCK_DETAILS], memberQuery, brandScope, storeScope));
      setEcommerceRows(filterEcommerce([...MOCK_ECOMMERCE], paymentMethodFilter));
    }

    setLoading(false);
  }, [brandScope, dateFrom, dateTo, memberQuery, paymentMethodFilter, storeScope, tenantId, storeId, authToken]);

  const handleReset = useCallback(() => {
    setDateFrom("2026-04-01");
    setDateTo("2026-04-18");
    setStoreScope("all");
    setBrandScope("all");
    setMemberQuery("");
    setPaymentMethodFilter("all");
    setSummaryRows([]);
    setDetailsRows([]);
    setEcommerceRows([]);
    setHasSearched(false);
  }, []);

  const handleExport = useCallback(async () => {
    if (tab === "summary" && summaryRows.length > 0) {
      await exportReportRowsToXlsx(
        "TransactionSummary",
        "transaction-summary-7-1-1.xlsx",
        ["Payment method", "Number of transactions", "Total amount"],
        summaryRows.map((r) => [r.paymentMethod, String(r.transactionCount), r.totalAmount])
      );
    } else if (tab === "details" && detailsRows.length > 0) {
      await exportReportRowsToXlsx(
        "TransactionDetails",
        "transaction-details-7-1-2.xlsx",
        ["Order ID", "Date", "Member", "Store", "Brand", "Payment method", "Amount", "Status"],
        detailsRows.map((r) => [r.orderId, r.date, r.member, r.store, r.brandCode, r.paymentMethod, r.amount, r.status])
      );
    } else if (tab === "ecommerce" && ecommerceRows.length > 0) {
      await exportReportRowsToXlsx(
        "EcommercePayments",
        "ecommerce-payments-7-1-3.xlsx",
        ["Order ID", "Payment method", "Amount", "Transaction ref", "Date"],
        ecommerceRows.map((r) => [r.orderId, r.paymentMethod, r.amount, r.transactionRef, r.date])
      );
    }
  }, [detailsRows, ecommerceRows, summaryRows, tab]);

  const emptyMessage =
    dateFrom > dateTo
      ? "Invalid date range: From is after To."
      : "No rows match the current filters. Adjust filters and click Search.";

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Transaction reports">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>Reports</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Transaction</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Transaction reports</h1>
          <p className="max-w-3xl text-sm text-on-surface-variant">
            Summary, detailed order lines, and ecommerce payment rows (including split tender per order). Demo data is tenant-scoped to the current backoffice context.
          </p>
          <p className="text-xs font-semibold uppercase tracking-wider text-primary">
            {tenantId ? `Tenant: ${tenantId}` : "Current tenant: Demo supermarket (mock)"}
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
        <div className="flex flex-wrap gap-1" role="tablist" aria-label="Transaction report type">
          {(Object.keys(TAB_LABELS) as ReportTab[]).map((id) => {
            const { title } = TAB_LABELS[id];
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
              </button>
            );
          })}
        </div>
      </div>

      <div className="w-full flex-1 space-y-6 p-6">
        <ReportFilterPanel onSearch={() => void handleSearch()} onReset={handleReset} searchDisabled={loading}>
          <ReportFilterField label="From" htmlFor="tx-from">
            <input id="tx-from" type="date" className={inputClass} value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
          </ReportFilterField>
          <ReportFilterField label="To" htmlFor="tx-to">
            <input id="tx-to" type="date" className={inputClass} value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
          </ReportFilterField>
          <ReportFilterField label="Store" htmlFor="tx-store">
            <select id="tx-store" className={inputClass} value={storeScope} onChange={(e) => setStoreScope(e.target.value)}>
              {STORE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
          {(tab === "summary" || tab === "details") && (
            <ReportFilterField label="Brand" htmlFor="tx-brand">
              <select id="tx-brand" className={inputClass} value={brandScope} onChange={(e) => setBrandScope(e.target.value)}>
                {BRAND_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </ReportFilterField>
          )}
          {tab === "details" && (
            <ReportFilterField label="Member contains" htmlFor="tx-member">
              <input
                id="tx-member"
                type="search"
                placeholder="Email or name fragment"
                className={inputClass}
                value={memberQuery}
                onChange={(e) => setMemberQuery(e.target.value)}
              />
            </ReportFilterField>
          )}
          {tab === "ecommerce" && (
            <ReportFilterField label="Payment method" htmlFor="tx-pay">
              <select id="tx-pay" className={inputClass} value={paymentMethodFilter} onChange={(e) => setPaymentMethodFilter(e.target.value)}>
                {PAYMENT_METHOD_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </ReportFilterField>
          )}
        </ReportFilterPanel>

        {error && (
          <div className="rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
            {error}
          </div>
        )}

        {tab === "summary" && !loading && hasSearched && summaryMetrics && summaryRows.length > 0 && (
          <div className="grid gap-4 md:grid-cols-3">
            <div className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-4 shadow-sm">
              <p className="text-xs font-bold uppercase tracking-wider text-on-surface-variant">Total transactions</p>
              <p className="mt-1 font-headline text-2xl font-bold tabular-nums text-on-surface">{summaryMetrics.totalCount}</p>
            </div>
            <div className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-4 shadow-sm">
              <p className="text-xs font-bold uppercase tracking-wider text-on-surface-variant">Total amount</p>
              <p className="mt-1 font-headline text-2xl font-bold tabular-nums text-primary">${summaryMetrics.totalAmount}</p>
            </div>
            <div className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-4 shadow-sm">
              <p className="text-xs font-bold uppercase tracking-wider text-on-surface-variant">Payment methods</p>
              <p className="mt-1 font-headline text-2xl font-bold tabular-nums text-on-surface">{summaryMetrics.methodCount}</p>
            </div>
          </div>
        )}

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

        {!loading && summaryRows.length > 0 && tab === "summary" && (
          <DataTable
            columns={summaryColumns}
            data={summaryRows}
            getRowId={(row) => row.id}
            globalFilterPlaceholder="Search in results…"
          />
        )}
        {!loading && detailsRows.length > 0 && tab === "details" && (
          <DataTable
            columns={detailsColumns}
            data={detailsRows}
            getRowId={(row) => row.id}
            globalFilterPlaceholder="Search by order, member, store…"
          />
        )}
        {!loading && ecommerceRows.length > 0 && tab === "ecommerce" && (
          <DataTable
            columns={ecommerceColumns}
            data={ecommerceRows}
            getRowId={(row) => row.id}
            globalFilterPlaceholder="Search by order, ref…"
          />
        )}

        {!loading && !hasSearched && (
          <p className="rounded-xl border border-outline-variant/15 bg-surface-container-low/50 px-4 py-3 text-center text-xs text-on-surface-variant">
            Choose filters and click <span className="font-bold text-on-surface">Search</span> to load{" "}
            <span className="font-semibold text-on-surface">{TAB_LABELS[tab].title}</span>.
          </p>
        )}

        {tab === "ecommerce" && hasSearched && ecommerceRows.length > 0 && (
          <p className="text-xs text-on-surface-variant">
            Note: orders paid with multiple methods appear as one row per tender (see duplicate order IDs in the demo).
          </p>
        )}
      </div>
    </div>
  );
}
