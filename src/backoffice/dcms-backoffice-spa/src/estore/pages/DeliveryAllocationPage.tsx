import { useEffect, useMemo, useState } from "react";
import {
  IconAddCircle,
  IconCheckCircle,
  IconClose,
  IconDelete,
  IconEdit,
  IconInfo,
} from "../../orders/icons";
import type { StockLocation } from "../EStoreApp";

const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";

type Props = {
  stockLocations: StockLocation[];
  onChange: (next: StockLocation[]) => void;
  onNavigateToFulfillmentOptions: () => void;
};

function seedId() {
  return `dc-${Math.random().toString(36).slice(2, 8)}`;
}

function seedCodeFromName(name: string) {
  const cleaned = name
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");
  return cleaned || "STOCK_LOCATION";
}

export function DeliveryAllocationPage({ stockLocations, onChange, onNavigateToFulfillmentOptions }: Props) {
  const [filter, setFilter] = useState("");
  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    if (!q) return stockLocations;
    return stockLocations.filter((s) => [s.name, s.code].some((x) => x.toLowerCase().includes(q)));
  }, [filter, stockLocations]);

  const [modalOpen, setModalOpen] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [active, setActive] = useState(true);

  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  function openAdd() {
    setModalOpen(true);
    setEditId(null);
    setName("");
    setCode("");
    setActive(true);
  }

  function openEdit(row: StockLocation) {
    setModalOpen(true);
    setEditId(row.id);
    setName(row.name);
    setCode(row.code);
    setActive(row.active);
  }

  function handleNameBlur() {
    if (!code.trim() && name.trim()) setCode(seedCodeFromName(name));
  }

  function save() {
    const record: StockLocation = {
      id: editId ?? seedId(),
      name: name.trim() || "Unnamed Location",
      code: (code.trim() || seedCodeFromName(name)).toUpperCase(),
      active,
    };
    onChange(editId ? stockLocations.map((s) => (s.id === editId ? record : s)) : [record, ...stockLocations]);
    setModalOpen(false);
    setToast(editId ? "Stock location updated." : "Stock location created.");
  }

  function remove(id: string) {
    onChange(stockLocations.filter((s) => s.id !== id));
    setToast("Stock location deleted.");
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      <header className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-start md:justify-between">
        <div className="space-y-2 min-w-0">
          <nav className="mb-1 flex flex-wrap gap-x-2 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
            <span>eStore</span>
            <span className="text-on-surface-variant/50">/</span>
            <button type="button" className="text-primary hover:underline" onClick={onNavigateToFulfillmentOptions}>
              Fulfillment Options
            </button>
            <span className="text-on-surface-variant/50">/</span>
            <span className="text-primary">Delivery Allocation</span>
          </nav>
          <h1 className="font-headline text-2xl font-bold tracking-tight text-on-surface">Delivery Allocation</h1>
          <p className="text-sm text-on-surface-variant max-w-2xl">
            Manage stock locations / distribution centers used to issue inventory for delivery slots.
          </p>
        </div>
        <button
          type="button"
          className="flex shrink-0 items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
          onClick={openAdd}
        >
          <IconAddCircle className="h-4 w-4 shrink-0" />
          Add Stock Location
        </button>
      </header>

      <div className="flex-1 space-y-6 p-6 pb-24">
        <div className="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
          <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
          <p className="text-xs text-on-surface-variant leading-relaxed">
            These stock locations can be selected when limiting a fulfillment group to a distribution center, or when enabling “Pick stocks from selected Stock Location”.
          </p>
        </div>

        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Stock Locations</h3>
            <div className="ml-auto flex items-center gap-3">
              <input
                className={`${inputBase} w-64`}
                placeholder="Search name or code…"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              />
              <span className="rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
                {filtered.length} location{filtered.length !== 1 ? "s" : ""}
              </span>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">Name</th>
                  <th className="px-4 py-3">Code</th>
                  <th className="px-4 py-3">Active</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((s) => (
                  <tr key={s.id} className="text-xs hover:bg-surface-container-low transition-colors">
                    <td className="px-5 py-3.5 font-semibold text-on-surface">{s.name}</td>
                    <td className="px-4 py-3.5">
                      <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">{s.code}</code>
                    </td>
                    <td className="px-4 py-3.5">
                      {s.active ? (
                        <span className="rounded-full bg-secondary-container/20 px-2 py-0.5 text-[9px] font-bold uppercase text-on-secondary-container">Yes</span>
                      ) : (
                        <span className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant/60">No</span>
                      )}
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          title="Edit"
                          className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                          onClick={() => openEdit(s)}
                        >
                          <IconEdit className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          title="Delete"
                          className="rounded p-2 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                          onClick={() => remove(s.id)}
                        >
                          <IconDelete className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {filtered.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-6 py-12 text-center text-xs text-on-surface-variant italic">
                      No stock locations found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[640px] max-w-[calc(100vw-2rem)] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl overflow-hidden">
            <div className="flex items-start justify-between gap-4 border-b border-outline-variant/10 px-6 py-5">
              <div>
                <h3 className="text-base font-bold text-on-surface">{editId ? "Edit Stock Location" : "Add Stock Location"}</h3>
                <p className="mt-1 text-xs text-on-surface-variant">Used for issuing inventory from a selected distribution center.</p>
              </div>
              <button type="button" className="rounded p-2 hover:bg-surface-container transition-colors" onClick={() => setModalOpen(false)}>
                <IconClose className="h-5 w-5 text-on-surface-variant" />
              </button>
            </div>

            <div className="px-6 py-6 space-y-5">
              <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Name <span className="text-error">*</span>
                  </label>
                  <input className={inputBase} value={name} onChange={(e) => setName(e.target.value)} onBlur={handleNameBlur} />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Code <span className="text-error">*</span>
                  </label>
                  <input className={inputBase} value={code} onChange={(e) => setCode(e.target.value)} placeholder="AUTO-GENERATED" />
                </div>
              </div>
              <label className="flex cursor-pointer items-center gap-2.5 select-none">
                <input type="checkbox" className="h-4 w-4 accent-primary" checked={active} onChange={(e) => setActive(e.target.checked)} />
                <span className="text-xs font-bold text-on-surface">Active</span>
              </label>
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
                onClick={save}
              >
                <IconCheckCircle className="h-4 w-4 shrink-0" />
                Save
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}

