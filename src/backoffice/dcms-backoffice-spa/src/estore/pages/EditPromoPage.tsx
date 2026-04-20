import { useEffect, useRef, useState } from "react";
import {
  IconArrowBack,
  IconCheckCircle,
  IconChevronDown,
  IconClose,
  IconCloudUpload,
  IconDelete,
  IconSearch,
  IconWarning,
} from "../../orders/icons";
import type { PromoListRow } from "../promotions-columns";
import { MultiLangTextarea } from "../components/MultiLangField";

// ── Style tokens ─────────────────────────────────────────────────────────────
const sectionCard = "rounded-xl border border-outline-variant/10 bg-surface-container-lowest p-6 shadow-sm";
const sectionTitle = "mb-5 text-sm font-bold uppercase tracking-widest text-on-surface";
const labelBase = "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase = "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-xs text-on-surface-variant";

// ── Mock data for pickers ─────────────────────────────────────────────────────
const MOCK_BRANDS = ["Cronos Ltd.", "SoundWave Co.", "Apex Footwear", "NovaCam", "FurniCraft"];
const MOCK_CATEGORIES = ["Electronics", "Furniture", "Timepieces > Luxury", "Audio > Wireless", "Footwear > Athletics"];
const MOCK_PAYMENT_MODES = ["Credit Card", "Debit Card", "eWallet", "Bank Transfer", "Buy Now Pay Later"];

// ── Searchable multi-select picker ────────────────────────────────────────────
function SearchablePicker({
  label,
  options,
  selected,
  onChange,
  placeholder,
}: {
  label: string;
  options: string[];
  selected: string[];
  onChange: (next: string[]) => void;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const filtered = options.filter((o) => o.toLowerCase().includes(search.toLowerCase()));

  function toggle(opt: string) {
    onChange(selected.includes(opt) ? selected.filter((s) => s !== opt) : [...selected, opt]);
  }

  return (
    <div ref={ref} className="relative space-y-1.5">
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {selected.map((s) => (
            <span key={s} className="inline-flex items-center gap-1 rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-bold uppercase text-primary">
              {s}
              <button type="button" onClick={() => toggle(s)} className="rounded p-0.5 hover:bg-primary/20 transition-colors">
                <IconClose className="h-2.5 w-2.5" />
              </button>
            </span>
          ))}
        </div>
      )}
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center gap-2 rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2 text-xs transition-colors hover:border-primary/30 focus:outline-none focus:ring-1 focus:ring-primary"
      >
        <IconSearch className="h-3.5 w-3.5 shrink-0 text-on-surface-variant" />
        <span className={selected.length === 0 ? "flex-1 text-left text-on-surface-variant/60" : "flex-1 text-left text-on-surface"}>
          {selected.length === 0 ? (placeholder ?? `Search ${label.toLowerCase()}…`) : `${selected.length} selected`}
        </span>
        <IconChevronDown className={`h-3.5 w-3.5 shrink-0 text-on-surface-variant transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="absolute left-0 top-full z-30 mt-1 w-full overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
          <div className="border-b border-outline-variant/10 p-2">
            <input autoFocus type="text" placeholder="Search…" value={search} onChange={(e) => setSearch(e.target.value)}
              className="w-full rounded bg-surface-container-low px-2.5 py-1.5 text-xs outline-none placeholder:text-on-surface-variant/50" />
          </div>
          <div className="max-h-44 overflow-y-auto">
            {filtered.length === 0
              ? <p className="px-3 py-3 text-center text-xs text-on-surface-variant">No results</p>
              : filtered.map((opt) => (
                <label key={opt} className="flex cursor-pointer items-center gap-2.5 px-3 py-2 hover:bg-surface-container transition-colors select-none">
                  <input type="checkbox" className="h-3.5 w-3.5 accent-primary" checked={selected.includes(opt)} onChange={() => toggle(opt)} />
                  <span className="text-xs text-on-surface">{opt}</span>
                </label>
              ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ── Import Users (account-bound) ──────────────────────────────────────────────
type ImportedUserRow = { id: string; email: string; status: "ready" | "error"; errorMsg?: string };

function isValidEmail(email: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}

function mockParseEmails(_file: File): ImportedUserRow[] {
  return [
    { id: "1", email: "alice@example.com", status: "ready" },
    { id: "2", email: "bob@store.co", status: "ready" },
    { id: "3", email: "carol@brand.io", status: "ready" },
    { id: "4", email: "david@domain.net", status: "ready" },
    { id: "5", email: "not-an-email", status: "error", errorMsg: "Invalid email format" },
    { id: "6", email: "emma@shop.com", status: "ready" },
  ];
}

// ── PromoType label ───────────────────────────────────────────────────────────
type PromoType = "standard" | "shareable" | "account-bound";

function promoTypeLabel(t: PromoType) {
  if (t === "shareable") return "Multiple Shareable Promo Codes";
  if (t === "account-bound") return "Multiple Account Bound Promo Codes";
  return "Standard Promo Code";
}

// ── Component ─────────────────────────────────────────────────────────────────
type Props = {
  mode: "add" | "edit";
  promoType: PromoType;
  promo?: PromoListRow;
  onBack: () => void;
};

export function EditPromoPage({ mode, promoType, promo, onBack }: Props) {
  const isAdd = mode === "add";

  // ── General ───────────────────────────────────────────────────────────────
  const [promoCode, setPromoCode] = useState(promo?.code ?? "");
  // Shareable / account-bound General fields
  const [codePrefix, setCodePrefix] = useState("");
  const [codeSuffixLength, setCodeSuffixLength] = useState("");
  const [numCodesToGenerate, setNumCodesToGenerate] = useState("");

  // Account-bound Import Users
  const usersFileInputRef = useRef<HTMLInputElement>(null);
  const [usersFile, setUsersFile] = useState<File | null>(null);
  const [usersUploadDone, setUsersUploadDone] = useState(false);
  const [userRows, setUserRows] = useState<ImportedUserRow[]>([]);

  function handleUsersFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0] ?? null;
    setUsersFile(file);
    setUsersUploadDone(false);
    setUserRows([]);
  }

  function handleUsersUpload() {
    if (!usersFile) return;
    setUserRows(mockParseEmails(usersFile));
    setUsersUploadDone(true);
  }

  function removeUserRow(id: string) {
    setUserRows((rows) => rows.filter((r) => r.id !== id));
  }

  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");

  // ── Qualifiers ────────────────────────────────────────────────────────────
  const [brands, setBrands] = useState<string[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [product, setProduct] = useState("");
  const [minSpend, setMinSpend] = useState("");
  const [paymentModes, setPaymentModes] = useState<string[]>([]);
  const [binNumbers, setBinNumbers] = useState("");
  const [qualifierMode, setQualifierMode] = useState<"inclusion" | "exclusion">("inclusion");

  // ── Mechanics ─────────────────────────────────────────────────────────────
  const [discountAppliedTo, setDiscountAppliedTo] = useState<"entire-order" | "single-item">("entire-order");
  const [discountType, setDiscountType] = useState<"fixed" | "percent">("fixed");
  const [discountAmount, setDiscountAmount] = useState("");
  const [localDeliveryDiscount, setLocalDeliveryDiscount] = useState("");
  const [overseasDeliveryDiscount, setOverseasDeliveryDiscount] = useState("");
  const [totalRedemptionLimit, setTotalRedemptionLimit] = useState("");
  const [redemptionPerCustomer, setRedemptionPerCustomer] = useState("");
  const [redemptionLimit, setRedemptionLimit] = useState("");

  // ── Others ────────────────────────────────────────────────────────────────
  const [showPopup, setShowPopup] = useState(false);

  // ── Delete confirmation ───────────────────────────────────────────────────
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);

  function handleDeleteConfirm() {
    setDeleteConfirmOpen(false);
    setToast("Promo code deleted.");
    setTimeout(() => onBack(), 1800);
  }

  // ── Actions dropdown ──────────────────────────────────────────────────────
  const [actionsOpen, setActionsOpen] = useState(false);
  const actionsRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function handler(e: MouseEvent) {
      if (actionsRef.current && !actionsRef.current.contains(e.target as Node)) setActionsOpen(false);
    }
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  // ── Toast ─────────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<string | null>(null);
  useEffect(() => {
    if (!toast) return;
    const t = setTimeout(() => setToast(null), 3000);
    return () => clearTimeout(t);
  }, [toast]);

  function handleSavePublish() {
    setActionsOpen(false);
    setToast("Promo code published successfully.");
    setTimeout(() => onBack(), 1800);
  }

  function handleSaveApproval() {
    setActionsOpen(false);
    setToast("Promo code sent for approval.");
    setTimeout(() => onBack(), 1800);
  }

  function handleChangeHistory() {
    setActionsOpen(false);
    setToast("Change history: no changes recorded yet.");
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="-m-6 flex min-h-[calc(100dvh-6rem)] flex-col bg-surface-container-low">

      {/* ── Top bar ─────────────────────────────────────────────────────── */}
      <div className="flex shrink-0 items-center justify-between border-b border-outline-variant/15 bg-surface px-6 py-4">
        <div>
          <button
            type="button"
            onClick={onBack}
            className="mb-1 flex items-center gap-2 text-xs font-bold uppercase tracking-tighter text-primary hover:opacity-80"
          >
            <IconArrowBack className="h-3 w-3 shrink-0" />
            Back to Promo Codes
          </button>
          <h2 className="text-2xl font-bold tracking-tight text-on-surface">
            {isAdd ? "Add Promotion Code" : "Edit Promotion Code"}
          </h2>
          <p className="mt-0.5 text-sm text-on-surface-variant">
            {promoTypeLabel(promoType)}{promo ? ` · ${promo.code}` : ""}
          </p>
        </div>

        {/* Actions dropdown */}
        <div className="relative" ref={actionsRef}>
          <button
            type="button"
            className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-xs font-bold text-on-primary shadow-lg shadow-primary/20 transition-all hover:opacity-90"
            onClick={() => setActionsOpen((o) => !o)}
          >
            Actions
            <IconChevronDown className={`h-3.5 w-3.5 shrink-0 transition-transform ${actionsOpen ? "rotate-180" : ""}`} />
          </button>
          {actionsOpen && (
            <div className="absolute right-0 top-full z-20 mt-1 w-56 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-lowest shadow-xl">
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={handleSavePublish}
              >
                <IconCheckCircle className="h-4 w-4 shrink-0 text-primary" />
                Save and Publish
              </button>
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={handleSaveApproval}
              >
                <IconCheckCircle className="h-4 w-4 shrink-0 text-secondary" />
                Save and Request for Approval
              </button>
              <div className="my-1 border-t border-outline-variant/10" />
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-on-surface hover:bg-surface-container transition-colors"
                onClick={handleChangeHistory}
              >
                <IconSearch className="h-4 w-4 shrink-0 text-on-surface-variant" />
                Show Change History
              </button>
              <div className="my-1 border-t border-outline-variant/10" />
              <button
                type="button"
                className="flex w-full items-center gap-2.5 px-4 py-2.5 text-left text-xs font-medium text-error hover:bg-error/5 transition-colors"
                onClick={() => { setActionsOpen(false); setDeleteConfirmOpen(true); }}
              >
                <IconDelete className="h-4 w-4 shrink-0 text-error" />
                Delete
              </button>
            </div>
          )}
        </div>
      </div>

      {/* ── Body ────────────────────────────────────────────────────────── */}
      <div className="flex-1 space-y-6 p-6 pb-24">

        {/* ══ GENERAL ══════════════════════════════════════════════════════ */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>General</h3>
          {promoType === "standard" ? (
            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              {/* Promo Code */}
              <div>
                <label className={labelBase}>
                  Promo Code <span className="text-error">*</span>
                </label>
                <input
                  type="text"
                  className={inputBase}
                  placeholder="e.g. SUMMER50"
                  value={promoCode}
                  onChange={(e) => setPromoCode(e.target.value.toUpperCase())}
                />
                <p className={hintText}>Customers enter this code at checkout.</p>
              </div>

              {/* Start Date */}
              <div>
                <label className={labelBase}>
                  Start Date <span className="text-error">*</span>
                </label>
                <input
                  type="datetime-local"
                  className={inputBase}
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                />
                <p className={hintText}>Promo code becomes active from this date.</p>
              </div>

              {/* End Date */}
              <div>
                <label className={labelBase}>End Date</label>
                <input
                  type="datetime-local"
                  className={inputBase}
                  value={endDate}
                  onChange={(e) => setEndDate(e.target.value)}
                />
                <p className={hintText}>Leave blank for a permanent code.</p>
              </div>
            </div>
          ) : promoType === "account-bound" ? (
            <>
              {/* ── Account-bound: prefix / suffix / count ── */}
              <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
                {/* Promo Code Prefix */}
                <div>
                  <label className={labelBase}>
                    Promo Code Prefix <span className="text-error">*</span>
                  </label>
                  <input
                    type="text"
                    className={inputBase}
                    placeholder="e.g. VIP"
                    value={codePrefix}
                    onChange={(e) => setCodePrefix(e.target.value.toUpperCase())}
                  />
                  <p className={hintText}>Prefix added to the beginning of each generated code.</p>
                </div>

                {/* Promo Code Suffix Length */}
                <div>
                  <label className={labelBase}>
                    Promo Code Suffix Length <span className="text-error">*</span>
                  </label>
                  <input
                    type="number"
                    min="1"
                    step="1"
                    className={inputBase}
                    placeholder="e.g. 8"
                    value={codeSuffixLength}
                    onChange={(e) => setCodeSuffixLength(e.target.value)}
                  />
                  <p className={hintText}>Length of the generated suffix (numbers/letters).</p>
                </div>

                {/* No. of Promo Codes to Generate */}
                <div>
                  <label className={labelBase}>
                    No. of Promo Codes to Generate <span className="text-error">*</span>
                  </label>
                  <input
                    type="number"
                    min="1"
                    step="1"
                    className={inputBase}
                    placeholder="e.g. 200"
                    value={numCodesToGenerate}
                    onChange={(e) => setNumCodesToGenerate(e.target.value)}
                  />
                  <p className={hintText}>How many account-bound codes to generate.</p>
                </div>
              </div>

              {/* ── Import Users ── */}
              <div className="mt-6 space-y-3">
                <label className={labelBase}>Import Users <span className="text-error">*</span></label>
                <p className="text-xs text-on-surface-variant -mt-1 mb-2 leading-relaxed">
                  Upload an Excel / CSV file containing the email addresses of users who will receive a promo code.
                  Only one email address per cell.
                </p>

                {/* File picker row */}
                <div className="flex flex-wrap items-center gap-4">
                  <div
                    className="flex cursor-pointer items-center gap-3 rounded-lg border-2 border-dashed border-primary/20 bg-primary/5 px-4 py-3 transition-colors hover:border-primary/40 hover:bg-primary/10"
                    onClick={() => usersFileInputRef.current?.click()}
                  >
                    <IconCloudUpload className="h-5 w-5 shrink-0 text-primary/50" />
                    <div className="min-w-0">
                      {usersFile ? (
                        <>
                          <p className="truncate text-xs font-semibold text-on-surface max-w-[220px]">{usersFile.name}</p>
                          <p className="text-xs text-on-surface-variant">{(usersFile.size / 1024).toFixed(1)} KB · Click to change</p>
                        </>
                      ) : (
                        <>
                          <p className="text-xs font-semibold text-primary/80">Choose File</p>
                          <p className="text-xs text-on-surface-variant">Accepts .xlsx, .xls, .csv</p>
                        </>
                      )}
                    </div>
                  </div>
                  <input
                    ref={usersFileInputRef}
                    type="file"
                    accept=".xlsx,.xls,.csv"
                    className="hidden"
                    onChange={handleUsersFileChange}
                  />
                  <button
                    type="button"
                    disabled={!usersFile}
                    onClick={handleUsersUpload}
                    className="flex items-center gap-2 rounded-md bg-primary px-5 py-2.5 text-xs font-bold uppercase tracking-widest text-on-primary shadow-sm shadow-primary/20 transition-all hover:opacity-90 disabled:pointer-events-none disabled:opacity-40"
                  >
                    <IconCloudUpload className="h-3.5 w-3.5 shrink-0" />
                    Upload File
                  </button>
                </div>

                {/* Email preview table */}
                {usersUploadDone && userRows.length > 0 && (() => {
                  const validCount = userRows.filter((r) => r.status === "ready").length;
                  const invalidCount = userRows.filter((r) => r.status === "error").length;
                  return (
                    <div className="mt-3 overflow-hidden rounded-xl border border-outline-variant/10 bg-surface-container-lowest shadow-sm">
                      {/* Summary bar */}
                      <div className="flex flex-wrap items-center gap-3 border-b border-outline-variant/10 bg-surface-container-low/60 px-4 py-3">
                        <p className="text-xs font-bold text-on-surface">{userRows.length} emails parsed</p>
                        <span className="rounded-full bg-secondary-container/20 px-2.5 py-0.5 text-[9px] font-bold uppercase text-on-secondary-container">
                          {validCount} valid
                        </span>
                        {invalidCount > 0 && (
                          <span className="rounded-full bg-error-container px-2.5 py-0.5 text-[9px] font-bold uppercase text-on-error-container">
                            {invalidCount} invalid
                          </span>
                        )}
                        <p className="ml-auto text-xs text-on-surface-variant">
                          Remove invalid rows before saving.
                        </p>
                      </div>
                      {/* Table */}
                      <div className="max-h-56 overflow-y-auto">
                        <table className="w-full border-collapse text-left">
                          <thead>
                            <tr className="bg-surface-container-high text-xs font-bold uppercase tracking-widest text-primary">
                              <th className="px-4 py-2">Email</th>
                              <th className="px-4 py-2">Status</th>
                              <th className="w-10 px-4 py-2 text-right">Remove</th>
                            </tr>
                          </thead>
                          <tbody>
                            {userRows.map((row) => (
                              <tr
                                key={row.id}
                                className={`text-xs transition-colors hover:bg-surface-container-low ${row.status === "error" ? "opacity-70" : ""}`}
                              >
                                <td className="px-4 py-2.5 font-mono text-xs text-on-surface">{row.email}</td>
                                <td className="px-4 py-2.5">
                                  {row.status === "ready" ? (
                                    <span className="rounded-full bg-secondary-container/20 px-2 py-0.5 text-[9px] font-bold uppercase text-on-secondary-container">Ready</span>
                                  ) : (
                                    <span className="flex flex-col gap-0.5">
                                      <span className="rounded-full bg-error-container px-2 py-0.5 text-[9px] font-bold uppercase text-on-error-container">Error</span>
                                      {row.errorMsg && <span className="text-[9px] text-error">{row.errorMsg}</span>}
                                    </span>
                                  )}
                                </td>
                                <td className="px-4 py-2.5 text-right">
                                  <button
                                    type="button"
                                    title="Remove row"
                                    onClick={() => removeUserRow(row.id)}
                                    className="rounded p-1 text-on-surface-variant hover:bg-error/10 hover:text-error transition-colors"
                                  >
                                    <IconDelete className="h-3.5 w-3.5" />
                                  </button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  );
                })()}
              </div>

              {/* ── Start/End dates ── */}
              <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
                <div>
                  <label className={labelBase}>
                    Start Date <span className="text-error">*</span>
                  </label>
                  <input
                    type="datetime-local"
                    className={inputBase}
                    value={startDate}
                    onChange={(e) => setStartDate(e.target.value)}
                  />
                  <p className={hintText}>Promo codes become active from this date.</p>
                </div>
                <div>
                  <label className={labelBase}>End Date</label>
                  <input
                    type="datetime-local"
                    className={inputBase}
                    value={endDate}
                    onChange={(e) => setEndDate(e.target.value)}
                  />
                  <p className={hintText}>Promo codes will no longer be valid after this date.</p>
                </div>
              </div>
            </>
          ) : (
            <>
              {/* ── Shareable: prefix / suffix / count ── */}
              <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
                {/* Promo Code Prefix */}
                <div>
                  <label className={labelBase}>
                    Promo Code Prefix <span className="text-error">*</span>
                  </label>
                  <input
                    type="text"
                    className={inputBase}
                    placeholder="e.g. SPRING"
                    value={codePrefix}
                    onChange={(e) => setCodePrefix(e.target.value.toUpperCase())}
                  />
                  <p className={hintText}>Prefix added to the beginning of each generated code.</p>
                </div>

                {/* Promo Code Suffix Length */}
                <div>
                  <label className={labelBase}>
                    Promo Code Suffix Length <span className="text-error">*</span>
                  </label>
                  <input
                    type="number"
                    min="1"
                    step="1"
                    className={inputBase}
                    placeholder="e.g. 6"
                    value={codeSuffixLength}
                    onChange={(e) => setCodeSuffixLength(e.target.value)}
                  />
                  <p className={hintText}>Length of the generated suffix (numbers/letters).</p>
                </div>

                {/* No. of Promo Codes to Generate */}
                <div>
                  <label className={labelBase}>
                    No. of Promo Codes to Generate <span className="text-error">*</span>
                  </label>
                  <input
                    type="number"
                    min="1"
                    step="1"
                    className={inputBase}
                    placeholder="e.g. 100"
                    value={numCodesToGenerate}
                    onChange={(e) => setNumCodesToGenerate(e.target.value)}
                  />
                  <p className={hintText}>How many promo codes to generate for this campaign.</p>
                </div>
              </div>

              <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
                {/* Start Date */}
                <div>
                  <label className={labelBase}>
                    Start Date <span className="text-error">*</span>
                  </label>
                  <input
                    type="datetime-local"
                    className={inputBase}
                    value={startDate}
                    onChange={(e) => setStartDate(e.target.value)}
                  />
                  <p className={hintText}>Promo codes become active from this date.</p>
                </div>

                {/* End Date */}
                <div>
                  <label className={labelBase}>End Date</label>
                  <input
                    type="datetime-local"
                    className={inputBase}
                    value={endDate}
                    onChange={(e) => setEndDate(e.target.value)}
                  />
                  <p className={hintText}>Promo codes will no longer be valid after this date.</p>
                </div>
              </div>
            </>
          )}
        </section>

        {/* ══ PROMOTION QUALIFIERS ══════════════════════════════════════════ */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Promotion Qualifiers</h3>
          <div className="space-y-6">

            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              {/* Brand */}
              <div>
                <label className={labelBase}>Brand</label>
                <SearchablePicker label="Brand" options={MOCK_BRANDS} selected={brands} onChange={setBrands} placeholder="Search brands…" />
                <p className={hintText}>Brands to which the promotion will be applied.</p>
              </div>

              {/* Category */}
              <div>
                <label className={labelBase}>Category</label>
                <SearchablePicker label="Category" options={MOCK_CATEGORIES} selected={categories} onChange={setCategories} placeholder="Search categories…" />
                <p className={hintText}>Categories to which the promotion will be applied.</p>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              {/* Product */}
              <div>
                <label className={labelBase}>Product</label>
                <input
                  type="text"
                  className={inputBase}
                  placeholder="Enter product name or UPC…"
                  value={product}
                  onChange={(e) => setProduct(e.target.value)}
                />
                <p className={hintText}>Specific product or UPC for this promotion.</p>
              </div>

              {/* Min Spend */}
              <div>
                <label className={labelBase}>Min Spend Amount on Qualifying Items</label>
                <div className="flex items-center gap-0 overflow-hidden rounded-md border border-outline-variant/20 focus-within:ring-1 focus-within:ring-primary">
                  <span className="shrink-0 bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant">$</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="flex-1 bg-surface-container-lowest px-3 py-2 text-xs outline-none"
                    placeholder="0.00"
                    value={minSpend}
                    onChange={(e) => setMinSpend(e.target.value)}
                  />
                </div>
                <p className={hintText}>Minimum cart value for this promotion to apply.</p>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
              {/* Payment Modes */}
              <div>
                <label className={labelBase}>Payment Modes</label>
                <SearchablePicker label="Payment Mode" options={MOCK_PAYMENT_MODES} selected={paymentModes} onChange={setPaymentModes} placeholder="Select payment modes…" />
                <p className={hintText}>Applicable payment methods for this promotion.</p>
              </div>

              {/* BIN Numbers */}
              <div>
                <label className={labelBase}>BIN Numbers</label>
                <textarea
                  className={`${inputBase} min-h-[80px] resize-y`}
                  placeholder={"Enter one BIN per line\ne.g. 411111\n    52341234"}
                  value={binNumbers}
                  onChange={(e) => setBinNumbers(e.target.value)}
                />
                <p className={hintText}>BINs must be 6 or 8 digits. Enter one per line.</p>
              </div>
            </div>

            {/* Qualifier Mode */}
            <div>
              <label className={labelBase}>Qualifier Mode</label>
              <div className="flex gap-6 mt-2">
                {(["inclusion", "exclusion"] as const).map((m) => (
                  <label key={m} className="flex cursor-pointer items-center gap-2.5 select-none">
                    <input
                      type="radio"
                      name="qualifierMode"
                      value={m}
                      checked={qualifierMode === m}
                      onChange={() => setQualifierMode(m)}
                      className="accent-primary"
                    />
                    <div>
                      <p className="text-xs font-bold text-on-surface capitalize">{m}</p>
                      <p className="text-xs text-on-surface-variant">
                        {m === "inclusion"
                          ? "Include specified parameters for promotion"
                          : "Remove specified parameters from promotion"}
                      </p>
                    </div>
                  </label>
                ))}
              </div>
            </div>
          </div>
        </section>

        {/* ══ PROMOTION MECHANICS ══════════════════════════════════════════ */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Promotion Mechanics</h3>
          <div className="space-y-6">

            {/* Discount Applied To */}
            <div>
              <label className={labelBase}>Discount is Applied To</label>
              <div className="flex gap-6 mt-2">
                {([
                  { value: "entire-order" as const, label: "Entire Order", hint: "Discount applies to the full order total." },
                  { value: "single-item" as const, label: "A Single Item", hint: "Discount applies to one product only." },
                ]).map((opt) => (
                  <label key={opt.value} className="flex cursor-pointer items-start gap-2.5 select-none">
                    <input
                      type="radio"
                      name="discountAppliedTo"
                      value={opt.value}
                      checked={discountAppliedTo === opt.value}
                      onChange={() => setDiscountAppliedTo(opt.value)}
                      className="mt-0.5 accent-primary"
                    />
                    <div>
                      <p className="text-xs font-bold text-on-surface">{opt.label}</p>
                      <p className="text-xs text-on-surface-variant">{opt.hint}</p>
                    </div>
                  </label>
                ))}
              </div>
            </div>

            {/* Discount Type */}
            <div>
              <label className={labelBase}>Discount Type</label>
              <div className="flex gap-6 mt-2">
                {([
                  { value: "fixed" as const, label: "Fixed Value", hint: "Subtract a fixed amount." },
                  { value: "percent" as const, label: "Percent", hint: "Subtract a percentage of the total." },
                ]).map((opt) => (
                  <label key={opt.value} className="flex cursor-pointer items-start gap-2.5 select-none">
                    <input
                      type="radio"
                      name="discountType"
                      value={opt.value}
                      checked={discountType === opt.value}
                      onChange={() => setDiscountType(opt.value)}
                      className="mt-0.5 accent-primary"
                    />
                    <div>
                      <p className="text-xs font-bold text-on-surface">{opt.label}</p>
                      <p className="text-xs text-on-surface-variant">{opt.hint}</p>
                    </div>
                  </label>
                ))}
              </div>
            </div>

            <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
              {/* Discount Amount */}
              <div>
                <label className={labelBase}>Discount Amount</label>
                <div className="flex items-center gap-0 overflow-hidden rounded-md border border-outline-variant/20 focus-within:ring-1 focus-within:ring-primary">
                  <span className="shrink-0 bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant">
                    {discountType === "percent" ? "%" : "$"}
                  </span>
                  <input
                    type="number"
                    min="0"
                    step={discountType === "percent" ? "1" : "0.01"}
                    className="flex-1 bg-surface-container-lowest px-3 py-2 text-xs outline-none"
                    placeholder="0"
                    value={discountAmount}
                    onChange={(e) => setDiscountAmount(e.target.value)}
                  />
                </div>
              </div>

              {/* Local Delivery Discount */}
              <div>
                <label className={labelBase}>Local Delivery Discount</label>
                <div className="flex items-center gap-0 overflow-hidden rounded-md border border-outline-variant/20 focus-within:ring-1 focus-within:ring-primary">
                  <span className="shrink-0 bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant">$</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="flex-1 bg-surface-container-lowest px-3 py-2 text-xs outline-none"
                    placeholder="0.00"
                    value={localDeliveryDiscount}
                    onChange={(e) => setLocalDeliveryDiscount(e.target.value)}
                  />
                </div>
                <p className={hintText}>Applied to the local delivery fee.</p>
              </div>

              {/* Overseas Delivery Discount */}
              <div>
                <label className={labelBase}>Overseas Delivery Discount</label>
                <div className="flex items-center gap-0 overflow-hidden rounded-md border border-outline-variant/20 focus-within:ring-1 focus-within:ring-primary">
                  <span className="shrink-0 bg-surface-container-high px-3 py-2 text-xs font-bold text-on-surface-variant">$</span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="flex-1 bg-surface-container-lowest px-3 py-2 text-xs outline-none"
                    placeholder="0.00"
                    value={overseasDeliveryDiscount}
                    onChange={(e) => setOverseasDeliveryDiscount(e.target.value)}
                  />
                </div>
                <p className={hintText}>Applied to the shipping fee.</p>
              </div>
            </div>

            {promoType === "standard" ? (
              <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                {/* Total Redemption Limit */}
                <div>
                  <label className={labelBase}>Total Redemption Limit</label>
                  <input
                    type="number"
                    min="0"
                    step="1"
                    className={inputBase}
                    placeholder="e.g. 500"
                    value={totalRedemptionLimit}
                    onChange={(e) => setTotalRedemptionLimit(e.target.value)}
                  />
                  <p className={hintText}>Maximum number of times this code can be redeemed in total.</p>
                </div>

                {/* Redemption Limit per customer */}
                <div>
                  <label className={labelBase}>Redemption Limit (per customer)</label>
                  <input
                    type="number"
                    min="0"
                    step="1"
                    className={inputBase}
                    placeholder="e.g. 1"
                    value={redemptionPerCustomer}
                    onChange={(e) => setRedemptionPerCustomer(e.target.value)}
                  />
                  <p className={hintText}>
                    Applicable for Single Promo Code only. System checks by email or mobile number.
                  </p>
                </div>
              </div>
            ) : (
              <div>
                <label className={labelBase}>Redemption Limit</label>
                <input
                  type="number"
                  min="0"
                  step="1"
                  className={inputBase}
                  placeholder="e.g. 1"
                  value={redemptionLimit}
                  onChange={(e) => setRedemptionLimit(e.target.value)}
                />
                <p className={hintText}>Number of times each generated code can be used.</p>
              </div>
            )}
          </div>
        </section>

        {/* ══ OTHERS ════════════════════════════════════════════════════════ */}
        <section className={sectionCard}>
          <h3 className={sectionTitle}>Others</h3>
          <div className="space-y-5">
            <MultiLangTextarea
              label="Promo Description"
              rows={5}
              defaultValues={{ en: "" }}
              placeholders={{
                en: "Describe the purpose of the promotion and how it is used…",
                vn: "Mô tả mục đích khuyến mãi và cách sử dụng…",
                zh: "描述促销目的和使用方式…",
                ja: "プロモーションの目的と利用方法を説明してください…",
              }}
              className="min-h-[100px]"
            />

            {/* Show as popup */}
            <label className="flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-4 hover:border-primary/30 transition-colors select-none">
              <input
                type="checkbox"
                className="mt-0.5 h-4 w-4 accent-primary shrink-0"
                checked={showPopup}
                onChange={(e) => setShowPopup(e.target.checked)}
              />
              <div>
                <p className="text-xs font-bold text-on-surface">Show Description as Popup during Checkout</p>
                <p className="mt-0.5 text-xs text-on-surface-variant leading-relaxed">
                  The promo description will appear as a popup message when the customer applies this code at checkout.
                </p>
              </div>
            </label>
          </div>
        </section>
      </div>

      {/* ── Delete confirmation modal ────────────────────────────────── */}
      {deleteConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
          <div className="w-[400px] rounded-2xl border border-outline-variant/20 bg-surface-container-lowest shadow-2xl">
            <div className="flex items-start gap-4 p-6">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-error-container">
                <IconWarning className="h-5 w-5 text-error" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-on-surface">Delete Promo Code</h3>
                <p className="mt-1.5 text-xs text-on-surface-variant leading-relaxed">
                  Are you sure you want to delete this promo code? This action cannot be undone.
                </p>
              </div>
            </div>
            <div className="flex items-center justify-end gap-3 border-t border-outline-variant/10 px-6 py-4">
              <button
                type="button"
                className="rounded-md border border-outline-variant/30 px-5 py-2.5 text-xs font-bold text-on-surface-variant hover:bg-surface-container-high transition-colors"
                onClick={() => setDeleteConfirmOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="flex items-center gap-2 rounded-md bg-error px-5 py-2.5 text-xs font-bold text-on-error hover:opacity-90 transition-opacity"
                onClick={handleDeleteConfirm}
              >
                <IconDelete className="h-4 w-4 shrink-0" />
                Delete
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Toast ───────────────────────────────────────────────────────── */}
      {toast && (
        <div className="fixed bottom-6 left-1/2 z-50 -translate-x-1/2 flex items-center gap-3 rounded-full border border-secondary/20 bg-surface-container-lowest px-6 py-3 shadow-2xl">
          <IconCheckCircle className="h-5 w-5 shrink-0 text-secondary" />
          <p className="text-sm font-semibold text-on-surface">{toast}</p>
        </div>
      )}
    </div>
  );
}
