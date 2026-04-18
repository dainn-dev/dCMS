import type { FailedOrder } from "./types";

/** §3.2 — export list columns + key investigation fields (UTF-8 CSV, Excel-friendly). */
const HEADERS = [
  "DO Number",
  "Order Number",
  "Order Date",
  "Type",
  "Customer Name",
  "Customer Email",
  "Contact Number",
  "Delivery Date",
  "Delivery Option",
  "Fulfilled Date",
  "Shipping Status",
  "Status",
  "Processed By",
  "Tags",
  "Payment Method",
  "Payment Gateway",
  "Store",
  "Store Auto ID",
  "Failure Error Code",
  "Failure At",
  "Failure Reason",
] as const;

function cell(value: string | null | undefined): string {
  const v = value ?? "";
  return `"${String(v).replace(/"/g, '""')}"`;
}

function rowToCsv(o: FailedOrder): string {
  return [
    cell(o.doNumber),
    cell(o.orderId),
    cell(o.orderDate),
    cell(o.type),
    cell(o.customerName),
    cell(o.customerEmail),
    cell(o.customerContactNo),
    cell(o.deliveryDate),
    cell(o.deliveryOption),
    cell(o.fulfilledDate),
    cell(o.shippingStatus),
    cell(o.status),
    cell(o.processedBy),
    cell(o.tags),
    cell(o.paymentMethod),
    cell(o.paymentGateway),
    cell(o.store),
    cell(o.storeAutoId),
    cell(o.failureErrorCode),
    cell(o.failureAt),
    cell(o.failureReason),
  ].join(",");
}

export function exportFailedOrdersToCsv(orders: FailedOrder[]): void {
  const BOM = "\uFEFF";
  const header = HEADERS.map((h) => cell(h)).join(",");
  const body = orders.map(rowToCsv).join("\r\n");
  const csv = BOM + [header, body].join("\r\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "Failed-Orders-Export.csv";
  a.click();
  URL.revokeObjectURL(url);
}
