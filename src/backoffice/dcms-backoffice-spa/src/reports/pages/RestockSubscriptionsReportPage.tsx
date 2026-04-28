import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo } from "react";
import { fetchRestockSubscriptions, type RestockSubscriptionAggRow } from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = RestockSubscriptionAggRow & { id: string };

export function RestockSubscriptionsReportPage({ tenantId, storeId, authToken }: Props) {
  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "productId",
        header: "Product ID",
        cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("productId")}</span>,
      },
      {
        accessorKey: "subscriptions",
        header: "Subscriptions",
        cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("subscriptions")}</span>,
      },
      {
        accessorKey: "fulfilled",
        header: "Fulfilled",
        cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("fulfilled")}</span>,
      },
    ],
    [],
  );

  // The restock-subscriptions endpoint only filters by store; date range is ignored.
  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) return [];
      const apiRows = await fetchRestockSubscriptions(
        tenantId,
        { storeId: filters.storeScope !== "all" ? filters.storeScope : storeId ?? "all" },
        authToken,
      );
      return apiRows.map((r) => ({ ...r, id: r.productId }));
    },
    [tenantId, storeId, authToken],
  );

  return (
    <ReportView<UiRow>
      breadcrumb="Restock subscriptions"
      title="Restock subscriptions"
      description="Customers waiting for restock notifications, aggregated per product (analytics DB)."
      exportSheetName="RestockSubscriptions"
      exportFilename="restock-subscriptions.xlsx"
      columns={columns}
      exportHeaders={["Product ID", "Subscriptions", "Fulfilled"]}
      toExportRow={(r) => [r.productId, String(r.subscriptions), String(r.fulfilled)]}
      loadData={loadData}
      showDateFilters={false}
    />
  );
}
