import { useState } from "react";
import { OperationalReportPage } from "./pages/OperationalReportPage";
import { SalesReportPage } from "./pages/SalesReportPage";
import { TransactionReportPage } from "./pages/TransactionReportPage";
import { ReportsLayout, type ReportsPageId } from "./ReportsLayout";

export function ReportsApp() {
  const [page, setPage] = useState<ReportsPageId>("transaction");

  return (
    <ReportsLayout page={page} onPageChange={setPage}>
      {page === "transaction" && <TransactionReportPage />}
      {page === "sales" && <SalesReportPage />}
      {page === "operational" && <OperationalReportPage />}
    </ReportsLayout>
  );
}
