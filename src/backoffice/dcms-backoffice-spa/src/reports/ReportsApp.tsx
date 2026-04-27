import { useEffect, useState } from "react";
import { AbandonCartReportPage } from "./pages/AbandonCartReportPage";
import { DeliverySlotReportPage } from "./pages/DeliverySlotReportPage";
import { RestockSubscriptionsReportPage } from "./pages/RestockSubscriptionsReportPage";
import { SalesReportPage } from "./pages/SalesReportPage";
import { TransactionReportPage } from "./pages/TransactionReportPage";
import { reportsHashForPage, parseReportsPageFromHash } from "./reportsHashRouting";
import { ReportsLayout, type ReportsPageId } from "./ReportsLayout";

type ReportsAppProps = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

export function ReportsApp({ tenantId, storeId, authToken }: ReportsAppProps) {
  const [page, setPage] = useState<ReportsPageId>(() => parseReportsPageFromHash() ?? "transaction");

  function handlePageChange(id: ReportsPageId) {
    setPage(id);
    const next = reportsHashForPage(id);
    if (window.location.hash !== next) {
      window.location.hash = next;
    }
  }

  useEffect(() => {
    function onHashChange() {
      const p = parseReportsPageFromHash();
      if (!p) return;
      setPage(p);
    }
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  useEffect(() => {
    const p = parseReportsPageFromHash();
    if (!p) {
      window.location.hash = reportsHashForPage("transaction");
      return;
    }
    const preferred = reportsHashForPage(p);
    if (window.location.hash !== preferred) {
      window.location.hash = preferred;
    }
  }, []);

  return (
    <ReportsLayout page={page} onPageChange={handlePageChange}>
      {page === "transaction" && <TransactionReportPage tenantId={tenantId} storeId={storeId} authToken={authToken} />}
      {page === "sales" && <SalesReportPage tenantId={tenantId} storeId={storeId} authToken={authToken} />}
      {page === "abandon-cart" && <AbandonCartReportPage tenantId={tenantId} storeId={storeId} authToken={authToken} />}
      {page === "restock-subscriptions" && (
        <RestockSubscriptionsReportPage tenantId={tenantId} storeId={storeId} authToken={authToken} />
      )}
      {page === "delivery-slots" && <DeliverySlotReportPage tenantId={tenantId} storeId={storeId} authToken={authToken} />}
    </ReportsLayout>
  );
}
