import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getOrder, type OrderDetail } from "../lib/api/ordersApi";
import { useStoreScope } from "../lib/commerce/StoreContextProvider";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

export function OrderDetailPage() {
  const { orderId = "" } = useParams();
  const scope = useStoreScope();
  const { session } = useCustomerSession();
  const [order, setOrder] = useState<OrderDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session || !orderId) return;
    let cancelled = false;
    void getOrder({ ...scope, token: session.token }, orderId)
      .then(o => {
        if (!cancelled) setOrder(o);
      })
      .catch(e => {
        if (!cancelled) setError((e as Error).message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [orderId, scope, session]);

  if (loading) return <p className="sf-skeleton" role="status">Loading order…</p>;
  if (error) return <p className="sf-alert" role="alert">{error}</p>;
  if (!order) return <p className="sf-empty">Order not found.</p>;

  return (
    <section className="sf-page">
      <p><Link to="/orders">← Back to orders</Link></p>
      <h1>Order {order.orderId}</h1>
      <p>Status: <strong>{order.status}</strong></p>
      <p>
        Total: {order.total.amount} {order.total.currency}
      </p>
      {order.shippingAddress && (
        <address className="sf-address">
          {order.shippingAddress.line1}
          <br />
          {order.shippingAddress.city} {order.shippingAddress.postalCode}
          <br />
          {order.shippingAddress.countryCode}
        </address>
      )}
      {order.lines && order.lines.length > 0 && (
        <ul className="sf-order-lines">
          {order.lines.map(line => (
            <li key={line.lineId}>
              {line.productNameSnapshot ?? line.productId} × {line.quantity}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
