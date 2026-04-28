import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo } from "react";
import { fetchAbandonCart, type AbandonCartRow } from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = {
  id: string;
  cartId: string;
  createdAt: string;
};

export function AbandonCartReportPage({ tenantId, storeId, authToken }: Props) {
  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "cartId",
        header: "Cart ID",
        cell: ({ row }) => <span className="font-mono text-xs">{row.getValue("cartId")}</span>,
      },
      {
        accessorKey: "createdAt",
        header: "Created at",
        cell: ({ row }) => <span className="text-xs tabular-nums">{row.getValue("createdAt")}</span>,
      },
    ],
    [],
  );

  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) return [];
      const apiRows: AbandonCartRow[] = await fetchAbandonCart(
        tenantId,
        {
          dateFrom: filters.dateFrom,
          dateTo: filters.dateTo,
          storeId: filters.storeScope !== "all" ? filters.storeScope : storeId ?? "all",
        },
        authToken,
      );
      return apiRows.map((r) => ({
        id: r.cartId,
        cartId: r.cartId,
        createdAt: r.createdAt,
      }));
    },
    [tenantId, storeId, authToken],
  );

  return (
    <ReportView<UiRow>
      breadcrumb="Abandon cart"
      title="Abandon cart report"
      description="Carts created but not confirmed within 24h."
      exportSheetName="AbandonCart"
      exportFilename="abandon-cart.xlsx"
      columns={columns}
      exportHeaders={["Cart ID", "Created at"]}
      toExportRow={(r) => [r.cartId, r.createdAt]}
      loadData={loadData}
    />
  );
}
