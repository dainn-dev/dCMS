import { createRoot, type Root } from "react-dom/client";
import React from "react";
import { OrdersApp } from "./orders/OrdersApp";
import "./styles.css";

const roots = new WeakMap<HTMLElement, Root>();

type MountOptions = {
  tenantId?: string;
  storeId?: string;
  authToken?: string;
};

export function mount(host: HTMLElement, options?: MountOptions) {
  if (roots.has(host)) return;
  const root = createRoot(host);
  roots.set(host, root);
  root.render(
    <React.StrictMode>
      <OrdersApp tenantId={options?.tenantId} storeId={options?.storeId} authToken={options?.authToken} />
    </React.StrictMode>
  );
}

export function unmount(host: HTMLElement) {
  const root = roots.get(host);
  if (!root) return;
  root.unmount();
  roots.delete(host);
}

