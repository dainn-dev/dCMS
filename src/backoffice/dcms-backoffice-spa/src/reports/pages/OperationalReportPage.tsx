import type { ColumnDef } from "@tanstack/react-table";
import { useCallback, useState } from "react";
import { DataTable } from "../../orders/components/DataTable";
import { IconDownload } from "../../orders/icons";
import { exportReportRowsToXlsx } from "../shared/exportReportRowsToXlsx";
import { ReportFilterField, ReportFilterPanel, inputClass } from "../shared/ReportFilterPanel";
type OpsTab = "restock" | "slots" | "abandon";

const STORE_OPTIONS = [
  { value: "all", label: "All stores" },
  { value: "sg-flagship", label: "SG — Flagship" },
  { value: "my-central", label: "MY — Central" },
];

const BRAND_OPTIONS = [
  { value: "all", label: "All brands" },
  { value: "CAS-7721", label: "Luxe Heritage" },
  { value: "VEL-4490", label: "Velocity Tech" },
  { value: "AUR-5501", label: "Aura Essentials" },
];

const SLOT_OPTIONS = [
  { value: "all", label: "All slots" },
  { value: "slot-am", label: "Morning 8–12" },
  { value: "slot-pm", label: "Afternoon 12–18" },
  { value: "slot-ev", label: "Evening 18–22" },
];

const TAB_META: Record<OpsTab, { title: string; spec: string }> = {
  restock: { title: "Restock subscriptions", spec: "7.1.7" },
  slots: { title: "Delivery slots", spec: "7.1.8" },
  abandon: { title: "Abandon cart", spec: "7.1.9" },
};

/** One row per product — subscriber count is deduped by user+product on the server (mock pre-aggregated). */
type RestockRow = {
  id: string;
  productName: string;
  sku: string;
  brandCode: string;
  subscriberCount: number;
  dateSubscribed: string;
};

type SlotRow = {
  id: string;
  slotName: string;
  date: string;
  slotCapacity: number;
  deliveryCount: number;
  utilisationPct: string;
};

/** Registered customers only; non-expired abandoned carts (mock). */
type AbandonRow = {
  id: string;
  customerName: string;
  email: string;
  brandCode: string;
  cartValue: string;
  itemsCount: number;
  lastActiveDate: string;
};

const MOCK_RESTOCK: RestockRow[] = [
  {
    id: "r1",
    productName: "Organic whole milk 2L",
    sku: "SKU-MILK-2L",
    brandCode: "CAS-7721",
    subscriberCount: 142,
    dateSubscribed: "2026-04-02",
  },
  {
    id: "r2",
    productName: "Dark roast beans 500g",
    sku: "SKU-BEAN-500",
    brandCode: "VEL-4490",
    subscriberCount: 89,
    dateSubscribed: "2026-04-04",
  },
  {
    id: "r3",
    productName: "Recycled tote bag",
    sku: "SKU-TOTE-01",
    brandCode: "AUR-5501",
    subscriberCount: 56,
    dateSubscribed: "2026-04-06",
  },
];

const MOCK_SLOTS: SlotRow[] = [
  { id: "s1", slotName: "Morning 8–12", date: "2026-04-10", slotCapacity: 80, deliveryCount: 62, utilisationPct: "77.5%" },
  { id: "s2", slotName: "Morning 8–12", date: "2026-04-11", slotCapacity: 80, deliveryCount: 71, utilisationPct: "88.8%" },
  { id: "s3", slotName: "Afternoon 12–18", date: "2026-04-10", slotCapacity: 120, deliveryCount: 98, utilisationPct: "81.7%" },
  { id: "s4", slotName: "Evening 18–22", date: "2026-04-10", slotCapacity: 60, deliveryCount: 54, utilisationPct: "90.0%" },
];

const MOCK_ABANDON: AbandonRow[] = [
  {
    id: "a1",
    customerName: "Alex Ng",
    email: "alex.ng@example.com",
    brandCode: "CAS-7721",
    cartValue: "240.50",
    itemsCount: 4,
    lastActiveDate: "2026-04-16 14:22",
  },
  {
    id: "a2",
    customerName: "Priya Menon",
    email: "priya.m@example.com",
    brandCode: "VEL-4490",
    cartValue: "89.00",
    itemsCount: 2,
    lastActiveDate: "2026-04-15 09:10",
  },
  {
    id: "a3",
    customerName: "Jordan Lee",
    email: "jordan.lee@example.com",
    brandCode: "AUR-5501",
    cartValue: "512.00",
    itemsCount: 7,
    lastActiveDate: "2026-04-14 20:05",
  },
];

const restockColumns: ColumnDef<RestockRow>[] = [
  {
    accessorKey: "productName",
    header: "Product name",
    cell: ({ row }) => <span className="text-xs font-bold text-on-surface">{row.getValue("productName")}</span>,
  },
  { accessorKey: "sku", header: "SKU", cell: ({ row }) => <span className="font-mono text-xs font-semibold">{row.getValue("sku")}</span> },
  {
    accessorKey: "subscriberCount",
    header: "Subscriber count",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("subscriberCount")}</span>,
  },
  {
    accessorKey: "dateSubscribed",
    header: "Date subscribed",
    cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("dateSubscribed")}</span>,
  },
];

const slotColumns: ColumnDef<SlotRow>[] = [
  { accessorKey: "slotName", header: "Slot name", cell: ({ row }) => <span className="text-xs font-bold">{row.getValue("slotName")}</span> },
  { accessorKey: "date", header: "Date", cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("date")}</span> },
  {
    accessorKey: "slotCapacity",
    header: "Slot capacity",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("slotCapacity")}</span>,
  },
  {
    accessorKey: "deliveryCount",
    header: "Delivery count",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("deliveryCount")}</span>,
  },
  {
    accessorKey: "utilisationPct",
    header: "Utilisation",
    cell: ({ row }) => <span className="tabular-nums text-xs font-semibold text-primary">{row.getValue("utilisationPct")}</span>,
  },
];

const abandonColumns: ColumnDef<AbandonRow>[] = [
  { accessorKey: "customerName", header: "Customer name", cell: ({ row }) => <span className="text-xs font-bold">{row.getValue("customerName")}</span> },
  { accessorKey: "email", header: "Email", cell: ({ row }) => <span className="text-xs">{row.getValue("email")}</span> },
  { accessorKey: "cartValue", header: "Cart value", cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("cartValue")}</span> },
  {
    accessorKey: "itemsCount",
    header: "Items count",
    cell: ({ row }) => <span className="tabular-nums text-xs">{row.getValue("itemsCount")}</span>,
  },
  {
    accessorKey: "lastActiveDate",
    header: "Last active",
    cell: ({ row }) => <span className="text-xs text-on-surface-variant">{row.getValue("lastActiveDate")}</span>,
  },
];

function parseCartValue(s: string): number {
  return parseFloat(String(s).replace(/,/g, "")) || 0;
}

function parseUtilisation(s: string): number {
  return parseFloat(String(s).replace(/%/g, "")) || 0;
}

function sortRestock(rows: RestockRow[]): RestockRow[] {
  return [...rows].sort((a, b) => b.subscriberCount - a.subscriberCount);
}

function sortSlots(rows: SlotRow[]): SlotRow[] {
  return [...rows].sort((a, b) => parseUtilisation(b.utilisationPct) - parseUtilisation(a.utilisationPct));
}

function sortAbandon(rows: AbandonRow[]): AbandonRow[] {
  return [...rows].sort((a, b) => parseCartValue(b.cartValue) - parseCartValue(a.cartValue));
}

function filterRestock(rows: RestockRow[], store: string, brand: string, productQ: string): RestockRow[] {
  let out = [...rows];
  if (brand !== "all") out = out.filter((r) => r.brandCode === brand);
  const q = productQ.trim().toLowerCase();
  if (q) out = out.filter((r) => r.sku.toLowerCase().includes(q) || r.productName.toLowerCase().includes(q));
  const f = store === "all" ? 1 : 0.55;
  if (f < 1) {
    out = out.map((r) => ({
      ...r,
      subscriberCount: Math.max(1, Math.floor(r.subscriberCount * f)),
    }));
  }
  return sortRestock(out);
}

function slotKeyFromValue(v: string): string {
  if (v === "slot-am") return "Morning 8–12";
  if (v === "slot-pm") return "Afternoon 12–18";
  if (v === "slot-ev") return "Evening 18–22";
  return "";
}

function filterSlots(rows: SlotRow[], store: string, slotFilter: string): SlotRow[] {
  let out = [...rows];
  if (slotFilter !== "all") {
    const name = slotKeyFromValue(slotFilter);
    if (name) out = out.filter((r) => r.slotName === name);
  }
  const f = store === "all" ? 1 : 0.62;
  if (f < 1) {
    out = out.map((r) => {
      const cap = Math.max(1, Math.floor(r.slotCapacity * f));
      const del = Math.max(0, Math.floor(r.deliveryCount * f));
      return {
        ...r,
        slotCapacity: cap,
        deliveryCount: del,
        utilisationPct: `${cap ? ((del / cap) * 100).toFixed(1) : "0.0"}%`,
      };
    });
  }
  return sortSlots(out);
}

function filterAbandon(rows: AbandonRow[], store: string, brand: string): AbandonRow[] {
  const f = store === "all" ? 1 : 0.7;
  let out = rows.map((r) => ({
    ...r,
    cartValue: (parseCartValue(r.cartValue) * f).toFixed(2),
    itemsCount: Math.max(1, Math.floor(r.itemsCount * f)),
  }));
  if (brand !== "all") out = out.filter((r) => r.brandCode === brand);
  return sortAbandon(out);
}

const DEMO_ABANDON_EXPORT_KEY = "dcms.demoAbandonCartExportRole";

function readAbandonExportRole(): boolean {
  try {
    const v = localStorage.getItem(DEMO_ABANDON_EXPORT_KEY);
    if (v === null) return false;
    return v === "1";
  } catch {
    return false;
  }
}

export function OperationalReportPage() {
  const [tab, setTab] = useState<OpsTab>("restock");
  const [dateFrom, setDateFrom] = useState("2026-04-01");
  const [dateTo, setDateTo] = useState("2026-04-18");
  const [storeScope, setStoreScope] = useState("all");
  const [brandScope, setBrandScope] = useState("all");
  const [productQuery, setProductQuery] = useState("");
  const [slotFilter, setSlotFilter] = useState("all");
  const [abandonExportRole, setAbandonExportRole] = useState(() => readAbandonExportRole());

  const [restockRows, setRestockRows] = useState<RestockRow[]>([]);
  const [slotRows, setSlotRows] = useState<SlotRow[]>([]);
  const [abandonRows, setAbandonRows] = useState<AbandonRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  const activeRows = tab === "restock" ? restockRows : tab === "slots" ? slotRows : abandonRows;
  const baseExportDisabled = loading || activeRows.length === 0;
  const abandonExportBlocked = tab === "abandon" && !abandonExportRole;
  const exportDisabled = baseExportDisabled || abandonExportBlocked;

  const persistAbandonExport = useCallback((on: boolean) => {
    setAbandonExportRole(on);
    try {
      localStorage.setItem(DEMO_ABANDON_EXPORT_KEY, on ? "1" : "0");
    } catch {
      /* ignore */
    }
  }, []);

  const handleSearch = useCallback(async () => {
    if (dateFrom > dateTo) {
      setRestockRows([]);
      setSlotRows([]);
      setAbandonRows([]);
      setHasSearched(true);
      return;
    }
    setLoading(true);
    setHasSearched(true);
    await new Promise((r) => setTimeout(r, 480));

    setRestockRows(filterRestock([...MOCK_RESTOCK], storeScope, brandScope, productQuery));
    setSlotRows(filterSlots([...MOCK_SLOTS], storeScope, slotFilter));
    setAbandonRows(filterAbandon([...MOCK_ABANDON], storeScope, brandScope));

    setLoading(false);
  }, [brandScope, dateFrom, dateTo, productQuery, slotFilter, storeScope]);

  const handleReset = useCallback(() => {
    setDateFrom("2026-04-01");
    setDateTo("2026-04-18");
    setStoreScope("all");
    setBrandScope("all");
    setProductQuery("");
    setSlotFilter("all");
    setRestockRows([]);
    setSlotRows([]);
    setAbandonRows([]);
    setHasSearched(false);
  }, []);

  const handleExport = useCallback(async () => {
    if (tab === "restock" && restockRows.length) {
      await exportReportRowsToXlsx(
        "RestockSubscriptions",
        "restock-subscriptions-7-1-7.xlsx",
        ["Product name", "SKU", "Subscriber count (deduped)", "Date subscribed"],
        restockRows.map((r) => [r.productName, r.sku, String(r.subscriberCount), r.dateSubscribed])
      );
    } else if (tab === "slots" && slotRows.length) {
      await exportReportRowsToXlsx(
        "DeliverySlots",
        "delivery-slots-7-1-8.xlsx",
        ["Slot name", "Date", "Slot capacity", "Delivery count", "Utilisation %"],
        slotRows.map((r) => [r.slotName, r.date, String(r.slotCapacity), String(r.deliveryCount), r.utilisationPct])
      );
    } else if (tab === "abandon" && abandonRows.length && abandonExportRole) {
      await exportReportRowsToXlsx(
        "AbandonCart",
        "abandon-cart-7-1-9.xlsx",
        ["Customer name", "Email", "Cart value", "Items count", "Last active date"],
        abandonRows.map((r) => [r.customerName, r.email, r.cartValue, String(r.itemsCount), r.lastActiveDate])
      );
    }
  }, [abandonExportRole, abandonRows.length, restockRows, slotRows, tab]);

  const emptyMessage =
    dateFrom > dateTo
      ? "Invalid date range: From is after To."
      : "No rows match the current filters. Adjust filters and click Search.";

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low" aria-label="Operational reports">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="space-y-2">
          <nav className="mb-1 flex text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">            
            <span>Reports</span>
            <span className="mx-2">/</span>
            <span className="text-primary">Operational</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Operational reports</h1>
          <p className="max-w-3xl text-sm text-on-surface-variant">
            Restock alerts, delivery slot utilisation, and abandoned carts for registered customers (demo data). Subscriber counts are one row per product with deduped users.
          </p>
        </div>
        <div className="flex flex-col items-stretch gap-2 md:items-end">
          {tab === "abandon" && (
            <p className="max-w-xs text-right text-xs text-on-surface-variant">
              Abandon cart export requires role permission. Enable the demo toggle in filters to simulate an allowed role.
            </p>
          )}
          <button
            type="button"
            disabled={exportDisabled}
            title={abandonExportBlocked ? "Export not permitted for your role (demo: enable in filters)" : undefined}
            className="flex items-center gap-2 self-start rounded-lg border border-outline-variant/30 bg-white px-4 py-2 text-xs font-bold text-on-surface shadow-sm hover:bg-surface-container-high disabled:pointer-events-none disabled:opacity-40 md:self-end"
            onClick={() => void handleExport()}
          >
            <IconDownload className="h-4 w-4 shrink-0 text-secondary" />
            Export to Excel
          </button>
        </div>
      </header>

      <div className="border-b border-outline-variant/10 bg-surface px-6">
        <div className="flex flex-wrap gap-1" role="tablist" aria-label="Operational report type">
          {(Object.keys(TAB_META) as OpsTab[]).map((id) => {
            const { title, spec } = TAB_META[id];
            const active = tab === id;
            return (
              <button
                key={id}
                type="button"
                role="tab"
                aria-selected={active}
                className={`border-b-2 px-4 py-3 text-xs font-bold transition-colors ${
                  active
                    ? "border-primary text-primary"
                    : "border-transparent text-on-surface-variant hover:text-on-surface"
                }`}
                onClick={() => setTab(id)}
              >
                {title}
                <span className="ml-1.5 font-medium opacity-70">({spec})</span>
              </button>
            );
          })}
        </div>
      </div>

      <div className="w-full flex-1 space-y-6 p-6">
        <ReportFilterPanel onSearch={() => void handleSearch()} onReset={handleReset} searchDisabled={loading}>
          <ReportFilterField label="From" htmlFor="op-from">
            <input id="op-from" type="date" className={inputClass} value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
          </ReportFilterField>
          <ReportFilterField label="To" htmlFor="op-to">
            <input id="op-to" type="date" className={inputClass} value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
          </ReportFilterField>
          <ReportFilterField label="Store" htmlFor="op-store">
            <select id="op-store" className={inputClass} value={storeScope} onChange={(e) => setStoreScope(e.target.value)}>
              {STORE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </ReportFilterField>
          {(tab === "restock" || tab === "abandon") && (
            <ReportFilterField label="Brand" htmlFor="op-brand">
              <select id="op-brand" className={inputClass} value={brandScope} onChange={(e) => setBrandScope(e.target.value)}>
                {BRAND_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </ReportFilterField>
          )}
          {tab === "restock" && (
            <ReportFilterField label="Product / SKU contains" htmlFor="op-prod">
              <input
                id="op-prod"
                type="search"
                placeholder="Filter products"
                className={inputClass}
                value={productQuery}
                onChange={(e) => setProductQuery(e.target.value)}
              />
            </ReportFilterField>
          )}
          {tab === "slots" && (
            <ReportFilterField label="Slot" htmlFor="op-slot">
              <select id="op-slot" className={inputClass} value={slotFilter} onChange={(e) => setSlotFilter(e.target.value)}>
                {SLOT_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </ReportFilterField>
          )}
          {tab === "abandon" && (
            <div className="flex min-w-[240px] flex-col gap-2 rounded-lg border border-secondary/25 bg-secondary/5 px-3 py-2">
              <label className="flex cursor-pointer items-center gap-2 text-xs font-semibold text-on-surface">
                <input
                  type="checkbox"
                  className="h-3.5 w-3.5 accent-primary"
                  checked={abandonExportRole}
                  onChange={(e) => persistAbandonExport(e.target.checked)}
                />
                Demo: role may export Abandon Cart
              </label>
              <p className="text-xs leading-snug text-on-surface-variant">
                Maps to Role Management in production. Without this, the export button stays disabled on this tab.
              </p>
            </div>
          )}
        </ReportFilterPanel>

        {tab === "restock" && hasSearched && restockRows.length > 0 && !loading && (
          <p className="text-xs text-on-surface-variant">
            Subscriber count is aggregated per product with duplicate user+product subscriptions removed (mock behaves as post-dedup API).
          </p>
        )}
        {tab === "abandon" && hasSearched && abandonRows.length > 0 && !loading && (
          <p className="text-xs text-on-surface-variant">
            Listed carts: registered customers only, abandoned and not expired within the reporting window (demo).
          </p>
        )}

        {loading && (
          <div
            className="flex items-center justify-center rounded-xl border border-outline-variant/20 bg-surface-container-lowest py-16 text-sm font-medium text-on-surface-variant"
            role="status"
            aria-live="polite"
          >
            Loading report…
          </div>
        )}

        {!loading && hasSearched && activeRows.length === 0 && (
          <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-outline-variant/30 bg-surface-container-lowest py-16 px-6 text-center">
            <p className="text-sm font-semibold text-on-surface">No results</p>
            <p className="mt-2 max-w-md text-xs text-on-surface-variant">{emptyMessage}</p>
          </div>
        )}

        {!loading && restockRows.length > 0 && tab === "restock" && (
          <DataTable columns={restockColumns} data={restockRows} getRowId={(r) => r.id} globalFilterPlaceholder="Search products…" />
        )}
        {!loading && slotRows.length > 0 && tab === "slots" && (
          <DataTable columns={slotColumns} data={slotRows} getRowId={(r) => r.id} globalFilterPlaceholder="Search slots…" />
        )}
        {!loading && abandonRows.length > 0 && tab === "abandon" && (
          <DataTable columns={abandonColumns} data={abandonRows} getRowId={(r) => r.id} globalFilterPlaceholder="Search customers…" />
        )}

        {!loading && !hasSearched && (
          <p className="rounded-xl border border-outline-variant/15 bg-surface-container-low/50 px-4 py-3 text-center text-xs text-on-surface-variant">
            Choose filters and click <span className="font-bold text-on-surface">Search</span> to load{" "}
            <span className="font-semibold text-on-surface">{TAB_META[tab].title}</span>.
          </p>
        )}
      </div>
    </div>
  );
}
