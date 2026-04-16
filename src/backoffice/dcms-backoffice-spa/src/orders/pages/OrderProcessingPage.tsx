import { useMemo } from "react";
import { DataTable } from "../components/DataTable";
import { IconDownload } from "../icons";
import { createOrderColumns } from "../orders-columns";
import type { Order } from "../types";

// ── Mock data ─────────────────────────────────────────────────────────────────
// Replace with API call when backend is wired.
const MOCK_ORDERS: Order[] = [
  {
    doNumber: "DO-2024-001",
    orderId: "ORD-998122",
    orderDate: "24 May 2024",
    type: "Retail",
    customerName: "Michael Vance",
    customerEmail: "m.vance@example.com",
    status: "Open Order",
    fulfilledDate: null,
    store: "London Central",
    distributionCentre: "DC-North",
    paymentMethod: "Credit Card",
    paymentDate: "24 May 2024",
  },
  {
    doNumber: "DO-2024-002",
    orderId: "ORD-998123",
    orderDate: "24 May 2024",
    type: "Wholesale",
    customerName: "Sarah Connor",
    customerEmail: "s.connor@sky.net",
    status: "Delivered",
    fulfilledDate: "25 May 2024",
    store: "Berlin Hub",
    distributionCentre: "DC-Central",
    paymentMethod: "Multi Payment",
    paymentDate: "24 May 2024",
  },
  {
    doNumber: "DO-2024-003",
    orderId: "ORD-998124",
    orderDate: "23 May 2024",
    type: "Online",
    customerName: "James Bond",
    customerEmail: "007@mi6.gov",
    status: "Processing",
    fulfilledDate: null,
    store: "Global Online",
    distributionCentre: "DC-South",
    paymentMethod: "Bank Transfer",
    paymentDate: "23 May 2024",
  },
  {
    doNumber: "DO-2024-004",
    orderId: "ORD-998125",
    orderDate: "22 May 2024",
    type: "Retail",
    customerName: "Ada Lovelace",
    customerEmail: "ada@babbage.io",
    status: "Ready for Delivery",
    fulfilledDate: null,
    store: "London Central",
    distributionCentre: "DC-North",
    paymentMethod: "Credit Card",
    paymentDate: "22 May 2024",
  },
  {
    doNumber: "DO-2024-005",
    orderId: "ORD-998126",
    orderDate: "22 May 2024",
    type: "Online",
    customerName: "Alan Turing",
    customerEmail: "alan@enigma.uk",
    status: "Returned",
    fulfilledDate: "24 May 2024",
    store: "Global Online",
    distributionCentre: "DC-East",
    paymentMethod: "E-Wallet",
    paymentDate: "22 May 2024",
  },
  {
    doNumber: "DO-2024-006",
    orderId: "ORD-998127",
    orderDate: "21 May 2024",
    type: "Wholesale",
    customerName: "Grace Hopper",
    customerEmail: "grace@navy.mil",
    status: "Partially Fulfilled",
    fulfilledDate: null,
    store: "Berlin Hub",
    distributionCentre: "DC-West",
    paymentMethod: "Multi Payment",
    paymentDate: "21 May 2024",
  },
  {
    doNumber: "DO-2024-007",
    orderId: "ORD-998128",
    orderDate: "20 May 2024",
    type: "Retail",
    customerName: "Nikola Tesla",
    customerEmail: "nikola@ac-dc.com",
    status: "Admin Cancelled",
    fulfilledDate: null,
    store: "Paris Flagship",
    paymentMethod: "Credit Card",
    paymentDate: "20 May 2024",
  },
  {
    doNumber: "DO-2024-008",
    orderId: "ORD-998129",
    orderDate: "20 May 2024",
    type: "Online",
    customerName: "Marie Curie",
    customerEmail: "marie@radium.fr",
    status: "Picked Up",
    fulfilledDate: "21 May 2024",
    store: "Paris Flagship",
    distributionCentre: "DC-Central",
    paymentMethod: "Bank Transfer",
    paymentDate: "20 May 2024",
  },
  {
    doNumber: "DO-2024-009",
    orderId: "ORD-998130",
    orderDate: "19 May 2024",
    type: "Retail",
    customerName: "Isaac Newton",
    customerEmail: "isaac@apple.science",
    status: "Out for Delivery",
    fulfilledDate: null,
    store: "London Central",
    distributionCentre: "DC-North",
    paymentMethod: "Credit Card",
    paymentDate: "19 May 2024",
  },
  {
    doNumber: "DO-2024-010",
    orderId: "ORD-998131",
    orderDate: "18 May 2024",
    type: "Wholesale",
    customerName: "Linus Torvalds",
    customerEmail: "linus@kernel.org",
    status: "Open Order",
    fulfilledDate: null,
    store: "Helsinki Store",
    distributionCentre: "DC-North",
    paymentMethod: "Bank Transfer",
    paymentDate: "18 May 2024",
  },
  {
    doNumber: "DO-2024-011",
    orderId: "ORD-998132",
    orderDate: "17 May 2024",
    type: "Online",
    customerName: "Tim Berners-Lee",
    customerEmail: "tim@w3.org",
    status: "Delivered",
    fulfilledDate: "19 May 2024",
    store: "Global Online",
    paymentMethod: "Multi Payment",
    paymentDate: "17 May 2024",
  },
  {
    doNumber: "DO-2024-012",
    orderId: "ORD-998133",
    orderDate: "16 May 2024",
    type: "Retail",
    customerName: "Edsger Dijkstra",
    customerEmail: "edsger@graph.nl",
    status: "Ready for Pickup",
    fulfilledDate: null,
    store: "Amsterdam Central",
    distributionCentre: "DC-West",
    paymentMethod: "Credit Card",
    paymentDate: "16 May 2024",
  },
];

// ─────────────────────────────────────────────────────────────────────────────

type Props = {
  onViewOrder?: (orderId: string) => void;
  onTakeAction?: (orderId: string) => void;
};

export function OrderProcessingPage({ onViewOrder, onTakeAction }: Props) {
  const columns = useMemo(
    () => createOrderColumns(onViewOrder, onTakeAction),
    [onViewOrder, onTakeAction]
  );

  return (
    <>
      {/* Page header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Order Processing</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Order Processing
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Manage and track your end-to-end order lifecycle
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2"
            onClick={() => console.info("[Orders] Export (placeholder)")}
          >
            <IconDownload />
            Export
          </button>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={MOCK_ORDERS}
        defaultHiddenColumns={["distributionCentre", "paymentMethod", "paymentDate"]}
        globalFilterPlaceholder="Search by order, customer, store…"
      />
    </>
  );
}
