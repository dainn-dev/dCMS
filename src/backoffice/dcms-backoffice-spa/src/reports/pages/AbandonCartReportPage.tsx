import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo } from "react";
import { fetchAbandonCart, type AbandonCartRow } from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";
import { IconMail, IconVisibility } from "../../orders/icons";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = AbandonCartRow & { id: string };

// Multi-currency safe (CLAUDE.md): map ISO code → symbol, fall back to the code itself.
const CURRENCY_SYMBOL: Record<string, string> = { SGD: "S$", USD: "$", MYR: "RM", THB: "฿", VND: "₫", IDR: "Rp" };
const money = (value: number, currency: string) => {
  const symbol = CURRENCY_SYMBOL[currency] ?? `${currency} `;
  return `${symbol}${Number(value ?? 0).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
};
const num = (v: unknown) => Number(v ?? 0).toLocaleString("en-US");
// BRD example: "05/12/2024 4:45:17 PM". Blank when no reminder has been sent.
const dateTime = (v: unknown) => (v ? new Date(String(v)).toLocaleString("en-US") : "");

/**
 * Abandon Cart report (BRD §1–§10). Reads GET /reports/abandon-cart.
 * One row per abandoned cart (BR03): created but not confirmed within the inactivity window (BR01/BR02).
 * Cart value (BR04), product count (BR05) and reminder-email stats (BR06/BR07) come from the analytics
 * projection of cart events.
 */
export function AbandonCartReportPage({ tenantId, storeId, authToken }: Props) {
  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "customerName",
        header: "Name",
        cell: ({ row }) => <span className="text-xs font-semibold text-on-surface">{row.original.customerName || "—"}</span>,
      },
      {
        accessorKey: "customerEmail",
        header: "Email",
        cell: ({ row }) => <span className="text-xs">{row.original.customerEmail || "—"}</span>,
      },
      {
        accessorKey: "cartValue",
        header: "Cart Value (S$)",
        cell: ({ row }) => (
          <span className="text-xs font-semibold tabular-nums">{money(row.original.cartValue, row.original.currency)}</span>
        ),
      },
      {
        accessorKey: "productCount",
        header: "No. of Products in Cart",
        cell: ({ row }) => <span className="text-xs tabular-nums">{num(row.original.productCount)}</span>,
      },
      {
        accessorKey: "emailSentCount",
        header: "No. of Email Sent",
        cell: ({ row }) => <span className="text-xs tabular-nums">{num(row.original.emailSentCount)}</span>,
      },
      {
        accessorKey: "lastEmailSentAt",
        header: "Last Email Sent Date",
        // Blank = no reminder sent / customer not yet in the email campaign (BRD §5).
        cell: ({ row }) => <span className="text-xs tabular-nums">{dateTime(row.original.lastEmailSentAt) || "—"}</span>,
      },
      {
        id: "actions",
        header: "Actions",
        enableSorting: false,
        // BRD §5: View cart / Send reminder. Wired once the marketing-automation API ships (Phase 2);
        // disabled with a tooltip until then, matching the Transaction report's not-yet-available controls.
        cell: () => (
          <div className="flex items-center gap-1">
            <button
              type="button"
              disabled
              title="View cart details — available with the cart-detail API (Phase 2)."
              className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-40"
            >
              <IconVisibility className="h-4 w-4" />
            </button>
            <button
              type="button"
              disabled
              title="Send reminder email — available with the marketing-automation API (Phase 2)."
              className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high disabled:opacity-40"
            >
              <IconMail className="h-4 w-4" />
            </button>
          </div>
        ),
      },
    ],
    [],
  );

  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) return [];
      const apiRows = await fetchAbandonCart(
        tenantId,
        {
          dateFrom: filters.dateFrom,
          dateTo: filters.dateTo,
          storeId: filters.storeScope !== "all" ? filters.storeScope : storeId ?? "all",
        },
        authToken,
      );
      return apiRows.map((r, i) => ({ ...r, id: `${r.cartId}|${i}` }));
    },
    [tenantId, storeId, authToken],
  );

  return (
    <ReportView<UiRow>
      breadcrumb="Abandon cart"
      title="Abandon cart report"
      description="Customers who added products but didn't complete checkout — cart value, items and reminder-email history (BRD §1)."
      exportSheetName="AbandonCart"
      exportFilename="abandon-cart.xlsx"
      columns={columns}
      exportHeaders={[
        "Name",
        "Email",
        "Cart Value (S$)",
        "No. of Products in Cart",
        "No. of Email Sent",
        "Last Email Sent Date",
      ]}
      // BR08: export reflects the active filters (ReportView exports the currently loaded rows). Actions
      // column is UI-only, so it's omitted from the export.
      toExportRow={(r) => [
        r.customerName ?? "",
        r.customerEmail ?? "",
        money(r.cartValue, r.currency),
        String(r.productCount),
        String(r.emailSentCount),
        r.lastEmailSentAt ? dateTime(r.lastEmailSentAt) : "",
      ]}
      loadData={loadData}
      emptyMessage="No abandoned carts for the selected period (the cart-events projection is populated by the Phase-2 storefront)."
    />
  );
}
