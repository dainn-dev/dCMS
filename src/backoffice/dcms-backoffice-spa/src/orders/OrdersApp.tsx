import { useEffect, useRef, useState } from "react";
import { OrdersLayout, type OrdersPageId } from "./layout/OrdersLayout";
import { ordersHashForPage, parseOrdersPageFromHash } from "./ordersHashRouting";
import { FailedOrderDetailPage } from "./pages/FailedOrderDetailPage";
import { FailedOrdersPage } from "./pages/FailedOrdersPage";
import { OrderDetailPage } from "./pages/OrderDetailPage";
import { OrderProcessingPage } from "./pages/OrderProcessingPage";
import { RefundCasesPage } from "./pages/RefundCasesPage";

export function OrdersApp({ tenantId, storeId, authToken }: { tenantId?: string; storeId?: string; authToken?: string }) {
  const [page, setPage] = useState<OrdersPageId>(() => parseOrdersPageFromHash() ?? "order-processing");
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [selectedOrderMode, setSelectedOrderMode] = useState<"view" | "action">("view");
  const [selectedFailedOrderId, setSelectedFailedOrderId] = useState<string | null>(null);
  /** Ignore the hashchange caused by our own `location.hash` writes so drill-down state is not cleared. */
  const suppressHashChange = useRef(false);

  function syncOrdersHash(id: OrdersPageId) {
    const next = ordersHashForPage(id);
    if (window.location.hash !== next) {
      suppressHashChange.current = true;
      window.location.hash = next;
    }
  }

  function navigateToOrdersPage(id: OrdersPageId) {
    setSelectedOrderId(null);
    setSelectedFailedOrderId(null);
    setPage(id);
    syncOrdersHash(id);
  }

  function handleViewOrder(orderId: string) {
    setSelectedOrderId(orderId);
    setSelectedFailedOrderId(null);
    setSelectedOrderMode("view");
    setPage("order-detail");
    syncOrdersHash("order-detail");
  }

  function handleTakeAction(orderId: string) {
    setSelectedOrderId(orderId);
    setSelectedFailedOrderId(null);
    setSelectedOrderMode("action");
    setPage("order-detail");
    syncOrdersHash("order-detail");
  }

  function handleViewFailedOrder(orderId: string) {
    setSelectedFailedOrderId(orderId);
    setSelectedOrderId(null);
    setPage("failed-order-detail");
    syncOrdersHash("failed-order-detail");
  }

  function handleBackToProcessing() {
    navigateToOrdersPage("order-processing");
  }

  function handleBackFromFailedDetail() {
    setSelectedFailedOrderId(null);
    navigateToOrdersPage("failed-orders");
  }

  useEffect(() => {
    function onHashChange() {
      if (suppressHashChange.current) {
        suppressHashChange.current = false;
        return;
      }
      const p = parseOrdersPageFromHash();
      if (!p) return;
      setSelectedOrderId(null);
      setSelectedFailedOrderId(null);
      setPage(p);
    }
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  useEffect(() => {
    const p = parseOrdersPageFromHash();
    if (!p) {
      window.location.hash = ordersHashForPage("order-processing");
      return;
    }
    const preferred = ordersHashForPage(p);
    if (window.location.hash !== preferred) {
      window.location.hash = preferred;
    }
  }, []);

  return (
    <OrdersLayout page={page} onPageChange={navigateToOrdersPage}>
      {page === "order-processing" && (
        <OrderProcessingPage tenantId={tenantId} storeId={storeId} authToken={authToken} onViewOrder={handleViewOrder} onTakeAction={handleTakeAction} />
      )}
      {page === "failed-orders" && (
        <FailedOrdersPage
          tenantId={tenantId}
          storeId={storeId}
          authToken={authToken}
          onViewFailedOrder={handleViewFailedOrder}
        />
      )}
      {page === "refund-cases" && <RefundCasesPage tenantId={tenantId} storeId={storeId} authToken={authToken} />}
      {page === "order-detail" && (
        <OrderDetailPage
          orderId={selectedOrderId ?? "EX-99284-B"}
          mode={selectedOrderMode}
          onBack={handleBackToProcessing}
          tenantId={tenantId}
          storeId={storeId}
          authToken={authToken}
        />
      )}
      {page === "failed-order-detail" && (
        <FailedOrderDetailPage
          orderId={selectedFailedOrderId ?? ""}
          tenantId={tenantId}
          storeId={storeId}
          authToken={authToken}
          onBack={handleBackFromFailedDetail}
        />
      )}
    </OrdersLayout>
  );
}
