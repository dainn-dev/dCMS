const labelBase = "block text-[0.6875rem] font-bold text-on-surface-variant uppercase tracking-wider mb-1";
const inputBase =
  "w-full bg-surface-container-lowest border border-outline-variant/20 rounded-md py-2 px-3 text-xs focus:ring-1 focus:ring-primary outline-none";
const hintText = "mt-1 text-xs text-on-surface-variant";

const radioCard =
  "flex cursor-pointer items-start gap-3 rounded-lg border border-outline-variant/20 bg-surface-container-low p-3 transition-colors hover:border-primary/30";

export type AfterSalesMechanicsValue = {
  qualifyingProductsInCart: string;
  maxDiscountedProductSets: string;
  maxRewardPerOrder: string;
  emailSubject: string;
  emailTemplate: string;
  promotionCodeType: "standard" | "group";
  promotionCode: string;
  promoCodePrefix: string;
};

export function defaultAfterSalesMechanicsValue(): AfterSalesMechanicsValue {
  return {
    qualifyingProductsInCart: "",
    maxDiscountedProductSets: "",
    maxRewardPerOrder: "",
    emailSubject: "",
    emailTemplate: "",
    promotionCodeType: "standard",
    promotionCode: "",
    promoCodePrefix: "",
  };
}

type Props = {
  value: AfterSalesMechanicsValue;
  onChange: (next: AfterSalesMechanicsValue) => void;
};

export function AfterSalesMechanicsSection({ value, onChange }: Props) {
  function patch(p: Partial<AfterSalesMechanicsValue>) {
    onChange({ ...value, ...p });
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
        <div>
          <label className={labelBase}>No. of qualifying products in cart</label>
          <input
            type="number"
            min="0"
            step="1"
            className={inputBase}
            placeholder="e.g. 1"
            value={value.qualifyingProductsInCart}
            onChange={(e) => patch({ qualifyingProductsInCart: e.target.value })}
          />
          <p className={hintText}>Minimum cart lines to trigger the after-sales email.</p>
        </div>
        <div>
          <label className={labelBase}>Max number of discounted product sets</label>
          <input
            type="number"
            min="0"
            step="1"
            className={inputBase}
            placeholder="e.g. 1"
            value={value.maxDiscountedProductSets}
            onChange={(e) => patch({ maxDiscountedProductSets: e.target.value })}
          />
        </div>
        <div>
          <label className={labelBase}>Max reward per order</label>
          <input
            type="number"
            min="0"
            step="1"
            className={inputBase}
            placeholder="e.g. 1"
            value={value.maxRewardPerOrder}
            onChange={(e) => patch({ maxRewardPerOrder: e.target.value })}
          />
          <p className={hintText}>Cap on promo rewards issued from a single qualifying order.</p>
        </div>
      </div>

      <div>
        <label className={labelBase}>Email subject</label>
        <input
          type="text"
          className={inputBase}
          placeholder="e.g. Thanks for your order — here’s something for next time"
          value={value.emailSubject}
          onChange={(e) => patch({ emailSubject: e.target.value })}
        />
      </div>

      <div>
        <label className={labelBase}>Email template</label>
        <textarea
          className={`${inputBase} min-h-[160px] resize-y font-mono`}
          placeholder={"Hi {{customer_name}},\n\nThank you for your purchase…\n\nYour code: {{promo_code}}\n"}
          value={value.emailTemplate}
          onChange={(e) => patch({ emailTemplate: e.target.value })}
        />
        <p className={hintText}>Plain textarea for now; merge tags like {'{{promo_code}}'} are hints for future templating.</p>
      </div>

      <fieldset className="space-y-2">
        <legend className={labelBase}>Promotion code type</legend>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          <label className={radioCard}>
            <input
              type="radio"
              name="asp-code-type"
              className="mt-0.5 h-3.5 w-3.5 accent-primary shrink-0"
              checked={value.promotionCodeType === "standard"}
              onChange={() => patch({ promotionCodeType: "standard" })}
            />
            <div>
              <span className="text-xs font-semibold text-on-surface">Standard</span>
              <p className="mt-0.5 text-xs text-on-surface-variant">Single fixed promo code in the email.</p>
            </div>
          </label>
          <label className={radioCard}>
            <input
              type="radio"
              name="asp-code-type"
              className="mt-0.5 h-3.5 w-3.5 accent-primary shrink-0"
              checked={value.promotionCodeType === "group"}
              onChange={() => patch({ promotionCodeType: "group" })}
            />
            <div>
              <span className="text-xs font-semibold text-on-surface">Group</span>
              <p className="mt-0.5 text-xs text-on-surface-variant">Prefix for generated / pooled codes.</p>
            </div>
          </label>
        </div>
      </fieldset>

      {value.promotionCodeType === "standard" ? (
        <div>
          <label className={labelBase}>Promotion code</label>
          <input
            type="text"
            className={inputBase}
            placeholder="e.g. COMEBACK10"
            value={value.promotionCode}
            onChange={(e) => patch({ promotionCode: e.target.value.toUpperCase() })}
          />
        </div>
      ) : (
        <div>
          <label className={labelBase}>Promo code prefix</label>
          <input
            type="text"
            className={inputBase}
            placeholder="e.g. REORDER-"
            value={value.promoCodePrefix}
            onChange={(e) => patch({ promoCodePrefix: e.target.value.toUpperCase() })}
          />
          <p className={hintText}>System may append random suffix per customer (demo).</p>
        </div>
      )}
    </div>
  );
}
