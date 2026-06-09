import { FormEvent, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

export function LoginPage() {
  const { login } = useCustomerSession();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const returnUrl = params.get("returnUrl") ?? "/checkout";
  const [email, setEmail] = useState("customer@aeon.test");
  const [password, setPassword] = useState("ChangeMe123!");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await login(email.trim(), password);
      navigate(returnUrl, { replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="sf-page sf-narrow">
      <h1>Sign in</h1>
      <p className="sf-muted">Sign in to complete checkout and view your orders.</p>
      <form className="sf-form" onSubmit={e => void onSubmit(e)}>
        <label>
          Email
          <input
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={e => setEmail(e.target.value)}
          />
        </label>
        <label>
          Password
          <input
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={e => setPassword(e.target.value)}
          />
        </label>
        {error && <p className="sf-alert" role="alert">{error}</p>}
        <button type="submit" className="sf-btn" disabled={loading}>
          {loading ? "Signing in…" : "Sign in"}
        </button>
      </form>
      <p><Link to="/">← Back to shop</Link></p>
    </section>
  );
}
