import type { FailedOrder, FailedOrderLineItem, FailedOrderStatus } from "./types";

function lines(orderId: string): FailedOrderLineItem[] {
  return [
    { lineId: `${orderId}-L1`, sku: "UPC-902110", name: "Stainless Canister Set", qty: 1 },
    { lineId: `${orderId}-L2`, sku: "UPC-902118", name: "Replacement Lid Kit", qty: 2 },
  ];
}

function failureLogFor(status: FailedOrderStatus, terminalAt: string): FailedOrder["failureLog"] {
  const common: FailedOrder["failureLog"] = [
    { at: "2023-10-24 09:12:01", event: "Order received; validation started." },
    { at: "2023-10-24 09:12:08", event: "Payment intent created; awaiting capture." },
  ];
  if (status === "Payment Failed") {
    return [
      ...common,
      { at: "2023-10-24 09:12:14", event: "Gateway submitted charge to issuer." },
      { at: terminalAt, event: "Issuer declined (code 05). No capture performed." },
    ];
  }
  if (status === "Address Error") {
    return [
      ...common,
      { at: "2023-10-24 09:13:02", event: "Address validation service invoked." },
      { at: terminalAt, event: "Postal code / city mismatch; order held." },
    ];
  }
  if (status === "Auth Failed") {
    return [
      ...common,
      { at: "2023-10-24 09:14:00", event: "3DS challenge URL issued to client." },
      { at: terminalAt, event: "3DS session timed out; authentication incomplete." },
    ];
  }
  if (status === "Stock Error") {
    return [
      ...common,
      { at: "2023-10-24 09:11:55", event: "Inventory reservation requested at DC-East." },
      { at: terminalAt, event: "Insufficient stock for one or more lines; reservation rolled back." },
    ];
  }
  return [
    ...common,
    { at: "2023-10-24 09:12:11", event: "Persist transaction begin." },
    { at: terminalAt, event: "Unhandled exception in commit path; transaction aborted." },
  ];
}

/** Shared mock data for Failed Orders list + detail (replace with API). */
export const MOCK_FAILED_ORDERS: FailedOrder[] = [
  {
    doNumber: "DO-98231",
    orderId: "ORD-44520-22",
    orderDate: "2023-10-24",
    type: "Retail",
    customerName: "Alexander Vance",
    customerEmail: "a.vance@email.com",
    customerContactNo: "+1-202-555-0143",
    deliveryDate: "2023-10-26",
    deliveryOption: "Express",
    fulfilledDate: null,
    shippingStatus: "Pending",
    status: "Payment Failed",
    processedBy: "SYS-AUTO",
    tags: "Urgent",
    paymentMethod: "Visa **** 4492",
    paymentGateway: "Stripe",
    failureReason:
      "Card issuer declined the charge (code 05: Do not honor). No amount was captured. Customer may retry with another card.",
    failureErrorCode: "PAY-DECL-05",
    failureAt: "2023-10-24 09:12:15",
    failureRetryCount: 1,
    failureLog: failureLogFor("Payment Failed", "2023-10-24 09:12:15"),
    lineItems: lines("ORD-44520-22"),
    store: "London Central",
    storeAutoId: "STR-001",
    shippingAddressLine1: "1600 Pennsylvania Ave NW",
    shippingAddressLine2: null,
    shippingPostalCode: "20500",
    shippingCountry: "US",
    billingAddressLine1: "1600 Pennsylvania Ave NW",
    billingAddressLine2: null,
    billingPostalCode: "20500",
    billingCountry: "US",
  },
  {
    doNumber: "DO-98232",
    orderId: "ORD-44521-X9",
    orderDate: "2023-10-24",
    type: "Wholesale",
    customerName: "Elena Rodriguez",
    customerEmail: "elena.r@globalcorp.net",
    customerContactNo: "+1-305-555-0912",
    deliveryDate: "2023-10-27",
    deliveryOption: "Standard",
    fulfilledDate: null,
    shippingStatus: "Pending",
    status: "Address Error",
    processedBy: "Admin_04",
    tags: "Pending Review",
    paymentMethod: "Bank Transfer",
    paymentGateway: "Adyen",
    failureReason:
      "Shipping address failed validation: postal code does not match city/state. Order held until address is corrected.",
    failureErrorCode: "ADDR-VAL-440",
    failureAt: "2023-10-24 09:13:03",
    failureRetryCount: 0,
    failureLog: failureLogFor("Address Error", "2023-10-24 09:13:03"),
    lineItems: lines("ORD-44521-X9"),
    store: "Berlin Hub",
    storeAutoId: "STR-002",
    shippingAddressLine1: "Invalid Street 999",
    shippingAddressLine2: "Suite 12",
    shippingPostalCode: "00000",
    shippingCountry: "DE",
    billingAddressLine1: "Friedrichstr. 100",
    billingAddressLine2: null,
    billingPostalCode: "10117",
    billingCountry: "DE",
  },
  {
    doNumber: "DO-98235",
    orderId: "ORD-44525-K1",
    orderDate: "2023-10-25",
    type: "Retail",
    customerName: "Marcus Thorne",
    customerEmail: "m.thorne@techsys.com",
    customerContactNo: "+1-415-555-0621",
    deliveryDate: "2023-10-28",
    deliveryOption: "Express",
    fulfilledDate: null,
    shippingStatus: "Pending",
    status: "Auth Failed",
    processedBy: "SYS-AUTO",
    tags: "Retry Scheduled",
    paymentMethod: "Amex **** 1002",
    paymentGateway: "Stripe",
    failureReason:
      "3-D Secure authentication failed (timeout). The payment session expired before the customer completed verification.",
    failureErrorCode: "AUTH-3DS-TIMEOUT",
    failureAt: "2023-10-24 09:14:02",
    failureRetryCount: 2,
    failureLog: failureLogFor("Auth Failed", "2023-10-24 09:14:02"),
    lineItems: lines("ORD-44525-K1"),
    store: "Global Online",
    storeAutoId: "STR-003",
    shippingAddressLine1: "1 Market St",
    shippingAddressLine2: "Floor 4",
    shippingPostalCode: "94105",
    shippingCountry: "US",
    billingAddressLine1: "1 Market St",
    billingAddressLine2: "Floor 4",
    billingPostalCode: "94105",
    billingCountry: "US",
  },
  {
    doNumber: "DO-98240",
    orderId: "ORD-44530-B3",
    orderDate: "2023-10-25",
    type: "Online",
    customerName: "Priya Sharma",
    customerEmail: "priya.s@dev.in",
    customerContactNo: "+91-98100-55210",
    deliveryDate: "2023-10-29",
    deliveryOption: "Standard",
    fulfilledDate: null,
    shippingStatus: "Failed",
    status: "Stock Error",
    processedBy: "Admin_01",
    tags: "Out of Stock",
    paymentMethod: "UPI",
    paymentGateway: "Razorpay",
    failureReason:
      "One or more line items are out of stock at the assigned fulfilment location. Reservation could not be completed.",
    failureErrorCode: "INV-OOS-112",
    failureAt: "2023-10-24 09:11:58",
    failureRetryCount: 0,
    failureLog: failureLogFor("Stock Error", "2023-10-24 09:11:58"),
    lineItems: lines("ORD-44530-B3"),
    store: "Global Online",
    storeAutoId: "STR-003",
    shippingAddressLine1: "MG Road",
    shippingAddressLine2: "Apt 302",
    shippingPostalCode: "560001",
    shippingCountry: "IN",
    billingAddressLine1: "MG Road",
    billingAddressLine2: "Apt 302",
    billingPostalCode: "560001",
    billingCountry: "IN",
  },
  {
    doNumber: "DO-98245",
    orderId: "ORD-44535-Z7",
    orderDate: "2023-10-26",
    type: "Wholesale",
    customerName: "Carlos Mendes",
    customerEmail: "c.mendes@latam.biz",
    customerContactNo: "+55-11-9912-3344",
    deliveryDate: "2023-10-30",
    deliveryOption: "Express",
    fulfilledDate: null,
    shippingStatus: "Pending",
    status: "System Error",
    processedBy: "SYS-AUTO",
    tags: "Escalated",
    paymentMethod: "Credit Card",
    paymentGateway: "PayPal",
    failureReason:
      "Internal error while committing the order (reference: ERR-ORD-COMMIT-8841). Engineering has been notified; no customer charge was taken.",
    failureErrorCode: "ERR-ORD-COMMIT-8841",
    failureAt: "2023-10-24 09:12:12",
    failureRetryCount: 0,
    failureLog: failureLogFor("System Error", "2023-10-24 09:12:12"),
    lineItems: lines("ORD-44535-Z7"),
    store: "Berlin Hub",
    storeAutoId: "STR-002",
    shippingAddressLine1: "Av. Paulista 1000",
    shippingAddressLine2: null,
    shippingPostalCode: "01310-100",
    shippingCountry: "BR",
    billingAddressLine1: "Av. Paulista 1000",
    billingAddressLine2: null,
    billingPostalCode: "01310-100",
    billingCountry: "BR",
  },
];

export function getFailedOrderByOrderId(orderId: string): FailedOrder | undefined {
  return MOCK_FAILED_ORDERS.find((o) => o.orderId === orderId);
}
