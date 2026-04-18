import { createRoot, type Root } from "react-dom/client";
import React from "react";
import { EStoreApp } from "./EStoreApp";
import type { EStoreSidebarScope } from "./layout/EStoreLayout";
import type { UmbracoLanguage } from "./useUmbracoLanguages";
import "../styles.css";

type MountOptions = {
  languages?: UmbracoLanguage[];
  /** <c>access</c> = Umbraco Access section: sidebar only Users / Roles / Tenants. */
  sidebarScope?: EStoreSidebarScope;
};

const roots = new WeakMap<HTMLElement, Root>();

export function mount(host: HTMLElement, options?: MountOptions) {
  if (roots.has(host)) return;
  const root = createRoot(host);
  roots.set(host, root);
  root.render(
    <React.StrictMode>
      <EStoreApp languages={options?.languages} sidebarScope={options?.sidebarScope ?? "estore"} />
    </React.StrictMode>
  );
}

export function unmount(host: HTMLElement) {
  const root = roots.get(host);
  if (!root) return;
  root.unmount();
  roots.delete(host);
}

