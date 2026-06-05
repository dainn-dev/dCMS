import { useEffect, useState } from "react";
import { AbandonCartReportPage } from "./pages/AbandonCartReportPage";
import { DeliverySlotReportPage } from "./pages/DeliverySlotReportPage";
import { EcommercePaymentsReportPage } from "./pages/EcommercePaymentsReportPage";
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

type ResolvedContext = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

export function ReportsApp({ tenantId, storeId, authToken }: ReportsAppProps) {
  const [page, setPage] = useState<ReportsPageId>(() => parseReportsPageFromHash() ?? "transaction");
  // Loader (dcms-reports-section.js) is supposed to pass these, but older deployed
  // versions of the loader call mount(host) with no options. Fall back to the same
  // /umbraco/dcms/api/estore/context endpoint inside React so the page works either way.
  const [fallback, setFallback] = useState<ResolvedContext>({});

  useEffect(() => {
    if (tenantId && storeId && authToken) return;
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch("/umbraco/dcms/api/estore/context", {
          credentials: "same-origin",
          headers: { Accept: "application/json" },
        });
        if (!res.ok) return;
        const json: ResolvedContext = await res.json();
        if (!cancelled) setFallback(json ?? {});
      } catch {
        // ignore — page will surface the missing-tenant banner itself
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken]);

  const effTenantId = tenantId ?? fallback.tenantId;
  const effStoreId = storeId ?? fallback.storeId;
  const effAuthToken = authToken ?? fallback.authToken;

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
      {page === "transaction" && <TransactionReportPage tenantId={effTenantId} storeId={effStoreId} authToken={effAuthToken} />}
      {page === "sales" && <SalesReportPage tenantId={effTenantId} storeId={effStoreId} authToken={effAuthToken} />}
      {page === "ecommerce-payments" && (
        <EcommercePaymentsReportPage tenantId={effTenantId} storeId={effStoreId} authToken={effAuthToken} />
      )}
      {page === "abandon-cart" && <AbandonCartReportPage tenantId={effTenantId} storeId={effStoreId} authToken={effAuthToken} />}
      {page === "restock-subscriptions" && (
        <RestockSubscriptionsReportPage tenantId={effTenantId} storeId={effStoreId} authToken={effAuthToken} />
      )}
      {page === "delivery-slots" && <DeliverySlotReportPage tenantId={effTenantId} storeId={effStoreId} authToken={effAuthToken} />}
    </ReportsLayout>
  );
}
