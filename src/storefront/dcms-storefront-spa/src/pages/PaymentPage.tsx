import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getOrder, getOrderPayment, stubPaymentUrl } from "../lib/api/ordersApi";
import { useStoreScope } from "../lib/commerce/StoreContextProvider";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

const POLL_MS = 2000;
const MAX_POLL_MS = 30_000;

export function PaymentPage() {
  const { orderId = "" } = useParams();
  const scope = useStoreScope();
  const { session } = useCustomerSession();
  const [status, setStatus] = useState<string>("payment_pending");
  const [paymentUrl, setPaymentUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [timedOut, setTimedOut] = useState(false);

  useEffect(() => {
    if (!session || !orderId) return;
    let cancelled = false;
    const started = Date.now();

    async function poll() {
      try {
        const [order, payment] = await Promise.all([
          getOrder({ ...scope, token: session!.token }, orderId),
          getOrderPayment({ ...scope, token: session!.token }, orderId),
        ]);
        if (cancelled) return;
        setStatus(order.status);
        const ext = payment.components[0]?.externalRef;
        setPaymentUrl(stubPaymentUrl(orderId, ext));
        if (order.status === "confirmed" || order.status === "payment_failed") return;
        if (Date.now() - started >= MAX_POLL_MS) {
          setTimedOut(true);
          return;
        }
        window.setTimeout(() => void poll(), POLL_MS);
      } catch (e) {
        if (!cancelled) setError((e as Error).message);
      }
    }

    void poll();
    return () => { cancelled = true; };
  }, [orderId, scope, session]);

  return (
    <section className="sf-page sf-narrow">
      <h1>Payment</h1>
      <p>Order <code>{orderId}</code></p>
      <p role="status">Status: <strong>{status}</strong></p>
      {paymentUrl && (
        <p>
          <a href={paymentUrl} className="sf-btn" target="_blank" rel="noreferrer">
            Continue to payment
          </a>
        </p>
      )}
      {timedOut && (
        <p className="sf-muted" role="status">
          Payment is still processing. Refresh or check order history shortly.
        </p>
      )}
      {error && <p className="sf-alert" role="alert">{error}</p>}
      <p>
        <Link to={`/checkout/return?orderId=${encodeURIComponent(orderId)}&status=${encodeURIComponent(status)}`}>
          View payment result
        </Link>
        {" · "}
        <Link to="/orders">Order history</Link>
      </p>
    </section>
  );
}
