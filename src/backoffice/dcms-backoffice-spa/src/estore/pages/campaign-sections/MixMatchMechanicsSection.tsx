import { useMemo } from "react";

const labelBase = "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-xs text-on-surface-variant";

export type MixMatchLogicMode = "single-item" | "per-bundle" | "incremental";

export type MixMatchPriority = "lowest-first" | "highest-first" | "distribute-evenly";

export type MixMatchMechanicsValue = {
  logicMode: MixMatchLogicMode;
  singleItem: {
    discountType: "fixed" | "percentage";
    discountPriority: MixMatchPriority;
    discountValue: string;
    qualifyingProductsPerSet: string;
    maxDiscountedProductSets: string;
  };
  perBundle: {
    discountType: "fixed" | "percentage" | "set-price";
    discountPriority: MixMatchPriority;
    bundledAmount: string;
    qualifyingProductsPerSet: string;
    maxDiscountedProductSets: string;
  };
  incremental: {
    discountType: "fixed" | "percentage";
    discountPriority: MixMatchPriority;
    baseDiscount: string;
    additionalQualifying1: string;
    additionalQualifying2: string;
    additionalQualifying3: string;
    additionalQualifying4: string;
    additionalQualifying5: string;
    additionalQualifying6: string;
    qualifyingProductsForBase: string;
  };
};

const logicTabs: { id: MixMatchLogicMode; label: string; hint: string }[] = [
  { id: "single-item", label: "Single item", hint: "Discount applied per eligible line in the mix." },
  { id: "per-bundle", label: "Per bundle", hint: "One discount for the whole qualifying bundle." },
  { id: "incremental", label: "Incremental", hint: "Tiered discount as the customer adds more qualifying items." },
];

const priorityOptions: { id: MixMatchPriority; label: string }[] = [
  { id: "lowest-first", label: "Lowest price first" },
  { id: "highest-first", label: "Highest price first" },
  { id: "distribute-evenly", label: "Distribute evenly" },
];

const tabBtn = (active: boolean) =>
  `rounded-lg border px-3 py-2 text-left transition-colors ${
    active
      ? "border-primary bg-primary/10 text-primary"
      : "border-outline-variant/30 text-on-surface-variant hover:border-primary/40"
  }`;

const radioRow =
  "flex cursor-pointer items-center gap-2 rounded-lg border border-outline-variant/20 bg-surface-container-low px-3 py-2 text-xs hover:border-primary/30";

function OverrideField() {
  return (
    <div>
      <label className={labelBase}>Discount mode</label>
      <input type="text" className={`${inputBase} bg-surface-container-high text-on-surface-variant`} value="Override" readOnly disabled />
      <p className={hintText}>Fixed for Mix and Match campaigns.</p>
    </div>
  );
}

type Props = {
  value: MixMatchMechanicsValue;
  onChange: (next: MixMatchMechanicsValue) => void;
};

export const defaultMixMatchMechanicsValue = (): MixMatchMechanicsValue => ({
  logicMode: "single-item",
  singleItem: {
    discountType: "fixed",
    discountPriority: "lowest-first",
    discountValue: "",
    qualifyingProductsPerSet: "",
    maxDiscountedProductSets: "",
  },
  perBundle: {
    discountType: "fixed",
    discountPriority: "lowest-first",
    bundledAmount: "",
    qualifyingProductsPerSet: "",
    maxDiscountedProductSets: "",
  },
  incremental: {
    discountType: "fixed",
    discountPriority: "lowest-first",
    baseDiscount: "",
    additionalQualifying1: "",
    additionalQualifying2: "",
    additionalQualifying3: "",
    additionalQualifying4: "",
    additionalQualifying5: "",
    additionalQualifying6: "",
    qualifyingProductsForBase: "",
  },
});

export function MixMatchMechanicsSection({ value, onChange }: Props) {
  function patch<K extends keyof MixMatchMechanicsValue>(key: K, part: Partial<MixMatchMechanicsValue[K]>) {
    const cur = value[key] as Record<string, unknown>;
    onChange({ ...value, [key]: { ...cur, ...part } });
  }

  const siSuffix = value.singleItem.discountType === "percentage" ? "%" : "$";
  const incSuffix = value.incremental.discountType === "percentage" ? "%" : "$";
  const pbLabel = useMemo(() => {
    if (value.perBundle.discountType === "set-price") return "Set price (bundle)";
    if (value.perBundle.discountType === "percentage") return "Bundled discount (%)";
    return "Bundled discount ($)";
  }, [value.perBundle.discountType]);
  const pbSuffix = value.perBundle.discountType === "percentage" ? "%" : "$";

  return (
    <div className="space-y-6">
      <div>
        <p className={`${labelBase} mb-2`}>Discount logic</p>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
          {logicTabs.map((t) => (
            <button
              key={t.id}
              type="button"
              className={tabBtn(value.logicMode === t.id)}
              onClick={() => onChange({ ...value, logicMode: t.id })}
            >
              <span className="block text-xs font-bold text-on-surface">{t.label}</span>
              <span className="mt-0.5 block text-xs font-normal text-on-surface-variant leading-snug">{t.hint}</span>
            </button>
          ))}
        </div>
      </div>

      {value.logicMode === "single-item" && (
        <div className="space-y-5 border-t border-outline-variant/10 pt-5">
          <fieldset className="space-y-2">
            <legend className={labelBase}>Discount type</legend>
            <div className="flex flex-wrap gap-2">
              <label className={radioRow}>
                <input
                  type="radio"
                  name="mm-si-type"
                  className="h-3.5 w-3.5 accent-primary"
                  checked={value.singleItem.discountType === "fixed"}
                  onChange={() => patch("singleItem", { discountType: "fixed" })}
                />
                Fixed amount
              </label>
              <label className={radioRow}>
                <input
                  type="radio"
                  name="mm-si-type"
                  className="h-3.5 w-3.5 accent-primary"
                  checked={value.singleItem.discountType === "percentage"}
                  onChange={() => patch("singleItem", { discountType: "percentage" })}
                />
                Percentage
              </label>
            </div>
          </fieldset>
          <div>
            <label className={labelBase}>Discount priority</label>
            <select
              className={inputBase}
              value={value.singleItem.discountPriority}
              onChange={(e) => patch("singleItem", { discountPriority: e.target.value as MixMatchPriority })}
            >
              {priorityOptions.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          <OverrideField />
          <div>
            <label className={labelBase}>Specify discount value</label>
            <div className="flex">
              <span className="flex shrink-0 items-center rounded-l-md border border-r-0 border-outline-variant/20 bg-surface-container-high px-3 text-xs font-bold text-on-surface-variant">
                {siSuffix}
              </span>
              <input
                type="number"
                min="0"
                step={value.singleItem.discountType === "percentage" ? "0.1" : "0.01"}
                className={`${inputBase} rounded-l-none`}
                placeholder="0"
                value={value.singleItem.discountValue}
                onChange={(e) => patch("singleItem", { discountValue: e.target.value })}
              />
            </div>
          </div>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className={labelBase}>No. of qualifying products per set</label>
              <input
                type="number"
                min="1"
                step="1"
                className={inputBase}
                placeholder="e.g. 3"
                value={value.singleItem.qualifyingProductsPerSet}
                onChange={(e) => patch("singleItem", { qualifyingProductsPerSet: e.target.value })}
              />
            </div>
            <div>
              <label className={labelBase}>Maximum no. of discounted product sets</label>
              <input
                type="number"
                min="0"
                step="1"
                className={inputBase}
                placeholder="e.g. 2"
                value={value.singleItem.maxDiscountedProductSets}
                onChange={(e) => patch("singleItem", { maxDiscountedProductSets: e.target.value })}
              />
            </div>
          </div>
        </div>
      )}

      {value.logicMode === "per-bundle" && (
        <div className="space-y-5 border-t border-outline-variant/10 pt-5">
          <fieldset className="space-y-2">
            <legend className={labelBase}>Discount type</legend>
            <div className="flex flex-wrap gap-2">
              {(
                [
                  ["fixed", "Fixed amount"],
                  ["percentage", "Percentage"],
                  ["set-price", "Set price"],
                ] as const
              ).map(([id, lab]) => (
                <label key={id} className={radioRow}>
                  <input
                    type="radio"
                    name="mm-pb-type"
                    className="h-3.5 w-3.5 accent-primary"
                    checked={value.perBundle.discountType === id}
                    onChange={() => patch("perBundle", { discountType: id })}
                  />
                  {lab}
                </label>
              ))}
            </div>
          </fieldset>
          <div>
            <label className={labelBase}>Discount priority</label>
            <select
              className={inputBase}
              value={value.perBundle.discountPriority}
              onChange={(e) => patch("perBundle", { discountPriority: e.target.value as MixMatchPriority })}
            >
              {priorityOptions.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          <OverrideField />
          <div>
            <label className={labelBase}>{pbLabel}</label>
            <div className="flex">
              <span className="flex shrink-0 items-center rounded-l-md border border-r-0 border-outline-variant/20 bg-surface-container-high px-3 text-xs font-bold text-on-surface-variant">
                {value.perBundle.discountType === "set-price" ? "$" : pbSuffix}
              </span>
              <input
                type="number"
                min="0"
                step={value.perBundle.discountType === "percentage" ? "0.1" : "0.01"}
                className={`${inputBase} rounded-l-none`}
                placeholder="0"
                value={value.perBundle.bundledAmount}
                onChange={(e) => patch("perBundle", { bundledAmount: e.target.value })}
              />
            </div>
            <p className={hintText}>
              {value.perBundle.discountType === "set-price"
                ? "Total bundle price after discount logic."
                : "Discount off the bundle subtotal."}
            </p>
          </div>
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <div>
              <label className={labelBase}>No. of qualifying products per set</label>
              <input
                type="number"
                min="1"
                step="1"
                className={inputBase}
                placeholder="e.g. 3"
                value={value.perBundle.qualifyingProductsPerSet}
                onChange={(e) => patch("perBundle", { qualifyingProductsPerSet: e.target.value })}
              />
            </div>
            <div>
              <label className={labelBase}>Maximum no. of discounted product sets</label>
              <input
                type="number"
                min="0"
                step="1"
                className={inputBase}
                placeholder="e.g. 2"
                value={value.perBundle.maxDiscountedProductSets}
                onChange={(e) => patch("perBundle", { maxDiscountedProductSets: e.target.value })}
              />
            </div>
          </div>
        </div>
      )}

      {value.logicMode === "incremental" && (
        <div className="space-y-5 border-t border-outline-variant/10 pt-5">
          <fieldset className="space-y-2">
            <legend className={labelBase}>Discount type</legend>
            <div className="flex flex-wrap gap-2">
              <label className={radioRow}>
                <input
                  type="radio"
                  name="mm-inc-type"
                  className="h-3.5 w-3.5 accent-primary"
                  checked={value.incremental.discountType === "fixed"}
                  onChange={() => patch("incremental", { discountType: "fixed" })}
                />
                Fixed amount
              </label>
              <label className={radioRow}>
                <input
                  type="radio"
                  name="mm-inc-type"
                  className="h-3.5 w-3.5 accent-primary"
                  checked={value.incremental.discountType === "percentage"}
                  onChange={() => patch("incremental", { discountType: "percentage" })}
                />
                Percentage
              </label>
            </div>
          </fieldset>
          <div>
            <label className={labelBase}>Discount priority</label>
            <select
              className={inputBase}
              value={value.incremental.discountPriority}
              onChange={(e) => patch("incremental", { discountPriority: e.target.value as MixMatchPriority })}
            >
              {priorityOptions.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          <OverrideField />
          <div className="space-y-3">
            <p className={labelBase}>Specify discount value (tiered)</p>
            <div>
              <label className="text-xs font-bold uppercase tracking-wide text-on-surface-variant">Base discount</label>
              <div className="mt-1 flex">
                <span className="flex shrink-0 items-center rounded-l-md border border-r-0 border-outline-variant/20 bg-surface-container-high px-3 text-xs font-bold text-on-surface-variant">
                  {incSuffix}
                </span>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  className={`${inputBase} rounded-l-none`}
                  value={value.incremental.baseDiscount}
                  onChange={(e) => patch("incremental", { baseDiscount: e.target.value })}
                />
              </div>
            </div>
            {(
              [
                ["additionalQualifying1", "1 additional qualifying item"],
                ["additionalQualifying2", "2 additional qualifying items"],
                ["additionalQualifying3", "3 additional qualifying items"],
                ["additionalQualifying4", "4 additional qualifying items"],
                ["additionalQualifying5", "5 additional qualifying items"],
                ["additionalQualifying6", "6 additional qualifying items"],
              ] as const
            ).map(([field, lab]) => (
              <div key={field}>
                <label className="text-xs font-bold uppercase tracking-wide text-on-surface-variant">{lab}</label>
                <div className="mt-1 flex">
                  <span className="flex shrink-0 items-center rounded-l-md border border-r-0 border-outline-variant/20 bg-surface-container-high px-3 text-xs font-bold text-on-surface-variant">
                    {incSuffix}
                  </span>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className={`${inputBase} rounded-l-none`}
                    value={value.incremental[field]}
                    onChange={(e) =>
                      onChange({
                        ...value,
                        incremental: { ...value.incremental, [field]: e.target.value },
                      })
                    }
                  />
                </div>
              </div>
            ))}
          </div>
          <div>
            <label className={labelBase}>No. of qualifying products for base discount</label>
            <input
              type="number"
              min="1"
              step="1"
              className={inputBase}
              placeholder="e.g. 2"
              value={value.incremental.qualifyingProductsForBase}
              onChange={(e) => patch("incremental", { qualifyingProductsForBase: e.target.value })}
            />
            <p className={hintText}>Items required before base tier applies; further tiers use additional counts.</p>
          </div>
        </div>
      )}
    </div>
  );
}
