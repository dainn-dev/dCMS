import { useState } from "react";
import {
  IconChevronDown,
  IconChevronLeft,
  IconChevronRight,
  IconChevronUp,
  IconDownload,
  IconEdit,
  IconSearch,
  IconVisibility,
} from "../icons";

const labelBase = "block text-[11px] font-bold text-on-surface-variant tracking-tight uppercase";
const inputBase =
  "w-full bg-surface-container-low border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all";

type RefundStatus = "Completed" | "Pending" | "Rejected";

const statusStyles: Record<RefundStatus, string> = {
  Completed: "bg-green-100 text-green-700",
  Pending: "bg-amber-100 text-amber-700",
  Rejected: "bg-red-100 text-red-700",
};

const ROWS: {
  refundNo: string;
  hash: string;
  ret: string;
  doNo: string;
  order: string;
  customer: string;
  phone: string;
  email: string;
  amount: string;
  currency: string;
  paymentMethod: string;
  paymentDetail: string;
  requestDate: string;
  refundDate: string | null;
  refundDateLabel: string;
  status: RefundStatus;
  remark: string;
}[] = [
  {
    refundNo: "REF-2023-9941",
    hash: "#A9B2C3",
    ret: "RET-8812",
    doNo: "DO-1120",
    order: "ORD-2210",
    customer: "Jonathan Sterling",
    phone: "+1 (555) 902-1142",
    email: "j.sterling@cloudmail.net",
    amount: "$1,420.00",
    currency: "USD",
    paymentMethod: "Credit Card",
    paymentDetail: "VISA **** 4492",
    requestDate: "Oct 12, 2023",
    refundDate: "Oct 14, 2023",
    refundDateLabel: "completed",
    status: "Completed",
    remark: "Return requested due to damaged packaging on arrival. Verified.",
  },
  {
    refundNo: "REF-2023-9942",
    hash: "#D1E2F5",
    ret: "RET-8815",
    doNo: "DO-1124",
    order: "ORD-2215",
    customer: "Sarah Jenkins",
    phone: "+1 (555) 231-5589",
    email: "s.jenkins@servicehub.com",
    amount: "$284.50",
    currency: "USD",
    paymentMethod: "Bank Transfer",
    paymentDetail: "JP Morgan... 8821",
    requestDate: "Oct 13, 2023",
    refundDate: null,
    refundDateLabel: "Processing",
    status: "Pending",
    remark: "Customer changed mind. Item pending inspection at warehouse.",
  },
  {
    refundNo: "REF-2023-9943",
    hash: "#B8C9D1",
    ret: "RET-8820",
    doNo: "DO-1130",
    order: "ORD-2225",
    customer: "Michael Chen",
    phone: "+1 (555) 774-3321",
    email: "m.chen.dev@outlook.com",
    amount: "$59.99",
    currency: "USD",
    paymentMethod: "PayPal",
    paymentDetail: "Transaction ID: PP-992...",
    requestDate: "Oct 13, 2023",
    refundDate: null,
    refundDateLabel: "Rejected",
    status: "Rejected",
    remark: "Non-returnable item. Customer notified. Final decision.",
  },
];

export function RefundCasesPage() {
  const [filtersOpen, setFiltersOpen] = useState(true);

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Refund Cases</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Refund Cases</h1>
        </div>
        <div className="flex gap-3">
          <button
            type="button"
            className="bg-surface-container-lowest text-on-surface border border-outline-variant/20 px-4 py-2 rounded-lg text-sm font-semibold flex items-center gap-2 hover:bg-surface-container transition-colors shadow-sm"
            onClick={() => console.info("[RefundCases] Export")}
          >
            <IconDownload className="h-4 w-4" />
            Export
          </button>
          <button
            type="button"
            className="bg-primary text-on-primary px-4 py-2 rounded-lg text-sm font-bold flex items-center gap-2 hover:bg-primary-container transition-all shadow-md active:scale-95"
            onClick={() => console.info("[RefundCases] New Case")}
          >
            <span style={{ fontSize: 16, lineHeight: 1 }}>＋</span>
            New Case
          </button>
        </div>
      </div>

      {/* Filters panel */}
      <section
        className={`bg-surface-container-lowest rounded-xl shadow-sm ${
          filtersOpen ? "p-6" : "px-4 py-2"
        }`}
      >
        <div className={`flex items-center justify-between ${filtersOpen ? "mb-3" : ""}`}>
          <div className={`flex items-center gap-2 ${filtersOpen ? "min-h-6" : ""}`}>
            <IconSearch className="h-4 w-4 text-on-surface-variant" />
            <span className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              {filtersOpen ? "Search Filters" : "Search"}
            </span>
          </div>
          <button
            type="button"
            className={`rounded-lg hover:bg-surface-container-high text-on-surface-variant hover:text-primary transition-colors ${
              filtersOpen ? "p-2" : "p-1.5"
            }`}
            onClick={() => setFiltersOpen((v) => !v)}
            aria-expanded={filtersOpen}
            aria-label={filtersOpen ? "Collapse filters" : "Expand filters"}
          >
            {filtersOpen ? <IconChevronUp /> : <IconChevronDown />}
          </button>
        </div>

        {filtersOpen && (
          <>
            <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-y-5 gap-x-6">
          <div className="space-y-1.5">
            <label className={labelBase}>Refund Number</label>
            <input className={inputBase} placeholder="e.g. REF-2023-001" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Return Number</label>
            <input className={inputBase} placeholder="RET-9902" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>DO Number</label>
            <input className={inputBase} placeholder="DO-5521" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Order Number</label>
            <input className={inputBase} placeholder="ORD-281" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Customer Name</label>
            <input className={inputBase} placeholder="Full name" type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Contact No</label>
            <input className={inputBase} placeholder="+1..." type="tel" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Customer Email</label>
            <input className={inputBase} placeholder="client@example.com" type="email" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Refund Amount Range</label>
            <div className="flex items-center gap-2">
              <input
                className="w-1/2 bg-surface-container-low border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary transition-all"
                placeholder="Min"
                type="number"
              />
              <input
                className="w-1/2 bg-surface-container-low border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary transition-all"
                placeholder="Max"
                type="number"
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Payment Method</label>
            <select className={`${inputBase} appearance-none`}>
              <option>All Methods</option>
              <option>Credit Card</option>
              <option>Bank Transfer</option>
              <option>PayPal</option>
            </select>
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Reference No.</label>
            <input className={inputBase} placeholder="REF-BANK-..." type="text" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Request Date</label>
            <input className={inputBase} type="date" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Refund Date</label>
            <input className={inputBase} type="date" />
          </div>
          <div className="space-y-1.5">
            <label className={labelBase}>Status</label>
            <select className={`${inputBase} appearance-none`}>
              <option>All Statuses</option>
              <option>Pending</option>
              <option>Completed</option>
              <option>Rejected</option>
            </select>
          </div>
            </div>

            <div className="mt-8 pt-6 border-t border-outline-variant/10 flex justify-end gap-3">
              <button
                type="button"
                className="px-6 py-2 text-sm font-semibold text-primary hover:bg-surface-container-high rounded-lg transition-colors"
                onClick={() => console.info("[RefundCases] Reset")}
              >
                Reset
              </button>
              <button
                type="button"
                className="px-8 py-2 bg-primary text-on-primary text-sm font-bold rounded-lg shadow-lg hover:bg-primary-container transition-all flex items-center gap-2"
                onClick={() => console.info("[RefundCases] Search")}
              >
                Search Cases
              </button>
            </div>
          </>
        )}
      </section>

      {/* Table section */}
      <section className="bg-surface-container-lowest rounded-xl shadow-sm overflow-hidden flex flex-col">
        {/* Top pagination */}
        <div className="px-6 py-4 flex items-center justify-between border-b border-outline-variant/10">
          <div className="flex items-center gap-4">
            <span className="text-xs text-on-surface-variant">
              Showing <span className="font-bold text-on-surface">1–10</span> of 1,284 results
            </span>
            <div className="h-4 w-px bg-outline-variant/30" />
            <select className="bg-transparent border-none text-xs font-bold text-on-surface focus:ring-0 p-0 pr-6">
              <option>10 Rows</option>
              <option>25 Rows</option>
              <option>50 Rows</option>
            </select>
          </div>
          <div className="flex items-center gap-2">
            <button type="button" className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container transition-colors opacity-40 cursor-not-allowed">
              <IconChevronLeft />
            </button>
            <button type="button" className="w-8 h-8 flex items-center justify-center rounded-lg bg-primary text-on-primary text-xs font-bold shadow-sm">1</button>
            <button type="button" className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container text-xs font-bold transition-colors">2</button>
            <button type="button" className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container text-xs font-bold transition-colors">3</button>
            <span className="text-xs mx-1">...</span>
            <button type="button" className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container text-xs font-bold transition-colors">128</button>
            <button type="button" className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container transition-colors">
              <IconChevronRight />
            </button>
          </div>
        </div>

        {/* Table */}
        <div className="overflow-x-auto custom-scrollbar">
          <table className="w-full border-collapse text-left">
            <thead>
              <tr className="bg-surface-container-high">
                {["Refund No.", "Return / DO / Order", "Customer Identity", "Amount", "Payment", "Dates", "Status", "Remark"].map((h) => (
                  <th key={h} className="px-4 py-4 text-[10px] font-black uppercase text-primary tracking-widest whitespace-nowrap">
                    {h}
                  </th>
                ))}
                <th className="px-6 py-4 text-[10px] font-black uppercase text-primary tracking-widest whitespace-nowrap text-center">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-outline-variant/10">
              {ROWS.map((row) => (
                <tr key={row.refundNo} className="hover:bg-surface-container-low transition-colors group">
                  {/* Refund No */}
                  <td className="px-6 py-4">
                    <div className="text-xs font-bold text-on-surface">{row.refundNo}</div>
                    <div className="text-[10px] text-on-surface-variant font-mono">{row.hash}</div>
                  </td>
                  {/* Return/DO/Order */}
                  <td className="px-4 py-4">
                    <div className="text-xs space-y-0.5">
                      <div className="flex items-center gap-1.5">
                        <span className="text-[9px] font-black text-on-surface-variant w-6">RET</span>
                        {row.ret}
                      </div>
                      <div className="flex items-center gap-1.5">
                        <span className="text-[9px] font-black text-on-surface-variant w-6">DO</span>
                        {row.doNo}
                      </div>
                      <div className="flex items-center gap-1.5">
                        <span className="text-[9px] font-black text-on-surface-variant w-6">ORD</span>
                        {row.order}
                      </div>
                    </div>
                  </td>
                  {/* Customer */}
                  <td className="px-4 py-4">
                    <div className="flex flex-col">
                      <span className="text-xs font-bold">{row.customer}</span>
                      <span className="text-[10px] text-on-surface-variant">{row.phone}</span>
                      <span className="text-[10px] text-primary truncate" style={{ maxWidth: 128 }}>{row.email}</span>
                    </div>
                  </td>
                  {/* Amount */}
                  <td className="px-4 py-4 text-right">
                    <div className="text-sm font-black text-on-surface">{row.amount}</div>
                    <div className="text-[9px] text-on-surface-variant font-bold uppercase tracking-tight">{row.currency}</div>
                  </td>
                  {/* Payment */}
                  <td className="px-4 py-4">
                    <div className="text-xs font-medium">{row.paymentMethod}</div>
                    <div className="text-[10px] text-on-surface-variant">{row.paymentDetail}</div>
                  </td>
                  {/* Dates */}
                  <td className="px-4 py-4">
                    <div className="text-[10px] flex flex-col gap-1">
                      <span className="flex items-center gap-1">
                        <span style={{ fontSize: 14 }}>📅</span> {row.requestDate}
                      </span>
                      {row.refundDate ? (
                        <span className="flex items-center gap-1 text-primary/60">
                          <span style={{ fontSize: 14 }}>✓</span> {row.refundDate}
                        </span>
                      ) : row.status === "Rejected" ? (
                        <span className="flex items-center gap-1 text-red-600">
                          <span style={{ fontSize: 14 }}>✕</span> {row.refundDateLabel}
                        </span>
                      ) : (
                        <span className="text-on-surface-variant opacity-50 italic">{row.refundDateLabel}</span>
                      )}
                    </div>
                  </td>
                  {/* Status badge */}
                  <td className="px-4 py-4">
                    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-tighter ${statusStyles[row.status]}`}>
                      {row.status}
                    </span>
                  </td>
                  {/* Remark */}
                  <td className="px-4 py-4">
                    <div className="text-[10px] text-on-surface-variant italic" style={{ maxWidth: 150, display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden" }}>
                      {row.remark}
                    </div>
                  </td>
                  {/* Actions */}
                  <td className="px-6 py-4">
                    <div className="flex items-center justify-center gap-2">
                      <button
                        type="button"
                        className="p-1.5 text-on-surface-variant hover:text-primary transition-colors rounded"
                        aria-label="View"
                        onClick={() => console.info("[RefundCases] View", row.refundNo)}
                      >
                        <IconVisibility />
                      </button>
                      <button
                        type="button"
                        className="p-1.5 text-on-surface-variant hover:text-primary transition-colors rounded"
                        aria-label="Edit"
                        onClick={() => console.info("[RefundCases] Edit", row.refundNo)}
                      >
                        <IconEdit />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Bottom pagination */}
        <div className="px-6 py-4 flex items-center justify-between bg-surface-container-low/30">
          <div className="flex items-center gap-4">
            <span className="text-[10px] text-on-surface-variant font-medium">Quick jump to page</span>
            <input
              type="number"
              defaultValue={1}
              className="w-12 bg-white border border-outline-variant/20 rounded px-2 py-1 text-[10px] text-center font-bold focus:outline-none focus:ring-1 focus:ring-primary"
            />
          </div>
          <div className="flex items-center gap-2">
            {["First", "Prev"].map((label) => (
              <button key={label} type="button" className="px-3 py-1.5 text-[11px] font-bold text-on-surface-variant hover:bg-surface-container rounded-lg transition-colors border border-outline-variant/10">
                {label}
              </button>
            ))}
            <div className="flex gap-1 mx-2">
              <span className="px-3 py-1.5 text-[11px] font-bold bg-primary text-on-primary rounded-lg">1</span>
              {["2", "3"].map((p) => (
                <span key={p} className="px-3 py-1.5 text-[11px] font-bold hover:bg-surface-container rounded-lg cursor-pointer transition-colors">
                  {p}
                </span>
              ))}
            </div>
            {["Next", "Last"].map((label) => (
              <button key={label} type="button" className="px-3 py-1.5 text-[11px] font-bold text-on-surface-variant hover:bg-surface-container rounded-lg transition-colors border border-outline-variant/10">
                {label}
              </button>
            ))}
          </div>
        </div>
      </section>
    </div>
  );
}
