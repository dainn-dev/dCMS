import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { login as apiLogin } from "../api/identityApi";
import {
  clearCustomerSession,
  readCustomerSession,
  writeCustomerSession,
  type CustomerSessionSnapshot,
} from "./customerSession";

interface CustomerSessionValue {
  session: CustomerSessionSnapshot | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  refresh: () => void;
}

const CustomerSessionContext = createContext<CustomerSessionValue | null>(null);

export function CustomerSessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<CustomerSessionSnapshot | null>(() => readCustomerSession());

  const refresh = useCallback(() => {
    setSession(readCustomerSession());
  }, []);

  const logout = useCallback(() => {
    clearCustomerSession();
    setSession(null);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const result = await apiLogin(email, password);
    writeCustomerSession(result.accessToken, result.expiresAt, result.displayName);
    setSession(readCustomerSession());
  }, []);

  const value = useMemo(
    () => ({ session, login, logout, refresh }),
    [session, login, logout, refresh],
  );

  return (
    <CustomerSessionContext.Provider value={value}>{children}</CustomerSessionContext.Provider>
  );
}

export function useCustomerSession(): CustomerSessionValue {
  const ctx = useContext(CustomerSessionContext);
  if (!ctx) throw new Error("useCustomerSession must be used inside CustomerSessionProvider");
  return ctx;
}
