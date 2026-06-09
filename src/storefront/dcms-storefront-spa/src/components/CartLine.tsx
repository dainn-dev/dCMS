import type { CartLine as CartLineType } from "../lib/cart/cartStorage";

interface Props {
  line: CartLineType;
  onQuantityChange: (qty: number) => void;
  onRemove: () => void;
}

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}

export function CartLineRow({ line, onQuantityChange, onRemove }: Props) {
  return (
    <li className="sf-cart-line">
      <div className="sf-cart-line__info">
        <strong>{line.name}</strong>
        <span className="sf-muted">SKU {line.sku}</span>
      </div>
      <div className="sf-cart-line__qty">
        <label>
          Qty
          <input
            type="number"
            min={1}
            value={line.quantity}
            onChange={e => onQuantityChange(Number(e.target.value) || 1)}
          />
        </label>
      </div>
      <div className="sf-cart-line__total">
        {formatMoney(line.unitPrice * line.quantity, line.currency)}
      </div>
      <button type="button" className="sf-btn sf-btn--ghost" onClick={onRemove}>
        Remove
      </button>
    </li>
  );
}
