export type OrderType = "Retail" | "Wholesale" | "Online";

export type DeliveryOption = "Standard" | "Express" | "Click & Collect" | "Same Day" | null;

export type ShippingStatus =
  | "Pending"
  | "Shipped"
  | "Out for Delivery"
  | "Delivered"
  | "Failed"
  | "Returned"
  | null;

// ── Failed Orders ─────────────────────────────────────────────────────────────

export type FailedOrderStatus = "Payment Failed" | "Address Error" | "Auth Failed" | "Stock Error" | "System Error";

export type FailedOrderLineItem = {
  lineId: string;
  sku: string;
  name: string;
  qty: number;
};

export type FailedOrderLogEntry = {
  at: string;
  event: string;
};

/** Failed-order row — field names aligned with `Order` where applicable (§3.2 workspace). */
export type FailedOrder = {
  doNumber: string;
  orderId: string;
  orderDate: string;
  type: OrderType;
  customerName: string;
  customerEmail: string;
  customerContactNo: string | null;
  deliveryDate: string | null;
  deliveryOption: DeliveryOption;
  fulfilledDate: string | null;
  shippingStatus: ShippingStatus;
  status: FailedOrderStatus;
  processedBy: string | null;
  tags: string | null;
  paymentMethod: string | null;
  paymentGateway: string | null;
  /** Human-readable explanation shown on the detail page. */
  failureReason: string;
  /** Structured fields for Failed Order detail (mock / API). */
  failureErrorCode: string | null;
  failureAt: string | null;
  failureRetryCount: number;
  failureLog: FailedOrderLogEntry[];
  lineItems: FailedOrderLineItem[];
  store: string;
  storeAutoId: string | null;
  shippingAddressLine1: string | null;
  shippingAddressLine2: string | null;
  shippingPostalCode: string | null;
  shippingCountry: string | null;
  billingAddressLine1: string | null;
  billingAddressLine2: string | null;
  billingPostalCode: string | null;
  billingCountry: string | null;
};

// ── Refund Cases — §3.3 (mock until API) ───────────────────────────────────────

export type RefundStatus = "Pending Refund" | "Success" | "Failed";

export type RefundCaseDoLine = {
  sku: string;
  name: string;
  qty: number;
  lineTotal: number;
};

export type RefundCaseBreakdownLine = {
  label: string;
  amount: number;
};

export type RefundCaseHistoryEntry = {
  at: string;
  actor: string;
  status: RefundStatus;
  remark: string;
};

export type RefundCase = {
  refundNo: string;
  referenceHash: string;
  returnNo: string;
  doNumber: string;
  orderId: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  paymentDetail: string;
  paymentReferenceNo: string;
  requestDate: string;
  refundDate: string | null;
  status: RefundStatus;
  /** Short remark in table */
  remark: string;
  /** Full processor / gateway message (Remark Details popup). */
  paymentGatewayMessage: string;
  /** When true, status edit is only allowed for Pending Refund or Failed. */
  isPaymentGatewayCase: boolean;
  doCancelledItems: RefundCaseDoLine[];
  refundBreakdown: RefundCaseBreakdownLine[];
  changeHistory: RefundCaseHistoryEntry[];
};

export type OrderStatus =
  | "Open Order"
  | "Processing"
  | "Ready for Delivery"
  | "Ready for Pickup"
  | "Out for Delivery"
  | "Picked Up"
  | "Delivered"
  | "Returned"
  | "Admin Cancelled"
  | "Pending Cancellation"
  | "User Cancelled"
  | "Partially Fulfilled"
  // DAI-637 failure states — same Orders lifecycle, operator-recoverable
  | "Payment Failed"
  | "Auth Failed"
  | "Address Error"
  | "Stock Error"
  | "System Error";

export type Order = {
  doNumber: string;
  orderId: string;
  orderDate: string;
  type: OrderType;
  customerName: string;
  customerEmail: string;
  customerContactNo: string | null;
  status: OrderStatus;
  fulfilledDate: string | null;
  deliveryDate: string | null;
  deliveryOption: DeliveryOption;
  shippingStatus: ShippingStatus;
  processingOfficer: string | null;
  tags: string | null;
  paymentGateway: string | null;
  store: string;
  storeAutoId: string | null;
  /** Optional columns — toggled via Site Settings / column visibility */
  distributionCentre?: string;
  paymentMethod?: string | "Multi Payment";
  paymentDate?: string | null;
  /** Shipping address */
  shippingAddressLine1: string | null;
  shippingAddressLine2: string | null;
  shippingPostalCode: string | null;
  shippingCountry: string | null;
  /** Billing address */
  billingAddressLine1: string | null;
  billingAddressLine2: string | null;
  billingPostalCode: string | null;
  billingCountry: string | null;
};
