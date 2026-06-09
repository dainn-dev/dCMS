import { useMemo, useState } from "react";
import type { VariantCombination } from "../lib/api/catalogApi";

interface Props {
  combinations: Record<string, VariantCombination>;
  onSelect: (key: string, variant: VariantCombination) => void;
}

export function VariantPicker({ combinations, onSelect }: Props) {
  const entries = useMemo(
    () => Object.entries(combinations).filter(([, v]) => v.inStock),
    [combinations],
  );
  const [selected, setSelected] = useState<string>(entries[0]?.[0] ?? "");

  if (entries.length === 0) {
    return <p className="sf-muted" role="status">No variants in stock.</p>;
  }

  return (
    <fieldset className="sf-variant-picker">
      <legend>Choose variant</legend>
      <div className="sf-variant-picker__options">
        {entries.map(([key, variant]) => (
          <label key={key} className="sf-variant-option">
            <input
              type="radio"
              name="variant"
              value={key}
              checked={selected === key}
              onChange={() => {
                setSelected(key);
                onSelect(key, variant);
              }}
            />
            <span>{variant.sku}</span>
            <span className="sf-variant-option__price">{variant.basePriceAmount}</span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}
