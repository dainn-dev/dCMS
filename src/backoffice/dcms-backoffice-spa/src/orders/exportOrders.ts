import type { Order } from "./types";

// §3.1.5 — export column headers, in guide-specified order.
const HEADERS = [
  "Delivery Order Number",
  "Order Numbers",
  "Order Date",
  "Type",
  "Name",
  "Contact Number",
  "Delivery Date",
  "Delivery Option",
  "Fulfillment Date",
  "Shipping Status",
  "Processing Officer",
  "Tags",
  "Email",
  "Order Status",
  "Payment Gateway",
  "Store Auto ID",
  "Store Name",
  "Payment Date",
  "Shipping Address Line 1",
  "Shipping Address Line 2",
  "Shipping Postal Code",
  "Shipping Country",
  "Billing Address Line 1",
  "Billing Address Line 2",
  "Billing Postal Code",
  "Billing Country",
] as const;

function cell(value: string | null | undefined): string {
  const v = value ?? "";
  // RFC 4180: wrap in quotes and double any internal quotes.
  return `"${v.replace(/"/g, '""')}"`;
}

function orderToRow(o: Order): string {
  return [
    cell(o.doNumber),
    cell(o.orderId),
    cell(o.orderDate),
    cell(o.type),
    cell(o.customerName),
    cell(o.customerContactNo),
    cell(o.deliveryDate),
    cell(o.deliveryOption),
    cell(o.fulfilledDate),
    cell(o.shippingStatus),
    cell(o.processingOfficer),
    cell(o.tags),
    cell(o.customerEmail),
    cell(o.status),
    cell(o.paymentGateway),
    cell(o.storeAutoId),
    cell(o.store),
    cell(o.paymentDate),
    cell(o.shippingAddressLine1),
    cell(o.shippingAddressLine2),
    cell(o.shippingPostalCode),
    cell(o.shippingCountry),
    cell(o.billingAddressLine1),
    cell(o.billingAddressLine2),
    cell(o.billingPostalCode),
    cell(o.billingCountry),
  ].join(",");
}

/**
 * Generates a UTF-8 CSV (with BOM so Excel auto-detects encoding) from the
 * given order rows and triggers a browser download named
 * "Order-Processing-Export.csv". Opens natively in Excel without conversion.
 */
export function exportOrderProcessingFile(orders: Order[]): void {
  const BOM = "\uFEFF";
  const header = HEADERS.map((h) => cell(h)).join(",");
  const rows = orders.map(orderToRow);
  const csv = BOM + [header, ...rows].join("\r\n");

  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "Order-Processing-Export.csv";
  a.click();
  URL.revokeObjectURL(url);
}
