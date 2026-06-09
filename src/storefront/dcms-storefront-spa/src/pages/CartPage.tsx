import { Link } from "react-router-dom";
import { CartLineRow } from "../components/CartLine";
import { useCart } from "../lib/cart/CartProvider";

export function CartPage() {
  const { lines, itemCount, subtotal, setQuantity, remove } = useCart();

  if (itemCount === 0) {
    return (
      <section className="sf-page">
        <h1>Your cart</h1>
        <p className="sf-empty" role="status">Your cart is empty.</p>
        <Link to="/" className="sf-btn">Continue shopping</Link>
      </section>
    );
  }

  const currency = lines[0]?.currency ?? "VND";

  return (
    <section className="sf-page">
      <h1>Your cart</h1>
      <ul className="sf-cart-list">
        {lines.map(line => (
          <CartLineRow
            key={line.variantId}
            line={line}
            onQuantityChange={qty => setQuantity(line.variantId, qty)}
            onRemove={() => remove(line.variantId)}
          />
        ))}
      </ul>
      <p className="sf-cart-summary">
        Subtotal ({itemCount} items): {subtotal} {currency}
      </p>
      <div className="sf-actions">
        <Link to="/" className="sf-btn sf-btn--secondary">Continue shopping</Link>
        <Link to="/checkout" className="sf-btn">Checkout</Link>
      </div>
    </section>
  );
}
