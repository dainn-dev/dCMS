export type OrderType = "Retail" | "Wholesale" | "Online";

// ── Failed Orders ─────────────────────────────────────────────────────────────

export type FailedOrderStatus = "Payment Failed" | "Address Error" | "Auth Failed" | "Stock Error" | "System Error";

export type FailedOrder = {
  doNumber: string;
  orderId: string;
  orderDate: string;
  type: OrderType;
  customerName: string;
  customerEmail: string;
  contactNo: string;
  deliveryDate: string;
  deliveryOption: string;
  fulfilledDate: string | null;
  status: FailedOrderStatus;
  processedBy: string;
  tags: string;
  paymentMethod: string;
};

// ── Refund Cases ──────────────────────────────────────────────────────────────

export type RefundStatus = "Completed" | "Pending" | "Rejected";

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
  requestDate: string;
  refundDate: string | null;
  status: RefundStatus;
  remark: string;
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
  | "User Cancelled"
  | "Partially Fulfilled";

export type Order = {
  doNumber: string;
  orderId: string;
  orderDate: string;
  type: OrderType;
  customerName: string;
  customerEmail: string;
  status: OrderStatus;
  fulfilledDate: string | null;
  store: string;
  /** Optional columns — toggled via Site Settings / column visibility */
  distributionCentre?: string;
  paymentMethod?: string | "Multi Payment";
  paymentDate?: string | null;
};
