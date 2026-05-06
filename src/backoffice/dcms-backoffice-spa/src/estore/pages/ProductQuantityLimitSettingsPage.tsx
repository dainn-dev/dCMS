import { useMemo, useState } from "react";
import {
  IconAdd,
  IconCalendarToday,
  IconCheckCircle,
  IconDelete,
  IconEdit,
  IconSave,
  IconSearch,
  IconVisibility,
} from "../../orders/icons";
import { AdvanceQuantityLimitSettingsPage } from "./AdvanceQuantityLimitSettingsPage";

export type QuantityLimitRow = {
  id: string;
  name: string;
  limitType: "Per Cart" | "Per User" | "Per User, Per Cart";
  startDate: string;
  endDate: string;
  modifiedBy: string;
};

type FormState =
  | { mode: "list" }
  | { mode: "add" }
  | { mode: "edit"; row: QuantityLimitRow };

const fieldBase =
  "h-9 w-full rounded border border-outline-variant/30 bg-surface-container-lowest px-3 text-xs text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container";
const PAGE_SIZE = 50;

const DEFAULT_ROWS: QuantityLimitRow[] = [
  {
    id: "q1",
    name: "Chanel per transaction",
    limitType: "Per Cart",
    startDate: "01-Jun-2022",
    endDate: "",
    modifiedBy: "jasmine.toh",
  },
  {
    id: "q2",
    name: "elc test",
    limitType: "Per User",
    startDate: "01-Jun-2024",
    endDate: "",
    modifiedBy: "",
  },
];

export function ProductQuantityLimitSettingsPage({ onNavigateToProducts }: { onNavigateToProducts: () => void }) {
  const [rows, setRows] = useState<QuantityLimitRow[]>(DEFAULT_ROWS);
  const [formState, setFormState] = useState<FormState>({ mode: "list" });
  const [cartLimit, setCartLimit] = useState("1000");
  const [nameFilter, setNameFilter] = useState("");
  const [limitTypeFilter, setLimitTypeFilter] = useState("Per User, Per Cart");
  const [dateFilter, setDateFilter] = useState("");
  const [page, setPage] = useState(1);
  const [toast, setToast] = useState(false);

  const filtered = useMemo(() => {
    const q = nameFilter.trim().toLowerCase();
    return rows.filter((row) => {
      const matchesName = !q || row.name.toLowerCase().includes(q);
      const matchesLimitType = limitTypeFilter === "Per User, Per Cart" || row.limitType === limitTypeFilter;
      const matchesDate = !dateFilter || row.startDate.toLowerCase().includes(dateFilter.toLowerCase());
      return matchesName && matchesLimitType && matchesDate;
    });
  }, [dateFilter, limitTypeFilter, nameFilter, rows]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const visibleRows = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  function showToast(messageDelay = 3000) {
    setToast(true);
    window.setTimeout(() => setToast(false), messageDelay);
  }

  function handleAdvanceSave(next: QuantityLimitRow) {
    setRows((prev) => {
      const exists = prev.some((r) => r.id === next.id);
      return exists ? prev.map((r) => (r.id === next.id ? next : r)) : [next, ...prev];
    });
    setFormState({ mode: "list" });
    showToast();
  }

  if (formState.mode !== "list") {
    return (
      <AdvanceQuantityLimitSettingsPage
        row={formState.mode === "edit" ? formState.row : undefined}
        onBack={() => setFormState({ mode: "list" })}
        onSave={handleAdvanceSave}
      />
    );
  }

  return (
    <div className="-m-6 min-h-[calc(100dvh-6rem)] bg-surface-container-low text-on-surface" aria-label="Product quantity limit settings">
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div className="min-w-0 space-y-1">
          <nav className="mb-1 flex flex-wrap gap-x-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="text-on-surface-variant/50">/</span>
            <button type="button" className="text-primary hover:underline" onClick={onNavigateToProducts}>
              Products
            </button>
            <span className="text-on-surface-variant/50">/</span>
            <span className="text-primary">Product Quantity Limit Settings</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Product Quantity Limit Settings</h1>
        </div>
        <button type="button" className={btnPrimary} onClick={() => setFormState({ mode: "add" })}>
          <IconAdd className="h-4 w-4 shrink-0" />
          New Advance Settings
        </button>
      </div>

      <main className="space-y-3 p-5">
        <section className="overflow-hidden rounded border border-outline-variant/25 bg-surface-container-lowest">
          <div className="border-b border-outline-variant/25 bg-surface-container px-5 py-3 text-xs text-on-surface-variant">General Settings</div>
          <div className="px-9 py-6">
            <div className="grid max-w-[1320px] grid-cols-[520px_1fr] items-center gap-x-8">
              <label className="text-sm text-on-surface-variant">Cart Quantity Limit (per product)</label>
              <div className="flex items-center gap-3">
                <input className={`${fieldBase} max-w-[420px]`} value={cartLimit} onChange={(e) => setCartLimit(e.target.value)} />
                <button type="button" className="flex shrink-0 items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container" onClick={() => showToast()}>
                  <IconSave className="h-4 w-4 shrink-0" />
                  Save
                </button>
              </div>
            </div>
          </div>
        </section>

        <section className="overflow-hidden rounded border border-outline-variant/25 bg-surface-container-lowest">
          <div className="border-b border-outline-variant/25 bg-surface-container px-5 py-3 text-xs text-on-surface-variant">Advance Settings</div>
          <div className="p-5">
            <div className="border-b border-outline-variant/20 bg-surface px-5 py-3 text-xs text-on-surface-variant">
              <span className="inline-flex items-center gap-1"><IconSearch className="h-3.5 w-3.5" />Search</span>
            </div>

            <div className="space-y-4 bg-surface px-5 py-5">
              <div className="grid grid-cols-[1fr_1fr_1fr] gap-8">
                <div className="space-y-1.5">
                  <label className="text-xs text-on-surface-variant">Name</label>
                  <input className={fieldBase} placeholder="Enter name" value={nameFilter} onChange={(e) => setNameFilter(e.target.value)} />
                </div>
                <div className="space-y-1.5">
                  <label className="text-xs text-on-surface-variant">Limit Type</label>
                  <select className={fieldBase} value={limitTypeFilter} onChange={(e) => setLimitTypeFilter(e.target.value)}>
                    <option value="Per User, Per Cart">Per User, Per Cart</option>
                    <option value="Per Cart">Per Cart</option>
                    <option value="Per User">Per User</option>
                  </select>
                </div>
                <div className="space-y-1.5">
                  <label className="text-xs text-on-surface-variant">Date</label>
                  <div className="flex">
                    <input className={`${fieldBase} rounded-r-none`} placeholder="Select date or date range" value={dateFilter} onChange={(e) => setDateFilter(e.target.value)} />
                    <button type="button" className="h-9 border-y border-outline-variant/30 px-3 text-on-surface-variant" onClick={() => setDateFilter("")}>×</button>
                    <button type="button" className="h-9 rounded-r border border-outline-variant/30 px-3 text-on-surface-variant"><IconCalendarToday className="h-4 w-4" /></button>
                  </div>
                </div>
              </div>
              <div className="flex justify-end gap-2">
                <button type="button" className="rounded border border-outline-variant/50 bg-surface-container-lowest px-7 py-2 text-xs font-semibold text-on-surface hover:bg-surface-container-high" onClick={() => { setNameFilter(""); setLimitTypeFilter("Per User, Per Cart"); setDateFilter(""); setPage(1); }}>Reset</button>
                <button type="button" className="rounded bg-on-surface px-7 py-2 text-xs font-semibold text-surface" onClick={() => setPage(1)}>Search</button>
              </div>
            </div>

            <div className="border border-outline-variant/15">
              <div className="flex items-center justify-between border-b border-outline-variant/15 bg-surface px-4 py-3 text-xs text-on-surface-variant">
                <span>Page {filtered.length === 0 ? 0 : safePage} of {filtered.length === 0 ? 0 : totalPages} pages, Each page {PAGE_SIZE}, Total {filtered.length} records found</span>
                <div className="flex items-center gap-2"><span>Per Page</span><select className="h-7 rounded border border-outline-variant/30 bg-surface-container-lowest px-2 text-xs" value="50" onChange={() => undefined}><option value="50">50</option></select></div>
              </div>

              <div className="flex items-center justify-end gap-4 bg-surface px-4 py-2 text-xs text-on-surface-variant">
                <button type="button" disabled={safePage <= 1} onClick={() => setPage(1)}>&lt;&lt;</button>
                <button type="button" disabled={safePage <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>&lt;</button>
                <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">{safePage}</span>
                <button type="button" disabled={safePage >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>&gt;</button>
                <button type="button" disabled={safePage >= totalPages} onClick={() => setPage(totalPages)}>&gt;&gt;</button>
              </div>

              <table className="w-full border-collapse bg-surface text-center text-xs">
                <thead><tr className="border-y border-outline-variant/15 text-on-surface"><th className="px-4 py-3 font-semibold">Name</th><th className="px-4 py-3 font-semibold">Limit Type</th><th className="px-4 py-3 font-semibold">Start Date</th><th className="px-4 py-3 font-semibold">End Date</th><th className="px-4 py-3 font-semibold">Modified By</th><th className="px-4 py-3 font-semibold text-on-surface">Action</th></tr></thead>
                <tbody>
                  {visibleRows.map((row) => (
                    <tr key={row.id} className="border-b border-outline-variant/15">
                      <td className="px-4 py-3">{row.name}</td><td className="px-4 py-3">{row.limitType}</td><td className="px-4 py-3">{row.startDate}</td><td className="px-4 py-3">{row.endDate}</td><td className="px-4 py-3">{row.modifiedBy}</td>
                      <td className="px-4 py-3"><div className="flex items-center justify-center gap-1.5 text-on-surface-variant"><button type="button" aria-label="Edit" onClick={() => setFormState({ mode: "edit", row })}><IconEdit className="h-4 w-4" /></button><button type="button" aria-label="Delete" onClick={() => setRows((prev) => prev.filter((r) => r.id !== row.id))}><IconDelete className="h-4 w-4" /></button><button type="button" aria-label="View" onClick={() => setFormState({ mode: "edit", row })}><IconVisibility className="h-4 w-4" /></button></div></td>
                    </tr>
                  ))}
                  {visibleRows.length === 0 && <tr><td colSpan={6} className="px-4 py-8 text-on-surface-variant">No records found.</td></tr>}
                </tbody>
              </table>

              <div className="flex items-center justify-end gap-4 bg-surface px-4 py-3 text-xs text-on-surface-variant">
                <button type="button" disabled={safePage <= 1} onClick={() => setPage(1)}>&lt;&lt;</button><button type="button" disabled={safePage <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>&lt;</button><span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">{safePage}</span><button type="button" disabled={safePage >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>&gt;</button><button type="button" disabled={safePage >= totalPages} onClick={() => setPage(totalPages)}>&gt;&gt;</button>
              </div>
            </div>
          </div>
        </section>
      </main>

      {toast && <div className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl"><IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" /><span className="text-sm font-semibold text-on-surface">Product quantity limit settings saved.</span></div>}
    </div>
  );
}
