/**
 * DAI-584: Centralized gateway base URLs for all SPA API clients.
 * All fetch calls go through dCMS.Gateway (YARP) instead of hitting services directly.
 *
 * Dev proxy: configure Vite to proxy /gateway/** → http://localhost:5100
 * Production: same-origin (gateway served at same host as SPA via reverse proxy).
 */

export const GATEWAY = {
  /** dCMS.Catalog.Api routes — /gateway/v1/catalog/... → catalog-api /api/v1/... */
  catalog: "/gateway/v1/catalog",
  /** dCMS.Order.Api routes — /gateway/v1/orders/... → order-api /api/v1/... */
  orders: "/gateway/v1/orders",
  /** dCMS.Inventory.Api routes — /gateway/v1/inventory/... → inventory-api /api/v1/... */
  inventory: "/gateway/v1/inventory",
  /** dCMS.Promotions.Api routes — /gateway/v1/promotions/... → promotions-api /api/v1/... */
  promotions: "/gateway/v1/promotions",
} as const;
