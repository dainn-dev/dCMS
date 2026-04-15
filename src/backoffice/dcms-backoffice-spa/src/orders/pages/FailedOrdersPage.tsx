import { useState } from "react";
import { IconChevronDown, IconChevronLeft, IconChevronRight, IconChevronUp, IconSearch, IconVisibility } from "../icons";

const labelBase = "text-[0.6875rem] font-semibold text-on-surface-variant uppercase tracking-wider";
const inputBase =
  "w-full bg-surface-container-low border border-outline-variant/20 rounded-md px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary";
const dateInput =
  "w-full bg-surface-container-low border border-outline-variant/20 rounded-md px-2 py-2 text-xs";

const ROWS = [
  {
    do: "DO-98231",
    order: "ORD-44520-22",
    date: "2023-10-24",
    type: "Retail",
    customer: "Alexander Vance",
    contact: "+1-202-555-0143",
    email: "a.vance@email.com",
    deliveryDate: "2023-10-26",
    option: "Express",
    fulfilled: "N/A",
    status: "Payment Failed",
    processedBy: "SYS-AUTO",
    tags: "Urgent",
    payment: "Visa **** 4492",
    alt: false,
  },
  {
    do: "DO-98232",
    order: "ORD-44521-X9",
    date: "2023-10-24",
    type: "Wholesale",
    customer: "Elena Rodriguez",
    contact: "+1-305-555-0912",
    email: "elena.r@globalcorp.net",
    deliveryDate: "2023-10-27",
    option: "Standard",
    fulfilled: "N/A",
    status: "Address Error",
    processedBy: "Admin_04",
    tags: "Pending Review",
    payment: "Bank Transfer",
    alt: true,
  },
  {
    do: "DO-98235",
    order: "ORD-44525-K1",
    date: "2023-10-25",
    type: "Retail",
    customer: "Marcus Thorne",
    contact: "+1-415-555-0621",
    email: "m.thorne@techsys.com",
    deliveryDate: "2023-10-28",
    option: "Premium",
    fulfilled: "N/A",
    status: "Auth Failed",
    processedBy: "SYS-AUTO",
    tags: "Retry Scheduled",
    payment: "Amex **** 1002",
    alt: false,
  },
];

export function FailedOrdersPage() {
  const [filtersOpen, setFiltersOpen] = useState(true);

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <span>Orders</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Failed Orders</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Failed Orders</h1>
          <p className="text-sm text-on-surface-variant mt-1">Monitor and resolve order failures and payment issues.</p>
        </div>
      </div>

      {/* Filter panel */}
      <section
        className={`bg-surface-container-lowest rounded-xl shadow-sm border border-outline-variant/10 ${
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
          <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-4">
          <div className="space-y-1">
            <label className={labelBase}>DO Number</label>
            <input className={inputBase} type="text" />
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Order Number</label>
            <input className={inputBase} type="text" />
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Order Type</label>
            <select className={`${inputBase} appearance-none`}>
              <option>All Types</option>
              <option>Retail</option>
              <option>Wholesale</option>
            </select>
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Delivery Option</label>
            <input className={inputBase} type="text" />
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Shipping Status</label>
            <select className={`${inputBase} appearance-none`}>
              <option>Failed</option>
              <option>Pending</option>
              <option>Returned</option>
            </select>
          </div>

          <div className="space-y-1">
            <label className={labelBase}>Order Date Range</label>
            <div className="flex gap-2">
              <input className={dateInput} type="date" />
            </div>
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Delivery Date Range</label>
            <div className="flex gap-2">
              <input className={dateInput} type="date" />
            </div>
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Fulfilled On</label>
            <div className="flex gap-2">
              <input className={dateInput} type="date" />
            </div>
          </div>

          <div className="space-y-1">
            <label className={labelBase}>Customer Name</label>
            <input className={inputBase} type="text" />
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Customer Contact</label>
            <input className={inputBase} type="text" />
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Payment Method</label>
            <select className={`${inputBase} appearance-none`}>
              <option>Any</option>
              <option>Credit Card</option>
              <option>Bank Transfer</option>
            </select>
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Affiliate ID</label>
            <input className={inputBase} type="text" />
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Processed By</label>
            <input className={inputBase} type="text" />
          </div>
          <div className="space-y-1">
            <label className={labelBase}>Tags</label>
            <input className={inputBase} placeholder="Urgent, Review..." type="text" />
          </div>

          {/* Action buttons */}
          <div className="flex items-end gap-2 md:col-span-3 lg:col-span-1">
            <button
              type="button"
              className="flex-1 bg-primary text-on-primary py-2 rounded-md text-xs font-bold hover:bg-primary-container transition-all"
              onClick={() => console.info("[FailedOrders] Search")}
            >
              Search
            </button>
            <button
              type="button"
              className="flex-1 bg-surface-container-high text-on-surface py-2 rounded-md text-xs font-bold hover:bg-surface-container transition-all"
              onClick={() => console.info("[FailedOrders] Reset")}
            >
              Reset
            </button>
          </div>
          </div>
        )}
      </section>

      {/* Table info bar */}
      <div className="flex items-center justify-between px-2">
        <span className="text-xs font-medium text-on-surface-variant">
          Showing <span className="text-on-surface font-bold">1–25</span> of{" "}
          <span className="text-on-surface font-bold">1,248</span> records
        </span>
        <div className="flex items-center gap-2">
          <span className="text-[0.7rem] font-bold text-on-surface-variant uppercase">Per Page:</span>
          <select className="bg-transparent border-none text-xs font-bold text-primary focus:ring-0 cursor-pointer">
            <option>25</option>
            <option>50</option>
            <option>100</option>
          </select>
        </div>
      </div>

      {/* Data table */}
      <div className="overflow-x-auto bg-surface-container-lowest rounded-xl shadow-sm border border-outline-variant/10 custom-scrollbar">
        <table className="w-full text-left border-collapse" style={{ minWidth: 1600 }}>
          <thead>
            <tr className="bg-surface-container-high">
              {[
                "DO Number","Order Number","Order Date","Type","Customer Name",
                "Contact No","Email","Delivery Date","Option","Fulfilled",
                "Status","Processed By","Tags","Payment",
              ].map((h) => (
                <th key={h} className="px-4 py-3 text-[0.7rem] font-bold text-primary uppercase tracking-wider whitespace-nowrap">
                  {h}
                </th>
              ))}
              <th className="px-4 py-3 text-[0.7rem] font-bold text-primary uppercase tracking-wider text-right whitespace-nowrap">
                Action
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-outline-variant/10">
            {ROWS.map((row) => (
              <tr
                key={row.do}
                className={`hover:bg-surface-container-low transition-colors group ${
                  row.alt ? "bg-surface-container-low/30" : ""
                }`}
              >
                <td className="px-4 py-3 text-[0.75rem] font-semibold text-on-surface whitespace-nowrap">{row.do}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.order}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.date}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.type}</td>
                <td className="px-4 py-3 text-[0.75rem] font-bold text-on-surface whitespace-nowrap">{row.customer}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.contact}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.email}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.deliveryDate}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.option}</td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.fulfilled}</td>
                <td className="px-4 py-3 whitespace-nowrap">
                  <span className="px-2 py-0.5 rounded-full bg-error-container text-on-error-container text-[0.65rem] font-bold uppercase">
                    {row.status}
                  </span>
                </td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.processedBy}</td>
                <td className="px-4 py-3 whitespace-nowrap">
                  <span className="px-2 py-0.5 rounded-full bg-secondary-container/20 text-secondary text-[0.65rem] font-medium italic">
                    {row.tags}
                  </span>
                </td>
                <td className="px-4 py-3 text-[0.75rem] text-on-surface-variant whitespace-nowrap">{row.payment}</td>
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    className="p-1 hover:text-primary transition-colors text-on-surface-variant"
                    aria-label="View"
                    onClick={() => console.info("[FailedOrders] View", row.do)}
                  >
                    <IconVisibility />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination + last sync */}
      <footer className="flex items-center justify-between pb-8">
        <div className="flex items-center gap-1">
          <button type="button" className="w-8 h-8 flex items-center justify-center rounded-md border border-outline-variant/30 text-on-surface-variant hover:border-primary hover:text-primary transition-all">
            <IconChevronLeft />
          </button>
          <button type="button" className="w-8 h-8 flex items-center justify-center rounded-md bg-primary text-on-primary text-xs font-bold">
            1
          </button>
          <button type="button" className="w-8 h-8 flex items-center justify-center rounded-md border border-outline-variant/30 text-on-surface-variant hover:border-primary hover:text-primary text-xs font-medium">
            2
          </button>
          <button type="button" className="w-8 h-8 flex items-center justify-center rounded-md border border-outline-variant/30 text-on-surface-variant hover:border-primary hover:text-primary text-xs font-medium">
            3
          </button>
          <span className="px-2 text-on-surface-variant">...</span>
          <button type="button" className="w-8 h-8 flex items-center justify-center rounded-md border border-outline-variant/30 text-on-surface-variant hover:border-primary hover:text-primary text-xs font-medium">
            50
          </button>
          <button type="button" className="w-8 h-8 flex items-center justify-center rounded-md border border-outline-variant/30 text-on-surface-variant hover:border-primary hover:text-primary transition-all">
            <IconChevronRight />
          </button>
        </div>
        <div className="text-[0.7rem] font-medium text-on-surface-variant italic">
          Last sync: October 25, 2023 — 14:32:05
        </div>
      </footer>
    </div>
  );
}
