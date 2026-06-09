import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { listOrders, type OrderListItem } from "../lib/api/ordersApi";
import { useStoreScope } from "../lib/commerce/StoreContextProvider";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

export function OrdersPage() {
  const scope = useStoreScope();
  const { session } = useCustomerSession();
  const [items, setItems] = useState<OrderListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session) return;
    let cancelled = false;
    setLoading(true);
    void listOrders({ ...scope, token: session.token })
      .then(page => {
        if (!cancelled) setItems(page.items);
      })
      .catch(e => {
        if (!cancelled) setError((e as Error).message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [scope, session]);

  return (
    <section className="sf-page">
      <h1>Your orders</h1>
      {loading && <p className="sf-skeleton" role="status">Loading orders…</p>}
      {error && <p className="sf-alert" role="alert">{error}</p>}
      {!loading && !error && items.length === 0 && (
        <p className="sf-empty" role="status">No orders yet.</p>
      )}
      <ul className="sf-order-list">
        {items.map(o => (
          <li key={o.orderId}>
            <Link to={`/orders/${o.orderId}`}>
              <strong>{o.orderId.slice(0, 8)}…</strong>
              {" — "}
              {o.status}
              {" — "}
              {o.totalAmount} {o.currency}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
