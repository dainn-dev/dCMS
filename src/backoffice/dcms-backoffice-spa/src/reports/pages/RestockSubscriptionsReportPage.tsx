import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useMemo, useState } from "react";
import { fetchRestockSubscriptions, type RestockSubscriptionRow } from "../api/reportsApi";
import { ReportView, type ReportFilters } from "../shared/ReportView";
import { ReportFilterField, inputClass } from "../shared/ReportFilterPanel";

type Props = { tenantId?: string; storeId?: string; authToken?: string };

type UiRow = RestockSubscriptionRow & { id: string };

// Subscription/sent timestamps are ISO; the report shows the calendar date only.
const day = (v: unknown) => (v ? String(v).slice(0, 10) : "");
const dash = (v: unknown) => (v == null || v === "" ? "—" : String(v));

/**
 * Restock Notifications Subscriptions report (BRD §1–§9).
 * Reads GET /reports/restock-subscriptions — one row per subscription (BR03).
 * Filters: Subscription Date range (BR01, built-in date filters) + optional UPC/SKU/Product Name/Email (BR02).
 */
export function RestockSubscriptionsReportPage({ tenantId, storeId, authToken }: Props) {
  // BRD §2.2–§2.5 — optional text filters, kept outside ReportView's built-in date filters.
  const [upc, setUpc] = useState("");
  const [sku, setSku] = useState("");
  const [productName, setProductName] = useState("");
  const [email, setEmail] = useState("");

  const columns: ColumnDef<UiRow>[] = useMemo(
    () => [
      {
        accessorKey: "upc",
        header: "UPC",
        cell: ({ row }) => <span className="font-mono text-xs">{dash(row.getValue("upc"))}</span>,
      },
      {
        accessorKey: "sku",
        header: "SKU",
        cell: ({ row }) => <span className="font-mono text-xs">{dash(row.getValue("sku"))}</span>,
      },
      {
        accessorKey: "productName",
        header: "Product Name",
        cell: ({ row }) => <span className="text-xs font-semibold text-on-surface">{dash(row.getValue("productName"))}</span>,
      },
      {
        accessorKey: "email",
        header: "Email",
        cell: ({ row }) => <span className="text-xs">{dash(row.getValue("email"))}</span>,
      },
      {
        accessorKey: "subscriptionDate",
        header: "Subscription Date",
        cell: ({ row }) => <span className="text-xs tabular-nums">{day(row.getValue("subscriptionDate"))}</span>,
      },
      {
        accessorKey: "restockNotificationSentOn",
        header: "Restock Notification Sent On",
        // Blank/— means the product isn't restocked yet or the email hasn't been sent (BRD §4).
        cell: ({ row }) => {
          const v = row.getValue("restockNotificationSentOn");
          return <span className="text-xs tabular-nums">{v ? day(v) : "—"}</span>;
        },
      },
    ],
    [],
  );

  // Extra filters are captured in this closure, so changing them (then Search) re-runs the fetch.
  const loadData = useCallback(
    async (filters: ReportFilters): Promise<UiRow[]> => {
      if (!tenantId) return [];
      const rows = await fetchRestockSubscriptions(
        tenantId,
        { dateFrom: filters.dateFrom, dateTo: filters.dateTo, storeId },
        { upc, sku, productName, email },
        authToken,
      );
      // BR03: each subscription is its own row — key on product + email + subscription instant.
      return rows.map((r, i) => ({ ...r, id: `${r.productId}|${r.email ?? ""}|${r.subscriptionDate}|${i}` }));
    },
    [tenantId, storeId, authToken, upc, sku, productName, email],
  );

  return (
    <ReportView<UiRow>
      breadcrumb="Restock subscriptions"
      title="Restock subscriptions"
      description="Customers waiting for out-of-stock products to be restocked — one row per subscription (BRD §1)."
      exportSheetName="RestockSubscriptions"
      exportFilename="restock-subscriptions.xlsx"
      columns={columns}
      exportHeaders={["UPC", "SKU", "Product Name", "Email", "Subscription Date", "Restock Notification Sent On"]}
      toExportRow={(r) => [
        r.upc ?? "",
        r.sku ?? "",
        r.productName ?? "",
        r.email ?? "",
        day(r.subscriptionDate),
        r.restockNotificationSentOn ? day(r.restockNotificationSentOn) : "",
      ]}
      loadData={loadData}
      showStoreFilter={false}
      onReset={() => {
        setUpc("");
        setSku("");
        setProductName("");
        setEmail("");
      }}
      emptyMessage="No restock subscriptions match the current filters (the subscribe/notify projection is populated by the Phase-2 storefront)."
      extraFilters={
        <>
          <ReportFilterField label="UPC" htmlFor="restock-upc">
            <input id="restock-upc" className={inputClass} value={upc} onChange={(e) => setUpc(e.target.value)} placeholder="UPC" />
          </ReportFilterField>
          <ReportFilterField label="SKU" htmlFor="restock-sku">
            <input id="restock-sku" className={inputClass} value={sku} onChange={(e) => setSku(e.target.value)} placeholder="SKU" />
          </ReportFilterField>
          <ReportFilterField label="Product name" htmlFor="restock-name">
            <input id="restock-name" className={inputClass} value={productName} onChange={(e) => setProductName(e.target.value)} placeholder="Product name" />
          </ReportFilterField>
          <ReportFilterField label="Email" htmlFor="restock-email">
            <input id="restock-email" className={inputClass} value={email} onChange={(e) => setEmail(e.target.value)} placeholder="Customer email" />
          </ReportFilterField>
        </>
      }
    />
  );
}
