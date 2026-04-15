import { useState } from "react";
import {
  IconAdd,
  IconChevronLeft,
  IconChevronRight,
  IconChevronDown,
  IconChevronUp,
  IconDownload,
  IconEdit,
  IconFirstPage,
  IconLastPage,
  IconSearch,
  IconVisibility,
} from "../icons";

const inputBase =
  "w-full bg-surface-container-low border-0 rounded px-3 py-2 text-xs focus:ring-1 focus:ring-primary/40";
const labelBase =
  "block text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant";

type Props = { onViewOrder?: (orderId: string) => void };

export function OrderProcessingPage({ onViewOrder }: Props) {
  const [filtersOpen, setFiltersOpen] = useState(true);

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
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">Order Processing</h1>
          <p className="text-sm text-on-surface-variant mt-1">Manage and track your end-to-end order lifecycle</p>
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
          <button
            type="button"
            className="px-5 py-2 text-sm font-bold text-on-primary bg-primary rounded-lg shadow-lg shadow-primary/10 hover:bg-primary-container transition-all active:scale-95 flex items-center gap-2"
            onClick={() => console.info("[Orders] New Order (placeholder)")}
          >
            <IconAdd />
            New Order
          </button>
        </div>
      </div>

      {/* Search / filter panel */}
      <section
        className={`bg-surface-container-lowest rounded-xl shadow-sm border border-outline-variant/20 ${
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
            aria-label={filtersOpen ? "Collapse search filters" : "Expand search filters"}
          >
            {filtersOpen ? <IconChevronUp /> : <IconChevronDown />}
          </button>
        </div>

        {filtersOpen && (
          <>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          <div className="space-y-4">
            <div className="space-y-1.5">
              <label className={labelBase}>DO Number</label>
              <input className={inputBase} placeholder="Enter DO#" type="text" />
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Order Date Range</label>
              <div className="flex items-center gap-2">
                <input className={`${inputBase} px-2`} type="date" />
                <span className="text-outline shrink-0">to</span>
                <input className={`${inputBase} px-2`} type="date" />
              </div>
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Customer Name</label>
              <input className={inputBase} placeholder="John Doe" type="text" />
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Payment Method</label>
              <select className={inputBase}>
                <option>All Methods</option>
                <option>Credit Card</option>
                <option>Bank Transfer</option>
              </select>
            </div>
          </div>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <label className={labelBase}>Order Number</label>
              <input className={inputBase} placeholder="Enter Order#" type="text" />
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Delivery Date Range</label>
              <div className="flex items-center gap-2">
                <input className={`${inputBase} px-2`} type="date" />
                <span className="text-outline shrink-0">to</span>
                <input className={`${inputBase} px-2`} type="date" />
              </div>
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Contact No</label>
              <input className={inputBase} placeholder="+1..." type="text" />
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Store Name</label>
              <input className={inputBase} placeholder="Flagship Store" type="text" />
            </div>
          </div>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <label className={labelBase}>Order Type</label>
              <select className={inputBase}>
                <option>Retail</option>
                <option>Wholesale</option>
                <option>Online</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Fulfilled On Range</label>
              <div className="flex items-center gap-2">
                <input className={`${inputBase} px-2`} type="date" />
                <span className="text-outline shrink-0">to</span>
                <input className={`${inputBase} px-2`} type="date" />
              </div>
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Email Address</label>
              <input className={inputBase} placeholder="customer@domain.com" type="email" />
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Affiliate ID</label>
              <input className={inputBase} placeholder="AFF-000" type="text" />
            </div>
          </div>

          <div className="space-y-4">
            <div className="space-y-1.5">
              <label className={labelBase}>Shipping Status</label>
              <select className={inputBase}>
                <option>All Statuses</option>
                <option>Pending</option>
                <option>Shipped</option>
                <option>Delivered</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Processed By</label>
              <input className={inputBase} placeholder="Staff Name" type="text" />
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Delivery Option</label>
              <select className={inputBase}>
                <option>Standard</option>
                <option>Express</option>
                <option>Pick-up</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className={labelBase}>Tags</label>
              <input className={inputBase} placeholder="VIP, Refund, Urgent" type="text" />
            </div>
          </div>
        </div>

            <div className="mt-8 flex items-center justify-end gap-3 border-t border-outline-variant/10 pt-6">
              <button
                type="button"
                className="px-6 py-2 text-xs font-bold text-primary hover:bg-primary/5 transition-colors rounded-lg"
                onClick={() => console.info("[Orders] Reset filters (placeholder)")}
              >
                Reset
              </button>
              <button
                type="button"
                className="px-8 py-2 text-xs font-bold text-on-primary bg-primary rounded-lg shadow-md shadow-primary/10 hover:bg-primary-container transition-all"
                onClick={() => console.info("[Orders] Search (placeholder)")}
              >
                Search
              </button>
            </div>
          </>
        )}
      </section>

      {/* Data table */}
      <div className="bg-surface-container-lowest rounded-xl shadow-sm border border-outline-variant/20 overflow-hidden flex flex-col">
        <div className="px-6 py-4 flex items-center justify-between border-b border-outline-variant/10 bg-surface-container-low/30">
          <div className="flex items-center gap-4">
            <div className="flex items-center gap-2">
              <span className="text-[11px] font-semibold text-on-surface-variant uppercase">Show</span>
              <select className="text-xs bg-transparent border-0 focus:ring-0 cursor-pointer font-bold text-on-surface">
                <option>10</option>
                <option>25</option>
                <option>50</option>
              </select>
              <span className="text-[11px] font-semibold text-on-surface-variant uppercase">Entries</span>
            </div>
          </div>
          <div className="flex items-center gap-1">
            <button type="button" className="p-1.5 rounded hover:bg-surface-container-high transition-colors text-on-surface-variant">
              <IconFirstPage />
            </button>
            <button type="button" className="p-1.5 rounded hover:bg-surface-container-high transition-colors text-on-surface-variant">
              <IconChevronLeft />
            </button>
            <div className="flex items-center gap-1 px-4">
              <span className="text-xs font-bold text-primary">1</span>
              <span className="text-xs text-outline mx-1">of</span>
              <span className="text-xs font-bold text-on-surface">12</span>
            </div>
            <button type="button" className="p-1.5 rounded hover:bg-surface-container-high transition-colors text-on-surface-variant">
              <IconChevronRight />
            </button>
            <button type="button" className="p-1.5 rounded hover:bg-surface-container-high transition-colors text-on-surface-variant">
              <IconLastPage />
            </button>
          </div>
        </div>

        <div className="overflow-x-auto custom-scrollbar">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-surface-container-high">
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">DO Number</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">Order ID</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">Order Date</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">Type</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">Customer</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">Status</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">Fulfilled</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap">Store</th>
                <th className="px-4 py-3 text-[11px] font-bold text-primary uppercase whitespace-nowrap text-center">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-outline-variant/10">
              <tr className="hover:bg-surface-container-low transition-colors group">
                <td className="px-4 py-4 text-xs font-bold text-on-surface">DO-2024-001</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">ORD-998122</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">24 May 2024</td>
                <td className="px-4 py-4">
                  <span className="inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase bg-tertiary-container/10 text-tertiary">
                    Retail
                  </span>
                </td>
                <td className="px-4 py-4">
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-on-surface">Michael Vance</span>
                    <span className="text-[10px] text-on-surface-variant">m.vance@example.com</span>
                  </div>
                </td>
                <td className="px-4 py-4">
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase bg-secondary-container/20 text-on-secondary-container">
                    <span className="w-1.5 h-1.5 rounded-full bg-secondary-container" />
                    Open
                  </span>
                </td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">--</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">London Central</td>
                <td className="px-4 py-4 text-center">
                  <div className="flex items-center justify-center gap-2">
                    <button
                      type="button"
                      className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all"
                      aria-label="View"
                      onClick={() => onViewOrder?.("ORD-998122")}
                    >
                      <IconVisibility />
                    </button>
                    <button
                      type="button"
                      className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all"
                      aria-label="Edit"
                      onClick={() => console.info("[Orders] Edit row 1")}
                    >
                      <IconEdit />
                    </button>
                  </div>
                </td>
              </tr>
              <tr className="hover:bg-surface-container-low transition-colors group">
                <td className="px-4 py-4 text-xs font-bold text-on-surface">DO-2024-002</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">ORD-998123</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">24 May 2024</td>
                <td className="px-4 py-4">
                  <span className="inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase bg-tertiary-container/10 text-tertiary">
                    Wholesale
                  </span>
                </td>
                <td className="px-4 py-4">
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-on-surface">Sarah Connor</span>
                    <span className="text-[10px] text-on-surface-variant">s.connor@sky.net</span>
                  </div>
                </td>
                <td className="px-4 py-4">
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase bg-green-100 text-green-700">
                    <span className="w-1.5 h-1.5 rounded-full bg-green-500" />
                    Shipped
                  </span>
                </td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">25 May 2024</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">Berlin Hub</td>
                <td className="px-4 py-4 text-center">
                  <div className="flex items-center justify-center gap-2">
                    <button type="button" className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all" aria-label="View" onClick={() => onViewOrder?.("ORD-998123")}>
                      <IconVisibility />
                    </button>
                    <button type="button" className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all" aria-label="Edit">
                      <IconEdit />
                    </button>
                  </div>
                </td>
              </tr>
              <tr className="hover:bg-surface-container-low transition-colors group">
                <td className="px-4 py-4 text-xs font-bold text-on-surface">DO-2024-003</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">ORD-998124</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">23 May 2024</td>
                <td className="px-4 py-4">
                  <span className="inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase bg-tertiary-container/10 text-tertiary">
                    Online
                  </span>
                </td>
                <td className="px-4 py-4">
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-on-surface">James Bond</span>
                    <span className="text-[10px] text-on-surface-variant">007@mi6.gov</span>
                  </div>
                </td>
                <td className="px-4 py-4">
                  <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase bg-red-100 text-red-700">
                    <span className="w-1.5 h-1.5 rounded-full bg-red-500" />
                    Pending
                  </span>
                </td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">--</td>
                <td className="px-4 py-4 text-xs text-on-surface-variant">E-Store Global</td>
                <td className="px-4 py-4 text-center">
                  <div className="flex items-center justify-center gap-2">
                    <button type="button" className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all" aria-label="View" onClick={() => onViewOrder?.("ORD-998124")}>
                      <IconVisibility />
                    </button>
                    <button type="button" className="p-1.5 rounded hover:bg-white text-on-surface-variant hover:text-primary transition-all" aria-label="Edit">
                      <IconEdit />
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div className="px-6 py-4 flex flex-col sm:flex-row items-center justify-between gap-4 border-t border-outline-variant/10">
          <span className="text-xs text-on-surface-variant">
            Showing <span className="font-bold text-on-surface">1</span> to <span className="font-bold text-on-surface">10</span> of{" "}
            <span className="font-bold text-on-surface">124</span> orders
          </span>
          <div className="flex items-center gap-1">
            <button
              type="button"
              className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container-high text-on-surface-variant transition-colors"
            >
              <IconChevronLeft />
            </button>
            <button
              type="button"
              className="w-8 h-8 flex items-center justify-center rounded-lg bg-primary text-on-primary font-bold text-xs"
            >
              1
            </button>
            <button
              type="button"
              className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container-high text-on-surface-variant font-bold text-xs"
            >
              2
            </button>
            <button
              type="button"
              className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container-high text-on-surface-variant font-bold text-xs"
            >
              3
            </button>
            <span className="px-2 text-outline">...</span>
            <button
              type="button"
              className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container-high text-on-surface-variant font-bold text-xs"
            >
              12
            </button>
            <button
              type="button"
              className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-surface-container-high text-on-surface-variant transition-colors"
            >
              <IconChevronRight />
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
