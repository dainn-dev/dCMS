import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconDownload } from "../../orders/icons";
import { exportRowsByColumns, type ExportColumn } from "../shared/exportReportRowsToXlsx";
import { ReportFilterField, ReportFilterPanel, inputClass } from "../shared/ReportFilterPanel";
import { useReportExportState } from "../shared/useReportExport";
import {
  fetchTransactionDetails,
  fetchTransactionsOverview,
  type TransactionDetailRow as ApiDetailRow,
  type TransactionsOverviewRow,
} from "../api/reportsApi";

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

const PAYMENT_TYPE_OPTIONS = [
  { value: "all", label: "All types" },
  { value: "Gateway", label: "Gateway" },
  { value: "Voucher", label: "Voucher" },
  { value: "GiftCard", label: "Gift card" },
  { value: "LoyaltyPoints", label: "Loyalty points" },
];

const COUNTRY_OPTIONS = [
  { value: "all", label: "All countries" },
  { value: "SG", label: "Singapore" },
  { value: "MY", label: "Malaysia" },
  { value: "TH", label: "Thailand" },
  { value: "VN", label: "Vietnam" },
  { value: "ID", label: "Indonesia" },
];

type TransactionRow = {
  id: string;
  receiptNumber: string;
  orderId: string;
  orderIdShort: string;
  date: string;
  source: string;
  transactionType: string;
  store: string;
  brandCode: string;
  customerName: string;
  customerEmail: string;
  member: string;
  membershipType: string;
  membershipTier: string;
  campaigns: string;
  orderPromoCode: string;
  itemPromoCodes: string;
  rebatesCode: string;
  paymentType: string;
  paymentMethod: string;
  amount: string;
  taxAmount: string;
  shippingFee: string;
  orderDiscount: string;
  rebatesAmount: string;
  pointsEarned: string;
  pointsRedeemed: string;
  status: string;
  affiliateId: string;
  billingCountry: string;
  userAgent: string;
  browser: string;
  os: string;
  application: string;
};

const PENDING = "—";

function formatAmount(n: number): string {
  return n.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function mapApiDetail(r: ApiDetailRow): TransactionRow {
  return {
    id: r.orderId,
    receiptNumber: r.receiptNumber ?? PENDING,
    orderId: r.orderId,
    orderIdShort: r.orderId.substring(0, 12).toUpperCase(),
    date: (r.date ?? "").substring(0, 10),
    source: PENDING,
    transactionType: PENDING,
    store: r.store,
    brandCode: PENDING,
    customerName: r.customerName ?? PENDING,
    customerEmail: r.customerEmail ?? PENDING,
    member: r.member,
    membershipType: PENDING,
    membershipTier: PENDING,
    campaigns: r.campaigns?.join(", ") || PENDING,
    orderPromoCode: r.orderPromoCode ?? PENDING,
    itemPromoCodes: r.itemPromoCodes?.join(", ") || PENDING,
    rebatesCode: PENDING,
    paymentType: r.paymentType || PENDING,
    paymentMethod: PENDING,
    amount: formatAmount(r.amount),
    taxAmount: formatAmount(r.taxAmount),
    shippingFee: PENDING,
    orderDiscount: formatAmount(r.orderDiscount),
    rebatesAmount: PENDING,
    pointsEarned: PENDING,
    pointsRedeemed: PENDING,
    status: r.status,
    affiliateId: PENDING,
    billingCountry: r.billingCountry ?? PENDING,
    userAgent: PENDING,
    browser: PENDING,
    os: PENDING,
    application: PENDING,
  };
}

const txt =
  (key: keyof TransactionRow) =>
  ({ row }: { row: { getValue: (k: string) => unknown } }) => {
    const v = row.getValue(key as string);
    return <span className="whitespace-nowrap text-xs text-on-surface-variant">{String(v ?? "")}</span>;
  };

const num =
  (key: keyof TransactionRow) =>
  ({ row }: { row: { getValue: (k: string) => unknown } }) => (
    <span className="tabular-nums whitespace-nowrap text-xs">{String(row.getValue(key as string) ?? "")}</span>
  );

const PENDING_KEYS: ReadonlySet<keyof TransactionRow> = new Set<keyof TransactionRow>([
  "source",
  "transactionType",
  "brandCode",
  "membershipType",
  "membershipTier",
  "rebatesCode",
  "paymentMethod",
  "shippingFee",
  "rebatesAmount",
  "pointsEarned",
  "pointsRedeemed",
  "affiliateId",
  "userAgent",
  "browser",
  "os",
  "application",
  "receiptNumber",
]);

function makeColumn(key: keyof TransactionRow, header: string, kind: "text" | "num" = "text"): ColumnDef<TransactionRow> {
  return {
    accessorKey: key,
    header,
    cell: kind === "num" ? num(key) : txt(key),
    meta: PENDING_KEYS.has(key) ? { dataPending: true } : undefined,
  };
}

const COLUMNS: ColumnDef<TransactionRow>[] = [
  makeColumn("receiptNumber", "Receipt #"),
  {
    accessorKey: "orderIdShort",
    header: "Order ID",
    cell: ({ row }) => <span className="font-mono text-xs font-bold">{row.getValue("orderIdShort") as string}</span>,
  },
  makeColumn("date", "Date"),
  makeColumn("source", "Source"),
  makeColumn("transactionType", "Trans. type"),
  makeColumn("store", "Store"),
  makeColumn("brandCode", "Brand"),
  makeColumn("customerName", "Customer name"),
  makeColumn("customerEmail", "Email"),
  makeColumn("member", "Member"),
  makeColumn("membershipType", "Mem. type"),
  makeColumn("membershipTier", "Mem. tier"),
  makeColumn("campaigns", "Campaigns"),
  makeColumn("orderPromoCode", "Order promo"),
  makeColumn("itemPromoCodes", "Item promos"),
  makeColumn("rebatesCode", "Rebates code"),
  makeColumn("paymentType", "Payment type"),
  makeColumn("paymentMethod", "Payment method"),
  makeColumn("amount", "Amount", "num"),
  makeColumn("taxAmount", "Tax", "num"),
  makeColumn("shippingFee", "Shipping", "num"),
  makeColumn("orderDiscount", "Discount", "num"),
  makeColumn("rebatesAmount", "Rebate amt", "num"),
  makeColumn("pointsEarned", "Pts earned", "num"),
  makeColumn("pointsRedeemed", "Pts redeemed", "num"),
  makeColumn("status", "Status"),
  makeColumn("affiliateId", "Affiliate"),
  makeColumn("billingCountry", "Country"),
  makeColumn("browser", "Browser"),
  makeColumn("os", "OS"),
  makeColumn("application", "App"),
];

const EXPORT_COLUMNS: ExportColumn<TransactionRow>[] = COLUMNS
  .filter((c): c is ColumnDef<TransactionRow> & { accessorKey: keyof TransactionRow; header: string } =>
    typeof (c as { accessorKey?: unknown }).accessorKey === "string" && typeof c.header === "string")
  .map((c) => ({
    header: c.header,
    value: (row: TransactionRow) => row[c.accessorKey],
  }));

type TransactionReportPageProps = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

const TODAY_FROM = "2026-04-01";
const TODAY_TO = "2026-04-29";

export function TransactionReportPage({ tenantId, storeId, authToken }: TransactionReportPageProps) {
  const [dateFrom, setDateFrom] = useState(TODAY_FROM);
  const [dateTo, setDateTo] = useState(TODAY_TO);
  const [storeScope, setStoreScope] = useState("all");
  const [brandScope, setBrandScope] = useState("all");
  const [memberQuery, setMemberQuery] = useState("");
  const [orderPromoCode, setOrderPromoCode] = useState("");
  const [itemPromoCode, setItemPromoCode] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("all");
  const [paymentType, setPaymentType] = useState("all");
  const [billingCountry, setBillingCountry] = useState("all");
  const [receiptNumber, setReceiptNumber] = useState("");

  const [rows, setRows] = useState<TransactionRow[]>([]);
  const [overview, setOverview] = useState<TransactionsOverviewRow | null>(null);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { exportDisabled } = useReportExportState(loading, rows.length);

  const handleSearch = useCallback(async () => {
    if (!tenantId) {
      setError("Missing tenant context. Please reload the backoffice.");
      setRows([]);
      setOverview(null);
      setHasSearched(true);
      return;
    }
    if (dateFrom > dateTo) {
      setRows([]);
      setOverview(null);
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
      paymentMethod,
      paymentType,
      billingCountry,
      orderPromoCode: orderPromoCode || undefined,
      itemPromoCode: itemPromoCode || undefined,
    };
    try {
      const [details, ov] = await Promise.all([
        fetchTransactionDetails(tenantId, filters, { limit: 100 }, authToken),
        fetchTransactionsOverview(tenantId, filters, authToken),
      ]);
      setRows(details.rows.map(mapApiDetail));
      setOverview(ov);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load report data");
      setRows([]);
      setOverview(null);
    }

    setLoading(false);
  }, [
    authToken,
    billingCountry,
    brandScope,
    dateFrom,
    dateTo,
    itemPromoCode,
    memberQuery,
    orderPromoCode,
    paymentMethod,
    paymentType,
    storeId,
    storeScope,
    tenantId,
  ]);

  const handleReset = useCallback(() => {
    setDateFrom(TODAY_FROM);
    setDateTo(TODAY_TO);
    setStoreScope("all");
    setBrandScope("all");
    setMemberQuery("");
    setOrderPromoCode("");
    setItemPromoCode("");
    setPaymentMethod("all");
    setPaymentType("all");
    setBillingCountry("all");
    setReceiptNumber("");
    setRows([]);
    setOverview(null);
    setHasSearched(false);
  }, []);

  const handleExport = useCallback(async () => {
    if (rows.length === 0) return;
    await exportRowsByColumns("Transactions", `transactions-${dateFrom}_${dateTo}.xlsx`, EXPORT_COLUMNS, rows);
  }, [rows, dateFrom, dateTo]);

  const overviewMetrics = useMemo(() => {
    if (!overview) return [];
    const fmt = (n: number) => n.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const fmtInt = (n: number) => n.toLocaleString("en-US");
    return [
      { label: "Total transactions", value: fmtInt(overview.totalTransactions) },
      { label: "Total amount", value: `$${fmt(overview.totalAmount)}` },
      { label: "Total tax", value: `$${fmt(overview.totalTax)}` },
      { label: "Total discount", value: `$${fmt(overview.totalDiscount)}` },
      { label: "Unique customers", value: fmtInt(overview.uniqueCustomers) },
      { label: "Avg order value", value: `$${fmt(overview.averageOrderValue)}` },
      {
        label: `Avg / month (${overview.distinctMonths})`,
        value: `$${fmt(overview.averageMonthlyAmount)}`,
      },
      {
        label: `Avg / day (${overview.distinctDays})`,
        value: `$${fmt(overview.averageDailyAmount)}`,
      },
      {
        label: "Top day",
        value: overview.topDayDate ? overview.topDayDate.substring(0, 10) : "—",
      },
      {
        label: "Top day amount",
        value: overview.topDayAmount !== null ? `$${fmt(overview.topDayAmount)}` : "—",
      },
      {
        label: "Top day txns",
        value: overview.topDayTransactions !== null ? fmtInt(overview.topDayTransactions) : "—",
      },
      {
        label: "Date range",
        value: `${dateFrom} → ${dateTo}`,
      },
    ];
  }, [overview, dateFrom, dateTo]);

  const emptyMessage =
    dateFrom > dateTo
      ? "Invalid date range: From is after To."
      : "No rows match the current filters. Adjust filters and click Search.";

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Transaction report">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>Reports</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Transaction</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Transaction report</h1>
          <p className="max-w-3xl text-sm text-on-surface-variant">
            Single flat view of every transaction — receipt, customer, payment composition, promos and rebates. Columns
            marked “—” are persisted in upcoming releases (storefront telemetry, receipt sequence, loyalty membership).
          </p>
          <p className="text-xs font-semibold uppercase tracking-wider text-primary">
            {tenantId ? `Tenant: ${tenantId}` : "Tenant context unavailable"}
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
          <ReportFilterField label="Brand" htmlFor="tx-brand">
            <select id="tx-brand" className={inputClass} value={brandScope} onChange={(e) => setBrandScope(e.target.value)}>
              {BRAND_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
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
          <ReportFilterField label="Order promo" htmlFor="tx-orderpromo">
            <input
              id="tx-orderpromo"
              type="search"
              placeholder="Promo code"
              className={inputClass}
              value={orderPromoCode}
              onChange={(e) => setOrderPromoCode(e.target.value)}
            />
          </ReportFilterField>
          <ReportFilterField label="Item promo" htmlFor="tx-itempromo">
            <input
              id="tx-itempromo"
              type="search"
              placeholder="Promo code"
              className={inputClass}
              value={itemPromoCode}
              onChange={(e) => setItemPromoCode(e.target.value)}
            />
          </ReportFilterField>
          <ReportFilterField label="Payment method" htmlFor="tx-pay">
            <select id="tx-pay" className={inputClass} value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)}>
              {PAYMENT_METHOD_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
          <ReportFilterField label="Payment type" htmlFor="tx-paytype">
            <select id="tx-paytype" className={inputClass} value={paymentType} onChange={(e) => setPaymentType(e.target.value)}>
              {PAYMENT_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
          <ReportFilterField label="Country" htmlFor="tx-country">
            <select id="tx-country" className={inputClass} value={billingCountry} onChange={(e) => setBillingCountry(e.target.value)}>
              {COUNTRY_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
          <ReportFilterField label="Receipt #" htmlFor="tx-receipt">
            <input
              id="tx-receipt"
              type="search"
              placeholder="Available after PR2"
              className={`${inputClass} cursor-not-allowed opacity-50`}
              value={receiptNumber}
              onChange={(e) => setReceiptNumber(e.target.value)}
              disabled
              title="Receipt number filter is enabled after the PR2 receipt-sequence rollout."
            />
          </ReportFilterField>
        </ReportFilterPanel>

        {error && (
          <div className="rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-800">
            {error}
          </div>
        )}

        {!loading && hasSearched && overview && rows.length > 0 && (
          <div className="grid gap-3 md:grid-cols-3 lg:grid-cols-4">
            {overviewMetrics.map((m) => (
              <div
                key={m.label}
                className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-4 shadow-sm"
              >
                <p className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">{m.label}</p>
                <p className="mt-1 font-headline text-lg font-bold tabular-nums text-on-surface">{m.value}</p>
              </div>
            ))}
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

        {!loading && hasSearched && rows.length === 0 && (
          <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-outline-variant/30 bg-surface-container-lowest py-16 px-6 text-center">
            <p className="text-sm font-semibold text-on-surface">No results</p>
            <p className="mt-2 max-w-md text-xs text-on-surface-variant">{emptyMessage}</p>
          </div>
        )}

        {!loading && rows.length > 0 && (
          <DataTable
            columns={COLUMNS}
            data={rows}
            getRowId={(row) => row.id}
            globalFilterPlaceholder="Search by order, member, store, promo…"
          />
        )}

        {!loading && !hasSearched && (
          <p className="rounded-xl border border-outline-variant/15 bg-surface-container-low/50 px-4 py-3 text-center text-xs text-on-surface-variant">
            Choose filters and click <span className="font-bold text-on-surface">Search</span> to load transactions.
          </p>
        )}
      </div>
    </div>
  );
}
