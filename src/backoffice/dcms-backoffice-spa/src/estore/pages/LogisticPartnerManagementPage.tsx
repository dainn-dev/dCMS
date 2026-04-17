import { useEffect, useMemo, useState } from "react";
import { IconAddCircle, IconArrowBack, IconCheckCircle, IconClose, IconDelete, IconEdit, IconInfo } from "../../orders/icons";
import type { LogisticPartner } from "../EStoreApp";

const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";

type Props = {
  partners: LogisticPartner[];
  onChange: (next: LogisticPartner[]) => void;
  onBack: () => void;
};

function seedId() {
  return `lp-${Math.random().toString(36).slice(2, 8)}`;
}

export function LogisticPartnerManagementPage({ partners, onChange, onBack }: Props) {
  const [filter, setFilter] = useState("");
  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    if (!q) return partners;
    return partners.filter((p) => [p.name, p.code].some((x) => x.toLowerCase().includes(q)));
  }, [filter, partners]);

  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  const [modalOpen, setModalOpen] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [enabled, setEnabled] = useState(true);
  const [integratedLogistic, setIntegratedLogistic] = useState(true);

  function openAdd() {
    setModalOpen(true);
    setEditId(null);
    setName("");
    setCode("");
    setEnabled(true);
    setIntegratedLogistic(true);
  }

  function openEdit(p: LogisticPartner) {
    setModalOpen(true);
    setEditId(p.id);
    setName(p.name);
    setCode(p.code);
    setEnabled(p.enabled);
    setIntegratedLogistic(p.integratedLogistic);
  }

  function save() {
    const record: LogisticPartner = {
      id: editId ?? seedId(),
      name: name.trim() || "Unnamed Partner",
      code: (code.trim() || name.trim() || "PARTNER").toUpperCase().replace(/[^A-Z0-9_]+/g, "_"),
      enabled,
      integratedLogistic,
    };
    onChange(editId ? partners.map((p) => (p.id === editId ? record : p)) : [record, ...partners]);
    setModalOpen(false);
    setToast(editId ? "Logistic partner updated." : "Logistic partner created.");
  }

  function toggleEnabled(id: string, next: boolean) {
    onChange(partners.map((p) => (p.id === id ? { ...p, enabled: next } : p)));
    setToast(next ? "Logistic partner enabled." : "Logistic partner disabled.");
  }

  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Fulfillment Options
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">Logistic Partner Management</h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            Enable/disable logistic partners and configure integration settings.
          </p>
        </div>
        <button
          type="button"
          className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity"
          onClick={openAdd}
        >
          <IconAddCircle className="h-4 w-4 shrink-0" />
          Add Logistic Partner
        </button>
      </div>

      <div className="flex-1 space-y-6 p-6 pb-24">
        <div className="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-4">
          <IconInfo className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
          <p className="text-xs text-on-surface-variant leading-relaxed">
            Enabled logistic partners become selectable on Fulfillment Options Management (Advanced Settings).
          </p>
        </div>

        <section className="rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm overflow-hidden">
          <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 px-6 py-4">
            <h3 className="text-sm font-bold uppercase tracking-widest text-on-surface">Partners</h3>
            <div className="ml-auto flex items-center gap-3">
              <input
                className={`${inputBase} w-64`}
                placeholder="Search name or code…"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
              />
              <span className="rounded-full bg-surface-container-high px-3 py-0.5 text-[10px] font-bold text-on-surface-variant">
                {filtered.length} partner{filtered.length !== 1 ? "s" : ""}
              </span>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="bg-surface-container-high text-[10px] font-bold uppercase tracking-widest text-primary">
                  <th className="px-5 py-3">Name</th>
                  <th className="px-4 py-3">Code</th>
                  <th className="px-4 py-3">Integrated</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((p) => (
                  <tr key={p.id} className="text-xs hover:bg-surface-container-low transition-colors">
                    <td className="px-5 py-3.5 font-semibold text-on-surface">{p.name}</td>
                    <td className="px-4 py-3.5">
                      <code className="rounded bg-surface-container-high px-2 py-1 font-mono text-[11px] font-bold text-on-surface-variant">
                        {p.code}
                      </code>
                    </td>
                    <td className="px-4 py-3.5">
                      {p.integratedLogistic ? (
                        <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[9px] font-bold uppercase text-primary">Yes</span>
                      ) : (
                        <span className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant/60">No</span>
                      )}
                    </td>
                    <td className="px-4 py-3.5">
                      {p.enabled ? (
                        <span className="rounded-full bg-secondary-container/20 px-2 py-0.5 text-[9px] font-bold uppercase text-on-secondary-container">Enabled</span>
                      ) : (
                        <span className="rounded-full bg-surface-container-high px-2 py-0.5 text-[9px] font-bold uppercase text-on-surface-variant/60">Disabled</span>
                      )}
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          type="button"
                          title="Edit"
                          className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                          onClick={() => openEdit(p)}
                        >
                          <IconEdit className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          title={p.enabled ? "Disable" : "Enable"}
                          className="rounded p-2 text-on-surface-variant hover:bg-primary/10 hover:text-primary transition-colors"
                          onClick={() => toggleEnabled(p.id, !p.enabled)}
                        >
                          <IconCheckCircle className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {filtered.length === 0 && (
                  <tr>
                    <td colSpan={5} className="px-6 py-12 text-center text-xs text-on-surface-variant italic">
                      No logistic partners found.
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
                <h3 className="text-base font-bold text-on-surface">{editId ? "Configure Logistic Partner" : "Add Logistic Partner"}</h3>
                <p className="mt-1 text-xs text-on-surface-variant">Edit partner details and integration settings.</p>
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
                  <input className={inputBase} value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. DHL" />
                </div>
                <div>
                  <label className="block text-[0.6875rem] font-bold uppercase tracking-wider text-on-surface-variant mb-1">
                    Code <span className="text-error">*</span>
                  </label>
                  <input className={inputBase} value={code} onChange={(e) => setCode(e.target.value)} placeholder="e.g. DHL" />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                  <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
                  <div>
                    <p className="text-xs font-bold text-on-surface">Enabled</p>
                    <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                      When enabled, this partner is selectable in Fulfillment Options Management.
                    </p>
                  </div>
                </label>
                <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-lowest p-4 hover:border-primary/30 transition-colors select-none">
                  <input
                    type="checkbox"
                    className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                    checked={integratedLogistic}
                    onChange={(e) => setIntegratedLogistic(e.target.checked)}
                  />
                  <div>
                    <p className="text-xs font-bold text-on-surface">Integrated Logistic</p>
                    <p className="mt-0.5 text-[10px] text-on-surface-variant leading-relaxed">
                      If enabled, users cannot edit Actual Delivery Fee in Order Processing.
                    </p>
                  </div>
                </label>
              </div>
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
                className="flex items-center gap-2 rounded-md bg-primary px-6 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 hover:opacity-90 transition-opacity disabled:pointer-events-none disabled:opacity-40"
                disabled={!name.trim() || !code.trim()}
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

