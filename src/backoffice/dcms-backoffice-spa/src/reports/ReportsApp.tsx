import { useEffect, useState } from "react";
import { OperationalReportPage } from "./pages/OperationalReportPage";
import { SalesReportPage } from "./pages/SalesReportPage";
import { TransactionReportPage } from "./pages/TransactionReportPage";
import { reportsHashForPage, parseReportsPageFromHash } from "./reportsHashRouting";
import { ReportsLayout, type ReportsPageId } from "./ReportsLayout";

export function ReportsApp() {
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
      {page === "transaction" && <TransactionReportPage />}
      {page === "sales" && <SalesReportPage />}
      {page === "operational" && <OperationalReportPage />}
    </ReportsLayout>
  );
}
