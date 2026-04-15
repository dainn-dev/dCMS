import { createRoot, type Root } from "react-dom/client";
import React from "react";
import { OrdersApp } from "./orders/OrdersApp";
import "./styles.css";

const roots = new WeakMap<HTMLElement, Root>();

export function mount(host: HTMLElement) {
  if (roots.has(host)) return;
  const root = createRoot(host);
  roots.set(host, root);
  root.render(
    <React.StrictMode>
      <OrdersApp />
    </React.StrictMode>
  );
}

export function unmount(host: HTMLElement) {
  const root = roots.get(host);
  if (!root) return;
  root.unmount();
  roots.delete(host);
}

