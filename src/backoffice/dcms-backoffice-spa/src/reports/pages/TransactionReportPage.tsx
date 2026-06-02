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

const TAB_LABELS: Record<ReportTab, { title: string }> = {
  summary: { title: "Transaction summary" },
  details: { title: "Transaction details" },
  ecommerce: { title: "Ecommerce payments" },
};

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
  {
    accessorKey: "date",
    header: "Date",
    cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("date")}</span>,
  },
  { accessorKey: "member", header: "Member", cell: ({ row }) => <span className="text-xs">{row.getValue("member")}</span> },
  { accessorKey: "store", header: "Store", cell: ({ row }) => <span className="text-xs">{row.getValue("store")}</span> },
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

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

const today = () => new Date().toISOString().slice(0, 10);
const sevenDaysAgo = () => new Date(Date.now() - 7 * 86400_000).toISOString().slice(0, 10);

export function TransactionReportPage({ tenantId, storeId, authToken }: Props) {
  const [tab, setTab] = useState<ReportTab>("summary");
  const [dateFrom, setDateFrom] = useState(sevenDaysAgo);
  const [dateTo, setDateTo] = useState(today);
  const [storeScope, setStoreScope] = useState("all");
  const [brandScope, setBrandScope] = useState("all");
  const [memberQuery, setMemberQuery] = useState("");
  const [paymentMethodFilter, setPaymentMethodFilter] = useState("all");

  const [summaryRows, setSummaryRows] = useState<SummaryRow[]>([]);
  const [detailsRows, setDetailsRows] = useState<DetailsRow[]>([]);
  const [ecommerceRows, setEcommerceRows] = useState<EcommerceRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const activeRows = tab === "summary" ? summaryRows : tab === "details" ? detailsRows : ecommerceRows;
  const { exportDisabled } = useReportExportState(loading, activeRows.length);

  const summaryMetrics = useMemo(() => {
    if (summaryRows.length === 0) return null;
    const totalCount = summaryRows.reduce((a, r) => a + r.transactionCount, 0);
    const totalAmt = summaryRows.reduce(
      (a, r) => a + parseFloat(String(r.totalAmount).replace(/,/g, "")),
      0,
    );
    return {
      totalCount,
      totalAmount: totalAmt.toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }),
      methodCount: summaryRows.length,
    };
  }, [summaryRows]);

  const handleSearch = useCallback(async () => {
    if (!tenantId) {
      setError("Missing tenant context — cannot load reports.");
      setSummaryRows([]);
      setDetailsRows([]);
      setEcommerceRows([]);
      setHasSearched(true);
      return;
    }
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

    const filters = {
      dateFrom,
      dateTo,
      storeId: storeScope !== "all" ? storeScope : storeId,
      brandCode: brandScope,
      memberQuery,
      paymentMethod: paymentMethodFilter,
    };

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
    } finally {
      setLoading(false);
    }
  }, [
    brandScope,
    dateFrom,
    dateTo,
    memberQuery,
    paymentMethodFilter,
    storeScope,
    tenantId,
    storeId,
    authToken,
  ]);

  const handleReset = useCallback(() => {
    setDateFrom(sevenDaysAgo());
    setDateTo(today());
    setStoreScope("all");
    setBrandScope("all");
    setMemberQuery("");
    setPaymentMethodFilter("all");
    setSummaryRows([]);
    setDetailsRows([]);
    setEcommerceRows([]);
    setHasSearched(false);
    setError(null);
  }, []);

  const handleExport = useCallback(async () => {
    if (tab === "summary" && summaryRows.length > 0) {
      await exportReportRowsToXlsx(
        "TransactionSummary",
        "transaction-summary-7-1-1.xlsx",
        ["Payment method", "Number of transactions", "Total amount"],
        summaryRows.map((r) => [r.paymentMethod, String(r.transactionCount), r.totalAmount]),
      );
    } else if (tab === "details" && detailsRows.length > 0) {
      await exportReportRowsToXlsx(
        "TransactionDetails",
        "transaction-details-7-1-2.xlsx",
        ["Order ID", "Date", "Member", "Store", "Amount", "Status"],
        detailsRows.map((r) => [r.orderId, r.date, r.member, r.store, r.amount, r.status]),
      );
    } else if (tab === "ecommerce" && ecommerceRows.length > 0) {
      await exportReportRowsToXlsx(
        "EcommercePayments",
        "ecommerce-payments-7-1-3.xlsx",
        ["Order ID", "Payment method", "Amount", "Transaction ref", "Date"],
        ecommerceRows.map((r) => [r.orderId, r.paymentMethod, r.amount, r.transactionRef, r.date]),
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
            Summary, detailed order lines, and ecommerce payment rows (including split tender per order).
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

        {!loading && hasSearched && activeRows.length === 0 && !error && (
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
            emptyMessage="No matching records."
            itemNoun="records"
          />
        )}
        {!loading && detailsRows.length > 0 && tab === "details" && (
          <DataTable
            columns={detailsColumns}
            data={detailsRows}
            getRowId={(row) => row.id}
            globalFilterPlaceholder="Search by order, member, store…"
            emptyMessage="No matching transactions."
            itemNoun="transactions"
          />
        )}
        {!loading && ecommerceRows.length > 0 && tab === "ecommerce" && (
          <DataTable
            columns={ecommerceColumns}
            data={ecommerceRows}
            getRowId={(row) => row.id}
            globalFilterPlaceholder="Search by order, ref…"
            emptyMessage="No matching orders."
            itemNoun="orders"
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
            Note: orders paid with multiple methods appear as one row per tender.
          </p>
        )}
      </div>
    </div>
  );
}
