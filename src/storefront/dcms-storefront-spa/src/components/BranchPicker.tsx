import { useState } from "react";
import { useBranch } from "../lib/branch/BranchProvider";

// DAI-751 / US-4 (T4.4): Top-bar pill + modal for branch switching.
// The pill is always visible; clicking it opens a list of branches under the current client.

export function BranchPicker() {
  const { active, branches, bootstrap, selectBranch } = useBranch();
  const [open, setOpen] = useState(false);

  const label =
    bootstrap === "resolving" ? "Locating…" :
    active                    ? active.name :
                                "Choose branch";

  return (
    <>
      <button
        type="button"
        className="dcms-branch-pill"
        onClick={() => setOpen(true)}
        aria-haspopup="dialog"
        style={pillStyle}
      >
        <span aria-hidden style={{ marginRight: 6 }}>📍</span>
        <strong>{label}</strong>
        <span style={{ marginLeft: 8, color: "#64748b", fontSize: 12 }}>Change</span>
      </button>

      {open && (
        <div role="dialog" aria-modal="true" style={backdropStyle} onClick={() => setOpen(false)}>
          <div style={modalStyle} onClick={e => e.stopPropagation()}>
            <header style={{ marginBottom: 12 }}>
              <h2 style={{ margin: 0, fontSize: 18 }}>Select your branch</h2>
              <p style={{ margin: "6px 0 0", color: "#475569", fontSize: 13 }}>
                Prices, stock and promotions are scoped to the branch you pick.
              </p>
            </header>

            <ul style={{ listStyle: "none", margin: 0, padding: 0, display: "grid", gap: 8 }}>
              {branches.length === 0 && (
                <li style={{ color: "#64748b" }}>No branches available.</li>
              )}
              {branches.map(b => {
                const isActive = active?.tenantId === b.tenantId;
                return (
                  <li key={b.tenantId}>
                    <button
                      type="button"
                      onClick={() => { selectBranch(b.tenantId); setOpen(false); }}
                      style={{
                        ...rowStyle,
                        borderColor: isActive ? "#aa0014" : "#e2e8f0",
                        background: isActive ? "#fef2f2" : "white",
                      }}
                    >
                      <div>
                        <div style={{ fontWeight: 600 }}>{b.name}</div>
                        {b.address && <div style={{ color: "#64748b", fontSize: 12 }}>{b.address}</div>}
                      </div>
                      {isActive && <span style={{ color: "#aa0014", fontWeight: 600, fontSize: 12 }}>Selected</span>}
                    </button>
                  </li>
                );
              })}
            </ul>

            <div style={{ marginTop: 16, textAlign: "right" }}>
              <button type="button" onClick={() => setOpen(false)} style={btnGhost}>Close</button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

const pillStyle: React.CSSProperties = {
  display: "inline-flex", alignItems: "center",
  background: "white", border: "1px solid #e2e8f0", borderRadius: 999,
  padding: "6px 14px", cursor: "pointer", fontFamily: "inherit", fontSize: 13,
};

const backdropStyle: React.CSSProperties = {
  position: "fixed", inset: 0, background: "rgba(15,23,42,0.55)",
  display: "flex", alignItems: "center", justifyContent: "center", zIndex: 1000,
};

const modalStyle: React.CSSProperties = {
  background: "white", padding: 20, borderRadius: 12, width: 460, maxWidth: "92vw",
  boxShadow: "0 20px 50px rgba(0,0,0,0.25)",
};

const rowStyle: React.CSSProperties = {
  width: "100%", textAlign: "left",
  display: "flex", alignItems: "center", justifyContent: "space-between",
  padding: "10px 12px", borderRadius: 8,
  border: "1px solid #e2e8f0", cursor: "pointer", fontFamily: "inherit",
};

const btnGhost: React.CSSProperties = {
  background: "transparent", border: "1px solid #cbd5e1", color: "#0f172a",
  padding: "6px 14px", borderRadius: 6, cursor: "pointer", fontWeight: 600, fontSize: 13,
};
