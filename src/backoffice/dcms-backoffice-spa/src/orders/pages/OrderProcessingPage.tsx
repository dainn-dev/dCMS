import { useEffect, useMemo, useState } from "react";
import { DataTable } from "../components/DataTable";
import { IconDownload } from "../icons";
import { createOrderColumns } from "../orders-columns";
import { exportOrderProcessingFile } from "../exportOrders";
import type { Order } from "../types";
import {
  cancelOrder,
  deliverOrder,
  fetchAllOrdersForExport,
  fetchOrders,
  getOrder,
  shipOrder,
  type OrderFilters,
} from "../api/ordersApi";

// (DAI-626) Mock orders removed — API-backed list.
/*
  {
    doNumber: "DO-2024-002",
    orderId: "ORD-998123",
    orderDate: "24 May 2024",
    type: "Wholesale",
    customerName: "Sarah Connor",
    customerEmail: "s.connor@sky.net",
    customerContactNo: "+49 30 12345678",
    status: "Delivered",
    fulfilledDate: "25 May 2024",
    deliveryDate: "25 May 2024",
    deliveryOption: "Express",
    shippingStatus: "Delivered",
    processingOfficer: "Ben Müller",
    tags: "Wholesale,Priority",
    paymentGateway: "PayPal",
    store: "Berlin Hub",
    storeAutoId: "STR-002",
    distributionCentre: "DC-Central",
    paymentMethod: "Multi Payment",
    paymentDate: "24 May 2024",
    shippingAddressLine1: "Unter den Linden 5",
    shippingAddressLine2: null,
    shippingPostalCode: "10117",
    shippingCountry: "DE",
    billingAddressLine1: "Friedrichstr. 100",
    billingAddressLine2: "3. OG",
    billingPostalCode: "10117",
    billingCountry: "DE",
  },
  {
    doNumber: "DO-2024-003",
    orderId: "ORD-998124",
    orderDate: "23 May 2024",
    type: "Online",
    customerName: "James Bond",
    customerEmail: "007@mi6.gov",
    customerContactNo: "+44 20 7946 0007",
    status: "Processing",
    fulfilledDate: null,
    deliveryDate: "28 May 2024",
    deliveryOption: "Standard",
    shippingStatus: "Pending",
    processingOfficer: "Alice Tan",
    tags: null,
    paymentGateway: "Stripe",
    store: "Global Online",
    storeAutoId: "STR-003",
    distributionCentre: "DC-South",
    paymentMethod: "Bank Transfer",
    paymentDate: "23 May 2024",
    shippingAddressLine1: "85 Albert Embankment",
    shippingAddressLine2: null,
    shippingPostalCode: "SE1 7TP",
    shippingCountry: "GB",
    billingAddressLine1: "85 Albert Embankment",
    billingAddressLine2: null,
    billingPostalCode: "SE1 7TP",
    billingCountry: "GB",
  },
  {
    doNumber: "DO-2024-004",
    orderId: "ORD-998125",
    orderDate: "22 May 2024",
    type: "Retail",
    customerName: "Ada Lovelace",
    customerEmail: "ada@babbage.io",
    customerContactNo: "+44 7700 900123",
    status: "Ready for Delivery",
    fulfilledDate: null,
    deliveryDate: "26 May 2024",
    deliveryOption: "Express",
    shippingStatus: "Shipped",
    processingOfficer: "Charlie Ng",
    tags: "New Customer",
    paymentGateway: "Stripe",
    store: "London Central",
    storeAutoId: "STR-001",
    distributionCentre: "DC-North",
    paymentMethod: "Credit Card",
    paymentDate: "22 May 2024",
    shippingAddressLine1: "10 St James's Square",
    shippingAddressLine2: null,
    shippingPostalCode: "SW1Y 4LE",
    shippingCountry: "GB",
    billingAddressLine1: "10 St James's Square",
    billingAddressLine2: null,
    billingPostalCode: "SW1Y 4LE",
    billingCountry: "GB",
  },
  {
    doNumber: "DO-2024-005",
    orderId: "ORD-998126",
    orderDate: "22 May 2024",
    type: "Online",
    customerName: "Alan Turing",
    customerEmail: "alan@enigma.uk",
    customerContactNo: "+44 7911 654321",
    status: "Returned",
    fulfilledDate: "24 May 2024",
    deliveryDate: "23 May 2024",
    deliveryOption: "Same Day",
    shippingStatus: "Returned",
    processingOfficer: "Diana Park",
    tags: "Returned",
    paymentGateway: "PayPal",
    store: "Global Online",
    storeAutoId: "STR-003",
    distributionCentre: "DC-East",
    paymentMethod: "E-Wallet",
    paymentDate: "22 May 2024",
    shippingAddressLine1: "Bletchley Park",
    shippingAddressLine2: "Block H",
    shippingPostalCode: "MK3 6EB",
    shippingCountry: "GB",
    billingAddressLine1: "Bletchley Park",
    billingAddressLine2: "Block H",
    billingPostalCode: "MK3 6EB",
    billingCountry: "GB",
  },
  {
    doNumber: "DO-2024-006",
    orderId: "ORD-998127",
    orderDate: "21 May 2024",
    type: "Wholesale",
    customerName: "Grace Hopper",
    customerEmail: "grace@navy.mil",
    customerContactNo: "+1 202 555 0143",
    status: "Partially Fulfilled",
    fulfilledDate: null,
    deliveryDate: "30 May 2024",
    deliveryOption: "Standard",
    shippingStatus: "Shipped",
    processingOfficer: "Ben Müller",
    tags: "Wholesale,Partial",
    paymentGateway: "Adyen",
    store: "Berlin Hub",
    storeAutoId: "STR-002",
    distributionCentre: "DC-West",
    paymentMethod: "Multi Payment",
    paymentDate: "21 May 2024",
    shippingAddressLine1: "Pentagon, Arlington",
    shippingAddressLine2: null,
    shippingPostalCode: "22201",
    shippingCountry: "US",
    billingAddressLine1: "Pentagon, Arlington",
    billingAddressLine2: null,
    billingPostalCode: "22201",
    billingCountry: "US",
  },
  {
    doNumber: "DO-2024-007",
    orderId: "ORD-998128",
    orderDate: "20 May 2024",
    type: "Retail",
    customerName: "Nikola Tesla",
    customerEmail: "nikola@ac-dc.com",
    customerContactNo: "+1 212 555 0187",
    status: "Admin Cancelled",
    fulfilledDate: null,
    deliveryDate: null,
    deliveryOption: null,
    shippingStatus: null,
    processingOfficer: "Charlie Ng",
    tags: "Cancelled",
    paymentGateway: "Stripe",
    store: "Paris Flagship",
    storeAutoId: "STR-004",
    distributionCentre: undefined,
    paymentMethod: "Credit Card",
    paymentDate: "20 May 2024",
    shippingAddressLine1: "5 Avenue Anatole France",
    shippingAddressLine2: null,
    shippingPostalCode: "75007",
    shippingCountry: "FR",
    billingAddressLine1: "5 Avenue Anatole France",
    billingAddressLine2: null,
    billingPostalCode: "75007",
    billingCountry: "FR",
  },
  {
    doNumber: "DO-2024-008",
    orderId: "ORD-998129",
    orderDate: "20 May 2024",
    type: "Online",
    customerName: "Marie Curie",
    customerEmail: "marie@radium.fr",
    customerContactNo: "+33 1 23 45 67 89",
    status: "Picked Up",
    fulfilledDate: "21 May 2024",
    deliveryDate: "21 May 2024",
    deliveryOption: "Click & Collect",
    shippingStatus: "Delivered",
    processingOfficer: "Diana Park",
    tags: null,
    paymentGateway: "PayPal",
    store: "Paris Flagship",
    storeAutoId: "STR-004",
    distributionCentre: "DC-Central",
    paymentMethod: "Bank Transfer",
    paymentDate: "20 May 2024",
    shippingAddressLine1: "Paris Flagship Store",
    shippingAddressLine2: "1 Rue de Rivoli",
    shippingPostalCode: "75001",
    shippingCountry: "FR",
    billingAddressLine1: "36 Quai de Béthune",
    billingAddressLine2: null,
    billingPostalCode: "75004",
    billingCountry: "FR",
  },
  {
    doNumber: "DO-2024-009",
    orderId: "ORD-998130",
    orderDate: "19 May 2024",
    type: "Retail",
    customerName: "Isaac Newton",
    customerEmail: "isaac@apple.science",
    customerContactNo: "+44 1223 337733",
    status: "Out for Delivery",
    fulfilledDate: null,
    deliveryDate: "19 May 2024",
    deliveryOption: "Same Day",
    shippingStatus: "Out for Delivery",
    processingOfficer: "Alice Tan",
    tags: null,
    paymentGateway: "Stripe",
    store: "London Central",
    storeAutoId: "STR-001",
    distributionCentre: "DC-North",
    paymentMethod: "Credit Card",
    paymentDate: "19 May 2024",
    shippingAddressLine1: "Trinity College",
    shippingAddressLine2: "Trinity Street",
    shippingPostalCode: "CB2 1TQ",
    shippingCountry: "GB",
    billingAddressLine1: "Trinity College",
    billingAddressLine2: "Trinity Street",
    billingPostalCode: "CB2 1TQ",
    billingCountry: "GB",
  },
  {
    doNumber: "DO-2024-010",
    orderId: "ORD-998131",
    orderDate: "18 May 2024",
    type: "Wholesale",
    customerName: "Linus Torvalds",
    customerEmail: "linus@kernel.org",
    customerContactNo: "+358 9 123 4567",
    status: "Open Order",
    fulfilledDate: null,
    deliveryDate: "25 May 2024",
    deliveryOption: "Standard",
    shippingStatus: "Pending",
    processingOfficer: "Erik Lindqvist",
    tags: "Wholesale",
    paymentGateway: "Adyen",
    store: "Helsinki Store",
    storeAutoId: "STR-005",
    distributionCentre: "DC-North",
    paymentMethod: "Bank Transfer",
    paymentDate: "18 May 2024",
    shippingAddressLine1: "Leppävaara 1",
    shippingAddressLine2: null,
    shippingPostalCode: "02600",
    shippingCountry: "FI",
    billingAddressLine1: "Leppävaara 1",
    billingAddressLine2: null,
    billingPostalCode: "02600",
    billingCountry: "FI",
  },
  {
    doNumber: "DO-2024-011",
    orderId: "ORD-998132",
    orderDate: "17 May 2024",
    type: "Online",
    customerName: "Tim Berners-Lee",
    customerEmail: "tim@w3.org",
    customerContactNo: "+44 7700 900456",
    status: "Delivered",
    fulfilledDate: "19 May 2024",
    deliveryDate: "19 May 2024",
    deliveryOption: "Express",
    shippingStatus: "Delivered",
    processingOfficer: "Alice Tan",
    tags: null,
    paymentGateway: "PayPal",
    store: "Global Online",
    storeAutoId: "STR-003",
    distributionCentre: undefined,
    paymentMethod: "Multi Payment",
    paymentDate: "17 May 2024",
    shippingAddressLine1: "76 Chancery Lane",
    shippingAddressLine2: null,
    shippingPostalCode: "WC2A 1AA",
    shippingCountry: "GB",
    billingAddressLine1: "76 Chancery Lane",
    billingAddressLine2: null,
    billingPostalCode: "WC2A 1AA",
    billingCountry: "GB",
  },
  {
    doNumber: "DO-2024-012",
    orderId: "ORD-998133",
    orderDate: "16 May 2024",
    type: "Retail",
    customerName: "Edsger Dijkstra",
    customerEmail: "edsger@graph.nl",
    customerContactNo: "+31 20 123 4567",
    status: "Ready for Pickup",
    fulfilledDate: null,
    deliveryDate: "18 May 2024",
    deliveryOption: "Click & Collect",
    shippingStatus: "Shipped",
    processingOfficer: "Charlie Ng",
    tags: null,
    paymentGateway: "Stripe",
    store: "Amsterdam Central",
    storeAutoId: "STR-006",
    distributionCentre: "DC-West",
    paymentMethod: "Credit Card",
    paymentDate: "16 May 2024",
    shippingAddressLine1: "Amsterdam Central Store",
    shippingAddressLine2: "Dam 1",
    shippingPostalCode: "1012 JS",
    shippingCountry: "NL",
    billingAddressLine1: "Herengracht 282",
    billingAddressLine2: null,
    billingPostalCode: "1016 BX",
    billingCountry: "NL",
  },


*/
// ─────────────────────────────────────────────────────────────────────────────

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
  onViewOrder?: (orderId: string) => void;
  onTakeAction?: (orderId: string) => void;
};

const STATUS_OPTIONS: Array<{ value: string; label: string }> = [
  { value: "all",                label: "All statuses" },
  { value: "Created",            label: "Open Order" },
  { value: "Processing",         label: "Processing" },
  { value: "ReadyForDelivery",   label: "Ready for Delivery" },
  { value: "ReadyForPickup",     label: "Ready for Pickup" },
  { value: "Shipped",            label: "Out for Delivery" },
  { value: "Delivered",          label: "Delivered" },
  { value: "Returned",           label: "Returned" },
  { value: "AdminCancelled",     label: "Admin Cancelled" },
  { value: "PendingCancellation",label: "Pending Cancellation" },
  { value: "UserCancelled",      label: "User Cancelled" },
  { value: "PartiallyFulfilled", label: "Partially Fulfilled" },
  { value: "payment_failed",     label: "Payment Failed" },
  { value: "auth_failed",        label: "Auth Failed" },
  { value: "address_error",      label: "Address Error" },
  { value: "stock_error",        label: "Stock Error" },
  { value: "system_error",       label: "System Error" },
];

export function OrderProcessingPage({ tenantId, storeId, authToken, onViewOrder }: Props) {
  const [actionToast, setActionToast] = useState<{ kind: "success" | "error"; message: string } | null>(null);
  const [actionBusy, setActionBusy] = useState(false);
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);
  const [deliverTarget, setDeliverTarget] = useState<string | null>(null);
  const [shipTarget, setShipTarget] = useState<string | null>(null);
  const [shipCarrier, setShipCarrier] = useState("");
  const [shipTracking, setShipTracking] = useState("");

  useEffect(() => {
    if (!actionToast) return;
    const t = setTimeout(() => setActionToast(null), 3000);
    return () => clearTimeout(t);
  }, [actionToast]);

  const columns = useMemo(
    () =>
      createOrderColumns(onViewOrder, {
        onCancel: (id) => setCancelTarget(id),
        onShip: (id) => setShipTarget(id),
        onDeliver: (id) => setDeliverTarget(id),
      }),
    [onViewOrder]
  );

  const [rows, setRows] = useState<Order[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState("all");
  const [exporting, setExporting] = useState(false);

  const filters: OrderFilters | undefined = useMemo(
    () => (statusFilter === "all" ? undefined : { status: statusFilter }),
    [statusFilter]
  );

  useEffect(() => {
    if (!tenantId || !storeId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);
    fetchOrders(tenantId, storeId, filters, { cursor: undefined, limit: 50 }, authToken)
      .then(({ rows, nextCursor }) => {
        if (cancelled) return;
        setRows(rows);
        setNextCursor(nextCursor);
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        setRows([]);
        setNextCursor(null);
        setError(e instanceof Error ? e.message : "Failed to load orders");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken, filters]);

  async function refreshRow(orderId: string) {
    if (!tenantId || !storeId) return;
    try {
      const updated = await getOrder(tenantId, storeId, orderId, authToken);
      setRows((prev) => prev.map((r) => (r.orderId === orderId ? { ...r, status: updated.status } : r)));
    } catch {
      // ignore row refresh failure
    }
  }

  async function runCancel() {
    if (!tenantId || !storeId || !cancelTarget) return;
    setActionBusy(true);
    try {
      await cancelOrder(tenantId, storeId, cancelTarget, null, authToken);
      setActionToast({ kind: "success", message: `Order ${cancelTarget} cancelled` });
      await refreshRow(cancelTarget);
      setCancelTarget(null);
    } catch (e: unknown) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Cancel failed" });
    } finally {
      setActionBusy(false);
    }
  }

  async function runDeliver() {
    if (!tenantId || !storeId || !deliverTarget) return;
    setActionBusy(true);
    try {
      await deliverOrder(tenantId, storeId, deliverTarget, authToken);
      setActionToast({ kind: "success", message: `Order ${deliverTarget} delivered` });
      await refreshRow(deliverTarget);
      setDeliverTarget(null);
    } catch (e: unknown) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Deliver failed" });
    } finally {
      setActionBusy(false);
    }
  }

  async function runShip() {
    if (!tenantId || !storeId || !shipTarget) return;
    if (!shipCarrier.trim() || !shipTracking.trim()) {
      setActionToast({ kind: "error", message: "Carrier and tracking number are required." });
      return;
    }
    setActionBusy(true);
    try {
      await shipOrder(
        tenantId,
        storeId,
        shipTarget,
        { carrier: shipCarrier.trim(), trackingNumber: shipTracking.trim() },
        authToken
      );
      setActionToast({ kind: "success", message: `Order ${shipTarget} shipped` });
      await refreshRow(shipTarget);
      setShipTarget(null);
      setShipCarrier("");
      setShipTracking("");
    } catch (e: unknown) {
      setActionToast({ kind: "error", message: e instanceof Error ? e.message : "Ship failed" });
    } finally {
      setActionBusy(false);
    }
  }

  async function loadMore() {
    if (!tenantId || !storeId) return;
    if (!nextCursor) return;
    setLoading(true);
    setError(null);
    try {
      const out = await fetchOrders(tenantId, storeId, filters, { cursor: nextCursor, limit: 50 }, authToken);
      setRows((prev) => [...prev, ...out.rows]);
      setNextCursor(out.nextCursor);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load more orders");
    } finally {
      setLoading(false);
    }
  }

  async function runExport() {
    if (!tenantId || !storeId) return;
    setExporting(true);
    setError(null);
    try {
      const { rows, limited } = await fetchAllOrdersForExport(tenantId, storeId, filters, authToken, {
        limit: 5000,
        pageSize: 100,
      });
      if (limited) setError("Export limited to first 5000 rows. Please refine filters.");
      exportOrderProcessingFile(rows);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Export failed");
    } finally {
      setExporting(false);
    }
  }

  return (
    <>
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
          <select
            className="h-10 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 text-sm text-on-surface focus:outline-none focus:ring-1 focus:ring-primary"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            disabled={!tenantId || !storeId}
            aria-label="Status filter"
          >
            {STATUS_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="px-4 py-2 text-sm font-medium text-on-surface hover:bg-surface-container-high transition-colors rounded-lg flex items-center gap-2 disabled:opacity-40"
            onClick={() => void runExport()}
            disabled={!tenantId || !storeId || exporting}
          >
            <IconDownload />
            {exporting ? "Exporting…" : "Export"}
          </button>
        </div>
      </div>

      {actionToast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-outline-variant/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <p className={`text-sm font-semibold ${actionToast.kind === "error" ? "text-error" : "text-on-surface"}`}>
            {actionToast.message}
          </p>
        </div>
      )}

      {cancelTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6">
              <h3 className="text-sm font-bold text-on-surface">Cancel order</h3>
              <p className="mt-2 text-xs text-on-surface-variant">
                Cancel <strong>{cancelTarget}</strong>?
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setCancelTarget(null)}
                disabled={actionBusy}
              >
                Close
              </button>
              <button
                type="button"
                className="rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 disabled:opacity-40"
                onClick={() => void runCancel()}
                disabled={actionBusy}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {deliverTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[420px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6">
              <h3 className="text-sm font-bold text-on-surface">Mark as delivered</h3>
              <p className="mt-2 text-xs text-on-surface-variant">
                Mark <strong>{deliverTarget}</strong> as delivered?
              </p>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setDeliverTarget(null)}
                disabled={actionBusy}
              >
                Close
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2.5 text-xs font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
                onClick={() => void runDeliver()}
                disabled={actionBusy}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {shipTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[460px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="p-6 space-y-4">
              <div>
                <h3 className="text-sm font-bold text-on-surface">Mark as shipped</h3>
                <p className="mt-1 text-xs text-on-surface-variant">
                  Enter carrier + tracking for <strong>{shipTarget}</strong>.
                </p>
              </div>
              <div className="grid grid-cols-1 gap-4">
                <div>
                  <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Carrier</label>
                  <input
                    className="w-full bg-surface-container-lowest border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:ring-1 focus:ring-primary outline-none"
                    value={shipCarrier}
                    onChange={(e) => setShipCarrier(e.target.value)}
                    placeholder="e.g. DHL"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-bold uppercase tracking-wider text-on-surface-variant mb-1">Tracking number</label>
                  <input
                    className="w-full bg-surface-container-lowest border border-outline-variant/20 rounded-lg px-3 py-2 text-xs focus:ring-1 focus:ring-primary outline-none"
                    value={shipTracking}
                    onChange={(e) => setShipTracking(e.target.value)}
                    placeholder="e.g. TRK-123"
                  />
                </div>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high"
                onClick={() => setShipTarget(null)}
                disabled={actionBusy}
              >
                Close
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2.5 text-xs font-bold text-on-primary hover:opacity-90 disabled:opacity-40"
                onClick={() => void runShip()}
                disabled={actionBusy}
              >
                Confirm
              </button>
            </div>
          </div>
        </div>
      )}

      {!tenantId || !storeId ? (
        <div className="mt-6 rounded-xl border border-outline-variant/20 bg-surface-container-lowest p-6 text-sm text-on-surface-variant">
          Select a tenant + store to view orders.
        </div>
      ) : (
        <>
          {error && (
            <div className="mt-6 rounded-xl border border-error/25 bg-error/5 px-5 py-4 text-sm text-error">
              {error}
            </div>
          )}

          <div className="mt-6">
            <DataTable
              columns={columns}
              data={rows}
              defaultHiddenColumns={["distributionCentre", "paymentMethod", "paymentDate"]}
              globalFilterPlaceholder="Search by order, customer, store…"
              loading={loading && rows.length === 0}
              emptyMessage="No orders found."
              itemNoun="orders"
              footerMode="loadMore"
              loadMore={{
                onClick: () => void loadMore(),
                disabled: loading || nextCursor === null,
                label: nextCursor ? (loading ? "Loading…" : "Load more") : "No more",
              }}
            />
          </div>
        </>
      )}
    </>
  );
}
