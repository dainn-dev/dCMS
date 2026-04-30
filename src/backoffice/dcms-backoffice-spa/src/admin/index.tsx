import React from "react";
import { createRoot, type Root } from "react-dom/client";
import { AuthProvider } from "../auth/AuthProvider";
import { ClientAdminDashboard } from "./ClientAdminDashboard";
import { ImpersonationBanner } from "./ImpersonationBanner";

const roots = new WeakMap<HTMLElement, Root>();

export function mount(host: HTMLElement) {
  if (roots.has(host)) return;
  const root = createRoot(host);
  roots.set(host, root);
  root.render(
    <React.StrictMode>
      <AuthProvider>
        <ImpersonationBanner />
        <ClientAdminDashboard />
      </AuthProvider>
    </React.StrictMode>
  );
}

export function unmount(host: HTMLElement) {
  const root = roots.get(host);
  if (!root) return;
  root.unmount();
  roots.delete(host);
}
