import { useMemo } from "react";
import { DataTable } from "../components/DataTable";
import { createFailedOrderColumns } from "../failed-orders-columns";
import { IconDownload } from "../icons";
import type { FailedOrder } from "../types";

const MOCK_FAILED_ORDERS: FailedOrder[] = [
  {
    doNumber: "DO-98231",
    orderId: "ORD-44520-22",
    orderDate: "2023-10-24",
    type: "Retail",
    customerName: "Alexander Vance",
    customerEmail: "a.vance@email.com",
    contactNo: "+1-202-555-0143",
    deliveryDate: "2023-10-26",
    deliveryOption: "Express",
    fulfilledDate: null,
    status: "Payment Failed",
    processedBy: "SYS-AUTO",
    tags: "Urgent",
    paymentMethod: "Visa **** 4492",
  },
  {
    doNumber: "DO-98232",
    orderId: "ORD-44521-X9",
    orderDate: "2023-10-24",
    type: "Wholesale",
    customerName: "Elena Rodriguez",
    customerEmail: "elena.r@globalcorp.net",
    contactNo: "+1-305-555-0912",
    deliveryDate: "2023-10-27",
    deliveryOption: "Standard",
    fulfilledDate: null,
    status: "Address Error",
    processedBy: "Admin_04",
    tags: "Pending Review",
    paymentMethod: "Bank Transfer",
  },
  {
    doNumber: "DO-98235",
    orderId: "ORD-44525-K1",
    orderDate: "2023-10-25",
    type: "Retail",
    customerName: "Marcus Thorne",
    customerEmail: "m.thorne@techsys.com",
    contactNo: "+1-415-555-0621",
    deliveryDate: "2023-10-28",
    deliveryOption: "Premium",
    fulfilledDate: null,
    status: "Auth Failed",
    processedBy: "SYS-AUTO",
    tags: "Retry Scheduled",
    paymentMethod: "Amex **** 1002",
  },
  {
    doNumber: "DO-98240",
    orderId: "ORD-44530-B3",
    orderDate: "2023-10-25",
    type: "Online",
    customerName: "Priya Sharma",
    customerEmail: "priya.s@dev.in",
    contactNo: "+91-98100-55210",
    deliveryDate: "2023-10-29",
    deliveryOption: "Standard",
    fulfilledDate: null,
    status: "Stock Error",
    processedBy: "Admin_01",
    tags: "Out of Stock",
    paymentMethod: "UPI",
  },
  {
    doNumber: "DO-98245",
    orderId: "ORD-44535-Z7",
    orderDate: "2023-10-26",
    type: "Wholesale",
    customerName: "Carlos Mendes",
    customerEmail: "c.mendes@latam.biz",
    contactNo: "+55-11-9912-3344",
    deliveryDate: "2023-10-30",
    deliveryOption: "Express",
    fulfilledDate: null,
    status: "System Error",
    processedBy: "SYS-AUTO",
    tags: "Escalated",
    paymentMethod: "Credit Card",
  },
];

export function FailedOrdersPage() {
  const columns = useMemo(() => createFailedOrderColumns(), []);

  return (
    <>
      {/* Page header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Failed Orders</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Failed Orders
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Monitor and resolve order failures and payment issues.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2"
            onClick={() => console.info("[FailedOrders] Export")}
          >
            <IconDownload />
            Export
          </button>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={MOCK_FAILED_ORDERS}
        globalFilterPlaceholder="Search by order, customer, status…"
      />
    </>
  );
}
