import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  endImpersonation,
  fetchMe,
  isImpersonating,
  login as identityLogin,
  logout as identityLogout,
  readClaims,
  startImpersonation,
  type Role,
  type SessionClaims,
  type StartImpersonationResponse,
} from "./identityClient";

interface AuthContextValue {
  claims: SessionClaims | null;
  impersonating: boolean;
  login(email: string, password: string): Promise<SessionClaims>;
  logout(): void;
  refresh(): Promise<void>;
  impersonate(targetTenantId: string, reason: string, assumedRole?: Role): Promise<StartImpersonationResponse>;
  endImpersonation(): Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [claims, setClaims] = useState<SessionClaims | null>(() => readClaims());
  const [impersonating, setImpersonating] = useState<boolean>(() => isImpersonating());

  const refresh = useCallback(async () => {
    try {
      const next = await fetchMe();
      setClaims(next);
      setImpersonating(isImpersonating());
    } catch {
      setClaims(null);
      setImpersonating(false);
    }
  }, []);

  useEffect(() => {
    if (claims === null) {
      // No claims in storage; don't ping /me — caller (LoginPage) will trigger login.
      return;
    }
    void refresh();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const value = useMemo<AuthContextValue>(() => ({
    claims,
    impersonating,
    async login(email, password) {
      await identityLogin(email, password);
      const next = readClaims();
      setClaims(next);
      setImpersonating(false);
      return next!;
    },
    logout() {
      identityLogout();
      setClaims(null);
      setImpersonating(false);
    },
    refresh,
    async impersonate(targetTenantId, reason, assumedRole) {
      const res = await startImpersonation(targetTenantId, reason, assumedRole);
      setClaims(readClaims());
      setImpersonating(true);
      return res;
    },
    async endImpersonation() {
      await endImpersonation();
      setClaims(readClaims());
      setImpersonating(false);
    },
  }), [claims, impersonating, refresh]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside <AuthProvider>");
  return ctx;
}
