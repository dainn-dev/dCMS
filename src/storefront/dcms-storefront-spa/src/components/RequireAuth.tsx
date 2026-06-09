import { Navigate, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { useCustomerSession } from "../lib/session/CustomerSessionProvider";

export function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useCustomerSession();
  const location = useLocation();
  if (!session) {
    const returnUrl = `${location.pathname}${location.search}`;
    return <Navigate to={`/login?returnUrl=${encodeURIComponent(returnUrl)}`} replace />;
  }
  return <>{children}</>;
}
