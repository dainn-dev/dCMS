import { useState } from "react";
import { OrdersLayout, type OrdersPageId } from "./layout/OrdersLayout";
import { FailedOrderDetailPage } from "./pages/FailedOrderDetailPage";
import { FailedOrdersPage } from "./pages/FailedOrdersPage";
import { OrderDetailPage } from "./pages/OrderDetailPage";
import { OrderProcessingPage } from "./pages/OrderProcessingPage";
import { RefundCasesPage } from "./pages/RefundCasesPage";

export function OrdersApp() {
  const [page, setPage] = useState<OrdersPageId>("order-processing");
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [selectedOrderMode, setSelectedOrderMode] = useState<"view" | "action">("view");
  const [selectedFailedOrderId, setSelectedFailedOrderId] = useState<string | null>(null);

  function handleViewOrder(orderId: string) {
    setSelectedOrderId(orderId);
    setSelectedFailedOrderId(null);
    setSelectedOrderMode("view");
    setPage("order-detail");
  }

  function handleTakeAction(orderId: string) {
    setSelectedOrderId(orderId);
    setSelectedFailedOrderId(null);
    setSelectedOrderMode("action");
    setPage("order-detail");
  }

  function handleViewFailedOrder(orderId: string) {
    setSelectedFailedOrderId(orderId);
    setSelectedOrderId(null);
    setPage("failed-order-detail");
  }

  function handleBackToProcessing() {
    setSelectedOrderId(null);
    setPage("order-processing");
  }

  function handleBackFromFailedDetail() {
    setSelectedFailedOrderId(null);
    setPage("failed-orders");
  }

  function handlePageChange(id: OrdersPageId) {
    setSelectedOrderId(null);
    setSelectedFailedOrderId(null);
    setPage(id);
  }

  return (
    <OrdersLayout page={page} onPageChange={handlePageChange}>
      {page === "order-processing" && (
        <OrderProcessingPage onViewOrder={handleViewOrder} onTakeAction={handleTakeAction} />
      )}
      {page === "failed-orders" && <FailedOrdersPage onViewFailedOrder={handleViewFailedOrder} />}
      {page === "refund-cases" && <RefundCasesPage />}
      {page === "order-detail" && (
        <OrderDetailPage
          orderId={selectedOrderId ?? "EX-99284-B"}
          mode={selectedOrderMode}
          onBack={handleBackToProcessing}
        />
      )}
      {page === "failed-order-detail" && (
        <FailedOrderDetailPage
          orderId={selectedFailedOrderId ?? ""}
          onBack={handleBackFromFailedDetail}
        />
      )}
    </OrdersLayout>
  );
}
