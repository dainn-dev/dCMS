import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import { fetchEcommercePayments, type EcommercePaymentRow } from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";
import { ReportFilterField, inputClass } from "../shared/ReportFilterPanel";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = EcommercePaymentRow & { id: string };

// BRD §2.6 — gateway status codes mapped to the BRD's display labels. The dropdown sends the raw
// gateway value (what PaymentTransactions.Status stores); the cell renders the BRD label.
const STATUS_OPTIONS = [
  { value: "all", label: "All statuses" },
  { value: "succeeded", label: "Payment Received" },
  { value: "pending", label: "Pending Payment" },
  { value: "failed", label: "Failed" },
  { value: "cancelled", label: "Cancelled" },
  { value: "refunded", label: "Refunded" },
];

const STATUS_LABELS: Record<string, string> = {
  succeeded: "PAYMENT RECEIVED",
  completed: "PAYMENT RECEIVED",
  initiated: "PENDING PAYMENT",
  pending: "PENDING PAYMENT",
  failed: "FAILED",
  cancelled: "CANCELLED",
  refunded: "REFUNDED",
};
const statusLabel = (s: string) => STATUS_LABELS[s?.toLowerCase().trim()] ?? (s || "").toUpperCase();
const isReceived = (s: string) => ["succeeded", "completed"].includes(s?.toLowerCase().trim());

const CURRENCY_SYMBOL: Record<string, string> = { SGD: "S$", USD: "$", MYR: "RM", THB: "฿", VND: "₫", IDR: "Rp" };
const money = (value: number, currency: string) => {
  const symbol = CURRENCY_SYMBOL[currency] ?? `${currency} `;
  return `${symbol}${Number(value ?? 0).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
};

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
// BRD §4 "Payment Datetime" example: 04-Jun-2026 14:45:39 (rendered in the browser's local time).
const paymentDateTime = (iso: string) => {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const p = (n: number) => String(n).padStart(2, "0");
  return `${p(d.getDate())}-${MONTHS[d.getMonth()]}-${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
};
const dash = (v: unknown) => (v == null || v === "" ? "—" : String(v));

/**
 * Ecommerce Payments report (BRD §1–§10). Reads GET /orders/reports/transactions/ecommerce.
 * One row per payment transaction event (BR01), every status incl. cancelled/refunded (BR07).
 * Card numbers are always masked (BR03) — not stored yet, shown as "—".
 */
export function EcommercePaymentsReportPage({ tenantId, storeId, authToken }: Props) {
  // BRD §2 search criteria (outside ReportView's built-in payment-date range).
  const [orderNumber, setOrderNumber] = useState("");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [gatewayName, setGatewayName] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("");
  const [paymentStatus, setPaymentStatus] = useState("all");
  const [amountMin, setAmountMin] = useState("");
  const [amountMax, setAmountMax] = useState("");
  const [rows, setRows] = useState<UiRow[]>([]);

  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "orderId",
        header: "Order Number",
        cell: ({ row }) => <span className="font-mono text-xs font-semibold">{row.original.orderId.substring(0, 12).toUpperCase()}</span>,
      },
      {
        accessorKey: "referenceNumber",
        header: "Reference Number",
        cell: ({ row }) => <span className="font-mono text-xs">{dash(row.original.referenceNumber)}</span>,
      },
      { accessorKey: "gatewayName", header: "Gateway Name", cell: ({ row }) => <span className="text-xs">{dash(row.original.gatewayName)}</span> },
      { accessorKey: "paymentMethod", header: "Payment Method", cell: ({ row }) => <span className="text-xs">{dash(row.original.paymentMethod)}</span> },
      {
        accessorKey: "paymentStatus",
        header: "Payment Status",
        cell: ({ row }) => {
          const s = row.original.paymentStatus;
          const received = isReceived(s);
          const refunded = s?.toLowerCase().trim() === "refunded";
          const failedish = ["failed", "cancelled"].includes(s?.toLowerCase().trim());
          const cls = received
            ? "bg-green-100 text-green-800"
            : failedish
              ? "bg-red-100 text-red-800"
              : refunded
                ? "bg-amber-100 text-amber-800"
                : "bg-stone-100 text-stone-700";
          return <span className={`inline-block rounded px-2 py-0.5 text-[10px] font-bold ${cls}`}>{statusLabel(s)}</span>;
        },
      },
      { accessorKey: "gatewayMessage", header: "Gateway Message", cell: ({ row }) => <span className="text-xs text-on-surface-variant">{dash(row.original.gatewayMessage)}</span> },
      { accessorKey: "paymentDatetime", header: "Payment Datetime", cell: ({ row }) => <span className="text-xs tabular-nums whitespace-nowrap">{paymentDateTime(row.original.paymentDatetime)}</span> },
      { accessorKey: "cardNo", header: "Card No", cell: ({ row }) => <span className="font-mono text-xs">{dash(row.original.cardNo)}</span> },
      {
        accessorKey: "amount",
        header: "Amount",
        cell: ({ row }) => <span className="text-xs font-semibold tabular-nums">{money(row.original.amount, row.original.currency)}</span>,
      },
    ],
    [],
  );

  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) {
        setRows([]);
        return [];
      }
      const apiRows = await fetchEcommercePayments(
        tenantId,
        {
          dateFrom: filters.dateFrom,
          dateTo: filters.dateTo,
          storeId: filters.storeScope !== "all" ? filters.storeScope : storeId ?? "all",
          orderNumber,
          referenceNumber,
          gatewayName,
          paymentMethod,
          paymentStatus,
          amountMin,
          amountMax,
        },
        authToken,
      );
      // BR01: one row per transaction; reference number is unique per gateway txn (BR02).
      const mapped = apiRows.map((r, i) => ({ ...r, id: `${r.referenceNumber || r.orderId}-${i}` }));
      setRows(mapped);
      return mapped;
    },
    [tenantId, storeId, authToken, orderNumber, referenceNumber, gatewayName, paymentMethod, paymentStatus, amountMin, amountMax],
  );

  // BRD §9/§10 KPI overview — computed from the filtered rows (BR08 export/displayed parity).
  const overviewContent = useMemo(() => {
    if (rows.length === 0) return undefined;
    const total = rows.length;
    const received = rows.filter((r) => isReceived(r.paymentStatus));
    const receivedCount = received.length;
    const receivedAmount = received.reduce((s, r) => s + Number(r.amount ?? 0), 0);
    const totalAmount = rows.reduce((s, r) => s + Number(r.amount ?? 0), 0);
    const currency = rows[0]?.currency ?? "SGD";
    const successRate = total > 0 ? (receivedCount / total) * 100 : 0;
    const avgTxn = receivedCount > 0 ? receivedAmount / receivedCount : 0;
    const cards = [
      { label: "Total Payment Amount", value: money(totalAmount, currency) },
      { label: "Successful Payments", value: receivedCount.toLocaleString("en-US") },
      { label: "Payment Success Rate", value: `${successRate.toFixed(1)}%` },
      { label: "Avg. Transaction Amount", value: money(avgTxn, currency) },
    ];
    return (
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        {cards.map((m) => (
          <div key={m.label} className="rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-4 shadow-sm">
            <p className="text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">{m.label}</p>
            <p className="mt-1 font-headline text-lg font-bold tabular-nums text-on-surface">{m.value}</p>
          </div>
        ))}
      </div>
    );
  }, [rows]);

  return (
    <ReportView<UiRow>
      breadcrumb="Ecommerce payments"
      title="Ecommerce payments"
      description="Every payment transaction processed through the payment gateways — status, reference and amount (Ecommerce Payments BRD §1). Card numbers are masked (BR03); Gateway message / Card No are not captured yet and show “—”."
      exportSheetName="EcommercePayments"
      exportFilename="ecommerce-payments.xlsx"
      columns={columns}
      exportHeaders={[
        "Order Number",
        "Reference Number",
        "Gateway Name",
        "Payment Method",
        "Payment Status",
        "Gateway Message",
        "Payment Datetime",
        "Card No",
        "Amount",
      ]}
      toExportRow={(r) => [
        r.orderId,
        r.referenceNumber ?? "",
        r.gatewayName ?? "",
        r.paymentMethod ?? "",
        statusLabel(r.paymentStatus),
        r.gatewayMessage ?? "",
        paymentDateTime(r.paymentDatetime),
        r.cardNo ?? "",
        money(r.amount, r.currency),
      ]}
      loadData={loadData}
      overviewContent={overviewContent}
      showStoreFilter={false}
      onReset={() => {
        setOrderNumber("");
        setReferenceNumber("");
        setGatewayName("");
        setPaymentMethod("");
        setPaymentStatus("all");
        setAmountMin("");
        setAmountMax("");
        setRows([]);
      }}
      emptyMessage="No payment transactions match the current filters (Payment Date range + search criteria)."
      extraFilters={
        <>
          <ReportFilterField label="Order number" htmlFor="pay-order">
            <input id="pay-order" className={inputClass} value={orderNumber} onChange={(e) => setOrderNumber(e.target.value)} placeholder="Order id" />
          </ReportFilterField>
          <ReportFilterField label="Reference number" htmlFor="pay-ref">
            <input id="pay-ref" className={inputClass} value={referenceNumber} onChange={(e) => setReferenceNumber(e.target.value)} placeholder="Gateway ref" />
          </ReportFilterField>
          <ReportFilterField label="Gateway name" htmlFor="pay-gw">
            <input id="pay-gw" className={inputClass} value={gatewayName} onChange={(e) => setGatewayName(e.target.value)} placeholder="e.g. ADYEN_visa" />
          </ReportFilterField>
          <ReportFilterField label="Payment method" htmlFor="pay-method">
            <input id="pay-method" className={inputClass} value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)} placeholder="e.g. Visa" />
          </ReportFilterField>
          <ReportFilterField label="Payment status" htmlFor="pay-status">
            <select id="pay-status" className={inputClass} value={paymentStatus} onChange={(e) => setPaymentStatus(e.target.value)}>
              {STATUS_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
          <ReportFilterField label="Card no" htmlFor="pay-card">
            <input
              id="pay-card"
              className={`${inputClass} cursor-not-allowed opacity-50`}
              placeholder="Not captured yet"
              disabled
              title="Masked card number is not stored on the payment transaction yet (BR03)."
            />
          </ReportFilterField>
          <ReportFilterField label="Amount min" htmlFor="pay-min">
            <input id="pay-min" type="number" className={inputClass} value={amountMin} onChange={(e) => setAmountMin(e.target.value)} placeholder="Min" />
          </ReportFilterField>
          <ReportFilterField label="Amount max" htmlFor="pay-max">
            <input id="pay-max" type="number" className={inputClass} value={amountMax} onChange={(e) => setAmountMax(e.target.value)} placeholder="Max" />
          </ReportFilterField>
        </>
      }
    />
  );
}
