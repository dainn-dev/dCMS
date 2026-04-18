const labelBase = "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-xs text-on-surface-variant";

const radioCard =
  "flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-3 transition-colors hover:border-primary/30";

export type ProductDiscountMechanicsValue = {
  discountType: "fixed" | "percentage";
  discountMode: "add" | "override";
  discountValue: string;
  qualifyingProductsToAvail: string;
};

export function defaultProductDiscountMechanicsValue(): ProductDiscountMechanicsValue {
  return {
    discountType: "fixed",
    discountMode: "add",
    discountValue: "",
    qualifyingProductsToAvail: "",
  };
}

type Props = {
  value: ProductDiscountMechanicsValue;
  onChange: (next: ProductDiscountMechanicsValue) => void;
};

export function ProductDiscountMechanicsSection({ value, onChange }: Props) {
  function patch(p: Partial<ProductDiscountMechanicsValue>) {
    onChange({ ...value, ...p });
  }

  const suffix = value.discountType === "percentage" ? "%" : "$";
  const valueLabel = value.discountType === "percentage" ? "Discount (%)" : "Discount amount";

  return (
    <div className="space-y-6">
      <fieldset className="space-y-2">
        <legend className={labelBase}>Discount type</legend>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          <label className={radioCard}>
            <input
              type="radio"
              name="pd-discount-type"
              className="mt-0.5 h-3.5 w-3.5 accent-primary shrink-0"
              checked={value.discountType === "fixed"}
              onChange={() => patch({ discountType: "fixed" })}
            />
            <div>
              <span className="text-xs font-semibold text-on-surface">Fixed amount</span>
              <p className="mt-0.5 text-xs text-on-surface-variant">Subtract a flat currency amount per eligible unit.</p>
            </div>
          </label>
          <label className={radioCard}>
            <input
              type="radio"
              name="pd-discount-type"
              className="mt-0.5 h-3.5 w-3.5 accent-primary shrink-0"
              checked={value.discountType === "percentage"}
              onChange={() => patch({ discountType: "percentage" })}
            />
            <div>
              <span className="text-xs font-semibold text-on-surface">Percentage</span>
              <p className="mt-0.5 text-xs text-on-surface-variant">Reduce price by a percent off qualifying lines.</p>
            </div>
          </label>
        </div>
      </fieldset>

      <fieldset className="space-y-2">
        <legend className={labelBase}>Discount mode</legend>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          <label className={radioCard}>
            <input
              type="radio"
              name="pd-discount-mode"
              className="mt-0.5 h-3.5 w-3.5 accent-primary shrink-0"
              checked={value.discountMode === "add"}
              onChange={() => patch({ discountMode: "add" })}
            />
            <div>
              <span className="text-xs font-semibold text-on-surface">Add (stack)</span>
              <p className="mt-0.5 text-xs text-on-surface-variant">Combine with other eligible discounts.</p>
            </div>
          </label>
          <label className={radioCard}>
            <input
              type="radio"
              name="pd-discount-mode"
              className="mt-0.5 h-3.5 w-3.5 accent-primary shrink-0"
              checked={value.discountMode === "override"}
              onChange={() => patch({ discountMode: "override" })}
            />
            <div>
              <span className="text-xs font-semibold text-on-surface">Override</span>
              <p className="mt-0.5 text-xs text-on-surface-variant">Replace other discounts on the same lines.</p>
            </div>
          </label>
        </div>
      </fieldset>

      <div>
        <label className={labelBase}>{valueLabel}</label>
        <div className="flex">
          <span className="flex shrink-0 items-center rounded-l-md border border-r-0 border-outline-variant/20 bg-surface-container-high px-3 text-xs font-bold text-on-surface-variant">
            {suffix}
          </span>
          <input
            type="number"
            min="0"
            step={value.discountType === "percentage" ? "0.1" : "0.01"}
            className={`${inputBase} rounded-l-none`}
            placeholder="0"
            value={value.discountValue}
            onChange={(e) => patch({ discountValue: e.target.value })}
          />
        </div>
        <p className={hintText}>Prefix shows $ or % based on discount type.</p>
      </div>

      <div>
        <label className={labelBase}>No. of qualifying products to avail</label>
        <input
          type="number"
          min="0"
          step="1"
          className={inputBase}
          placeholder="e.g. 1"
          value={value.qualifyingProductsToAvail}
          onChange={(e) => patch({ qualifyingProductsToAvail: e.target.value })}
        />
        <p className={hintText}>How many discounted units the customer can receive when rules are met.</p>
      </div>
    </div>
  );
}
