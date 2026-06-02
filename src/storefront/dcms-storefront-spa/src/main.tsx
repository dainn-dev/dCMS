import React from "react";
import { createRoot } from "react-dom/client";
import { BranchProvider, useBranch } from "./lib/branch/BranchProvider";
import { BranchPicker } from "./components/BranchPicker";

function StorefrontShell() {
  const { active, bootstrap, error } = useBranch();
  return (
    <main style={{ fontFamily: "system-ui, -apple-system, sans-serif", padding: 24 }}>
      <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
        <h1 style={{ fontSize: 22, margin: 0 }}>dCMS Storefront</h1>
        <BranchPicker />
      </header>

      {bootstrap === "resolving" && <p>Detecting your nearest branch…</p>}
      {error && <p role="alert" style={{ color: "#aa0014" }}>{error}</p>}
      {active && (
        <section>
          <p style={{ color: "#475569" }}>
            Active branch: <strong>{active.name}</strong> (<code>{active.tenantId}</code>).
            Every API call carries <code>X-Active-Tenant: {active.tenantId}</code>.
          </p>
        </section>
      )}
    </main>
  );
}

const host = document.getElementById("root");
if (host) {
  createRoot(host).render(
    <React.StrictMode>
      <BranchProvider>
        <StorefrontShell />
      </BranchProvider>
    </React.StrictMode>
  );
}
