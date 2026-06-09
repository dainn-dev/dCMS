import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { getOrder } from "../lib/api/ordersApi";
import { useStoreScope } from "../lib/commerce/StoreContextProvider";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

export function CheckoutReturnPage() {
  const [params] = useSearchParams();
  const orderId = params.get("orderId") ?? "";
  const initialStatus = params.get("status") ?? "payment_pending";
  const scope = useStoreScope();
  const { session } = useCustomerSession();
  const [status, setStatus] = useState(initialStatus);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!session || !orderId) return;
    void getOrder({ ...scope, token: session.token }, orderId)
      .then(o => setStatus(o.status))
      .catch(e => setError((e as Error).message));
  }, [orderId, scope, session]);

  const isSuccess = status === "confirmed" || status === "processing";
  const isFailed = status === "payment_failed" || status === "cancelled";

  return (
    <section className="sf-page sf-narrow">
      <h1>Payment result</h1>
      {orderId && <p>Order <code>{orderId}</code></p>}
      {isSuccess && <p className="sf-success" role="status">Thank you — your order is confirmed.</p>}
      {isFailed && <p className="sf-alert" role="alert">Payment failed or order was cancelled.</p>}
      {!isSuccess && !isFailed && (
        <p role="status">Status: <strong>{status}</strong> — still pending.</p>
      )}
      {error && <p className="sf-alert" role="alert">{error}</p>}
      <p>
        <Link to="/orders">View orders</Link>
        {" · "}
        <Link to="/">Continue shopping</Link>
      </p>
    </section>
  );
}
