import { useState } from "react";
import { OrdersLayout, type OrdersPageId } from "./layout/OrdersLayout";
import { FailedOrdersPage } from "./pages/FailedOrdersPage";
import { OrderDetailPage } from "./pages/OrderDetailPage";
import { OrderProcessingPage } from "./pages/OrderProcessingPage";
import { RefundCasesPage } from "./pages/RefundCasesPage";

export function OrdersApp() {
  const [page, setPage] = useState<OrdersPageId>("order-processing");
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);

  function handleViewOrder(orderId: string) {
    setSelectedOrderId(orderId);
    setPage("order-detail");
  }

  function handleBackToProcessing() {
    setSelectedOrderId(null);
    setPage("order-processing");
  }

  function handlePageChange(id: OrdersPageId) {
    setSelectedOrderId(null);
    setPage(id);
  }

  return (
    <OrdersLayout page={page} onPageChange={handlePageChange}>
      {page === "order-processing" && <OrderProcessingPage onViewOrder={handleViewOrder} />}
      {page === "failed-orders" && <FailedOrdersPage />}
      {page === "refund-cases" && <RefundCasesPage />}
      {page === "order-detail" && (
        <OrderDetailPage orderId={selectedOrderId ?? "EX-99284-B"} onBack={handleBackToProcessing} />
      )}
    </OrdersLayout>
  );
}
