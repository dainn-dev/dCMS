import { useCallback, useEffect, useMemo, useState } from "react";
import {
  IconAdd,
  IconCalendarToday,
  IconCheckCircle,
  IconClose,
  IconDelete,
  IconEdit,
  IconHistory,
  IconSave,
  IconSearch,
  IconVisibility,
} from "../../orders/icons";
import {
  deleteQuantityLimitRule,
  fetchQuantityLimitHistory,
  fetchQuantityLimitSettings,
  formatDisplayDate,
  formatLimitTypeDisplay,
  saveCartQuantityLimit,
  type QuantityLimitHistoryEntry,
  type QuantityLimitRule,
} from "../api/quantityLimitSettingsApi";
import { AdvanceQuantityLimitSettingsPage } from "./AdvanceQuantityLimitSettingsPage";

const fieldBase =
  "h-9 w-full rounded border border-outline-variant/30 bg-surface-container-lowest px-3 text-xs text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary";
const btnPrimary =
  "flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container disabled:opacity-40";
const btnSecondary =
  "flex items-center gap-2 rounded-md border border-outline-variant/30 px-4 py-2 text-xs font-semibold text-on-surface-variant transition-colors hover:bg-surface-container-high disabled:opacity-40";
const PAGE_SIZE = 50;

function ChangeHistoryModal({
  open,
  onClose,
  entries,
  loading,
  error,
}: {
  open: boolean;
  onClose: () => void;
  entries: QuantityLimitHistoryEntry[];
  loading: boolean;
  error: string | null;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-scrim/40 p-4" role="dialog" aria-modal="true" aria-label="Quantity limit change history">
      <div className="flex max-h-[80vh] w-full max-w-2xl flex-col overflow-hidden rounded-lg border border-outline-variant/20 bg-surface shadow-2xl">
        <div className="flex items-center justify-between border-b border-outline-variant/15 px-5 py-4">
          <h2 className="font-headline text-lg font-bold">Change History</h2>
          <button type="button" className="rounded p-1 text-on-surface-variant hover:bg-surface-container-high" onClick={onClose} aria-label="Close">
            <IconClose className="h-5 w-5" />
          </button>
        </div>
        <div className="overflow-y-auto p-5">
          {loading ? <p className="text-sm text-on-surface-variant">Loading history…</p> : null}
          {error ? <p className="rounded-md border border-error/30 bg-error/10 px-3 py-2 text-xs text-on-error-container">{error}</p> : null}
          {!loading && !error && entries.length === 0 ? <p className="text-sm text-on-surface-variant">No saved changes yet.</p> : null}
          {!loading && entries.length > 0 ? (
            <ul className="space-y-3">
              {entries.map((e) => (
                <li key={e.id} className="rounded-md border border-outline-variant/20 bg-surface-container-lowest px-4 py-3 text-xs">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-semibold text-on-surface">{new Date(e.createdAt).toLocaleString()}</span>
                    <span className="text-on-surface-variant">{e.userId} · {e.userRole}</span>
                  </div>
                  <p className="mt-2 text-on-surface-variant">
                    Action: <strong className="text-on-surface">{e.action.replace(/_/g, " ")}</strong>
                  </p>
                </li>
              ))}
            </ul>
          ) : null}
        </div>
      </div>
    </div>
  );
}

type FormState =
  | { mode: "list" }
  | { mode: "add" }
  | { mode: "edit"; rule: QuantityLimitRule };

type Props = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
  onNavigateToProducts: () => void;
};

export function ProductQuantityLimitSettingsPage({ tenantId, storeId, authToken, onNavigateToProducts }: Props) {
  const apiReady = Boolean(tenantId && storeId);

  const [rules, setRules] = useState<QuantityLimitRule[]>([]);
  const [cartLimit, setCartLimit] = useState("1000");
  const [savedCartLimit, setSavedCartLimit] = useState("1000");
  const [formState, setFormState] = useState<FormState>({ mode: "list" });
  const [nameFilter, setNameFilter] = useState("");
  const [limitTypeFilter, setLimitTypeFilter] = useState("all");
  const [dateFilter, setDateFilter] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(apiReady);
  const [savingGeneral, setSavingGeneral] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [historyEntries, setHistoryEntries] = useState<QuantityLimitHistoryEntry[]>([]);

  const generalDirty = cartLimit !== savedCartLimit;

  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(false), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  const load = useCallback(async () => {
    if (!apiReady) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const cfg = await fetchQuantityLimitSettings(tenantId!, storeId!, authToken);
      setRules(cfg.rules);
      const limitStr = String(cfg.cartLimitPerProduct);
      setCartLimit(limitStr);
      setSavedCartLimit(limitStr);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load quantity limit settings.");
    } finally {
      setLoading(false);
    }
  }, [apiReady, tenantId, storeId, authToken]);

  useEffect(() => {
    void load();
  }, [load]);

  const filtered = useMemo(() => {
    const q = nameFilter.trim().toLowerCase();
    return rules.filter((row) => {
      const matchesName = !q || row.name.toLowerCase().includes(q);
      const matchesLimitType =
        limitTypeFilter === "all" ||
        (limitTypeFilter === "per_cart" && row.limitType === "per_cart") ||
        (limitTypeFilter === "per_user" && row.limitType === "per_user");
      const displayStart = formatDisplayDate(row.startDate);
      const matchesDate = !dateFilter || displayStart.toLowerCase().includes(dateFilter.toLowerCase()) || row.startDate.includes(dateFilter);
      return matchesName && matchesLimitType && matchesDate;
    });
  }, [dateFilter, limitTypeFilter, nameFilter, rules]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const visibleRows = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  async function handleShowHistory() {
    if (!apiReady) return;
    setHistoryOpen(true);
    setHistoryLoading(true);
    setHistoryError(null);
    try {
      setHistoryEntries(await fetchQuantityLimitHistory(tenantId!, storeId!, authToken));
    } catch (e) {
      setHistoryError(e instanceof Error ? e.message : "Failed to load change history.");
      setHistoryEntries([]);
    } finally {
      setHistoryLoading(false);
    }
  }

  async function handleSaveGeneral() {
    const parsed = Number(cartLimit);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError("Cart quantity limit must be a positive number.");
      return;
    }
    if (!apiReady) {
      setSavedCartLimit(cartLimit);
      setToast(true);
      return;
    }
    setSavingGeneral(true);
    setError(null);
    try {
      const saved = await saveCartQuantityLimit(tenantId!, storeId!, parsed, authToken);
      setCartLimit(String(saved.cartLimitPerProduct));
      setSavedCartLimit(String(saved.cartLimitPerProduct));
      setToast(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save cart quantity limit.");
    } finally {
      setSavingGeneral(false);
    }
  }

  async function handleDelete(ruleId: string) {
    if (!apiReady) {
      setRules((prev) => prev.filter((r) => r.id !== ruleId));
      return;
    }
    setDeletingId(ruleId);
    setError(null);
    try {
      await deleteQuantityLimitRule(tenantId!, storeId!, ruleId, authToken);
      setRules((prev) => prev.filter((r) => r.id !== ruleId));
      setToast(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to delete rule.");
    } finally {
      setDeletingId(null);
    }
  }

  function handleAdvanceSaved(rule: QuantityLimitRule) {
    setRules((prev) => {
      const exists = prev.some((r) => r.id === rule.id);
      return exists ? prev.map((r) => (r.id === rule.id ? rule : r)) : [rule, ...prev];
    });
    setFormState({ mode: "list" });
    setToast(true);
  }

  if (formState.mode !== "list") {
    return (
      <AdvanceQuantityLimitSettingsPage
        tenantId={tenantId}
        storeId={storeId}
        authToken={authToken}
        rule={formState.mode === "edit" ? formState.rule : undefined}
        onBack={() => setFormState({ mode: "list" })}
        onSave={handleAdvanceSaved}
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
        <div className="flex items-center gap-3">
          <button type="button" className={btnSecondary} disabled={!apiReady || loading} onClick={() => void handleShowHistory()}>
            <IconHistory className="h-4 w-4 shrink-0" />
            Show Change History
          </button>
          <button type="button" className={btnPrimary} onClick={() => setFormState({ mode: "add" })}>
            <IconAdd className="h-4 w-4 shrink-0" />
            New Advance Settings
          </button>
        </div>
      </div>

      <main className="space-y-3 p-5">
        {!apiReady && (
          <p className="rounded-md border border-outline-variant/20 bg-surface px-4 py-3 text-xs text-on-surface-variant">
            Demo mode — connect to a store context to persist settings to the catalog API.
          </p>
        )}
        {error ? (
          <p className="rounded-md border border-error/30 bg-error/10 px-4 py-3 text-xs text-on-error-container">{error}</p>
        ) : null}

        <section className="overflow-hidden rounded border border-outline-variant/25 bg-surface-container-lowest">
          <div className="border-b border-outline-variant/25 bg-surface-container px-5 py-3 text-xs text-on-surface-variant">General Settings</div>
          <div className="px-9 py-6">
            {loading ? (
              <p className="text-sm text-on-surface-variant">Loading…</p>
            ) : (
              <div className="grid max-w-[1320px] grid-cols-[520px_1fr] items-center gap-x-8">
                <label className="text-sm text-on-surface-variant">Cart Quantity Limit (per product)</label>
                <div className="flex items-center gap-3">
                  <input
                    className={`${fieldBase} max-w-[420px]`}
                    value={cartLimit}
                    onChange={(e) => setCartLimit(e.target.value)}
                    disabled={loading}
                  />
                  <button
                    type="button"
                    className="flex shrink-0 items-center gap-1.5 rounded-md bg-primary px-3 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container disabled:opacity-40"
                    disabled={!generalDirty || savingGeneral || loading}
                    onClick={() => void handleSaveGeneral()}
                  >
                    <IconSave className="h-4 w-4 shrink-0" />
                    {savingGeneral ? "Saving…" : "Save"}
                  </button>
                </div>
              </div>
            )}
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
                    <option value="all">Per User, Per Cart</option>
                    <option value="per_cart">Per Cart</option>
                    <option value="per_user">Per User</option>
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
                <button type="button" className="rounded border border-outline-variant/50 bg-surface-container-lowest px-7 py-2 text-xs font-semibold text-on-surface hover:bg-surface-container-high" onClick={() => { setNameFilter(""); setLimitTypeFilter("all"); setDateFilter(""); setPage(1); }}>Reset</button>
                <button type="button" className="rounded bg-on-surface px-7 py-2 text-xs font-semibold text-surface" onClick={() => setPage(1)}>Search</button>
              </div>
            </div>

            <div className="border border-outline-variant/15">
              <div className="flex items-center justify-between border-b border-outline-variant/15 bg-surface px-4 py-3 text-xs text-on-surface-variant">
                <span>Page {filtered.length === 0 ? 0 : safePage} of {filtered.length === 0 ? 0 : totalPages} pages, Each page {PAGE_SIZE}, Total {filtered.length} records found</span>
              </div>

              <div className="flex items-center justify-end gap-4 bg-surface px-4 py-2 text-xs text-on-surface-variant">
                <button type="button" disabled={safePage <= 1} onClick={() => setPage(1)}>&lt;&lt;</button>
                <button type="button" disabled={safePage <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>&lt;</button>
                <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">{safePage}</span>
                <button type="button" disabled={safePage >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>&gt;</button>
                <button type="button" disabled={safePage >= totalPages} onClick={() => setPage(totalPages)}>&gt;&gt;</button>
              </div>

              <table className="w-full border-collapse bg-surface text-center text-xs">
                <thead>
                  <tr className="border-y border-outline-variant/15 text-on-surface">
                    <th className="px-4 py-3 font-semibold">Name</th>
                    <th className="px-4 py-3 font-semibold">Limit Type</th>
                    <th className="px-4 py-3 font-semibold">Start Date</th>
                    <th className="px-4 py-3 font-semibold">End Date</th>
                    <th className="px-4 py-3 font-semibold">Modified By</th>
                    <th className="px-4 py-3 font-semibold text-on-surface">Action</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr><td colSpan={6} className="px-4 py-8 text-on-surface-variant">Loading rules…</td></tr>
                  ) : (
                    visibleRows.map((row) => (
                      <tr key={row.id} className="border-b border-outline-variant/15">
                        <td className="px-4 py-3">{row.name}</td>
                        <td className="px-4 py-3">{formatLimitTypeDisplay(row.limitType)}</td>
                        <td className="px-4 py-3">{formatDisplayDate(row.startDate)}</td>
                        <td className="px-4 py-3">{formatDisplayDate(row.endDate)}</td>
                        <td className="px-4 py-3">{row.modifiedBy}</td>
                        <td className="px-4 py-3">
                          <div className="flex items-center justify-center gap-1.5 text-on-surface-variant">
                            <button type="button" aria-label="Edit" onClick={() => setFormState({ mode: "edit", rule: row })}><IconEdit className="h-4 w-4" /></button>
                            <button type="button" aria-label="Delete" disabled={deletingId === row.id} onClick={() => void handleDelete(row.id)}><IconDelete className="h-4 w-4" /></button>
                            <button type="button" aria-label="View" onClick={() => setFormState({ mode: "edit", rule: row })}><IconVisibility className="h-4 w-4" /></button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                  {!loading && visibleRows.length === 0 && (
                    <tr><td colSpan={6} className="px-4 py-8 text-on-surface-variant">No records found.</td></tr>
                  )}
                </tbody>
              </table>

              <div className="flex items-center justify-end gap-4 bg-surface px-4 py-3 text-xs text-on-surface-variant">
                <button type="button" disabled={safePage <= 1} onClick={() => setPage(1)}>&lt;&lt;</button>
                <button type="button" disabled={safePage <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>&lt;</button>
                <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-xs font-bold text-on-primary">{safePage}</span>
                <button type="button" disabled={safePage >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>&gt;</button>
                <button type="button" disabled={safePage >= totalPages} onClick={() => setPage(totalPages)}>&gt;&gt;</button>
              </div>
            </div>
          </div>
        </section>
      </main>

      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <span className="text-sm font-semibold text-on-surface">Product quantity limit settings saved.</span>
        </div>
      )}

      <ChangeHistoryModal
        open={historyOpen}
        onClose={() => setHistoryOpen(false)}
        entries={historyEntries}
        loading={historyLoading}
        error={historyError}
      />
    </div>
  );
}
