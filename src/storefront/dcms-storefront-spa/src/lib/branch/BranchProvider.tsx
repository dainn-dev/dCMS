import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  clearActiveTenant,
  findNearestBranch,
  listBranches,
  readActiveTenant,
  writeActiveTenant,
  type Branch,
  type NearestBranch,
} from "../api/client";

// DAI-751 / US-4 (T4.3): Storefront branch context.
// Bootstrap order on app mount:
//   1. If localStorage has dcms.active-tenant → trust it, lazy-validate against /branches.
//   2. Else request geolocation → /branches/nearest → adopt the resolved tenant.
//   3. Geolocation denied or out-of-range → /branches/nearest returns a default fallback (server-side US-3 behaviour).
//   4. Failure → first active branch from /branches as last resort.

export type BootstrapState = "idle" | "resolving" | "ready" | "error";

interface BranchContextValue {
  branches: Branch[];
  active: Branch | null;
  bootstrap: BootstrapState;
  error: string | null;
  selectBranch: (tenantId: string) => void;
  refresh: () => Promise<void>;
  resolveByGeolocation: () => Promise<void>;
}

const BranchContext = createContext<BranchContextValue | null>(null);

export function BranchProvider({ children }: { children: ReactNode }) {
  const [branches, setBranches] = useState<Branch[]>([]);
  const [active, setActive] = useState<Branch | null>(null);
  const [bootstrap, setBootstrap] = useState<BootstrapState>("idle");
  const [error, setError] = useState<string | null>(null);

  const setActiveByTenant = useCallback((list: Branch[], tenantId: string | null): Branch | null => {
    if (!tenantId) return null;
    const match = list.find(b => b.tenantId === tenantId) ?? null;
    if (match) {
      writeActiveTenant(match.tenantId);
      setActive(match);
    }
    return match;
  }, []);

  const refresh = useCallback(async () => {
    const list = await listBranches();
    setBranches(list);
    const stored = readActiveTenant();
    const adopted = setActiveByTenant(list, stored);
    if (!adopted && stored) {
      // Stored tenant no longer valid — clear and let bootstrap re-resolve.
      clearActiveTenant();
      setActive(null);
    }
  }, [setActiveByTenant]);

  const resolveByGeolocation = useCallback(async () => {
    setBootstrap("resolving");
    setError(null);

    try {
      const list = await listBranches();
      setBranches(list);

      const stored = readActiveTenant();
      if (stored && setActiveByTenant(list, stored)) {
        setBootstrap("ready");
        return;
      }

      // No stored selection — try geolocation.
      const coords = await getGeolocation().catch(() => null);
      if (coords) {
        try {
          const nearest = await findNearestBranch(coords.latitude, coords.longitude);
          adoptNearest(list, nearest, setActive);
          setBootstrap("ready");
          return;
        } catch {
          /* fall through to default fallback */
        }
      }

      // Fallback: default branch from server, or the first active branch.
      const fallback = list.find(b => b.isDefault) ?? list[0] ?? null;
      if (fallback) {
        writeActiveTenant(fallback.tenantId);
        setActive(fallback);
      }
      setBootstrap("ready");
    } catch (e) {
      setError((e as Error).message);
      setBootstrap("error");
    }
  }, [setActiveByTenant]);

  useEffect(() => {
    void resolveByGeolocation();
  }, [resolveByGeolocation]);

  const selectBranch = useCallback((tenantId: string) => {
    const match = branches.find(b => b.tenantId === tenantId);
    if (!match) return;
    writeActiveTenant(match.tenantId);
    setActive(match);
  }, [branches]);

  const value = useMemo<BranchContextValue>(() => ({
    branches, active, bootstrap, error,
    selectBranch, refresh, resolveByGeolocation,
  }), [branches, active, bootstrap, error, selectBranch, refresh, resolveByGeolocation]);

  return <BranchContext.Provider value={value}>{children}</BranchContext.Provider>;
}

export function useBranch(): BranchContextValue {
  const ctx = useContext(BranchContext);
  if (!ctx) throw new Error("useBranch must be used inside <BranchProvider>");
  return ctx;
}

function adoptNearest(list: Branch[], nearest: NearestBranch, setActive: (b: Branch) => void) {
  const matched = list.find(b => b.tenantId === nearest.tenantId) ?? nearest;
  writeActiveTenant(matched.tenantId);
  setActive(matched);
}

function getGeolocation(): Promise<GeolocationCoordinates> {
  return new Promise((resolve, reject) => {
    if (!("geolocation" in navigator))
      return reject(new Error("Geolocation API not available."));
    navigator.geolocation.getCurrentPosition(
      pos => resolve(pos.coords),
      err => reject(new Error(err.message)),
      { timeout: 5000, maximumAge: 60_000 }
    );
  });
}
