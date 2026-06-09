import { createContext, useContext, useMemo, type ReactNode } from "react";
import { useBranch } from "../branch/BranchProvider";
import { resolveStoreScope, type StoreScope } from "./storeContext";

const StoreContext = createContext<StoreScope | null>(null);

export function StoreContextProvider({ children }: { children: ReactNode }) {
  const { active } = useBranch();
  const scope = useMemo(
    () => (active ? resolveStoreScope(active.tenantId) : null),
    [active],
  );
  return <StoreContext.Provider value={scope}>{children}</StoreContext.Provider>;
}

export function useStoreScope(): StoreScope {
  const ctx = useContext(StoreContext);
  if (!ctx) throw new Error("Store scope not ready — pick a branch first.");
  return ctx;
}

export function useOptionalStoreScope(): StoreScope | null {
  return useContext(StoreContext);
}
