import { useState } from "react";
import {
  IconArrowBack,
  IconBolt,
  IconCancel,
  IconCheckCircle,
  IconChevronDown,
  IconEditNote,
  IconMail,
  IconPerson,
  IconPhone,
  IconPrint,
  IconShipping,
  IconVisibility,
} from "../icons";

type OrderMode = "view" | "action";

type Props = {
  orderId: string;
  mode: OrderMode;
  onBack: () => void;
};

const ORDER_STATUSES = [
  "Open Order",
  "Processing",
  "Ready for Delivery",
  "Ready for Pickup",
  "Out for Delivery",
  "Picked Up",
  "Delivered",
  "Returned",
  "Admin Cancelled",
  "User Cancelled",
  "Partially Fulfilled",
] as const;

type OrderStatus = (typeof ORDER_STATUSES)[number];

const ORDER_TYPES = ["Delivery", "Self Collection", "Special"] as const;

const STATUS_STYLES: Record<OrderStatus, string> = {
  "Open Order": "bg-secondary-container/20 text-on-secondary-container",
  "Processing": "bg-blue-100 text-blue-700",
  "Ready for Delivery": "bg-cyan-100 text-cyan-700",
  "Ready for Pickup": "bg-cyan-100 text-cyan-700",
  "Out for Delivery": "bg-indigo-100 text-indigo-700",
  "Picked Up": "bg-teal-100 text-teal-700",
  "Delivered": "bg-green-100 text-green-700",
  "Returned": "bg-orange-100 text-orange-700",
  "Admin Cancelled": "bg-red-100 text-red-700",
  "User Cancelled": "bg-red-100 text-red-700",
  "Partially Fulfilled": "bg-yellow-100 text-yellow-700",
};

export function OrderDetailPage({ orderId, mode, onBack }: Props) {
  const isAction = mode === "action";

  // Group Actions dropdown
  const [groupActionsOpen, setGroupActionsOpen] = useState(false);
  const [orderStatus, setOrderStatus] = useState<OrderStatus>("Open Order");

  // Print dropdown
  const [printMenuOpen, setPrintMenuOpen] = useState(false);

  // Resend confirmation modal
  const [resendModalOpen, setResendModalOpen] = useState(false);
  const [resendMessage, setResendMessage] = useState("");

  // Customer edit
  const [editingCustomer, setEditingCustomer] = useState(false);
  const [customer, setCustomer] = useState({
    name: "Alexander Hamilton",
    email: "a.hamilton@treasury.gov",
    phone: "+1 (555) 019-2234",
    address: "123 Financial Plaza, Floor 14\nNew York City, NY 10005\nUnited States",
  });
  const [customerDraft, setCustomerDraft] = useState(customer);

  // Delivery edit
  const [editingDelivery, setEditingDelivery] = useState(false);
  const [delivery, setDelivery] = useState({
    orderType: "Delivery" as (typeof ORDER_TYPES)[number],
    logisticPartner: "Priority Express",
    trackingNumber: "TRK-8829-00192-A",
    timeslot: "Tomorrow, 10:00 AM – 12:00 PM",
    actualDeliveryFee: "25.00",
  });
  const [deliveryDraft, setDeliveryDraft] = useState(delivery);

  // Item-level statuses + cancellations
  const [itemStatuses, setItemStatuses] = useState<Record<string, OrderStatus>>({
    "AV-MAX-0019": "Open Order",
    "WTCH-H-042": "Open Order",
    "AUD-ZW-100": "Open Order",
  });
  const [cancelledItems, setCancelledItems] = useState<Set<string>>(new Set());

  // Remark input
  const [remarkText, setRemarkText] = useState("");

  function handleGroupStatusChange(status: OrderStatus) {
    setOrderStatus(status);
    setGroupActionsOpen(false);
  }

  function handleItemStatusChange(sku: string, status: OrderStatus) {
    setItemStatuses((prev) => ({ ...prev, [sku]: status }));
  }

  function handleCancelItem(sku: string) {
    setCancelledItems((prev) => new Set(prev).add(sku));
    setItemStatuses((prev) => ({ ...prev, [sku]: "Admin Cancelled" }));
  }

  function handlePostRemark() {
    if (!remarkText.trim()) return;
    console.info("[Orders] Post remark:", remarkText);
    setRemarkText("");
  }

  function handleResendConfirmation() {
    console.info("[Orders] Resend confirmation with message:", resendMessage);
    setResendModalOpen(false);
    setResendMessage("");
  }

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
            <span className="hover:text-primary cursor-pointer transition-colors" onClick={onBack}>
              Order Processing
            </span>
            <span>/</span>
            <span className="text-primary">Order Details</span>
          </nav>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight text-on-surface font-headline">
              Order #{orderId}
            </h1>
            {isAction ? (
              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase bg-amber-100 text-amber-700 border border-amber-200">
                <IconBolt className="h-3 w-3" />
                Processing
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase bg-surface-container-high text-on-surface-variant border border-outline-variant/30">
                <IconVisibility className="h-3 w-3" />
                View Only
              </span>
            )}
          </div>
        </div>

        <div className="flex gap-2 shrink-0 flex-wrap">
          {/* Print dropdown */}
          <div className="relative">
            <button
              type="button"
              className="flex items-center gap-2 px-4 py-2 text-primary font-semibold text-xs border border-outline-variant/30 rounded-lg bg-white hover:bg-surface-container-low transition-all"
              onClick={() => { setPrintMenuOpen((v) => !v); setGroupActionsOpen(false); }}
            >
              <IconPrint className="h-4 w-4" />
              Print
              <IconChevronDown className="h-3 w-3" />
            </button>
            {printMenuOpen && (
              <div className="absolute right-0 top-full mt-1 w-48 bg-white rounded-lg shadow-lg border border-outline-variant/20 z-20 py-1">
                {["Delivery Receipt", "Gift Receipt", "Packing Slip"].map((doc) => (
                  <button
                    key={doc}
                    type="button"
                    className="w-full text-left px-4 py-2 text-xs hover:bg-surface-container-low text-on-surface transition-colors"
                    onClick={() => { console.info(`[Orders] Print ${doc}`); setPrintMenuOpen(false); }}
                  >
                    {doc}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Action-only buttons */}
          {isAction && (
            <>
              {/* Group Actions */}
              <div className="relative">
                <button
                  type="button"
                  className="flex items-center gap-2 px-4 py-2 text-xs font-semibold border border-outline-variant/30 rounded-lg bg-white hover:bg-surface-container-low transition-all text-on-surface"
                  onClick={() => { setGroupActionsOpen((v) => !v); setPrintMenuOpen(false); }}
                >
                  Group Actions
                  <IconChevronDown className="h-3 w-3" />
                </button>
                {groupActionsOpen && (
                  <div className="absolute right-0 top-full mt-1 w-52 bg-white rounded-lg shadow-lg border border-outline-variant/20 z-20 py-1">
                    <p className="px-4 py-2 text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
                      Update All Items To
                    </p>
                    {ORDER_STATUSES.map((s) => (
                      <button
                        key={s}
                        type="button"
                        className={`w-full text-left px-4 py-2 text-xs hover:bg-surface-container-low transition-colors flex items-center gap-2 ${
                          orderStatus === s ? "font-bold text-primary" : "text-on-surface"
                        }`}
                        onClick={() => handleGroupStatusChange(s)}
                      >
                        {orderStatus === s && <span className="w-1.5 h-1.5 rounded-full bg-primary shrink-0" />}
                        {s}
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* Resend Confirmation */}
              <button
                type="button"
                className="flex items-center gap-2 px-4 py-2 text-xs font-semibold border border-outline-variant/30 rounded-lg bg-white hover:bg-surface-container-low transition-all text-on-surface"
                onClick={() => setResendModalOpen(true)}
              >
                <IconMail className="h-4 w-4" />
                Resend Confirmation
              </button>

              {/* Update Status (primary CTA) */}
              <button
                type="button"
                className="px-4 py-2 bg-primary text-white font-semibold text-xs rounded-lg shadow-md hover:bg-primary-container transition-all"
                onClick={() => console.info("[Orders] Save changes")}
              >
                Save Changes
              </button>
            </>
          )}
        </div>
      </div>

      {/* Order status banner — action mode only */}
      {isAction && (
        <div className="flex items-center gap-3 px-4 py-3 bg-amber-50 border border-amber-200 rounded-xl text-xs">
          <IconBolt className="h-4 w-4 text-amber-600 shrink-0" />
          <span className="text-amber-800">
            <span className="font-bold">Current Order Status:</span>{" "}
            <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[orderStatus]}`}>
              {orderStatus}
            </span>
            {" "}— Use <strong>Group Actions</strong> to update all items, or change individual item statuses below.
          </span>
        </div>
      )}

      {/* Bento grid */}
      <div className="grid grid-cols-1 md:grid-cols-12 gap-6">
        {/* Order Information */}
        <section className="md:col-span-4 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Order Information
            </h3>
            <span className={`px-2 py-1 rounded-full text-[10px] font-bold uppercase ${STATUS_STYLES[orderStatus]}`}>
              {orderStatus}
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
            <div className="flex justify-between items-start">
              <span className="text-xs text-on-surface-variant">Order Type</span>
              {isAction ? (
                <select
                  className="text-xs font-semibold text-primary bg-transparent border-0 border-b border-primary/30 focus:ring-0 focus:border-primary cursor-pointer text-right"
                  value={deliveryDraft.orderType}
                  onChange={(e) =>
                    setDeliveryDraft((d) => ({ ...d, orderType: e.target.value as typeof delivery.orderType }))
                  }
                >
                  {ORDER_TYPES.map((t) => (
                    <option key={t}>{t}</option>
                  ))}
                </select>
              ) : (
                <span className="text-xs font-semibold text-primary">{delivery.orderType}</span>
              )}
            </div>
          </div>
        </section>

        {/* Customer & Payment */}
        <section className="md:col-span-8 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Customer &amp; Payment
            </h3>
            {isAction && !editingCustomer && (
              <button
                type="button"
                className="text-[10px] font-bold text-primary hover:underline"
                onClick={() => { setCustomerDraft(customer); setEditingCustomer(true); }}
              >
                Edit
              </button>
            )}
            {isAction && editingCustomer && (
              <div className="flex gap-2">
                <button
                  type="button"
                  className="text-[10px] font-bold text-primary hover:underline"
                  onClick={() => { setCustomer(customerDraft); setEditingCustomer(false); }}
                >
                  Save
                </button>
                <button
                  type="button"
                  className="text-[10px] font-bold text-on-surface-variant hover:underline"
                  onClick={() => setEditingCustomer(false)}
                >
                  Cancel
                </button>
              </div>
            )}
          </div>

          {editingCustomer ? (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-3">
                <Field label="Full Name">
                  <input
                    className={inputCls}
                    value={customerDraft.name}
                    onChange={(e) => setCustomerDraft((d) => ({ ...d, name: e.target.value }))}
                  />
                </Field>
                <Field label="Email">
                  <input
                    className={inputCls}
                    type="email"
                    value={customerDraft.email}
                    onChange={(e) => setCustomerDraft((d) => ({ ...d, email: e.target.value }))}
                  />
                </Field>
                <Field label="Phone">
                  <input
                    className={inputCls}
                    value={customerDraft.phone}
                    onChange={(e) => setCustomerDraft((d) => ({ ...d, phone: e.target.value }))}
                  />
                </Field>
              </div>
              <div className="space-y-3">
                <Field label="Billing Address">
                  <textarea
                    className={`${inputCls} h-24 resize-none`}
                    value={customerDraft.address}
                    onChange={(e) => setCustomerDraft((d) => ({ ...d, address: e.target.value }))}
                  />
                </Field>
              </div>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="space-y-3">
                <div className="flex items-center gap-3">
                  <div className="h-10 w-10 bg-surface-container-high rounded-full flex items-center justify-center text-primary shrink-0">
                    <IconPerson className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="text-xs font-bold text-on-surface">{customer.name}</p>
                    <p className="text-[11px] text-on-surface-variant">ID: 8829-3329-XXXX</p>
                  </div>
                </div>
                <div className="pt-2">
                  <p className="text-[11px] font-semibold text-on-surface-variant uppercase mb-1">
                    Contact Details
                  </p>
                  <p className="text-xs flex items-center gap-2 text-on-surface">
                    <IconMail className="h-4 w-4 text-on-surface-variant shrink-0" />
                    {customer.email}
                  </p>
                  <p className="text-xs flex items-center gap-2 mt-1 text-on-surface">
                    <IconPhone className="h-4 w-4 text-on-surface-variant shrink-0" />
                    {customer.phone}
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
                  <p className="text-xs leading-relaxed text-on-surface whitespace-pre-line">
                    {customer.address}
                  </p>
                </div>
              </div>
            </div>
          )}

          {/* Payment methods */}
          <div className="mt-4 pt-4 border-t border-outline-variant/10">
            <p className="text-[11px] font-semibold text-on-surface-variant uppercase mb-2">
              Payment Methods
            </p>
            <div className="flex flex-wrap gap-2">
              <span className="px-2 py-1 bg-surface-container-low rounded text-[10px] font-semibold text-on-surface">
                Payment Gateway — $1,100.00
              </span>
              <span className="px-2 py-1 bg-surface-container-low rounded text-[10px] font-semibold text-on-surface">
                E-Voucher — $41.00
              </span>
            </div>
          </div>
        </section>

        {/* Delivery Logistics */}
        <section className="md:col-span-8 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Delivery Order Details
            </h3>
            {isAction && !editingDelivery && (
              <button
                type="button"
                className="text-[10px] font-bold text-primary hover:underline"
                onClick={() => { setDeliveryDraft(delivery); setEditingDelivery(true); }}
              >
                Edit
              </button>
            )}
            {isAction && editingDelivery && (
              <div className="flex gap-2">
                <button
                  type="button"
                  className="text-[10px] font-bold text-primary hover:underline"
                  onClick={() => { setDelivery(deliveryDraft); setEditingDelivery(false); }}
                >
                  Save
                </button>
                <button
                  type="button"
                  className="text-[10px] font-bold text-on-surface-variant hover:underline"
                  onClick={() => setEditingDelivery(false)}
                >
                  Cancel
                </button>
              </div>
            )}
          </div>

          {editingDelivery ? (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-3">
                <Field label="Logistic Partner">
                  <input
                    className={inputCls}
                    value={deliveryDraft.logisticPartner}
                    onChange={(e) => setDeliveryDraft((d) => ({ ...d, logisticPartner: e.target.value }))}
                  />
                </Field>
                <Field label="Tracking Number">
                  <input
                    className={inputCls}
                    value={deliveryDraft.trackingNumber}
                    onChange={(e) => setDeliveryDraft((d) => ({ ...d, trackingNumber: e.target.value }))}
                  />
                </Field>
              </div>
              <div className="space-y-3">
                <Field label="Delivery / Collection Timeslot">
                  <input
                    className={inputCls}
                    value={deliveryDraft.timeslot}
                    onChange={(e) => setDeliveryDraft((d) => ({ ...d, timeslot: e.target.value }))}
                  />
                </Field>
                <Field label="Actual Delivery Fee (Admin Override)">
                  <div className="flex items-center">
                    <span className="px-2.5 py-2 text-xs bg-surface-container-high border border-outline-variant/20 rounded-l text-on-surface-variant">
                      $
                    </span>
                    <input
                      className={`${inputCls} rounded-l-none`}
                      type="number"
                      min="0"
                      step="0.01"
                      value={deliveryDraft.actualDeliveryFee}
                      onChange={(e) => setDeliveryDraft((d) => ({ ...d, actualDeliveryFee: e.target.value }))}
                    />
                  </div>
                  <p className="text-[10px] text-on-surface-variant mt-1">
                    Does not affect the customer's sales order value.
                  </p>
                </Field>
              </div>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="p-3 bg-surface-container-low rounded-lg">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                  Logistic Partner
                </p>
                <p className="text-xs font-semibold text-on-surface">{delivery.logisticPartner}</p>
              </div>
              <div className="p-3 bg-surface-container-low rounded-lg">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                  Tracking Number
                </p>
                <p className="text-xs font-bold text-primary font-mono select-all">
                  {delivery.trackingNumber}
                </p>
              </div>
              <div className="p-3 bg-surface-container-low rounded-lg">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                  Timeslot
                </p>
                <p className="text-xs font-semibold text-on-surface">{delivery.timeslot}</p>
              </div>
              <div className="p-3 bg-surface-container-low rounded-lg md:col-span-1">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                  Recipient
                </p>
                <p className="text-xs font-semibold text-on-surface">{customer.name}</p>
                <p className="text-[10px] text-on-surface-variant mt-1 truncate">
                  Office Reception - Tower 2
                </p>
              </div>
              <div className="p-3 bg-surface-container-low rounded-lg md:col-span-2">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                  Actual Delivery Fee
                </p>
                <p className="text-xs font-bold text-on-surface">${delivery.actualDeliveryFee}</p>
                <p className="text-[10px] text-on-surface-variant mt-1">
                  Admin override — does not affect customer total.
                </p>
              </div>
            </div>
          )}
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
            <div className="flex items-center gap-3">
              <span className="text-[11px] text-on-surface-variant font-medium">3 Items Total</span>
              {isAction && (
                <button
                  type="button"
                  className="text-[10px] font-bold text-primary hover:underline"
                  onClick={() => console.info("[Orders] Add product")}
                >
                  + Add Product
                </button>
              )}
            </div>
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
                    Qty
                  </th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Unit Price
                  </th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Total
                  </th>
                  <th className="text-center py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                    Item Status
                  </th>
                  {isAction && (
                    <th className="text-center py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">
                      Action
                    </th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/10">
                {lineItems.map((item) => {
                  const isCancelled = cancelledItems.has(item.sku);
                  return (
                    <tr
                      key={item.sku}
                      className={`hover:bg-surface-container-low transition-colors ${isCancelled ? "opacity-40" : ""}`}
                    >
                      <td className="py-4 px-5">
                        <div className="flex items-center gap-3">
                          <div className="h-12 w-12 rounded bg-surface-container-high border border-outline-variant/20 overflow-hidden shrink-0 flex items-center justify-center text-on-surface-variant/40">
                            <IconShipping className="h-6 w-6" />
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
                      <td className="py-4 px-5 text-center">
                        {isAction && !isCancelled ? (
                          <select
                            className="text-[10px] font-bold bg-surface-container-low border border-outline-variant/20 rounded px-2 py-1 focus:ring-1 focus:ring-primary/40 cursor-pointer"
                            value={itemStatuses[item.sku]}
                            onChange={(e) =>
                              handleItemStatusChange(item.sku, e.target.value as OrderStatus)
                            }
                          >
                            {ORDER_STATUSES.map((s) => (
                              <option key={s}>{s}</option>
                            ))}
                          </select>
                        ) : (
                          <span
                            className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${
                              STATUS_STYLES[itemStatuses[item.sku]]
                            }`}
                          >
                            {itemStatuses[item.sku]}
                          </span>
                        )}
                      </td>
                      {isAction && (
                        <td className="py-4 px-5 text-center">
                          {isCancelled ? (
                            <span className="text-[10px] text-on-surface-variant italic">Cancelled</span>
                          ) : (
                            <button
                              type="button"
                              className="p-1.5 rounded hover:bg-red-50 text-on-surface-variant hover:text-red-600 transition-all"
                              aria-label="Cancel item"
                              title="Cancel item"
                              onClick={() => handleCancelItem(item.sku)}
                            >
                              <IconCancel className="h-4 w-4" />
                            </button>
                          )}
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Create Shipment — action mode only */}
          {isAction && (
            <div className="px-5 py-3 border-t border-outline-variant/10 flex justify-end">
              <button
                type="button"
                className="flex items-center gap-2 px-4 py-2 text-xs font-semibold bg-surface-container-high hover:bg-surface-container text-on-surface rounded-lg transition-colors"
                onClick={() => console.info("[Orders] Create shipment")}
              >
                <IconShipping className="h-4 w-4" />
                Create Shipment
              </button>
            </div>
          )}
        </section>

        {/* Internal System Log & Remarks */}
        <section className="md:col-span-12 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest mb-4">
            Internal System Log &amp; Remarks
          </h3>
          <div className="space-y-4">
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
                <p className="text-[10px] font-semibold text-primary mt-1">Operator: System Automated</p>
              </div>
            </div>
          </div>

          {/* Add remark — action mode only */}
          {isAction && (
            <div className="mt-6 flex gap-2">
              <input
                type="text"
                className="flex-1 bg-surface-container-low border border-outline-variant/20 rounded-md px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary"
                placeholder="Type an internal remark..."
                value={remarkText}
                onChange={(e) => setRemarkText(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && handlePostRemark()}
              />
              <button
                type="button"
                className="bg-primary text-white px-4 py-2 rounded text-xs font-semibold hover:bg-primary-container transition-colors"
                onClick={handlePostRemark}
              >
                Post Note
              </button>
            </div>
          )}
        </section>
      </div>

      {/* Resend Confirmation Modal */}
      {resendModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md mx-4 p-6 space-y-4">
            <h2 className="text-sm font-bold text-on-surface">Resend Order Confirmation</h2>
            <p className="text-xs text-on-surface-variant">
              Resend the order confirmation email to{" "}
              <span className="font-semibold text-on-surface">{customer.email}</span>.
            </p>
            <div className="space-y-1">
              <label className="block text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
                Custom Message (optional)
              </label>
              <textarea
                className="w-full bg-surface-container-low border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary h-24 resize-none"
                placeholder="Add a personal note to the customer..."
                value={resendMessage}
                onChange={(e) => setResendMessage(e.target.value)}
              />
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                className="px-4 py-2 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-low rounded-lg transition-colors"
                onClick={() => { setResendModalOpen(false); setResendMessage(""); }}
              >
                Cancel
              </button>
              <button
                type="button"
                className="px-4 py-2 text-xs font-bold bg-primary text-white rounded-lg hover:bg-primary-container transition-colors"
                onClick={handleResendConfirmation}
              >
                Send Email
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <label className="block text-[11px] font-bold text-on-surface-variant uppercase tracking-wider">
        {label}
      </label>
      {children}
    </div>
  );
}

const inputCls =
  "w-full bg-surface-container-low border border-outline-variant/20 rounded px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary";

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
