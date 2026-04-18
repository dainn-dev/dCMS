import type { RefundCase, RefundCaseHistoryEntry, RefundStatus } from "./types";

const nowActor = "Store Staff (mock)";

function hist(...entries: RefundCaseHistoryEntry[]): RefundCaseHistoryEntry[] {
  return entries;
}

/** Mock refund cases — §3.3 (replace with API). */
export const MOCK_REFUND_CASES: RefundCase[] = [
  {
    refundNo: "REF-2023-9941",
    referenceHash: "#A9B2C3",
    returnNo: "RET-8812",
    doNumber: "DO-1120",
    orderId: "ORD-2210",
    customerName: "Jonathan Sterling",
    customerPhone: "+1 (555) 902-1142",
    customerEmail: "j.sterling@cloudmail.net",
    amount: 1420.0,
    currency: "USD",
    paymentMethod: "Credit Card",
    paymentDetail: "VISA **** 4492",
    paymentReferenceNo: "STRIPE-CHG-882912",
    requestDate: "Oct 12, 2023",
    refundDate: "Oct 14, 2023",
    status: "Success",
    remark: "Return requested due to damaged packaging on arrival. Verified.",
    paymentGatewayMessage:
      "Gateway: Stripe. charge.succeeded id ch_3Nxxxxx. Refund id re_3Nyyyyy for USD 1,420.00. Reason: requested_by_customer. No further action required.",
    isPaymentGatewayCase: true,
    doCancelledItems: [
      { sku: "UPC-2210-A", name: "Glass Canister 3L", qty: 2, lineTotal: 1180.0 },
      { sku: "UPC-2210-B", name: "Silicone Lid Set", qty: 1, lineTotal: 240.0 },
    ],
    refundBreakdown: [
      { label: "Merchandise subtotal", amount: 1420.0 },
      { label: "Restocking fee waived", amount: 0 },
      { label: "Tax (included in items)", amount: 0 },
      { label: "Refund total", amount: 1420.0 },
    ],
    changeHistory: hist(
      {
        at: "2023-10-12 15:20",
        actor: "Customer Portal",
        status: "Pending Refund",
        remark: "Refund request submitted.",
      },
      {
        at: "2023-10-13 09:41",
        actor: nowActor,
        status: "Pending Refund",
        remark: "Return requested due to damaged packaging on arrival. Verified.",
      },
      {
        at: "2023-10-14 11:02",
        actor: "SYS-AUTO",
        status: "Success",
        remark: "Stripe refund confirmed; case closed.",
      }
    ),
  },
  {
    refundNo: "REF-2023-9942",
    referenceHash: "#D1E2F5",
    returnNo: "RET-8815",
    doNumber: "DO-1124",
    orderId: "ORD-2215",
    customerName: "Sarah Jenkins",
    customerPhone: "+1 (555) 231-5589",
    customerEmail: "s.jenkins@servicehub.com",
    amount: 284.5,
    currency: "USD",
    paymentMethod: "Bank Transfer",
    paymentDetail: "JP Morgan… 8821",
    paymentReferenceNo: "WIRE-REF-MANUAL-44102",
    requestDate: "Oct 13, 2023",
    refundDate: null,
    status: "Pending Refund",
    remark: "Customer changed mind. Item pending inspection at warehouse.",
    paymentGatewayMessage:
      "Manual refund queue: no payment gateway token. Finance to verify bank details before payout. Internal memo ID FIN-8821.",
    isPaymentGatewayCase: false,
    doCancelledItems: [
      { sku: "UPC-5510", name: "Desk Lamp LED", qty: 1, lineTotal: 284.5 },
    ],
    refundBreakdown: [
      { label: "Item total", amount: 284.5 },
      { label: "Refund total (pending)", amount: 284.5 },
    ],
    changeHistory: hist({
      at: "2023-10-13 10:05",
      actor: nowActor,
      status: "Pending Refund",
      remark: "Customer changed mind. Item pending inspection at warehouse.",
    }),
  },
  {
    refundNo: "REF-2023-9943",
    referenceHash: "#B8C9D1",
    returnNo: "RET-8820",
    doNumber: "DO-1130",
    orderId: "ORD-2225",
    customerName: "Michael Chen",
    customerPhone: "+1 (555) 774-3321",
    customerEmail: "m.chen.dev@outlook.com",
    amount: 59.99,
    currency: "USD",
    paymentMethod: "PayPal",
    paymentDetail: "Transaction ID: PP-992…",
    paymentReferenceNo: "PP-R-998221004",
    requestDate: "Oct 13, 2023",
    refundDate: null,
    status: "Failed",
    remark: "Non-returnable item. Customer notified. Final decision.",
    paymentGatewayMessage:
      "PayPal REFUND_FAILED: ITEM_NOT_RETURNABLE. Error code PAYER_CANNOT_PAY. Policy: digital goods not eligible for refund after download.",
    isPaymentGatewayCase: true,
    doCancelledItems: [{ sku: "DL-992", name: "Digital License Key", qty: 1, lineTotal: 59.99 }],
    refundBreakdown: [
      { label: "Requested refund", amount: 59.99 },
      { label: "Gateway outcome", amount: 0 },
    ],
    changeHistory: hist(
      {
        at: "2023-10-13 16:22",
        actor: nowActor,
        status: "Pending Refund",
        remark: "Customer requested refund for digital item.",
      },
      {
        at: "2023-10-14 08:00",
        actor: "PayPal Webhook",
        status: "Failed",
        remark: "Non-returnable item. Customer notified. Final decision.",
      }
    ),
  },
  {
    refundNo: "REF-2023-9944",
    referenceHash: "#C4D5E6",
    returnNo: "RET-8825",
    doNumber: "DO-1138",
    orderId: "ORD-2233",
    customerName: "Amelia Watson",
    customerPhone: "+44 7700 900142",
    customerEmail: "a.watson@mailbox.co.uk",
    amount: 899.0,
    currency: "GBP",
    paymentMethod: "Credit Card",
    paymentDetail: "Mastercard **** 7721",
    paymentReferenceNo: "ADYEN-PSP-77219001",
    requestDate: "Oct 15, 2023",
    refundDate: null,
    status: "Pending Refund",
    remark: "Wrong item delivered. Awaiting return shipment confirmation.",
    paymentGatewayMessage:
      "Adyen: refund not yet sent — awaiting warehouse return scan on DO-1138. Hold reason: RETURN_IN_TRANSIT.",
    isPaymentGatewayCase: true,
    doCancelledItems: [
      { sku: "UK-7781", name: "Kettle Pro 1.7L", qty: 1, lineTotal: 720.0 },
      { sku: "UK-7782", name: "Descaler Pack", qty: 1, lineTotal: 179.0 },
    ],
    refundBreakdown: [
      { label: "Merchandise", amount: 899.0 },
      { label: "Shipping (original)", amount: 0 },
      { label: "Refund total (pending)", amount: 899.0 },
    ],
    changeHistory: hist({
      at: "2023-10-15 14:18",
      actor: nowActor,
      status: "Pending Refund",
      remark: "Wrong item delivered. Awaiting return shipment confirmation.",
    }),
  },
  {
    refundNo: "REF-2023-9945",
    referenceHash: "#F1A2B3",
    returnNo: "RET-8830",
    doNumber: "DO-1145",
    orderId: "ORD-2240",
    customerName: "Hiroshi Tanaka",
    customerPhone: "+81-90-1234-5678",
    customerEmail: "h.tanaka@corp.jp",
    amount: 3200.0,
    currency: "JPY",
    paymentMethod: "Bank Transfer",
    paymentDetail: "Mizuho Bank ****3390",
    paymentReferenceNo: "MT-20231018-JP-8830",
    requestDate: "Oct 16, 2023",
    refundDate: "Oct 18, 2023",
    status: "Success",
    remark: "Duplicate order confirmed. Full refund processed.",
    paymentGatewayMessage:
      "Internal wire: duplicate order confirmed by ops. Amount JPY 3,200 credited to customer account MT-20231018-JP-8830.",
    isPaymentGatewayCase: false,
    doCancelledItems: [
      { sku: "JP-2201", name: "Office Chair Mat", qty: 1, lineTotal: 3200.0 },
    ],
    refundBreakdown: [
      { label: "Duplicate line reversal", amount: 3200.0 },
      { label: "Refund total", amount: 3200.0 },
    ],
    changeHistory: hist(
      {
        at: "2023-10-16 11:00",
        actor: nowActor,
        status: "Pending Refund",
        remark: "Customer reports duplicate charge.",
      },
      {
        at: "2023-10-18 09:30",
        actor: "Finance Bot",
        status: "Success",
        remark: "Duplicate order confirmed. Full refund processed.",
      }
    ),
  },
];

export function getRefundCaseByNo(refundNo: string): RefundCase | undefined {
  return MOCK_REFUND_CASES.find((c) => c.refundNo === refundNo);
}

export const REFUND_STATUS_OPTIONS: RefundStatus[] = ["Pending Refund", "Success", "Failed"];
