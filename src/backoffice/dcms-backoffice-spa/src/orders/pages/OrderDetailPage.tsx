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
  IconReceipt,
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
  "Partially Fulfilled",
  "Pending Cancellation",
  "User Cancelled",
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
  "Partially Fulfilled": "bg-yellow-100 text-yellow-700",
  "Pending Cancellation": "bg-orange-100 text-orange-800",
  "User Cancelled": "bg-red-100 text-red-700",
};

// Statuses that lock an order from further editing
const LOCKED_STATUSES: OrderStatus[] = ["Picked Up", "Returned", "Delivered"];

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

  // Payment Details modal
  const [paymentModalOpen, setPaymentModalOpen] = useState(false);

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
    contactName: "Alexander Hamilton",
    contactNumber: "+1 (555) 019-2234",
    cardNo: "VISA ****4321",
    receiptNo: "RCP-2024-001122",
    shippingStatus: "In Transit",
    logisticPartner: "Priority Express",
    trackingNumber: "TRK-8829-00192-A",
    deliveryOption: "Standard Delivery (2–3 Business Days)",
    deliveryDate: "26 Oct 2023",
    timeslot: "Tomorrow, 10:00 AM – 12:00 PM",
    collectionOption: "",
    collectionDate: "",
    address: "123 Financial Plaza, Floor 14\nNew York City, NY 10005\nUnited States",
    deliveryItemsAmount: "1,240.00",
    deliveryFeeAmount: "25.00",
    discount: "0.00",
    deliveryRemarks: "",
    actualDeliveryFee: "25.00",
  });
  const [deliveryDraft, setDeliveryDraft] = useState(delivery);

  // Item-level statuses — confirmed values
  const [itemStatuses, setItemStatuses] = useState<Record<string, OrderStatus>>({
    "AV-MAX-0019": "Open Order",
    "WTCH-H-042": "Open Order",
    "AUD-ZW-100": "Open Order",
  });
  // Item-level status drafts — staged (pending Update click)
  const [itemStatusDrafts, setItemStatusDrafts] = useState<Record<string, OrderStatus>>({
    "AV-MAX-0019": "Open Order",
    "WTCH-H-042": "Open Order",
    "AUD-ZW-100": "Open Order",
  });
  const [cancelledItems, setCancelledItems] = useState<Set<string>>(new Set());

  // Remark input
  const [remarkText, setRemarkText] = useState("");

  const isLocked = LOCKED_STATUSES.includes(orderStatus);

  function handleGroupStatusChange(status: OrderStatus) {
    setOrderStatus(status);
    setGroupActionsOpen(false);
  }

  function handleItemStatusDraftChange(sku: string, status: OrderStatus) {
    setItemStatusDrafts((prev) => ({ ...prev, [sku]: status }));
  }

  function handleUpdateItemStatus(sku: string) {
    const newStatus = itemStatusDrafts[sku];
    const updatedStatuses = { ...itemStatuses, [sku]: newStatus };
    setItemStatuses(updatedStatuses);

    // Validation: if all items are cancelled, derive order-level status from the most recent update
    const allCancelled = Object.values(updatedStatuses).every(
      (s) => s === "Admin Cancelled" || s === "User Cancelled"
    );
    if (allCancelled) {
      setOrderStatus(newStatus as "Admin Cancelled" | "User Cancelled");
    }
    console.info(`[Orders] Update item ${sku} → ${newStatus}`);
  }

  function handleCancelItem(sku: string) {
    setCancelledItems((prev) => new Set(prev).add(sku));
    const updatedStatuses = { ...itemStatuses, [sku]: "Admin Cancelled" as OrderStatus };
    setItemStatuses(updatedStatuses);
    setItemStatusDrafts((prev) => ({ ...prev, [sku]: "Admin Cancelled" }));

    const allCancelled = Object.values(updatedStatuses).every(
      (s) => s === "Admin Cancelled" || s === "User Cancelled"
    );
    if (allCancelled) {
      setOrderStatus("Admin Cancelled");
    }
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

  const canEdit = isAction && !isLocked;

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
          {/* Actions dropdown */}
          <div className="relative">
            <button
              type="button"
              className="flex items-center gap-2 px-4 py-2 text-primary font-semibold text-xs border border-outline-variant/30 rounded-lg bg-white hover:bg-surface-container-low transition-all"
              onClick={() => { setPrintMenuOpen((v) => !v); setGroupActionsOpen(false); }}
            >
              <IconPrint className="h-4 w-4" />
              Actions
              <IconChevronDown className="h-3 w-3" />
            </button>
            {printMenuOpen && (
              <div className="absolute right-0 top-full mt-1 w-56 bg-white rounded-lg shadow-lg border border-outline-variant/20 z-20 py-1">
                <p className="px-4 pt-2 pb-1 text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
                  Print
                </p>
                {["Delivery Receipt", "Packing Slip"].map((doc) => (
                  <button
                    key={doc}
                    type="button"
                    className="w-full text-left px-4 py-2 text-xs hover:bg-surface-container-low text-on-surface transition-colors"
                    onClick={() => { console.info(`[Orders] Print ${doc}`); setPrintMenuOpen(false); }}
                  >
                    {doc}
                  </button>
                ))}
                <div className="border-t border-outline-variant/10 mt-1 pt-1">
                  <button
                    type="button"
                    className="w-full text-left px-4 py-2 text-xs hover:bg-surface-container-low text-on-surface transition-colors flex items-center gap-2"
                    onClick={() => { console.info("[Orders] View Order Confirmation"); setPrintMenuOpen(false); }}
                  >
                    <IconReceipt className="h-3.5 w-3.5 text-on-surface-variant" />
                    View Order Confirmation
                  </button>
                  {isAction && (
                    <button
                      type="button"
                      className="w-full text-left px-4 py-2 text-xs hover:bg-surface-container-low text-on-surface transition-colors flex items-center gap-2"
                      onClick={() => { setResendModalOpen(true); setPrintMenuOpen(false); }}
                    >
                      <IconMail className="h-3.5 w-3.5 text-on-surface-variant" />
                      Resend Order Confirmation
                    </button>
                  )}
                </div>
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
                  <div className="absolute right-0 top-full mt-1 w-56 bg-white rounded-lg shadow-lg border border-outline-variant/20 z-20 py-1">
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

              {/* Save (primary CTA) */}
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
          {isLocked && (
            <span className="ml-auto text-[10px] font-bold text-red-600 bg-red-50 border border-red-200 px-2 py-0.5 rounded">
              Order Locked — No Further Edits
            </span>
          )}
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
          <div className="space-y-3">
            <InfoRow label="Order No" value={<span className="font-bold">{orderId}</span>} />
            <InfoRow label="Order Date" value="Oct 24, 2023, 14:32" />
            <InfoRow label="DO Number" value="DO-7729-X" />
            <InfoRow label="Delivery Orders" value="1" />
            <InfoRow
              label="Order Type"
              value={
                canEdit ? (
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
                  <span className="font-semibold text-primary">{delivery.orderType}</span>
                )
              }
            />
            <InfoRow
              label="Payment Method"
              value={
                <button
                  type="button"
                  className="text-xs font-semibold text-primary hover:underline focus:outline-none"
                  onClick={() => setPaymentModalOpen(true)}
                  title="View payment details"
                >
                  Multi Payment ↗
                </button>
              }
            />
          </div>
        </section>

        {/* Customer & Payment */}
        <section className="md:col-span-8 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Customer &amp; Payment
            </h3>
            {canEdit && !editingCustomer && (
              <button
                type="button"
                className="text-[10px] font-bold text-primary hover:underline"
                onClick={() => { setCustomerDraft(customer); setEditingCustomer(true); }}
              >
                Edit
              </button>
            )}
            {canEdit && editingCustomer && (
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

          {/* Payment methods — clickable to show details */}
          <div className="mt-4 pt-4 border-t border-outline-variant/10">
            <div className="flex items-center justify-between mb-2">
              <p className="text-[11px] font-semibold text-on-surface-variant uppercase">
                Payment Methods
              </p>
              <button
                type="button"
                className="text-[10px] font-bold text-primary hover:underline"
                onClick={() => setPaymentModalOpen(true)}
              >
                View Details
              </button>
            </div>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className="px-2 py-1 bg-surface-container-low rounded text-[10px] font-semibold text-on-surface hover:bg-primary/10 hover:text-primary transition-colors cursor-pointer"
                onClick={() => setPaymentModalOpen(true)}
                title="View payment details"
              >
                Payment Gateway — $1,100.00
              </button>
              <button
                type="button"
                className="px-2 py-1 bg-surface-container-low rounded text-[10px] font-semibold text-on-surface hover:bg-primary/10 hover:text-primary transition-colors cursor-pointer"
                onClick={() => setPaymentModalOpen(true)}
                title="View payment details"
              >
                E-Voucher — $41.00
              </button>
            </div>
            <p className="text-[10px] text-on-surface-variant mt-1">
              Click a payment method to view transaction details.
            </p>
          </div>
        </section>

        {/* Delivery Order Details */}
        <section className="md:col-span-8 bg-surface-container-lowest p-5 rounded-xl shadow-sm border border-outline-variant/10">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-[11px] font-bold text-on-surface-variant uppercase tracking-widest">
              Delivery Order Details
            </h3>
            {canEdit && !editingDelivery && (
              <button
                type="button"
                className="text-[10px] font-bold text-primary hover:underline"
                onClick={() => { setDeliveryDraft(delivery); setEditingDelivery(true); }}
              >
                Edit
              </button>
            )}
            {canEdit && editingDelivery && (
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
              <Field label="Contact Name">
                <input
                  className={inputCls}
                  value={deliveryDraft.contactName}
                  onChange={(e) => setDeliveryDraft((d) => ({ ...d, contactName: e.target.value }))}
                />
              </Field>
              <Field label="Contact Number">
                <input
                  className={inputCls}
                  value={deliveryDraft.contactNumber}
                  onChange={(e) => setDeliveryDraft((d) => ({ ...d, contactNumber: e.target.value }))}
                />
              </Field>
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
              {deliveryDraft.orderType === "Delivery" && (
                <>
                  <Field label="Delivery Option">
                    <input
                      className={inputCls}
                      value={deliveryDraft.deliveryOption}
                      onChange={(e) => setDeliveryDraft((d) => ({ ...d, deliveryOption: e.target.value }))}
                    />
                  </Field>
                  <Field label="Delivery Date">
                    <input
                      className={inputCls}
                      value={deliveryDraft.deliveryDate}
                      onChange={(e) => setDeliveryDraft((d) => ({ ...d, deliveryDate: e.target.value }))}
                    />
                  </Field>
                </>
              )}
              {deliveryDraft.orderType === "Self Collection" && (
                <>
                  <Field label="Collection Option">
                    <input
                      className={inputCls}
                      value={deliveryDraft.collectionOption}
                      onChange={(e) => setDeliveryDraft((d) => ({ ...d, collectionOption: e.target.value }))}
                    />
                  </Field>
                  <Field label="Collection Date">
                    <input
                      className={inputCls}
                      value={deliveryDraft.collectionDate}
                      onChange={(e) => setDeliveryDraft((d) => ({ ...d, collectionDate: e.target.value }))}
                    />
                  </Field>
                </>
              )}
              <Field label="Delivery / Collection Timeslot">
                <input
                  className={inputCls}
                  value={deliveryDraft.timeslot}
                  onChange={(e) => setDeliveryDraft((d) => ({ ...d, timeslot: e.target.value }))}
                />
              </Field>
              <div className="md:col-span-2">
                <Field label="Delivery Address">
                  <textarea
                    className={`${inputCls} h-20 resize-none`}
                    value={deliveryDraft.address}
                    onChange={(e) => setDeliveryDraft((d) => ({ ...d, address: e.target.value }))}
                  />
                </Field>
              </div>
              <div className="md:col-span-2">
                <Field label="Remarks">
                  <input
                    className={inputCls}
                    value={deliveryDraft.deliveryRemarks}
                    onChange={(e) => setDeliveryDraft((d) => ({ ...d, deliveryRemarks: e.target.value }))}
                  />
                </Field>
              </div>
              <div className="md:col-span-2">
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
                    Does not affect the customer's sales order value. Only available if logistic partner is not system-integrated.
                  </p>
                </Field>
              </div>
            </div>
          ) : (
            <div className="space-y-3">
              {/* Contact & Recipient */}
              <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                <DeliveryCard label="Contact Name" value={delivery.contactName} />
                <DeliveryCard label="Contact Number" value={delivery.contactNumber} />
                <DeliveryCard label="Card No" value={delivery.cardNo} mono />
              </div>

              {/* Order & Shipping Reference */}
              <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                <DeliveryCard label="DO Number" value="DO-7729-X" />
                <DeliveryCard label="Receipt No" value={delivery.receiptNo} />
                <DeliveryCard label="Shipping Status" value={delivery.shippingStatus} highlight />
              </div>

              {/* Logistics */}
              <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                <DeliveryCard label="Logistic Partner" value={delivery.logisticPartner} />
                <DeliveryCard label="Tracking Number" value={delivery.trackingNumber} mono />
                <DeliveryCard label="Timeslot" value={delivery.timeslot} />
              </div>

              {/* Delivery-specific */}
              {delivery.orderType === "Delivery" && (
                <div className="grid grid-cols-2 gap-3">
                  <DeliveryCard label="Delivery Option" value={delivery.deliveryOption} />
                  <DeliveryCard label="Delivery Date" value={delivery.deliveryDate} />
                </div>
              )}

              {/* Self Collection-specific */}
              {delivery.orderType === "Self Collection" && (
                <div className="grid grid-cols-2 gap-3">
                  <DeliveryCard label="Collection Option" value={delivery.collectionOption || "—"} />
                  <DeliveryCard label="Collection Date" value={delivery.collectionDate || "—"} />
                </div>
              )}

              {/* Address */}
              <div className="p-3 bg-surface-container-low rounded-lg">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">Address</p>
                <p className="text-xs text-on-surface whitespace-pre-line">{delivery.address}</p>
              </div>

              {/* Amounts */}
              <div className="grid grid-cols-3 gap-3">
                <DeliveryCard label="Delivery Items Amount" value={`$${delivery.deliveryItemsAmount}`} />
                <DeliveryCard label="Delivery Fee Amount" value={`$${delivery.deliveryFeeAmount}`} />
                <DeliveryCard label="Discount" value={`-$${delivery.discount}`} />
              </div>

              {/* Remarks */}
              {delivery.deliveryRemarks && (
                <div className="p-3 bg-surface-container-low rounded-lg">
                  <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">Remarks</p>
                  <p className="text-xs text-on-surface italic">"{delivery.deliveryRemarks}"</p>
                </div>
              )}

              {/* Actual Delivery Fee */}
              <div className="p-3 bg-surface-container-low rounded-lg">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">
                  Actual Delivery Fee
                </p>
                <p className="text-xs font-bold text-on-surface">${delivery.actualDeliveryFee}</p>
                <p className="text-[10px] text-on-surface-variant mt-1">
                  Admin override — does not affect customer total. Only editable when logistic partner is not system-integrated.
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
              <span className="opacity-80">Order Amount</span>
              <span className="font-medium">$1,240.00</span>
            </div>
            <div className="flex justify-between text-xs">
              <span className="opacity-80">Delivery Fee</span>
              <span className="font-medium">$25.00</span>
            </div>
            <div className="flex justify-between text-xs">
              <span className="opacity-80">Handling Fee</span>
              <span className="font-medium">$0.00</span>
            </div>
            <div className="flex justify-between text-xs">
              <span className="opacity-80">Promo Code Discount</span>
              <span className="font-bold text-yellow-300">-$124.00</span>
            </div>
            <div className="pt-3 mt-3 border-t border-white/20">
              <div className="flex justify-between items-baseline">
                <span className="text-xs font-bold uppercase">Total Amount Payable</span>
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
              {canEdit && (
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
                  <th className="text-left py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">Product</th>
                  <th className="text-left py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">Brand</th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">Qty</th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">Unit Price</th>
                  <th className="text-right py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">Total</th>
                  <th className="text-center py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">Item Status</th>
                  {isAction && (
                    <th className="text-center py-3 px-5 text-[11px] font-bold text-primary uppercase tracking-tight">Action</th>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-outline-variant/10">
                {lineItems.map((item) => {
                  const isCancelled = cancelledItems.has(item.sku);
                  const confirmedStatus = itemStatuses[item.sku];
                  const draftStatus = itemStatusDrafts[item.sku];
                  const isDirty = draftStatus !== confirmedStatus;

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
                      <td className="py-4 px-5 text-xs text-on-surface-variant font-medium">{item.brand}</td>
                      <td className="py-4 px-5 text-right text-xs font-semibold text-on-surface">{item.qty}</td>
                      <td className="py-4 px-5 text-right text-xs text-on-surface-variant font-medium">{item.unitPrice}</td>
                      <td className="py-4 px-5 text-right text-xs font-bold text-on-surface">{item.total}</td>
                      <td className="py-4 px-5 text-center">
                        {isAction && !isCancelled ? (
                          <div className="flex flex-col items-center gap-1.5">
                            <select
                              className="text-[10px] font-bold bg-surface-container-low border border-outline-variant/20 rounded px-2 py-1 focus:ring-1 focus:ring-primary/40 cursor-pointer"
                              value={draftStatus}
                              onChange={(e) =>
                                handleItemStatusDraftChange(item.sku, e.target.value as OrderStatus)
                              }
                            >
                              {ORDER_STATUSES.map((s) => (
                                <option key={s}>{s}</option>
                              ))}
                            </select>
                            {isDirty && (
                              <button
                                type="button"
                                className="text-[10px] font-bold text-white bg-primary hover:bg-primary-container px-2 py-0.5 rounded transition-colors"
                                onClick={() => handleUpdateItemStatus(item.sku)}
                              >
                                Update
                              </button>
                            )}
                          </div>
                        ) : (
                          <span
                            className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${
                              STATUS_STYLES[confirmedStatus]
                            }`}
                          >
                            {confirmedStatus}
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
            <LogEntry
              icon={<IconCheckCircle className="h-3.5 w-3.5" />}
              iconBg="bg-primary-container text-on-primary-container"
              title="Order Verified"
              time="Today, 09:12 AM"
              body="Identity verification successful. Address match confirmed via GIS services."
              operator="Sarah J."
              hasLine
            />
            <LogEntry
              icon={<IconShipping className="h-3.5 w-3.5" />}
              iconBg="bg-surface-container-high text-on-surface-variant"
              title="Dispatched to Warehouse"
              time="Today, 08:00 AM"
              body="Consolidated with Batch #NY-8829 for priority processing at Warehouse 4B."
              hasLine
            />
            <LogEntry
              icon={<IconEditNote className="h-3.5 w-3.5" />}
              iconBg="bg-surface-container-high text-on-surface-variant"
              title="Manual Note Added"
              time="Yesterday, 17:45 PM"
              body={'"Customer requested to leave the package at the main reception if the office is closed."'}
              operator="System Automated"
            />
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
                Add Remark
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

      {/* Payment Details Modal */}
      {paymentModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-sm">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 p-6 space-y-4">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-bold text-on-surface">Payment Details</h2>
              <button
                type="button"
                className="text-[10px] font-bold text-on-surface-variant hover:text-on-surface"
                onClick={() => setPaymentModalOpen(false)}
              >
                ✕ Close
              </button>
            </div>
            <p className="text-xs text-on-surface-variant">
              Order <span className="font-semibold text-on-surface">#{orderId}</span> was paid using multiple payment methods.
            </p>

            {/* Payment method 1: Gateway */}
            <div className="p-4 rounded-xl border border-outline-variant/20 bg-surface-container-lowest space-y-2">
              <div className="flex items-center justify-between">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">Payment Gateway</p>
                <span className="text-[10px] font-bold text-green-700 bg-green-50 px-2 py-0.5 rounded-full border border-green-200">
                  Completed
                </span>
              </div>
              <div className="space-y-1.5">
                <PaymentRow label="Amount" value="$1,100.00" />
                <PaymentRow label="Transaction ID" value="TXN-20231024-8829" mono />
                <PaymentRow label="Card" value="VISA ****4321" />
                <PaymentRow label="Date" value="Oct 24, 2023, 14:32" />
                <PaymentRow label="Gateway" value="Stripe" />
              </div>
            </div>

            {/* Payment method 2: E-Voucher */}
            <div className="p-4 rounded-xl border border-outline-variant/20 bg-surface-container-lowest space-y-2">
              <div className="flex items-center justify-between">
                <p className="text-[10px] font-bold text-on-surface-variant uppercase tracking-wider">E-Voucher / Points</p>
                <span className="text-[10px] font-bold text-green-700 bg-green-50 px-2 py-0.5 rounded-full border border-green-200">
                  Redeemed
                </span>
              </div>
              <div className="space-y-1.5">
                <PaymentRow label="Amount" value="$41.00" />
                <PaymentRow label="Voucher Code" value="EXECUTIVE-2023-FIRST" mono />
                <PaymentRow label="Type" value="Promotional Voucher" />
                <PaymentRow label="Date" value="Oct 24, 2023, 14:32" />
              </div>
            </div>

            <div className="flex justify-between items-center px-1 pt-1 border-t border-outline-variant/10">
              <span className="text-xs font-bold text-on-surface">Total Paid</span>
              <span className="text-sm font-extrabold text-primary">$1,141.00</span>
            </div>

            <p className="text-[10px] text-on-surface-variant">
              Note: Having two different payment methods from different payment gateways is not allowed. Only Payment Gateway + Voucher/Points combinations are supported.
            </p>
          </div>
        </div>
      )}
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between items-center">
      <span className="text-xs text-on-surface-variant">{label}</span>
      <span className="text-xs text-on-surface text-right">{value}</span>
    </div>
  );
}

function DeliveryCard({
  label,
  value,
  mono = false,
  highlight = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
  highlight?: boolean;
}) {
  return (
    <div className="p-3 bg-surface-container-low rounded-lg">
      <p className="text-[10px] font-bold text-on-surface-variant uppercase mb-1">{label}</p>
      <p
        className={`text-xs font-semibold ${mono ? "font-mono text-primary select-all" : ""} ${
          highlight ? "text-blue-700" : "text-on-surface"
        }`}
      >
        {value}
      </p>
    </div>
  );
}

function PaymentRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex justify-between items-center">
      <span className="text-[11px] text-on-surface-variant">{label}</span>
      <span className={`text-[11px] font-semibold text-on-surface ${mono ? "font-mono" : ""}`}>{value}</span>
    </div>
  );
}

function LogEntry({
  icon,
  iconBg,
  title,
  time,
  body,
  operator,
  hasLine = false,
}: {
  icon: React.ReactNode;
  iconBg: string;
  title: string;
  time: string;
  body: string;
  operator?: string;
  hasLine?: boolean;
}) {
  return (
    <div className="flex gap-4 relative">
      {hasLine && <div className="absolute left-[11px] top-6 bottom-0 w-0.5 bg-outline-variant/20" />}
      <div className={`h-6 w-6 rounded-full flex items-center justify-center z-10 shrink-0 ${iconBg}`}>
        {icon}
      </div>
      <div className="pb-4">
        <div className="flex items-center gap-2 mb-1">
          <span className="text-xs font-bold text-on-surface">{title}</span>
          <span className="text-[10px] text-on-surface-variant">{time}</span>
        </div>
        <p className="text-xs text-on-surface-variant">{body}</p>
        {operator && <p className="text-[10px] font-semibold text-primary mt-1">Operator: {operator}</p>}
      </div>
    </div>
  );
}

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
  { name: "Air Velocity Max X2", sku: "AV-MAX-0019", brand: "Lumina Sport", qty: 1, unitPrice: "$450.00", total: "$450.00" },
  { name: "Heritage Chrono Silver", sku: "WTCH-H-042", brand: "Stark & Co.", qty: 1, unitPrice: "$590.00", total: "$590.00" },
  { name: "Zenith Wireless Audio", sku: "AUD-ZW-100", brand: "AudioZen", qty: 1, unitPrice: "$200.00", total: "$200.00" },
];
