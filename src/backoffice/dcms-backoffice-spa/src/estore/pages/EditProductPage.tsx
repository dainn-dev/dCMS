import { useCallback, useEffect, useRef, useState, type ChangeEvent } from "react";
import { MultiLangInput, MultiLangTextarea } from "../components/MultiLangField";
import { MultiLangLexicalRichText } from "../components/MultiLangLexicalRichText";
import {
  IconAdd,
  IconArrowBack,
  IconAutoFixHigh,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconCloudUpload,
  IconCrop,
  IconDelete,
  IconEditNote,
  IconFactCheck,
  IconFolder,
  IconImage,
  IconInfo,
  IconLayers,
  IconLightbulb,
  IconOpenInNew,
  IconOpenWith,
  IconRestartAlt,
  IconRotateCcw,
  IconRotateCw,
  IconSave,
  IconSearch,
  IconTextFields,
  IconTune,
  IconVisibility,
} from "../../orders/icons";
import type { ProductListRow } from "./ProductsPage";
import {
  createProduct,
  createManualVariant,
  fetchProducts,
  getProduct,
  hideProduct,
  listRecommendations,
  listVariants,
  productToPayload,
  setRecommendations,
  unhideProduct,
  updateProduct,
  updateVariant,
  type ProductVariantDto,
} from "../api/productsApi";
import { fetchCategories } from "../api/categoriesApi";
import { fetchBrands } from "../api/brandsApi";
import { fetchProductFieldConfig, type ProductFieldRecord } from "../api/productFieldConfigApi";
import { ProductCustomFieldsSection } from "../components/ProductCustomFieldsSection";
import {
  buildCustomFieldsPayload,
  findMissingRequiredCustomField,
  initCustomFieldValues,
  parseProductCustomFields,
  tabForTargetPage,
  type ProductCustomFieldsMap,
} from "../utils/productCustomFieldUtils";
import { getVariantStock, setVariantOnHand } from "../api/inventoryApi";
import type { CatNode } from "./CategoriesPage";

/** Build a URL-safe slug from a product name. */
function slugify(input: string): string {
  return input
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 200);
}

/** ISO timestamp → value for an <input type="datetime-local"> (local "YYYY-MM-DDTHH:mm"), or "" when empty. */
function isoToLocalInput(iso?: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** datetime-local value → ISO string (UTC), or null when empty/invalid. */
function localInputToIso(value: string): string | null {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

/** Parse a JSON map of locale→string; returns {} on any error. */
function parseLocaleMap(json?: string | null): Record<string, string> {
  if (!json) return {};
  try {
    const parsed = JSON.parse(json) as Record<string, string>;
    return parsed && typeof parsed === "object" ? parsed : {};
  } catch {
    return {};
  }
}

/** Flatten the category tree to "id → indented label" options for the category select. */
function flattenCategoryNodes(nodes: CatNode[], depth = 0): { id: number; label: string }[] {
  const out: { id: number; label: string }[] = [];
  for (const n of nodes) {
    const idNum = Number(n.id);
    if (Number.isInteger(idNum)) out.push({ id: idNum, label: `${"\u00A0\u00A0".repeat(depth)}${n.name}` });
    if (n.children?.length) out.push(...flattenCategoryNodes(n.children, depth + 1));
  }
  return out;
}

// ── Style tokens ────────────────────────────────────────────────────────────
const labelBase =
  "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const sectionTitle =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2 mb-6";
const sectionTitleRow =
  "text-sm font-bold uppercase tracking-widest text-primary border-b border-outline-variant/20 pb-2";
const sectionIntroText = "text-sm text-on-surface-variant mt-2";
const btnFooterGhost =
  "text-on-surface-variant font-bold text-xs uppercase tracking-widest px-6 py-2 hover:bg-surface-container-high rounded-md transition-colors";
const btnFooterPrimary =
  "px-6 py-2 bg-primary text-on-primary rounded-md font-bold text-xs uppercase tracking-widest shadow-lg shadow-primary/20 hover:scale-[1.02] active:scale-95 transition-all flex items-center gap-2";

// ── Types ───────────────────────────────────────────────────────────────────
type EditTab = "general" | "product-page" | "recommendations";

type ItemType =
  | "physical"
  | "membership-points"
  | "membership-voucher"
  | "membership"
  | "stored-value-card";

type ProductOption = {
  id: string;
  type: "textbox" | "dropdown" | "bundle";
  label: string;
  required: boolean;
  dropdownOptions: string;
};

type ProductImage = {
  id: string;
  name: string;
  alt: string;
  /** bg colour used when `src` is missing */
  color: string;
  /** data URL (image) or blob: URL (video) */
  src?: string;
  isVideo?: boolean;
};

type Props = {
  mode: "add" | "edit";
  product?: ProductListRow;
  onBack: () => void;
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

// ── Mock data ───────────────────────────────────────────────────────────────
const MOCK_STORES = ["Main Store", "Outlet A", "Outlet B", "Online Store", "Pop-up Boutique"];
const MOCK_ATTRIBUTES = ["Color", "Size", "Material", "Weight", "Dimensions", "Style", "Pattern"];
const MOCK_CARD_TIERS = ["Gold", "Silver", "Platinum", "Bronze"];
const MOCK_CARD_TYPES = ["Standard", "Premium", "Elite"];

// ── Shared: SearchablePicker (Brand / Category / Attributes) ────────────────
function SearchablePicker({
  label,
  options,
  selected,
  onChange,
  placeholder = "Type to search...",
  multi = true,
}: {
  label: string;
  options: string[];
  selected: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
  multi?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onDown(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, []);

  const filtered = options.filter((o) =>
    o.toLowerCase().includes(search.toLowerCase())
  );

  const toggle = (item: string) => {
    if (!multi) {
      onChange(selected.includes(item) ? [] : [item]);
      setOpen(false);
      return;
    }
    onChange(
      selected.includes(item)
        ? selected.filter((s) => s !== item)
        : [...selected, item]
    );
  };

  return (
    <div className="space-y-1.5">
      <label className={labelBase}>{label}</label>
      <div ref={ref} className="relative space-y-1.5">
        {selected.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {selected.map((s) => (
              <span
                key={s}
                className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-[10px] font-bold uppercase text-primary"
              >
                {s}
                <button
                  type="button"
                  aria-label={`Remove ${s}`}
                  className="rounded p-0.5 hover:bg-primary/20"
                  onClick={() => toggle(s)}
                >
                  <IconClose className="h-2.5 w-2.5" />
                </button>
              </span>
            ))}
          </div>
        )}
        <div className="relative">
          <IconSearch className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-on-surface-variant" />
          <input
            type="text"
            className={`${inputBase} pl-8`}
            placeholder={placeholder}
            value={search}
            onFocus={() => setOpen(true)}
            onChange={(e) => { setSearch(e.target.value); setOpen(true); }}
          />
        </div>
        {open && (
          <div className="absolute left-0 right-0 top-full z-30 mt-1 max-h-48 overflow-y-auto rounded-lg border border-outline-variant/20 bg-surface shadow-xl">
            {filtered.length === 0 ? (
              <p className="px-4 py-3 text-xs italic text-on-surface-variant">No matches for "{search}"</p>
            ) : (
              filtered.map((item) => {
                const checked = selected.includes(item);
                return (
                  <label
                    key={item}
                    className={`flex cursor-pointer items-center gap-3 px-4 py-2.5 text-xs transition-colors hover:bg-surface-container-high ${
                      checked ? "bg-primary/5 font-semibold text-primary" : "text-on-surface"
                    }`}
                  >
                    {multi && (
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggle(item)}
                        className="h-3.5 w-3.5 accent-primary shrink-0"
                      />
                    )}
                    {item}
                  </label>
                );
              })
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// ── Variants panel (real SKU / price / stock, wired to Catalog + Inventory) ──
type VariantRow = ProductVariantDto & { qty: number };

/** Parse a major-unit price string (e.g. "199.00") into integer minor units. */
function priceToMinor(value: string): number {
  const n = Number(value.replace(/[^0-9.]/g, ""));
  if (!Number.isFinite(n) || n < 0) return 0;
  return Math.round(n * 100);
}

function minorToPrice(minor: number): string {
  return (Number.isFinite(minor) ? minor / 100 : 0).toFixed(2);
}

function VariantsPanel({
  tenantId,
  storeId,
  productId,
  authToken,
}: {
  tenantId: string;
  storeId: string;
  productId: string;
  authToken?: string;
}) {
  const [rows, setRows] = useState<VariantRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  // Add-variant form
  const [newSku, setNewSku] = useState("");
  const [newPrice, setNewPrice] = useState("");
  const [newQty, setNewQty] = useState("");
  const [adding, setAdding] = useState(false);

  // Per-row edit buffers keyed by variant id
  const [edits, setEdits] = useState<Record<string, { price: string; qty: string; status: string }>>({});

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const variants = await listVariants(tenantId, storeId, productId, authToken);
      const withStock = await Promise.all(
        variants.map(async (v) => {
          let qty = 0;
          try {
            const stock = await getVariantStock(tenantId, storeId, v.id, authToken);
            qty = stock.totalQuantity;
          } catch {
            qty = 0;
          }
          return { ...v, qty } as VariantRow;
        })
      );
      setRows(withStock);
      setEdits(
        Object.fromEntries(
          withStock.map((v) => [v.id, { price: minorToPrice(v.basePriceAmount), qty: String(v.qty), status: v.status }])
        )
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load variants.");
    } finally {
      setLoading(false);
    }
  }, [tenantId, storeId, productId, authToken]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);

  async function handleAdd() {
    const sku = newSku.trim();
    if (!sku) {
      setError("SKU is required to add a variant.");
      return;
    }
    setAdding(true);
    setError(null);
    try {
      const created = await createManualVariant(
        tenantId,
        storeId,
        productId,
        { sku, basePriceAmount: priceToMinor(newPrice), status: "active" },
        authToken
      );
      const qtyValue = Number(newQty);
      if (Number.isFinite(qtyValue) && qtyValue > 0) {
        await setVariantOnHand(tenantId, storeId, created.id, qtyValue, undefined, authToken);
      }
      setNewSku("");
      setNewPrice("");
      setNewQty("");
      setNotice(`Variant ${sku} added.`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to add variant.");
    } finally {
      setAdding(false);
    }
  }

  async function handleSaveRow(v: VariantRow) {
    const edit = edits[v.id];
    if (!edit) return;
    setBusyId(v.id);
    setError(null);
    try {
      const nextPrice = priceToMinor(edit.price);
      if (nextPrice !== v.basePriceAmount || edit.status !== v.status) {
        await updateVariant(
          tenantId,
          storeId,
          productId,
          v.id,
          { basePriceAmount: nextPrice, status: edit.status },
          authToken
        );
      }
      const nextQty = Number(edit.qty);
      if (Number.isFinite(nextQty) && nextQty >= 0 && nextQty !== v.qty) {
        await setVariantOnHand(tenantId, storeId, v.id, nextQty, undefined, authToken);
      }
      setNotice(`Variant ${v.sku} updated.`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to update variant.");
    } finally {
      setBusyId(null);
    }
  }

  function patchEdit(id: string, patch: Partial<{ price: string; qty: string; status: string }>) {
    setEdits((prev) => ({ ...prev, [id]: { ...prev[id], ...patch } }));
  }

  const inputCell =
    "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-1.5 px-2 text-xs focus:ring-1 focus:ring-primary outline-none";

  return (
    <div className="space-y-3">
      {notice ? (
        <p className="rounded-md border border-primary/20 bg-primary/5 px-3 py-2 text-[11px] text-primary">{notice}</p>
      ) : null}
      {error ? (
        <p className="rounded-md border border-error/30 bg-error/10 px-3 py-2 text-[11px] text-on-error-container">{error}</p>
      ) : null}

      <div className="overflow-x-auto rounded-lg border border-outline-variant/20 bg-surface">
        <table className="w-full text-left text-xs">
          <thead className="border-b border-outline-variant/20 bg-surface-container-low text-[10px] uppercase tracking-wider text-on-surface-variant">
            <tr>
              <th className="px-3 py-2 font-bold">SKU</th>
              <th className="px-3 py-2 font-bold">Base Price</th>
              <th className="px-3 py-2 font-bold">On-hand Qty</th>
              <th className="px-3 py-2 font-bold">Status</th>
              <th className="px-3 py-2 font-bold text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} className="px-3 py-6 text-center text-on-surface-variant">
                  Loading variants…
                </td>
              </tr>
            ) : rows.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-3 py-6 text-center text-on-surface-variant">
                  No variants yet. Add a SKU below to set its price and stock.
                </td>
              </tr>
            ) : (
              rows.map((v) => {
                const edit = edits[v.id] ?? { price: minorToPrice(v.basePriceAmount), qty: String(v.qty), status: v.status };
                const dirty =
                  priceToMinor(edit.price) !== v.basePriceAmount ||
                  edit.status !== v.status ||
                  Number(edit.qty) !== v.qty;
                return (
                  <tr key={v.id} className="border-b border-outline-variant/10 last:border-0">
                    <td className="px-3 py-2 font-mono text-on-surface">{v.sku}</td>
                    <td className="px-3 py-2">
                      <input
                        className={`${inputCell} font-mono`}
                        type="number"
                        min={0}
                        step="0.01"
                        value={edit.price}
                        onChange={(e) => patchEdit(v.id, { price: e.target.value })}
                      />
                    </td>
                    <td className="px-3 py-2">
                      <input
                        className={inputCell}
                        type="number"
                        min={0}
                        value={edit.qty}
                        onChange={(e) => patchEdit(v.id, { qty: e.target.value })}
                      />
                    </td>
                    <td className="px-3 py-2">
                      <select
                        className={`${inputCell} appearance-none`}
                        value={edit.status}
                        onChange={(e) => patchEdit(v.id, { status: e.target.value })}
                      >
                        <option value="active">Active</option>
                        <option value="inactive">Inactive</option>
                      </select>
                    </td>
                    <td className="px-3 py-2 text-right">
                      <button
                        type="button"
                        className="rounded-md bg-primary px-3 py-1.5 text-[11px] font-bold text-on-primary disabled:opacity-40"
                        disabled={!dirty || busyId === v.id}
                        onClick={() => void handleSaveRow(v)}
                      >
                        {busyId === v.id ? "Saving…" : "Save"}
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Add a new SKU */}
      <div className="grid grid-cols-1 gap-2 rounded-lg border border-dashed border-primary/30 bg-primary/5 p-3 sm:grid-cols-[1.5fr_1fr_1fr_auto] sm:items-end">
        <div className="space-y-1">
          <label className={labelBase}>New SKU</label>
          <input
            className={inputCell + " font-mono"}
            type="text"
            value={newSku}
            onChange={(e) => setNewSku(e.target.value)}
            placeholder="e.g. WT-550-B"
          />
        </div>
        <div className="space-y-1">
          <label className={labelBase}>Base Price</label>
          <input
            className={inputCell + " font-mono"}
            type="number"
            min={0}
            step="0.01"
            value={newPrice}
            onChange={(e) => setNewPrice(e.target.value)}
            placeholder="0.00"
          />
        </div>
        <div className="space-y-1">
          <label className={labelBase}>Initial Qty</label>
          <input
            className={inputCell}
            type="number"
            min={0}
            value={newQty}
            onChange={(e) => setNewQty(e.target.value)}
            placeholder="0"
          />
        </div>
        <button
          type="button"
          className="flex h-[34px] items-center justify-center gap-1.5 rounded-md bg-primary px-4 text-[11px] font-bold uppercase tracking-widest text-on-primary disabled:opacity-40"
          disabled={adding || !newSku.trim()}
          onClick={() => void handleAdd()}
        >
          <IconAdd className="h-4 w-4 shrink-0" />
          {adding ? "Adding…" : "Add"}
        </button>
      </div>
    </div>
  );
}

/**
 * Manual "related products" picker (Recommendations tab). Lists products in the store as
 * candidates, pre-checks the ones already recommended, and persists the selection (replace-all).
 */
function RecommendationsPanel({
  tenantId,
  storeId,
  productId,
  authToken,
}: {
  tenantId: string;
  storeId: string;
  productId: string;
  authToken?: string;
}) {
  const [candidates, setCandidates] = useState<{ id: string; name: string; sku: string }[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [{ rows }, recos] = await Promise.all([
        fetchProducts(tenantId, storeId, undefined, { page: 1, pageSize: 100 }, authToken),
        listRecommendations(tenantId, storeId, productId, authToken),
      ]);
      const recoIds = recos.map((r) => r.id);
      const list = rows
        .filter((r) => r.id !== productId)
        .map((r) => ({ id: r.id, name: r.name, sku: r.sku ?? "" }));
      // Surface already-recommended products even if outside the first page.
      for (const r of recos) {
        if (!list.some((c) => c.id === r.id)) {
          let nm = r.id;
          try {
            const parsed = JSON.parse(r.nameJson) as Record<string, string>;
            nm = parsed.vi ?? parsed.en ?? Object.values(parsed)[0] ?? r.id;
          } catch {
            // keep id as fallback
          }
          list.unshift({ id: r.id, name: nm, sku: "" });
        }
      }
      setCandidates(list);
      setSelected(recoIds);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load recommendations.");
    } finally {
      setLoading(false);
    }
  }, [tenantId, storeId, productId, authToken]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);

  function toggle(id: string, checked: boolean) {
    setSelected((prev) => (checked ? [...prev, id] : prev.filter((x) => x !== id)));
  }

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      const saved = await setRecommendations(tenantId, storeId, productId, selected, authToken);
      setSelected(saved);
      setNotice(`Saved ${saved.length} recommendation${saved.length === 1 ? "" : "s"}.`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save recommendations.");
    } finally {
      setSaving(false);
    }
  }

  const q = search.trim().toLowerCase();
  const visible = q
    ? candidates.filter((c) => c.name.toLowerCase().includes(q) || c.sku.toLowerCase().includes(q))
    : candidates;

  return (
    <div className="mt-4 space-y-3">
      {notice ? (
        <p className="rounded-md border border-primary/20 bg-primary/5 px-3 py-2 text-[11px] text-primary">{notice}</p>
      ) : null}
      {error ? (
        <p className="rounded-md border border-error/30 bg-error/10 px-3 py-2 text-[11px] text-on-error-container">{error}</p>
      ) : null}

      <input
        className={inputBase}
        type="text"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search products by name or SKU…"
      />

      <div className="space-y-2">
        {loading ? (
          <p className="px-3 py-6 text-center text-xs text-on-surface-variant">Loading products…</p>
        ) : visible.length === 0 ? (
          <p className="px-3 py-6 text-center text-xs text-on-surface-variant">No products found.</p>
        ) : (
          visible.map((p) => {
            const checked = selected.includes(p.id);
            return (
              <label
                key={p.id}
                className={`flex cursor-pointer items-center gap-4 rounded-lg border p-3 transition-colors ${
                  checked ? "border-primary/30 bg-primary/5" : "border-outline-variant/20 hover:bg-surface-container-low"
                }`}
              >
                <input
                  type="checkbox"
                  className="h-4 w-4 accent-primary shrink-0"
                  checked={checked}
                  onChange={(e) => toggle(p.id, e.target.checked)}
                />
                {p.sku ? <span className="w-24 shrink-0 font-mono text-xs text-on-surface-variant">{p.sku}</span> : null}
                <span className="flex-1 text-sm font-medium text-on-surface">{p.name}</span>
                {checked && (
                  <span className="ml-auto shrink-0 rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest text-primary">
                    Recommended
                  </span>
                )}
              </label>
            );
          })
        )}
      </div>

      <div className="flex items-center justify-between border-t border-outline-variant/20 pt-4">
        <p className="text-xs text-on-surface-variant">
          <span className="font-bold text-on-surface">{selected.length}</span> selected
        </p>
        <button
          type="button"
          className="flex items-center gap-1.5 rounded-md bg-primary px-4 py-2 text-[11px] font-bold uppercase tracking-widest text-on-primary disabled:opacity-40"
          disabled={saving}
          onClick={() => void handleSave()}
        >
          <IconSave className="h-4 w-4 shrink-0" />
          {saving ? "Saving…" : "Save Recommendations"}
        </button>
      </div>
    </div>
  );
}

// ── Component ───────────────────────────────────────────────────────────────
export function EditProductPage({ mode, product, onBack, tenantId, storeId, authToken }: Props) {
  const isAdd = mode === "add";
  const apiReady = Boolean(tenantId && storeId);

  // ── UI state ──────────────────────────────────────────────────────────────
  const [tab, setTab] = useState<EditTab>("general");
  const [actionsOpen, setActionsOpen] = useState(false);
  const [showSuccessModal, setShowSuccessModal] = useState(false);
  const [successMessage, setSuccessMessage] = useState("");
  const actionsRef = useRef<HTMLDivElement>(null);

  // ── Persisted-product fields (wired to the Catalog API) ─────────────────────
  const [name, setName] = useState(isAdd ? "" : product?.name ?? "");
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [brandCode, setBrandCode] = useState<string>("");
  const [descriptionByLocale, setDescriptionByLocale] = useState<Record<string, string>>({});
  const [descriptionInitial, setDescriptionInitial] = useState<Record<string, string>>({});
  const [slug, setSlug] = useState("");
  const [slugTouched, setSlugTouched] = useState(false);
  const [categoryChoices, setCategoryChoices] = useState<{ id: number; label: string }[]>([]);
  const [brandChoices, setBrandChoices] = useState<{ code: string; name: string }[]>([]);
  const [loadedStatus, setLoadedStatus] = useState<string>("");
  const [hiddenProduct, setHiddenProduct] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const [productFieldConfig, setProductFieldConfig] = useState<ProductFieldRecord[]>([]);
  const [customFieldValues, setCustomFieldValues] = useState<ProductCustomFieldsMap>({});

  // Load category + brand choices for the pickers.
  useEffect(() => {
    if (!tenantId) return;
    let cancelled = false;
    fetchCategories(tenantId, authToken)
      .then((tree) => {
        if (!cancelled) setCategoryChoices(flattenCategoryNodes(tree));
      })
      .catch(() => {
        if (!cancelled) setCategoryChoices([]);
      });
    fetchBrands(tenantId, { active: true, pageSize: 200 }, authToken)
      .then(({ rows }) => {
        if (!cancelled) setBrandChoices(rows.map((b) => ({ code: b.code, name: b.name })));
      })
      .catch(() => {
        if (!cancelled) setBrandChoices([]);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, authToken]);

  useEffect(() => {
    if (!tenantId || !storeId) return;
    let cancelled = false;
    fetchProductFieldConfig(tenantId, storeId, authToken)
      .then(({ fields }) => {
        if (cancelled) return;
        setProductFieldConfig(fields);
      })
      .catch(() => {
        if (!cancelled) setProductFieldConfig([]);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId, storeId, authToken]);

  // In edit mode, hydrate the persisted fields from the API (the list row lacks categoryId/slug).
  useEffect(() => {
    if (isAdd || !apiReady || !product?.id) return;
    let cancelled = false;
    getProduct(tenantId!, storeId!, product.id, authToken)
      .then((d) => {
        if (cancelled) return;
        let display = product?.name ?? "";
        try {
          const parsed = JSON.parse(d.nameJson) as Record<string, string>;
          display = parsed.vi || parsed.en || Object.values(parsed)[0] || display;
        } catch {
          // keep fallback display name
        }
        setName(display);
        setCategoryId(d.categoryId);
        setBrandCode(d.brandId ?? "");
        setSlug(d.slug);
        setLoadedStatus(d.status);
        setHiddenProduct(d.status === "hidden");
        const parsedDesc = parseLocaleMap(d.descriptionJson);
        setDescriptionByLocale(parsedDesc);
        setDescriptionInitial(parsedDesc);

        // Product Page / SEO metadata + flags
        const pageTitle = parseLocaleMap(d.pageTitleJson);
        setPageTitleByLocale(pageTitle);
        setPageTitleInitial(pageTitle);
        const metaKeywords = parseLocaleMap(d.metaKeywordsJson);
        setMetaKeywordsByLocale(metaKeywords);
        setMetaKeywordsInitial(metaKeywords);
        const metaDescription = parseLocaleMap(d.metaDescriptionJson);
        setMetaDescriptionByLocale(metaDescription);
        setMetaDescriptionInitial(metaDescription);
        setPublishFrom(isoToLocalInput(d.publishFrom));
        setPublishUntil(isoToLocalInput(d.publishUntil));
        setRecommendSimilar(d.recommendSimilar ?? true);
        setRecommendationsMode(d.recommendationsMode ?? "auto");
        setRestockNotification(d.restockNotification ?? false);
        const storedCustom = parseProductCustomFields(d.customFieldsJson);
        setCustomFieldValues((prev) => initCustomFieldValues(productFieldConfig, { ...prev, ...storedCustom }));
      })
      .catch(() => {
        // leave defaults; save will surface validation errors if needed
      });
    return () => {
      cancelled = true;
    };
  }, [isAdd, apiReady, product?.id, tenantId, storeId, authToken, productFieldConfig]);

  useEffect(() => {
    if (productFieldConfig.length === 0) return;
    setCustomFieldValues((prev) => initCustomFieldValues(productFieldConfig, prev));
  }, [productFieldConfig]);

  const effectiveSlug = slugTouched ? slug : slug || slugify(name);

  function setCustomFieldValue(id: string, val: string | string[]) {
    setCustomFieldValues((prev) => ({ ...prev, [id]: val }));
  }

  // ── Image state ───────────────────────────────────────────────────────────
  const MOCK_IMAGES: ProductImage[] = isAdd
    ? []
    : [
        { id: "img-1", name: "product-front.jpg",    alt: "Front view of the product", color: "#c7d2fe" },
        { id: "img-2", name: "product-side.jpg",     alt: "Side view of the product",  color: "#bbf7d0" },
        { id: "img-3", name: "product-detail.png",   alt: "Close-up detail shot",      color: "#fde68a" },
      ];
  const [images, setImages] = useState<ProductImage[]>(MOCK_IMAGES);
  const [altTextModal, setAltTextModal] = useState<{ open: boolean; imageId: string; value: string }>(
    { open: false, imageId: "", value: "" }
  );
  const [deleteConfirm, setDeleteConfirm] = useState<{ open: boolean; imageId: string }>(
    { open: false, imageId: "" }
  );
  const [editorImageId, setEditorImageId] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const imagesRef = useRef<ProductImage[]>([]);
  const [configureMainOpen, setConfigureMainOpen] = useState(false);
  const [variantNotice, setVariantNotice] = useState("");
  const [galleryNotice, setGalleryNotice] = useState("");

  const MAX_MEDIA_BYTES = 40 * 1024 * 1024;

  const ingestFiles = useCallback((files: FileList | File[]) => {
    for (const file of Array.from(files)) {
      if (!file.type.startsWith("image/") && !file.type.startsWith("video/")) continue;
      if (file.size > MAX_MEDIA_BYTES) continue;
      const id = `img-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
      if (file.type.startsWith("image/")) {
        const reader = new FileReader();
        reader.onload = () => {
          const src = reader.result;
          if (typeof src !== "string") return;
          setImages((prev) => [
            ...prev,
            { id, name: file.name, alt: "", color: "#e5e7eb", src, isVideo: false },
          ]);
        };
        reader.readAsDataURL(file);
      } else {
        const src = URL.createObjectURL(file);
        setImages((prev) => [
          ...prev,
          { id, name: file.name, alt: "", color: "#0f172a", src, isVideo: true },
        ]);
      }
    }
  }, []);

  imagesRef.current = images;
  useEffect(
    () => () => {
      imagesRef.current.forEach((img) => {
        if (img.src?.startsWith("blob:")) URL.revokeObjectURL(img.src);
      });
    },
    []
  );

  useEffect(() => {
    if (!variantNotice) return;
    const t = setTimeout(() => setVariantNotice(""), 4000);
    return () => clearTimeout(t);
  }, [variantNotice]);

  useEffect(() => {
    if (!galleryNotice) return;
    const t = setTimeout(() => setGalleryNotice(""), 4000);
    return () => clearTimeout(t);
  }, [galleryNotice]);

  // ── General tab state ─────────────────────────────────────────────────────
  const [itemType, setItemType] = useState<ItemType>("physical");
  const [selectedStores, setSelectedStores] = useState<string[]>(
    isAdd ? [] : ["Main Store", "Online Store"]
  );
  const [selectedAttributes, setSelectedAttributes] = useState<string[]>(
    isAdd ? [] : ["Color", "Size"]
  );
  const [productOptions, setProductOptions] = useState<ProductOption[]>([]);

  // Fulfilment flags
  const [enableCollection, setEnableCollection] = useState(!isAdd);
  const [enableDelivery, setEnableDelivery] = useState(!isAdd);
  const [enableOverseasDelivery, setEnableOverseasDelivery] = useState(false);
  const [shippingClause, setShippingClause] = useState(false);
  const [inventorySync, setInventorySync] = useState(false);
  const [priceSync, setPriceSync] = useState(false);
  const [allowOversell, setAllowOversell] = useState(false);

  // ── Product Page tab state ────────────────────────────────────────────────
  const [recommendSimilar, setRecommendSimilar] = useState(!isAdd);
  const [restockNotification, setRestockNotification] = useState(false);
  const [recommendationsMode, setRecommendationsMode] = useState("auto");
  const [useTitleForUrl, setUseTitleForUrl] = useState(false);
  const [useTitleForPageTitle, setUseTitleForPageTitle] = useState(false);
  const [publishFrom, setPublishFrom] = useState("");
  const [publishUntil, setPublishUntil] = useState("");
  const [pageTitleByLocale, setPageTitleByLocale] = useState<Record<string, string>>({});
  const [pageTitleInitial, setPageTitleInitial] = useState<Record<string, string>>({});
  const [metaKeywordsByLocale, setMetaKeywordsByLocale] = useState<Record<string, string>>({});
  const [metaKeywordsInitial, setMetaKeywordsInitial] = useState<Record<string, string>>({});
  const [metaDescriptionByLocale, setMetaDescriptionByLocale] = useState<Record<string, string>>({});
  const [metaDescriptionInitial, setMetaDescriptionInitial] = useState<Record<string, string>>({});

  // ── Close actions dropdown on outside click ───────────────────────────────
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node))
        setActionsOpen(false);
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  // ── Handlers ──────────────────────────────────────────────────────────────
  async function handleSave(message: string) {
    setActionsOpen(false);
    setSaveError(null);

    // When not embedded with API context (e.g. standalone demo), keep the original confirmation behaviour.
    if (!apiReady) {
      setSuccessMessage(message);
      setShowSuccessModal(true);
      return;
    }

    if (saving) return;

    const trimmedName = name.trim();
    if (!trimmedName) {
      setSaveError("Product Name is required.");
      setTab("general");
      return;
    }
    if (categoryId == null) {
      setSaveError("Please select a Category before saving.");
      setTab("general");
      return;
    }

    const missingField = findMissingRequiredCustomField(productFieldConfig, customFieldValues);
    if (missingField) {
      setSaveError(`${missingField.fieldName || missingField.columnLabel} is required.`);
      setTab(tabForTargetPage(missingField.targetPage));
      return;
    }

    // Preserve any locales the user didn't touch; ensure the edited name locales are present.
    const descriptionToSave =
      Object.keys(descriptionByLocale).length > 0 ? descriptionByLocale : descriptionInitial;

    const payload = productToPayload({
      name: trimmedName,
      categoryId,
      slug: effectiveSlug || slugify(trimmedName),
      nameByLocale: { vi: trimmedName, en: trimmedName },
      descriptionByLocale: descriptionToSave,
      brandId: brandCode || null,
      metadata: {
        pageTitleByLocale:
          Object.keys(pageTitleByLocale).length > 0 ? pageTitleByLocale : pageTitleInitial,
        metaKeywordsByLocale:
          Object.keys(metaKeywordsByLocale).length > 0 ? metaKeywordsByLocale : metaKeywordsInitial,
        metaDescriptionByLocale:
          Object.keys(metaDescriptionByLocale).length > 0 ? metaDescriptionByLocale : metaDescriptionInitial,
        publishFrom: localInputToIso(publishFrom),
        publishUntil: localInputToIso(publishUntil),
        recommendSimilar,
        recommendationsMode,
        restockNotification,
      },
      customFields: buildCustomFieldsPayload(customFieldValues),
    });

    setSaving(true);
    try {
      if (isAdd) {
        await createProduct(tenantId!, storeId!, payload, authToken);
      } else if (product?.id) {
        await updateProduct(tenantId!, storeId!, product.id, payload, authToken);
        await reconcileVisibility(product.id);
      } else {
        throw new Error("Missing product id for update.");
      }
      setSuccessMessage(message);
      setShowSuccessModal(true);
    } catch (e) {
      setSaveError(e instanceof Error ? e.message : "Failed to save product.");
    } finally {
      setSaving(false);
    }
  }

  /**
   * Reconcile the "Hidden Product" toggle with the catalog visibility state.
   * Only active↔hidden transitions are valid; drafts/pending/archived are left untouched.
   */
  async function reconcileVisibility(productId: string) {
    if (!apiReady) return;
    if (hiddenProduct && loadedStatus === "active") {
      await hideProduct(tenantId!, storeId!, productId, authToken);
      setLoadedStatus("hidden");
    } else if (!hiddenProduct && loadedStatus === "hidden") {
      await unhideProduct(tenantId!, storeId!, productId, authToken);
      setLoadedStatus("active");
    }
  }

  function addProductOption() {
    setProductOptions((prev) => [
      ...prev,
      { id: crypto.randomUUID(), type: "textbox", label: "", required: false, dropdownOptions: "" },
    ]);
  }

  function removeProductOption(id: string) {
    setProductOptions((prev) => prev.filter((o) => o.id !== id));
  }

  function updateProductOption(id: string, patch: Partial<ProductOption>) {
    setProductOptions((prev) =>
      prev.map((o) => (o.id === id ? { ...o, ...patch } : o))
    );
  }

  // ── Tab nav config ────────────────────────────────────────────────────────
  const tabs: { id: EditTab; label: string; Icon: typeof IconInfo }[] = [
    { id: "general",         label: "General",          Icon: IconInfo        },
    { id: "product-page",   label: "Product Page",     Icon: IconOpenInNew   },
    { id: "recommendations", label: "Recommendations",  Icon: IconLayers      },
  ];

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 flex-col gap-4 border-b border-outline-variant/15 bg-surface px-6 py-4 md:flex-row md:items-center md:justify-between">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Products
          </button>
          <h2 className="flex flex-wrap items-center gap-3 text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? (
              <span>Add Product</span>
            ) : (
              <>
                <span>Edit Product:</span>
                <span className="text-primary-container">{product?.name}</span>
                <span className="rounded-full bg-outline-variant/20 px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant">
                  {product?.statusLabel}
                </span>
              </>
            )}
          </h2>
        </div>

        <div className="flex items-center gap-3">
          {/* Actions dropdown */}
          <div className="relative" ref={actionsRef}>
            <button
              type="button"
              className="flex items-center gap-2 rounded-md bg-primary px-6 py-2 text-xs font-bold uppercase tracking-widest text-on-primary shadow-lg shadow-primary/20 transition-colors hover:bg-primary-container"
              onClick={() => setActionsOpen((o) => !o)}
            >
              Actions
              <IconChevronDown className="h-4 w-4 shrink-0" />
            </button>
            {actionsOpen && (
              <div className="absolute right-0 top-full z-20 mt-1 w-56 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
                {[
                  { label: "Save and Approve",           icon: IconCheckCircle, nav: false },
                  { label: "Save and Send for Approval", icon: IconSave,        nav: false },
                  { label: "Save and Archive",           icon: IconFolder,      nav: false },
                  { label: "Save Changes",               icon: IconSave,        nav: false },
                ].map(({ label, icon: Icon }) => (
                  <button
                    key={label}
                    type="button"
                    className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface transition-colors hover:bg-surface-container"
                    onClick={() => handleSave(label)}
                  >
                    <Icon className="h-4 w-4 shrink-0 text-primary" />
                    {label}
                  </button>
                ))}
                <div className="mx-3 my-1 border-t border-outline-variant/15" />
                {[
                  { label: "Preview Product",  icon: IconVisibility },
                  { label: "View Product Page", icon: IconOpenInNew  },
                ].map(({ label, icon: Icon }) => (
                  <button
                    key={label}
                    type="button"
                    className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface transition-colors hover:bg-surface-container"
                    onClick={() => {
                      setActionsOpen(false);
                      console.info(`[EditProduct] ${label} (placeholder navigation)`);
                    }}
                  >
                    <Icon className="h-4 w-4 shrink-0 text-primary" />
                    {label}
                  </button>
                ))}
                <div className="mx-3 my-1 border-t border-outline-variant/15" />
                {[
                  { label: "Copy to New Product", icon: IconFactCheck },
                  { label: "Copy to New Variant", icon: IconLayers    },
                ].map(({ label, icon: Icon }) => (
                  <button
                    key={label}
                    type="button"
                    className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface transition-colors hover:bg-surface-container"
                    onClick={() => handleSave(label)}
                  >
                    <Icon className="h-4 w-4 shrink-0 text-primary" />
                    {label}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Save error banner ─────────────────────────────────────────────── */}
      {saveError && (
        <div className="mx-6 mt-4 flex items-start gap-2 rounded-lg border border-error/30 bg-error/10 px-4 py-3 text-xs font-medium text-on-error-container">
          <span className="font-bold uppercase tracking-wider">Save failed:</span>
          <span>{saveError}</span>
        </div>
      )}

      {/* ── Body: sidebar + form ─────────────────────────────────────────── */}
      <div className="flex min-h-0 flex-1 flex-col gap-8 p-6 lg:flex-row">

        {/* Sidebar tab nav */}
        <aside className="flex w-full shrink-0 flex-row gap-1 overflow-x-auto lg:w-64 lg:flex-col lg:overflow-visible">
          {tabs.map(({ id, label, Icon }) => {
            const isActive = tab === id;
            return (
              <button
                key={id}
                type="button"
                onClick={() => setTab(id)}
                className={`flex items-center gap-3 whitespace-nowrap border-l-4 px-4 py-3 text-left text-sm font-medium transition-colors rounded-none lg:rounded-sm ${
                  isActive
                    ? "border-primary bg-surface-container-lowest font-bold text-primary shadow-sm"
                    : "border-transparent text-on-surface-variant hover:bg-surface-container-high"
                }`}
              >
                <Icon className="h-5 w-5 shrink-0" />
                {label}
              </button>
            );
          })}
        </aside>

        {/* Tab content */}
        <section
          className="min-h-0 flex-1 space-y-8 overflow-y-auto rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-8 shadow-[0_4px_24px_rgba(40,23,22,0.04)]"
          aria-label="Product form"
        >

          {/* ── GENERAL TAB ─────────────────────────────────────────── */}
          {tab === "general" && (
            <>
              {/* Identifiers */}
              <div>
                <h3 className={sectionTitle}>Identifiers</h3>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-4">
                  <div className="space-y-1.5">
                    <label className={labelBase}>UPC <span className="text-[9px] font-normal normal-case text-primary/70">system-generated</span></label>
                    <input
                      className={`${inputBase} font-mono text-on-surface-variant`}
                      readOnly
                      value={isAdd ? "Auto-generated on save" : product?.upc}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>SKU <span className="text-error">*</span></label>
                    <input className={`${inputBase} font-mono`} type="text" defaultValue={isAdd ? "" : product?.sku} placeholder="e.g. WT-550-B" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>PID1</label>
                    <input className={inputBase} type="text" placeholder="First identifier" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>PID2</label>
                    <input className={inputBase} type="text" placeholder="Additional identifier" />
                  </div>
                </div>
              </div>

              {/* Product Info */}
              <div>
                <h3 className={sectionTitle}>Product Information</h3>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Product Name <span className="text-error">*</span></label>
                    <input
                      className={inputBase}
                      type="text"
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      placeholder="Name for printed documents (orders, delivery notes)"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Category <span className="text-error">*</span></label>
                    <select
                      className={`${inputBase} appearance-none`}
                      value={categoryId ?? ""}
                      onChange={(e) => setCategoryId(e.target.value === "" ? null : Number(e.target.value))}
                    >
                      <option value="">— Select a category —</option>
                      {categoryChoices.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.label}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Brand</label>
                    <select
                      className={`${inputBase} appearance-none`}
                      value={brandCode}
                      onChange={(e) => setBrandCode(e.target.value)}
                    >
                      <option value="">— No brand —</option>
                      {brandChoices.map((b) => (
                        <option key={b.code} value={b.code}>
                          {b.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>URL Slug</label>
                    <input
                      className={`${inputBase} font-mono`}
                      type="text"
                      value={effectiveSlug}
                      onChange={(e) => {
                        setSlugTouched(true);
                        setSlug(e.target.value);
                      }}
                      placeholder="auto-generated-from-name"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Product Stores</label>
                    <div className="flex flex-wrap gap-3 rounded-md border border-outline-variant/20 bg-surface-container-lowest p-3">
                      {MOCK_STORES.map((store) => (
                        <label key={store} className="flex cursor-pointer items-center gap-2 text-xs text-on-surface-variant select-none">
                          <input
                            type="checkbox"
                            className="h-3.5 w-3.5 accent-primary"
                            checked={selectedStores.includes(store)}
                            onChange={(e) =>
                              setSelectedStores((prev) =>
                                e.target.checked ? [...prev, store] : prev.filter((s) => s !== store)
                              )
                            }
                          />
                          {store}
                        </label>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {/* Inventory & Pricing */}
              <div>
                <h3 className={sectionTitle}>Inventory & Pricing</h3>
                <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Total Quantity</label>
                    <div className="flex gap-2">
                      <input className={inputBase} type="number" defaultValue={isAdd ? 0 : product?.qty} min={0} />
                      <button type="button" className="shrink-0 whitespace-nowrap rounded-md border border-outline-variant/30 px-3 py-2 text-xs font-semibold text-on-surface-variant hover:bg-surface-container-high transition-colors">
                        Update Qty
                      </button>
                    </div>
                    <p className="text-[10px] text-on-surface-variant">
                      Quantity can be retrieved per outlet on the product page.
                    </p>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Total eStore Quantity</label>
                    <input className={inputBase} type="number" defaultValue={isAdd ? 0 : product?.qty} min={0} />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>eStore Cut-Off Quantity</label>
                    <input className={inputBase} type="number" defaultValue={0} min={0} />
                    <p className="text-[10px] text-on-surface-variant">Product tagged out of stock when this threshold is reached.</p>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Retail Price</label>
                    <div className="relative">
                      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                      <input className={`${inputBase} pl-6`} type="text" defaultValue={isAdd ? "" : product?.price?.replace("$", "")} placeholder="0.00" />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Current Price (Incl Tax)</label>
                    <div className="relative">
                      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                      <input className={`${inputBase} pl-6`} type="text" defaultValue={isAdd ? "" : product?.price?.replace("$", "")} placeholder="0.00" />
                    </div>
                  </div>
                </div>
              </div>

              {/* Item Type */}
              <div>
                <h3 className={sectionTitle}>Item Type</h3>
                <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Item Type</label>
                    <div className="relative">
                      <select
                        className={`${inputBase} appearance-none pr-10`}
                        value={itemType}
                        onChange={(e) => setItemType(e.target.value as ItemType)}
                      >
                        <option value="physical">Physical Product</option>
                        <option value="membership-points">Membership Points</option>
                        <option value="membership-voucher">Membership Voucher</option>
                        <option value="membership">Membership</option>
                        <option value="stored-value-card">Stored Value Card</option>
                      </select>
                      <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                    </div>
                  </div>

                  {/* Conditional sub-fields */}
                  {itemType === "membership-points" && (
                    <div className="space-y-1.5">
                      <label className={labelBase}>Points Awarded</label>
                      <input className={inputBase} type="number" min={0} placeholder="e.g. 100" />
                    </div>
                  )}
                  {itemType === "membership-voucher" && (
                    <div className="space-y-1.5">
                      <label className={labelBase}>Voucher Code</label>
                      <input className={`${inputBase} font-mono`} type="text" placeholder="e.g. VOUCHER-2024" />
                    </div>
                  )}
                  {(itemType === "membership" || itemType === "stored-value-card") && (
                    <>
                      <div className="space-y-1.5">
                        <label className={labelBase}>Card Tier</label>
                        <div className="relative">
                          <select className={`${inputBase} appearance-none pr-10`}>
                            {MOCK_CARD_TIERS.map((t) => <option key={t}>{t}</option>)}
                          </select>
                          <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                        </div>
                      </div>
                      <div className="space-y-1.5">
                        <label className={labelBase}>Card Type</label>
                        <div className="relative">
                          <select className={`${inputBase} appearance-none pr-10`}>
                            {MOCK_CARD_TYPES.map((t) => <option key={t}>{t}</option>)}
                          </select>
                          <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                        </div>
                      </div>
                      {itemType === "stored-value-card" && (
                        <div className="space-y-1.5">
                          <label className={labelBase}>Stored Value Amount</label>
                          <div className="relative">
                            <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                            <input className={`${inputBase} pl-6`} type="text" placeholder="0.00" />
                          </div>
                        </div>
                      )}
                    </>
                  )}
                </div>

                {(itemType === "membership-points" || itemType === "membership-voucher" || itemType === "membership" || itemType === "stored-value-card") && (
                  <div className="mt-4 flex items-start gap-3 rounded-md border border-amber-200/60 bg-amber-50/60 px-4 py-3">
                    <IconInfo className="h-4 w-4 shrink-0 text-amber-600 mt-0.5" />
                    <p className="text-[11px] text-amber-800">This item type must be connected to the CRM. Ensure CRM integration is configured before publishing.</p>
                  </div>
                )}
              </div>

              {/* Product Options — legacy mock UI; hidden when store Product Configuration is active */}
              {apiReady && productFieldConfig.some((f) => f.enabled) ? (
                <div className="rounded-lg border border-outline-variant/20 bg-surface-container-low px-4 py-3">
                  <p className="text-xs text-on-surface-variant">
                    Custom product fields are managed in{" "}
                    <span className="font-semibold text-on-surface">Product Configuration</span>. Use that page to
                    define field types, labels, and options for this store.
                  </p>
                </div>
              ) : (
              <div>
                <h3 className={sectionTitle}>Product Options</h3>
                <div className="space-y-3">
                  {productOptions.map((opt) => (
                    <div key={opt.id} className="rounded-lg border border-outline-variant/20 bg-surface p-4 space-y-3">
                      <div className="flex items-start gap-3">
                        <div className="flex-1 grid grid-cols-1 gap-3 sm:grid-cols-3">
                          <div className="space-y-1">
                            <label className={labelBase}>Type</label>
                            <div className="relative">
                              <select
                                className={`${inputBase} appearance-none pr-8`}
                                value={opt.type}
                                onChange={(e) => updateProductOption(opt.id, { type: e.target.value as ProductOption["type"] })}
                              >
                                <option value="textbox">Textbox</option>
                                <option value="dropdown">Dropdown List</option>
                                <option value="bundle">Product Bundle</option>
                              </select>
                              <IconChevronDown className="pointer-events-none absolute right-2 top-2.5 h-4 w-4 text-on-surface-variant" />
                            </div>
                          </div>
                          <div className="space-y-1">
                            <label className={labelBase}>Label</label>
                            <input
                              className={inputBase}
                              type="text"
                              value={opt.label}
                              onChange={(e) => updateProductOption(opt.id, { label: e.target.value })}
                              placeholder="Option label"
                            />
                          </div>
                          <div className="flex items-end pb-1">
                            <label className="flex cursor-pointer items-center gap-2 text-xs text-on-surface-variant select-none">
                              <input
                                type="checkbox"
                                className="h-3.5 w-3.5 accent-primary"
                                checked={opt.required}
                                onChange={(e) => updateProductOption(opt.id, { required: e.target.checked })}
                              />
                              Required field
                            </label>
                          </div>
                        </div>
                        <button
                          type="button"
                          className="shrink-0 rounded p-1 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                          onClick={() => removeProductOption(opt.id)}
                          aria-label="Remove option"
                        >
                          <IconDelete className="h-4 w-4" />
                        </button>
                      </div>
                      {opt.type === "dropdown" && (
                        <div className="space-y-1">
                          <label className={labelBase}>Options (one per line)</label>
                          <textarea
                            className={`${inputBase} resize-y min-h-[80px]`}
                            value={opt.dropdownOptions}
                            onChange={(e) => updateProductOption(opt.id, { dropdownOptions: e.target.value })}
                            placeholder={"Option 1\nOption 2\nOption 3"}
                          />
                        </div>
                      )}
                      {opt.type === "bundle" && (
                        <p className="text-[10px] text-on-surface-variant italic">
                          Bundle items have no inventory deduction. Set price to "0" for free items.
                        </p>
                      )}
                    </div>
                  ))}
                  <button
                    type="button"
                    className="flex items-center gap-2 rounded-md border border-dashed border-primary/30 px-4 py-2.5 text-xs font-semibold text-primary transition-colors hover:bg-primary/5 w-full justify-center"
                    onClick={addProductOption}
                  >
                    <IconAdd className="h-4 w-4 shrink-0" />
                    Add Option
                  </button>
                </div>
              </div>
              )}

              {/* Product Attributes */}
              <div>
                <h3 className={sectionTitle}>Product Attributes</h3>
                <SearchablePicker
                  label="Attributes"
                  options={MOCK_ATTRIBUTES}
                  selected={selectedAttributes}
                  onChange={setSelectedAttributes}
                  placeholder="Specify attribute name to find a match..."
                />
              </div>

              {/* Fulfilment flags */}
              <div>
                <h3 className={sectionTitle}>Fulfilment & Sync</h3>
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                  {[
                    { label: "Enable Collection",         hint: "Customers can collect from retail store",          value: enableCollection,      set: setEnableCollection      },
                    { label: "Enable Delivery",            hint: "Customers receive product at local address",       value: enableDelivery,        set: setEnableDelivery        },
                    { label: "Enable Overseas Delivery",   hint: "International shipping option at checkout",        value: enableOverseasDelivery, set: setEnableOverseasDelivery},
                    { label: "Shipping & Returns Clause",  hint: "Display shipping/returns clause on product page",  value: shippingClause,        set: setShippingClause        },
                    { label: "Inventory Sync",             hint: "Enable scheduled inventory synchronisation",       value: inventorySync,         set: setInventorySync         },
                    { label: "Price Sync",                 hint: "Enable scheduled price synchronisation",           value: priceSync,             set: setPriceSync             },
                    { label: "Allow Oversell",             hint: "Available for purchase even when out of stock",    value: allowOversell,         set: setAllowOversell         },
                  ].map(({ label, hint, value, set }) => (
                    <label key={label} className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface p-3 select-none hover:bg-surface-container-low transition-colors">
                      <input
                        type="checkbox"
                        className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                        checked={value}
                        onChange={(e) => set(e.target.checked)}
                      />
                      <div>
                        <p className="text-xs font-semibold text-on-surface">{label}</p>
                        <p className="text-[10px] text-on-surface-variant">{hint}</p>
                      </div>
                    </label>
                  ))}
                </div>
              </div>

              {/* Scheduling & Delivery */}
              <div>
                <h3 className={sectionTitle}>Scheduling & Delivery</h3>
                <div className="grid grid-cols-1 gap-6 sm:grid-cols-3">
                  <div className="space-y-1.5">
                    <label className={labelBase}>Delivery Start Date</label>
                    <input className={inputBase} type="date" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Delivery Lead Time (days)</label>
                    <input className={inputBase} type="number" min={0} placeholder="e.g. 3" />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelBase}>Handling Fee</label>
                    <div className="relative">
                      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-on-surface-variant">$</span>
                      <input className={`${inputBase} pl-6`} type="text" placeholder="0.00" />
                    </div>
                  </div>
                </div>
              </div>

              {/* Product Images */}
              <div>
                <h3 className={sectionTitle}>Product Images & Videos</h3>
                {galleryNotice ? (
                  <p className="mb-3 rounded-md border border-outline-variant/30 bg-surface-container-high px-3 py-2 text-[11px] text-on-surface-variant">
                    {galleryNotice}
                  </p>
                ) : null}

                {/* Inline image editor */}
                {editorImageId !== null && (() => {
                  const img = images.find((i) => i.id === editorImageId);
                  return (
                    <div className="overflow-hidden rounded-xl border border-outline-variant/20 bg-surface">
                      {/* Editor toolbar */}
                      <div className="flex flex-wrap items-center gap-1 border-b border-outline-variant/20 bg-surface-container-low px-3 py-2">
                        {[
                          { Icon: IconSave,        label: "Save"                   },
                          { Icon: IconRestartAlt,  label: "Undo"                   },
                          { Icon: IconRestartAlt,  label: "Redo"                   },
                        ].map(({ Icon, label }) => (
                          <button
                            key={label}
                            type="button"
                            aria-label={label}
                            title={label}
                            className="flex items-center gap-1.5 rounded px-2 py-1.5 text-[10px] font-semibold text-on-surface-variant transition-colors hover:bg-surface-container-high"
                            onClick={() => console.info(`[ImageEditor] ${label} (placeholder)`)}
                          >
                            <Icon className="h-4 w-4 shrink-0" />
                            {label}
                          </button>
                        ))}
                        <div className="mx-1 h-5 w-px bg-outline-variant/30" />
                        {[
                          { Icon: IconCrop,       label: "Crop"                },
                          { Icon: IconRotateCcw,  label: "Rotate CCW"          },
                          { Icon: IconRotateCw,   label: "Rotate CW"           },
                          { Icon: IconTune,       label: "Brightness/Contrast" },
                          { Icon: IconOpenWith,   label: "Move & Scale"        },
                          { Icon: IconEditNote,   label: "Eraser"              },
                        ].map(({ Icon, label }) => (
                          <button
                            key={label}
                            type="button"
                            aria-label={label}
                            title={label}
                            className="rounded p-1.5 text-on-surface-variant transition-colors hover:bg-surface-container-high hover:text-primary"
                            onClick={() => console.info(`[ImageEditor] ${label} (placeholder)`)}
                          >
                            <Icon className="h-4 w-4 shrink-0" />
                          </button>
                        ))}
                        <div className="ml-auto">
                          <button
                            type="button"
                            className="rounded-md border border-outline-variant/30 px-3 py-1.5 text-[10px] font-bold uppercase tracking-widest text-on-surface-variant hover:bg-surface-container-high transition-colors"
                            onClick={() => setEditorImageId(null)}
                          >
                            Close Editor
                          </button>
                        </div>
                      </div>
                      {/* Preview (uploaded media or colour placeholder) */}
                      <div className="flex min-h-[300px] items-center justify-center overflow-hidden rounded-b-xl bg-black/5">
                        {img?.src ? (
                          img.isVideo ? (
                            <video
                              src={img.src}
                              className="max-h-[min(70vh,520px)] w-full object-contain"
                              controls
                              muted
                              playsInline
                            />
                          ) : (
                            <img
                              src={img.src}
                              alt={img.alt || ""}
                              className="max-h-[min(70vh,520px)] w-full object-contain"
                            />
                          )
                        ) : (
                          <div
                            className="flex min-h-[300px] w-full flex-col items-center justify-center"
                            style={{ backgroundColor: img?.color ?? "#f3f4f6" }}
                          >
                            <div className="text-center">
                              <IconImage className="mx-auto mb-3 h-16 w-16 text-white/60" />
                              <p className="text-sm font-semibold text-white/80">{img?.name}</p>
                              <p className="mt-1 text-[11px] text-white/60">
                                Use the toolbar above to edit this image
                              </p>
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })()}

                {/* Gallery grid */}
                {editorImageId === null && (
                  <div className="flex flex-wrap gap-4">
                    {/* Image cards */}
                    {images.map((img) => (
                      <div
                        key={img.id}
                        className="group relative w-[160px] shrink-0 overflow-hidden rounded-xl border border-outline-variant/20 bg-surface shadow-sm"
                      >
                        {/* Thumbnail */}
                        <div className="relative flex h-[120px] w-full items-center justify-center overflow-hidden bg-black/5">
                          {img.src ? (
                            img.isVideo ? (
                              <video
                                src={img.src}
                                className="h-full w-full object-cover"
                                muted
                                playsInline
                                loop
                              />
                            ) : (
                              <img src={img.src} alt="" className="h-full w-full object-cover" />
                            )
                          ) : (
                            <>
                              <div className="absolute inset-0" style={{ backgroundColor: img.color }} />
                              <IconImage className="relative z-[1] h-10 w-10 text-white/60" />
                            </>
                          )}
                        </div>

                        {/* Filename */}
                        <div className="px-2.5 py-2">
                          <p className="truncate text-[10px] font-semibold text-on-surface" title={img.name}>{img.name}</p>
                          {img.alt && (
                            <p className="truncate text-[9px] text-on-surface-variant italic" title={img.alt}>{img.alt}</p>
                          )}
                        </div>

                        {/* Action bar — visible on hover */}
                        <div className="absolute bottom-0 left-0 right-0 flex translate-y-full items-center justify-around bg-surface-container-lowest/95 px-1 py-1.5 backdrop-blur-sm transition-transform duration-150 group-hover:translate-y-0">
                          <button
                            type="button"
                            aria-label="View Media"
                            title="View Media"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() => {
                              if (!img.src) {
                                setGalleryNotice("Add a file first — mock placeholders have no URL.");
                                return;
                              }
                              window.open(img.src, "_blank", "noopener,noreferrer");
                            }}
                          >
                            <IconVisibility className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Edit Alt Text"
                            title="Edit Alt Text"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() => setAltTextModal({ open: true, imageId: img.id, value: img.alt })}
                          >
                            <IconTextFields className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Optimize Image"
                            title="Optimize Image"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() =>
                              setGalleryNotice("Image optimization is not available in this demo build.")
                            }
                          >
                            <IconAutoFixHigh className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Edit Image"
                            title="Edit Image"
                            className="rounded p-1 text-on-surface-variant hover:text-primary transition-colors"
                            onClick={() => setEditorImageId(img.id)}
                          >
                            <IconEditNote className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            aria-label="Delete Image"
                            title="Delete Image"
                            className="rounded p-1 text-on-surface-variant hover:text-error transition-colors"
                            onClick={() => setDeleteConfirm({ open: true, imageId: img.id })}
                          >
                            <IconDelete className="h-4 w-4" />
                          </button>
                        </div>
                      </div>
                    ))}

                    {/* Add image dropzone */}
                    <input
                      ref={fileInputRef}
                      type="file"
                      multiple
                      accept="image/*,video/*"
                      className="sr-only"
                      onChange={(e: ChangeEvent<HTMLInputElement>) => {
                        if (e.target.files?.length) ingestFiles(e.target.files);
                        e.target.value = "";
                      }}
                    />
                    <div
                      className="flex w-[160px] shrink-0 cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-primary/20 bg-primary/5 px-3 py-6 transition-colors hover:border-primary/40 hover:bg-primary/10"
                      onDragOver={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                      }}
                      onDrop={(e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        if (e.dataTransfer.files?.length) ingestFiles(e.dataTransfer.files);
                      }}
                      onClick={() => fileInputRef.current?.click()}
                    >
                      <IconCloudUpload className="h-8 w-8 text-primary/40" />
                      <p className="text-center text-[10px] font-semibold leading-snug text-primary/70">
                        Drag & drop<br />or click here
                      </p>
                      <p className="text-center text-[9px] text-on-surface-variant">PNG · GIF · MP4 · WEBM</p>
                    </div>
                  </div>
                )}
              </div>

              {apiReady && productFieldConfig.some((f) => f.enabled && f.targetPage === "General") ? (
                <ProductCustomFieldsSection
                  fields={productFieldConfig}
                  targetPage="General"
                  values={customFieldValues}
                  onChange={setCustomFieldValue}
                  sectionTitleClass={sectionTitle}
                />
              ) : null}

              {/* Variants — SKU / price / stock (wired to Catalog + Inventory) */}
              <div>
                <h3 className={sectionTitle}>Variants · Pricing · Stock</h3>
                {isAdd || !apiReady || !product?.id ? (
                  <div className="rounded-lg border border-outline-variant/20 bg-surface p-4">
                    <p className="text-[11px] text-on-surface-variant">
                      Save the product first to manage SKUs, base price and on-hand stock. Each SKU's base price drives
                      the storefront price and its on-hand quantity drives stock availability.
                    </p>
                  </div>
                ) : (
                  <VariantsPanel
                    tenantId={tenantId!}
                    storeId={storeId!}
                    productId={product.id}
                    authToken={authToken}
                  />
                )}
              </div>

              {!isAdd && (
                <div className="flex items-center justify-end gap-3 border-t border-outline-variant/20 pt-8 mt-8">
                  <button type="button" className={btnFooterGhost} onClick={onBack}>
                    Cancel
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={() => handleSave("Save Changes")}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Save Changes
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── PRODUCT PAGE TAB ─────────────────────────────────────── */}
          {tab === "product-page" && (
            <>
              {/* Core */}
              <div>
                <h3 className={sectionTitle}>Product Page Settings</h3>
                <div className="grid grid-cols-1 gap-6">
                  <MultiLangInput
                    label="Title"
                    requiredMark
                    defaultValues={{ en: isAdd ? "" : (product?.name ?? "") }}
                    placeholders={{
                      en: "Title displayed on the product page",
                      vn: "Tiêu đề hiển thị trên trang sản phẩm",
                      zh: "产品页面上显示的商品标题",
                      ja: "商品ページに表示されるタイトル",
                    }}
                    hint="This is the title customers use to identify the product on the storefront."
                  />

                  <p className="rounded-md border border-outline-variant/20 bg-surface-container-low px-3 py-2 text-[11px] text-on-surface-variant">
                    Brand &amp; Category are set on the <span className="font-semibold text-on-surface">General</span> tab.
                  </p>

                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <div className="space-y-1.5">
                      <label className={labelBase}>Publish From</label>
                      <input
                        className={inputBase}
                        type="datetime-local"
                        value={publishFrom}
                        onChange={(e) => setPublishFrom(e.target.value)}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelBase}>Publish Until</label>
                      <input
                        className={inputBase}
                        type="datetime-local"
                        value={publishUntil}
                        onChange={(e) => setPublishUntil(e.target.value)}
                      />
                    </div>
                  </div>

                  {/* Overview → persisted as the product description (descriptionJson) */}
                  <MultiLangLexicalRichText
                    key={`overview-${product?.id ?? "add"}-${Object.keys(descriptionInitial).length}`}
                    label="Overview"
                    defaultValues={descriptionInitial}
                    onValuesChange={setDescriptionByLocale}
                    placeholders={{
                      en: "Enter product overview and description...",
                      vn: "Nhập mô tả sản phẩm...",
                      zh: "输入产品概述和描述...",
                      ja: "製品の概要と説明を入力してください...",
                    }}
                  />
                </div>
              </div>

              {/* Visibility flags */}
              <div>
                <h3 className={sectionTitleRow}>Visibility & Recommendations</h3>
                <p className={sectionIntroText}>Control product visibility and recommendation behaviour on the storefront.</p>
                <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
                  {[
                    { label: "Hidden Product",              hint: "Hide from search listings and search engines. Accessible only via direct URL.", value: hiddenProduct,        set: setHiddenProduct        },
                    { label: "Recommend Similar Products",  hint: "Allow system to automatically recommend products on this product's page.",    value: recommendSimilar,     set: setRecommendSimilar     },
                    { label: "Enable Restock Notification", hint: "Automatically send restock notification when inventory is replenished.",     value: restockNotification,  set: setRestockNotification  },
                  ].map(({ label, hint, value, set }) => (
                    <label key={label} className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface p-3 select-none hover:bg-surface-container-low transition-colors">
                      <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary shrink-0" checked={value} onChange={(e) => set(e.target.checked)} />
                      <div>
                        <p className="text-xs font-semibold text-on-surface">{label}</p>
                        <p className="text-[10px] text-on-surface-variant">{hint}</p>
                      </div>
                    </label>
                  ))}
                  {recommendSimilar && (
                    <div className="space-y-1.5">
                      <label className={labelBase}>Recommendations Mode</label>
                      <div className="relative">
                        <select
                          className={`${inputBase} appearance-none pr-10`}
                          value={recommendationsMode}
                          onChange={(e) => setRecommendationsMode(e.target.value)}
                        >
                          <option value="auto">Auto</option>
                          <option value="manual">Manual</option>
                          <option value="disabled">Disabled</option>
                        </select>
                        <IconChevronDown className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-on-surface-variant" />
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* URL & Meta */}
              <div>
                <h3 className={sectionTitleRow}>URL & SEO</h3>
                <p className={sectionIntroText}>Configure the product URL and metadata for search engine optimisation.</p>
                <div className="mt-6 grid grid-cols-1 gap-6 lg:grid-cols-2">
                  {/* Left: fields */}
                  <div className="space-y-6">
                    <div className="space-y-1.5">
                      <label className={labelBase}>Product URL (slug)</label>
                      <input
                        className={`${inputBase} ${useTitleForUrl ? "text-on-surface-variant" : ""}`}
                        type="text"
                        readOnly={useTitleForUrl}
                        placeholder="product-url-slug"
                        value={useTitleForUrl ? slugify(name) : slug}
                        onChange={(e) => setSlug(e.target.value)}
                      />
                      <label className="flex cursor-pointer items-center gap-2 text-[10px] text-on-surface-variant select-none">
                        <input type="checkbox" className="h-3 w-3 accent-primary" checked={useTitleForUrl} onChange={(e) => { setUseTitleForUrl(e.target.checked); if (e.target.checked) setSlug(slugify(name)); }} />
                        Use product title as URL
                      </label>
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelBase}>Page Title</label>
                      <input
                        className={`${inputBase} ${useTitleForPageTitle ? "text-on-surface-variant" : ""}`}
                        type="text"
                        readOnly={useTitleForPageTitle}
                        placeholder="Page title for browser tab"
                        value={useTitleForPageTitle ? name : (pageTitleByLocale.vi ?? "")}
                        onChange={(e) => setPageTitleByLocale((prev) => ({ ...prev, vi: e.target.value }))}
                      />
                      <label className="flex cursor-pointer items-center gap-2 text-[10px] text-on-surface-variant select-none">
                        <input type="checkbox" className="h-3 w-3 accent-primary" checked={useTitleForPageTitle} onChange={(e) => setUseTitleForPageTitle(e.target.checked)} />
                        Use product title as page title
                      </label>
                    </div>
                    <MultiLangInput
                      key={`metakw-${product?.id ?? "add"}-${Object.keys(metaKeywordsInitial).length}`}
                      label="Meta Keywords"
                      defaultValues={metaKeywordsInitial}
                      onValuesChange={setMetaKeywordsByLocale}
                      placeholders={{
                        en: "Enter keywords separated by commas…",
                        vn: "Nhập từ khóa cách nhau bằng dấu phẩy...",
                        zh: "输入以逗号分隔的关键词...",
                        ja: "カンマ区切りでキーワードを入力...",
                      }}
                      hint="List relevant keywords that customers might use to find this product."
                    />
                    <MultiLangTextarea
                      key={`metadesc-${product?.id ?? "add"}-${Object.keys(metaDescriptionInitial).length}`}
                      label="Meta Description"
                      rows={3}
                      defaultValues={metaDescriptionInitial}
                      onValuesChange={setMetaDescriptionByLocale}
                      placeholders={{
                        en: "Brief description for search results listings…",
                        vn: "Mô tả ngắn cho kết quả tìm kiếm...",
                        zh: "搜索结果中显示的简短描述...",
                        ja: "検索結果に表示される簡単な説明...",
                      }}
                      hint="Recommended: 160 characters or fewer."
                    />
                  </div>

                  {/* Right: Google preview */}
                  <div className="space-y-4">
                    <div>
                      <h4 className={`${sectionTitleRow} mb-4 flex items-center gap-2`}>
                        <IconVisibility className="h-4 w-4 text-primary shrink-0" />
                        Google Search Preview
                      </h4>
                      <div className="rounded-md border border-outline-variant/20 bg-white p-4 font-sans">
                        <div className="mb-0.5 truncate text-sm text-on-surface-variant/80">
                          https://your-estore.com › products › {isAdd ? "product-name" : (product?.name?.toLowerCase().replace(/\s+/g, "-") ?? "product")}
                        </div>
                        <h5 className="mb-1 cursor-pointer truncate text-lg font-normal text-[#1a0dab] hover:underline">
                          {isAdd
                            ? <span className="italic text-outline-variant">Page title will appear here</span>
                            : product?.name}
                        </h5>
                        <p className="line-clamp-3 text-[13px] leading-snug text-[#4d5156]">
                          {isAdd
                            ? <span className="italic text-outline-variant">Meta description will appear here</span>
                            : "Discover premium quality products at unbeatable prices. Fast delivery and easy returns."}
                        </p>
                      </div>
                    </div>
                    <div className="rounded-md border border-outline-variant/10 bg-primary-container/10 p-4">
                      <div className="flex items-start gap-3">
                        <IconLightbulb className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
                        <p className="text-xs leading-relaxed text-on-surface-variant">
                          <strong>SEO Tip:</strong> Use specific, descriptive keywords in your meta title and description to improve click-through rates from search results.
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {apiReady && productFieldConfig.some((f) => f.enabled && f.targetPage === "Product Page") ? (
                <ProductCustomFieldsSection
                  fields={productFieldConfig}
                  targetPage="Product Page"
                  values={customFieldValues}
                  onChange={setCustomFieldValue}
                  sectionTitleClass={sectionTitle}
                />
              ) : null}

              {!isAdd && (
                <div className="flex items-center justify-end gap-3 border-t border-outline-variant/20 pt-8 mt-8">
                  <button type="button" className={btnFooterGhost} onClick={() => console.info("[EditProduct] Discard")}>
                    Discard Changes
                  </button>
                  <button type="button" className={btnFooterPrimary} onClick={() => handleSave("Save Changes")}>
                    <IconSave className="h-4 w-4 shrink-0" />
                    Update Product Page
                  </button>
                </div>
              )}
            </>
          )}

          {/* ── RECOMMENDATIONS TAB ──────────────────────────────────── */}
          {tab === "recommendations" && (
            <>
              {apiReady && productFieldConfig.some((f) => f.enabled && f.targetPage === "Recommendations") ? (
                <ProductCustomFieldsSection
                  fields={productFieldConfig}
                  targetPage="Recommendations"
                  values={customFieldValues}
                  onChange={setCustomFieldValue}
                  sectionTitleClass={sectionTitleRow}
                />
              ) : null}

              <div>
                <h3 className={sectionTitleRow}>Product Recommendations</h3>
                <p className={sectionIntroText}>
                  Manually curate the related products shown on this product's storefront page.
                </p>
              </div>

              {isAdd || !apiReady || !product?.id ? (
                <div className="mt-4 rounded-lg border border-outline-variant/20 bg-surface p-4">
                  <p className="text-[11px] text-on-surface-variant">
                    Save the product first to curate its recommended products.
                  </p>
                </div>
              ) : (
                <RecommendationsPanel
                  tenantId={tenantId!}
                  storeId={storeId!}
                  productId={product.id}
                  authToken={authToken}
                />
              )}
            </>
          )}
        </section>
      </div>

      {/* ── Configure main product (variants) ───────────────────────────── */}
      {configureMainOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm p-4"
          onClick={() => setConfigureMainOpen(false)}
        >
          <div
            className="w-full max-w-md rounded-2xl border border-outline-variant/20 bg-surface-container-lowest p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-base font-bold text-on-surface">Configure main product</h3>
            <p className="mt-2 text-xs leading-relaxed text-on-surface-variant">
              Choose how this SKU relates to variant listings. In production this would load variant SKUs from your
              catalog service.
            </p>
            <div className="mt-5 flex flex-col gap-3">
              <button
                type="button"
                className="w-full rounded-lg border border-primary/30 bg-primary/5 px-4 py-3 text-left text-xs font-semibold text-primary transition-colors hover:bg-primary/10"
                onClick={() => {
                  setVariantNotice("This SKU is set as the main product for storefront listing (demo).");
                  setConfigureMainOpen(false);
                }}
              >
                Set Self as Main Product
              </button>
              <button
                type="button"
                className={btnFooterGhost + " w-full text-center"}
                onClick={() => setConfigureMainOpen(false)}
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Edit Alt Text modal ──────────────────────────────────────────── */}
      {altTextModal.open && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setAltTextModal({ open: false, imageId: "", value: "" })}
        >
          <div
            className="mx-4 w-full max-w-sm rounded-2xl bg-surface-container-lowest p-6 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="mb-1 text-base font-bold text-on-surface">Edit Alternate Text</h3>
            <p className="mb-4 text-[11px] text-on-surface-variant">
              Alt text helps search engines tag this image and is shown when the image fails to load.
            </p>
            <textarea
              className={`${inputBase} mb-4 resize-y min-h-[80px]`}
              value={altTextModal.value}
              onChange={(e) => setAltTextModal((m) => ({ ...m, value: e.target.value }))}
              placeholder="Describe the image for accessibility and SEO..."
              autoFocus
            />
            <div className="flex justify-end gap-3">
              <button
                type="button"
                className={btnFooterGhost}
                onClick={() => setAltTextModal({ open: false, imageId: "", value: "" })}
              >
                Cancel
              </button>
              <button
                type="button"
                className={btnFooterPrimary}
                onClick={() => {
                  setImages((prev) =>
                    prev.map((img) =>
                      img.id === altTextModal.imageId ? { ...img, alt: altTextModal.value } : img
                    )
                  );
                  setAltTextModal({ open: false, imageId: "", value: "" });
                }}
              >
                <IconSave className="h-4 w-4 shrink-0" />
                Save Changes
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Delete Confirmation popup ─────────────────────────────────────── */}
      {deleteConfirm.open && (() => {
        const img = images.find((i) => i.id === deleteConfirm.imageId);
        return (
          <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
            onClick={() => setDeleteConfirm({ open: false, imageId: "" })}
          >
            <div
              className="mx-4 w-full max-w-sm rounded-2xl bg-surface-container-lowest p-6 text-center shadow-2xl"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-error/10">
                <IconDelete className="h-6 w-6 text-error" />
              </div>
              <h3 className="mb-1 text-base font-bold text-on-surface">Delete Image?</h3>
              <p className="mb-5 text-sm text-on-surface-variant">
                <span className="font-semibold text-on-surface">{img?.name}</span>
                {" will be permanently removed from this product."}
              </p>
              <div className="flex justify-center gap-3">
                <button
                  type="button"
                  className={btnFooterGhost}
                  onClick={() => setDeleteConfirm({ open: false, imageId: "" })}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="rounded-md bg-error px-5 py-2 text-xs font-bold uppercase tracking-widest text-white hover:opacity-90 transition-opacity"
                  onClick={() => {
                    const rid = deleteConfirm.imageId;
                    const victim = images.find((i) => i.id === rid);
                    if (victim?.src?.startsWith("blob:")) URL.revokeObjectURL(victim.src);
                    setImages((prev) => prev.filter((i) => i.id !== rid));
                    if (editorImageId === rid) setEditorImageId(null);
                    setDeleteConfirm({ open: false, imageId: "" });
                  }}
                >
                  OK, Delete
                </button>
              </div>
            </div>
          </div>
        );
      })()}

      {/* ── Success modal ─────────────────────────────────────────────────── */}
      {showSuccessModal && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-on-surface/40 backdrop-blur-sm"
          onClick={() => setShowSuccessModal(false)}
        >
          <div
            className="mx-4 w-full max-w-sm rounded-2xl bg-surface-container-lowest p-8 text-center shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-secondary-container/20">
              <IconCheckCircle className="h-8 w-8 text-secondary" />
            </div>
            <h3 className="mb-2 text-lg font-bold text-on-surface">
              {successMessage === "Save and Approve"
                ? "Product Approved & Published!"
                : successMessage === "Save and Send for Approval"
                ? "Sent for Approval!"
                : successMessage === "Save and Archive"
                ? "Product Archived!"
                : isAdd
                ? "Product Created!"
                : "Changes Saved!"}
            </h3>
            <p className="mb-6 text-sm text-on-surface-variant">
              {successMessage === "Save and Approve"
                ? "The product has been approved and is now live in your eStore."
                : successMessage === "Save and Send for Approval"
                ? "The product has been submitted for admin review."
                : successMessage === "Save and Archive"
                ? `"${product?.name ?? "Product"}" has been archived and is no longer visible in the eStore.`
                : isAdd
                ? "The new product has been successfully created and is available in the catalog."
                : `"${product?.name ?? "Product"}" has been successfully updated.`}
            </p>
            <div className="flex justify-center gap-3">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2 text-xs font-bold uppercase tracking-widest text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={onBack}
              >
                View Products
              </button>
              <button
                type="button"
                className="rounded-md bg-primary px-5 py-2 text-xs font-bold uppercase tracking-widest text-on-primary hover:bg-primary-container transition-colors"
                onClick={() => setShowSuccessModal(false)}
              >
                {isAdd ? "Add Another" : "Continue Editing"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
