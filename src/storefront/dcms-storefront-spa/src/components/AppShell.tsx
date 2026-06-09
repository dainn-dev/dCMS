import { Link, Outlet, useLocation } from "react-router-dom";
import { useEffect } from "react";
import { BranchPicker } from "./BranchPicker";
import { useBranch } from "../lib/branch/BranchProvider";
import { useCart } from "../lib/cart/CartProvider";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

const ROUTE_TITLES: Record<string, string> = {
  "/": "Shop",
  "/cart": "Cart",
  "/login": "Sign in",
  "/checkout": "Checkout",
  "/orders": "Your orders",
};

export function AppShell() {
  const { active, bootstrap, error } = useBranch();
  const { itemCount } = useCart();
  const { session } = useCustomerSession();
  const location = useLocation();

  useEffect(() => {
    const base = ROUTE_TITLES[location.pathname] ?? "dCMS Storefront";
    document.title = `${base} · dCMS`;
  }, [location.pathname]);

  return (
    <div className="sf-app">
      <a href="#main" className="sf-skip-link">Skip to content</a>
      <header className="sf-header">
        <div className="sf-header__brand">
          <Link to="/" className="sf-logo">dCMS Shop</Link>
          {active && <span className="sf-branch-pill">{active.name}</span>}
        </div>
        <nav className="sf-nav" aria-label="Store">
          <Link to="/">Products</Link>
          <Link to="/cart" aria-live="polite">
            Cart{itemCount > 0 ? ` (${itemCount})` : ""}
          </Link>
          {session ? (
            <>
              <Link to="/orders">Orders</Link>
              <span className="sf-account">{session.displayName}</span>
            </>
          ) : (
            <Link to="/login">Sign in</Link>
          )}
          <BranchPicker />
        </nav>
      </header>

      {bootstrap === "resolving" && (
        <p className="sf-banner" role="status">Detecting your nearest branch…</p>
      )}
      {error && <p className="sf-alert" role="alert">{error}</p>}

      <main id="main" className="sf-main">
        <Outlet />
      </main>
    </div>
  );
}
