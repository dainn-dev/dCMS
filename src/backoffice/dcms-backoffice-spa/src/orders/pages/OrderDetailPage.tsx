import {
  IconArrowBack,
  IconCheckCircle,
  IconEditNote,
  IconMail,
  IconPerson,
  IconPhone,
  IconPrint,
  IconShipping,
} from "../icons";

type Props = {
  orderId: string;
  onBack: () => void;
};

export function OrderDetailPage({ orderId, onBack }: Props) {
  return (
    <div className="space-y-6 max-w-7xl">
      {/* Page header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <nav className="flex items-center gap-2 text-[10px] uppercase tracking-widest text-on-surface-variant font-bold mb-1">
            <button
              type="button"
              onClick={onBack}
              className="flex items-center gap-1 hover:text-primary transition-colors"
              aria-label="Back to Order Processing"
            >
              <IconArrowBack className="h-3 w-3" />
              <span>Orders</span>
            </button>
            <span>/</span>
            <span
              className="hover:text-primary cursor-pointer transition-colors"
              onClick={onBack}
            >
              Order Processing
            </span>
            <span>/</span>
            <span className="text-primary">Order Details</span>
          </nav>
          <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
            Order #{orderId}
          </h1>
        </div>
        <div className="flex gap-3 shrink-0">
          <button
            type="button"
            className="flex items-center gap-2 px-4 py-2 text-primary font-semibold text-xs border border-outline-variant/30 rounded bg-white hover:bg-surface-container-low transition-all"
            onClick={() => console.info("[Orders] Print invoice")}
          >
            <IconPrint className="h-4 w-4" />
            Print Invoice
          </button>
          <button
            type="button"
            className="px-4 py-2 bg-primary text-white font-semibold text-xs rounded shadow-md hover:bg-primary-container transition-all"
            onClick={() => console.info("[Orders] Update status")}
          >
            Update Status
          </button>
        </div>
      </div>

      {/* Bento grid */}
      <div className="grid grid-cols-1 md:grid-cols-12 gap-6">
        {/* Order Information */}
        <section className="md:col-span-4 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Order Information
            </h3>
            <span className="bg-secondary-container/20 text-secondary px-2 py-1 rounded-full text-[10px] font-bold uppercase">
              Processing
            </span>
          </div>
          <div className="space-y-4">
            <div className="flex justify-between items-center">
              <span className="text-xs text-on-surface-variant">Order No</span>
              <span className="text-xs font-bold text-on-surface">{orderId}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-xs text-on-surface-variant">Order Date</span>
              <span className="text-xs font-medium text-on-surface">Oct 24, 2023, 14:32</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-xs text-on-surface-variant">DO Number</span>
              <span className="text-xs font-medium text-on-surface">DO-7729-X</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-xs text-on-surface-variant">Current Status</span>
              <span className="text-xs font-semibold text-primary">Open Order</span>
            </div>
          </div>
        </section>

        {/* Customer Intelligence */}
        <section className="md:col-span-8 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest mb-4">
            Customer Intelligence
          </h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
            <div className="space-y-3">
              <div className="flex items-center gap-3">
                <div className="h-10 w-10 bg-surface-container-high rounded-full flex items-center justify-center text-primary shrink-0">
                  <IconPerson className="h-5 w-5" />
                </div>
                <div>
                  <p className="text-xs font-bold text-on-surface">Alexander Hamilton</p>
                  <p className="text-[11px] text-on-surface-variant">ID: 8829-3329-XXXX</p>
                </div>
              </div>
              <div className="pt-2">
                <p className="text-[11px] font-semibold text-on-surface-variant uppercase mb-1">
                  Contact Details
                </p>
                <p className="text-xs flex items-center gap-2 text-on-surface">
                  <IconMail className="h-4 w-4 text-on-surface-variant shrink-0" />
                  a.hamilton@treasury.gov
                </p>
                <p className="text-xs flex items-center gap-2 mt-1 text-on-surface">
                  <IconPhone className="h-4 w-4 text-on-surface-variant shrink-0" />
                  +1 (555) 019-2234
                </p>
              </div>
            </div>
            <div className="space-y-3">
              <div>
                <p className="text-[11px] font-semibold text-on-surface-variant uppercase mb-1">
                  Membership
                </p>
                <span className="bg-primary-container text-on-primary-container text-[10px] font-bold px-2 py-0.5 rounded">
                  Platinum Member
                </span>
              </div>
              <div className="pt-2">
                <p className="text-[11px] font-semibold text-on-surface-variant uppercase mb-1">
                  Billing Address
                </p>
                <p className="text-xs leading-relaxed text-on-surface">
                  123 Financial Plaza, Floor 14
                  <br />
                  New York City, NY 10005
                  <br />
                  United States
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* Delivery Logistics */}
        <section className="md:col-span-8 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Delivery Logistics
            </h3>
            <div className="flex items-center gap-2 text-on-surface-variant">
              <IconShipping className="h-5 w-5" />
              <span className="text-[11px] font-semibold">Priority Express</span>
            </div>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="p-3 bg-surface-container-low rounded-lg">
              <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                Shipping Status
              </p>
              <p className="text-xs font-semibold text-on-surface">In Transit (Local Hub)</p>
              <p className="text-[10px] text-on-surface-variant mt-1 italic">
                ETA: Tomorrow, 10:00 AM
              </p>
            </div>
            <div className="p-3 bg-surface-container-low rounded-lg">
              <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                Tracking Number
              </p>
              <p className="text-xs font-bold text-primary font-mono select-all">
                TRK-8829-00192-A
              </p>
              <button
                type="button"
                className="text-[10px] text-on-surface-variant hover:text-primary mt-1 underline"
                onClick={() => console.info("[Orders] Real-time map")}
              >
                Real-time Map
              </button>
            </div>
            <div className="p-3 bg-surface-container-low rounded-lg">
              <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                Recipient
              </p>
              <p className="text-xs font-semibold text-on-surface">Alexander Hamilton</p>
              <p className="text-[10px] text-on-surface-variant mt-1 truncate">
                Office Reception - Tower 2
              </p>
            </div>
          </div>
        </section>

        {/* Financial Summary */}
        <section className="md:col-span-4 bg-primary p-5 rounded-xl shadow-lg text-white">
          <h3 className="text-[11px] font-bold uppercase tracking-widest opacity-80 mb-6">
            Financial Summary
          </h3>
          <div className="space-y-3">
            <div className="flex justify-between text-xs">
              <span className="opacity-80">Subtotal</span>
              <span className="font-medium">$1,240.00</span>
            </div>
            <div className="flex justify-between text-xs">
              <span className="opacity-80">Shipping Fee</span>
              <span className="font-medium">$25.00</span>
            </div>
            <div className="flex justify-between text-xs">
              <span className="opacity-80">Promotional Discount</span>
              <span className="font-bold text-yellow-300">-$124.00</span>
            </div>
            <div className="pt-3 mt-3 border-t border-white/20">
              <div className="flex justify-between items-baseline">
                <span className="text-xs font-bold uppercase">Total Payable</span>
                <span className="text-xl font-extrabold tracking-tighter">$1,141.00</span>
              </div>
            </div>
          </div>
          <div className="mt-6 p-2 bg-white/10 rounded border border-white/10">
            <p className="text-[10px] uppercase font-bold opacity-70">Promotion Code</p>
            <p className="text-xs font-mono tracking-widest font-bold">EXECUTIVE-2023-FIRST</p>
          </div>
        </section>

        {/* Order Line Items */}
        <section className="md:col-span-12 overflow-hidden bg-surface-container-lowest rounded-xl shadow-sm border border-outline-variant/10">
          <div className="px-5 py-4 border-b border-outline-variant/10 flex justify-between items-center">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Order Line Items
            </h3>
            <span className="text-[11px] text-on-surface-variant font-medium">3 Items Total</span>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr className="bg-surface-container-high">
                  <th className="text-left py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Product
                  </th>
                  <th className="text-left py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Brand
                  </th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Quantity
                  </th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Unit Price
                  </th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Total
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/10">
                {lineItems.map((item) => (
                  <tr key={item.sku} className="hover:bg-surface-container-low transition-colors">
                    <td className="py-4 px-5">
                      <div className="flex items-center gap-3">
                        <div className="h-12 w-12 rounded bg-surface-container-high border border-outline-variant/20 overflow-hidden shrink-0 flex items-center justify-center text-on-surface-variant/40">
                          <svg viewBox="0 0 24 24" fill="currentColor" className="h-6 w-6">
                            <path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z" />
                          </svg>
                        </div>
                        <div>
                          <p className="text-xs font-bold text-on-surface">{item.name}</p>
                          <p className="text-[10px] text-on-surface-variant">SKU: {item.sku}</p>
                        </div>
                      </div>
                    </td>
                    <td className="py-4 px-5 text-xs text-on-surface-variant font-medium">
                      {item.brand}
                    </td>
                    <td className="py-4 px-5 text-right text-xs font-semibold text-on-surface">
                      {item.qty}
                    </td>
                    <td className="py-4 px-5 text-right text-xs text-on-surface-variant font-medium">
                      {item.unitPrice}
                    </td>
                    <td className="py-4 px-5 text-right text-xs font-bold text-on-surface">
                      {item.total}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        {/* Internal System Log & Remarks */}
        <section className="md:col-span-12 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest mb-4">
            Internal System Log &amp; Remarks
          </h3>
          <div className="space-y-4">
            {/* Log entry 1 */}
            <div className="flex gap-4 relative">
              <div className="absolute left-[11px] top-6 bottom-0 w-0.5 bg-outline-variant/20" />
              <div className="h-6 w-6 rounded-full bg-primary-container text-on-primary-container flex items-center justify-center z-10 shrink-0">
                <IconCheckCircle className="h-3.5 w-3.5" />
              </div>
              <div className="pb-4">
                <div className="flex items-center gap-2 mb-1">
                  <span className="text-xs font-bold text-on-surface">Order Verified</span>
                  <span className="text-[10px] text-on-surface-variant">Today, 09:12 AM</span>
                </div>
                <p className="text-xs text-on-surface-variant">
                  Identity verification successful. Address match confirmed via GIS services.
                </p>
                <p className="text-[10px] font-semibold text-primary mt-1">Operator: Sarah J.</p>
              </div>
            </div>
            {/* Log entry 2 */}
            <div className="flex gap-4 relative">
              <div className="absolute left-[11px] top-6 bottom-0 w-0.5 bg-outline-variant/20" />
              <div className="h-6 w-6 rounded-full bg-surface-container-high text-on-surface-variant flex items-center justify-center z-10 shrink-0">
                <IconShipping className="h-3.5 w-3.5" />
              </div>
              <div className="pb-4">
                <div className="flex items-center gap-2 mb-1">
                  <span className="text-xs font-bold text-on-surface">Dispatched to Warehouse</span>
                  <span className="text-[10px] text-on-surface-variant">Today, 08:00 AM</span>
                </div>
                <p className="text-xs text-on-surface-variant">
                  Consolidated with Batch #NY-8829 for priority processing at Warehouse 4B.
                </p>
              </div>
            </div>
            {/* Log entry 3 */}
            <div className="flex gap-4">
              <div className="h-6 w-6 rounded-full bg-surface-container-high text-on-surface-variant flex items-center justify-center z-10 shrink-0">
                <IconEditNote className="h-3.5 w-3.5" />
              </div>
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="text-xs font-bold text-on-surface">Manual Note Added</span>
                  <span className="text-[10px] text-on-surface-variant">Yesterday, 17:45 PM</span>
                </div>
                <p className="text-xs text-on-surface italic">
                  "Customer requested to leave the package at the main reception if the office is
                  closed."
                </p>
                <p className="text-[10px] font-semibold text-primary mt-1">
                  Operator: System Automated
                </p>
              </div>
            </div>
          </div>

          {/* Add remark input */}
          <div className="mt-6 flex gap-2">
            <input
              type="text"
              className="flex-1 bg-surface-container-low border border-outline-variant/20 rounded-md px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary"
              placeholder="Type an internal remark..."
            />
            <button
              type="button"
              className="bg-primary text-white px-4 py-2 rounded text-xs font-semibold hover:bg-primary-container transition-colors"
              onClick={() => console.info("[Orders] Post note")}
            >
              Post Note
            </button>
          </div>
        </section>
      </div>
    </div>
  );
}

const lineItems = [
  {
    name: "Air Velocity Max X2",
    sku: "AV-MAX-0019",
    brand: "Lumina Sport",
    qty: 1,
    unitPrice: "$450.00",
    total: "$450.00",
  },
  {
    name: "Heritage Chrono Silver",
    sku: "WTCH-H-042",
    brand: "Stark & Co.",
    qty: 1,
    unitPrice: "$590.00",
    total: "$590.00",
  },
  {
    name: "Zenith Wireless Audio",
    sku: "AUD-ZW-100",
    brand: "AudioZen",
    qty: 1,
    unitPrice: "$200.00",
    total: "$200.00",
  },
];
