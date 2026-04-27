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
  /** dCMS.Fulfillment.Api routes — /gateway/v1/fulfillment/... → fulfillment-api /api/v1/... */
  fulfillment: "/gateway/v1/fulfillment",
  /** DAI-728 — cross-order RMA endpoints — /gateway/v1/returns/... → order-api /api/returns/... */
  returns: "/gateway/v1/returns",
  /** DAI-685 — analytics reports API — /gateway/v1/reports/... → reports-api /api/v1/reports/... */
  reports: "/gateway/v1/reports",
  /** DAI-687 — notification/templates API — /gateway/v1/notifications/... → notification-api /api/v1/... */
  notifications: "/gateway/v1/notifications",
  /** DAI-688 — approvals API — /gateway/v1/approvals/... → approval-api /api/v1/... */
  approvals: "/gateway/v1/approvals",
} as const;
