import { FormEvent, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useCart } from "../lib/cart/CartProvider";
import { useStoreScope } from "../lib/commerce/StoreContextProvider";
import { createOrder } from "../lib/api/ordersApi";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

export function CheckoutPage() {
  const scope = useStoreScope();
  const { session } = useCustomerSession();
  const { lines, empty } = useCart();
  const navigate = useNavigate();
  const [name, setName] = useState(session?.displayName ?? "");
  const [email, setEmail] = useState("customer@aeon.test");
  const [phone, setPhone] = useState("");
  const [line1, setLine1] = useState("");
  const [city, setCity] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [countryCode, setCountryCode] = useState("VN");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  if (lines.length === 0) {
    return (
      <section className="sf-page">
        <h1>Checkout</h1>
        <p className="sf-empty">Your cart is empty.</p>
        <Link to="/" className="sf-btn">Shop products</Link>
      </section>
    );
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!session) return;
    setLoading(true);
    setError(null);
    try {
      const result = await createOrder(
        { tenantId: scope.tenantId, storeId: scope.storeId, token: session.token },
        {
          customerId: session.customerId,
          customerName: name,
          customerEmail: email,
          customerPhone: phone,
          shippingAddress: { line1, city, postalCode, countryCode },
          lines,
        },
      );
      empty();
      navigate(`/checkout/pay/${result.orderId}`, { replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="sf-page sf-narrow">
      <h1>Checkout</h1>
      <form className="sf-form" onSubmit={e => void onSubmit(e)}>
        <label>
          Full name
          <input required value={name} onChange={e => setName(e.target.value)} />
        </label>
        <label>
          Email
          <input type="email" required value={email} onChange={e => setEmail(e.target.value)} />
        </label>
        <label>
          Phone
          <input type="tel" value={phone} onChange={e => setPhone(e.target.value)} />
        </label>
        <label>
          Address
          <input required value={line1} onChange={e => setLine1(e.target.value)} />
        </label>
        <label>
          City
          <input required value={city} onChange={e => setCity(e.target.value)} />
        </label>
        <label>
          Postal code
          <input required value={postalCode} onChange={e => setPostalCode(e.target.value)} />
        </label>
        <label>
          Country
          <input required value={countryCode} onChange={e => setCountryCode(e.target.value)} />
        </label>
        {error && <p className="sf-alert" role="alert">{error}</p>}
        <button type="submit" className="sf-btn" disabled={loading}>
          {loading ? "Placing order…" : "Place order"}
        </button>
      </form>
    </section>
  );
}
