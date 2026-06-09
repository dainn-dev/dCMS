/** Gateway base URLs — proxied to dCMS.Gateway in dev (see vite.config.ts). */
export const GATEWAY = {
  catalog: "/gateway/v1/catalog",
  orders: "/gateway/v1/orders",
  identity: "/gateway/v1/identity",
  inventory: "/gateway/v1/inventory",
} as const;
