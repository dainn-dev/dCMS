import { useMemo } from "react";
import { DataTable } from "../components/DataTable";
import { IconDownload } from "../icons";
import { createRefundCaseColumns } from "../refund-cases-columns";
import type { RefundCase } from "../types";

const MOCK_REFUND_CASES: RefundCase[] = [
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
    requestDate: "Oct 12, 2023",
    refundDate: "Oct 14, 2023",
    status: "Completed",
    remark: "Return requested due to damaged packaging on arrival. Verified.",
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
    paymentDetail: "JP Morgan... 8821",
    requestDate: "Oct 13, 2023",
    refundDate: null,
    status: "Pending",
    remark: "Customer changed mind. Item pending inspection at warehouse.",
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
    paymentDetail: "Transaction ID: PP-992...",
    requestDate: "Oct 13, 2023",
    refundDate: null,
    status: "Rejected",
    remark: "Non-returnable item. Customer notified. Final decision.",
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
    requestDate: "Oct 15, 2023",
    refundDate: null,
    status: "Pending",
    remark: "Wrong item delivered. Awaiting return shipment confirmation.",
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
    requestDate: "Oct 16, 2023",
    refundDate: "Oct 18, 2023",
    status: "Completed",
    remark: "Duplicate order confirmed. Full refund processed.",
  },
];

export function RefundCasesPage() {
  const columns = useMemo(
    () =>
      createRefundCaseColumns(
        (refundNo) => console.info("[RefundCases] View", refundNo),
        (refundNo) => console.info("[RefundCases] Edit", refundNo)
      ),
    []
  );

  return (
    <>
      {/* Page header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Refund Cases</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Refund Cases
          </h1>
          <p className="text-sm text-on-surface-variant mt-1">
            Track and process customer refund requests.
          </p>
        </div>
        <div className="flex gap-3">
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2"
            onClick={() => console.info("[RefundCases] Export")}
          >
            <IconDownload className="h-4 w-4" />
            Export
          </button>
        </div>
      </div>

      <DataTable
        columns={columns}
        data={MOCK_REFUND_CASES}
        globalFilterPlaceholder="Search by refund no., customer, order…"
        columnLabels={{ references: "Return / DO / Order" }}
      />
    </>
  );
}
