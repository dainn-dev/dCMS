import type { RefundCase } from "./types";

const HEADERS = [
  "Refund No.",
  "DO Number",
  "Order Number",
  "Customer Email",
  "Customer Name",
  "Refund Amount",
  "Currency",
  "Payment Method",
  "Payment Reference No.",
  "Request Date",
  "Refund Date",
  "Refund Status",
  "Remark",
] as const;

function cell(value: string | number | null | undefined): string {
  const v = value ?? "";
  return `"${String(v).replace(/"/g, '""')}"`;
}

function rowToCsv(c: RefundCase): string {
  return [
    cell(c.refundNo),
    cell(c.doNumber),
    cell(c.orderId),
    cell(c.customerEmail),
    cell(c.customerName),
    cell(c.amount),
    cell(c.currency),
    cell(c.paymentMethod),
    cell(c.paymentReferenceNo),
    cell(c.requestDate),
    cell(c.refundDate),
    cell(c.status),
    cell(c.remark),
  ].join(",");
}

/** UTF-8 CSV with BOM — opens in Excel (§3.3.1 export). */
export function exportRefundCasesToCsv(cases: RefundCase[]): void {
  const BOM = "\uFEFF";
  const header = HEADERS.map((h) => cell(h)).join(",");
  const body = cases.map(rowToCsv).join("\r\n");
  const csv = BOM + [header, body].join("\r\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "Refund-Cases-Export.csv";
  a.click();
  URL.revokeObjectURL(url);
}
