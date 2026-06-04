import type { TemplateRow } from "./api/templatesApi";
import { GATEWAY } from "./api/gatewayConfig";

/**
 * Curated catalog of notification templates a store operator can edit.
 *
 * Operators pick a template by its human name (e.g. "Order Confirmation"),
 * never by the technical `key`/`channel` pair. Each entry also declares the
 * variables available to that template with friendly labels + realistic sample
 * values, which power the "Insert variable" picker and the live preview.
 */

export type TemplateVar = {
  /** Scriban path under `model.` (e.g. "orderId" → {{ model.orderId }}). */
  path: string;
  /** Friendly label shown in the Insert-variable menu. */
  label: string;
  /** Sample value used for the live preview. */
  sample: string | number;
};

export type TemplateCatalogEntry = {
  /** Stable id ("<key>|<channel>"). */
  id: string;
  /** Human name shown to operators. */
  name: string;
  /** One-line description of when this message is sent. */
  description: string;
  key: string;
  channel: TemplateRow["channel"];
  variables: TemplateVar[];
  /** Optional starter content offered when a brand-new template is created. */
  defaultSubject?: string;
  defaultBody?: string;
};

const ORDER_VARS: TemplateVar[] = [
  { path: "orderId", label: "Order number", sample: "SO-100428" },
  { path: "customerName", label: "Customer name", sample: "Jane Doe" },
  { path: "orderDate", label: "Order date", sample: "4 Jun 2026" },
  { path: "total", label: "Order total", sample: "$123.45" },
  { path: "storeName", label: "Store name", sample: "Acme Mart" },
];

const SHIP_VARS: TemplateVar[] = [
  { path: "orderId", label: "Order number", sample: "SO-100428" },
  { path: "customerName", label: "Customer name", sample: "Jane Doe" },
  { path: "trackingNumber", label: "Tracking number", sample: "VN1234567890" },
  { path: "carrier", label: "Carrier", sample: "GHN Express" },
  { path: "storeName", label: "Store name", sample: "Acme Mart" },
];

const ACCOUNT_VARS: TemplateVar[] = [
  { path: "customerName", label: "Customer name", sample: "Jane Doe" },
  { path: "storeName", label: "Store name", sample: "Acme Mart" },
  { path: "actionUrl", label: "Action link", sample: "https://shop.example.com/verify?t=abc" },
];

const id = (key: string, channel: TemplateRow["channel"]) => `${key}|${channel}`;

export const TEMPLATE_CATALOG: TemplateCatalogEntry[] = [
  {
    id: id("order.confirmation", "email"),
    name: "Order Confirmation",
    description: "Sent to the customer right after they place an order.",
    key: "order.confirmation",
    channel: "email",
    variables: ORDER_VARS,
    defaultSubject: "Your {{ model.storeName }} order {{ model.orderId }} is confirmed",
    defaultBody:
      "<p>Hi {{ model.customerName }},</p>" +
      "<p>Thanks for your order <b>{{ model.orderId }}</b> placed on {{ model.orderDate }}.</p>" +
      "<p>Order total: <b>{{ model.total }}</b></p>" +
      "<p>We'll let you know when it ships.</p>",
  },
  {
    id: id("order.shipped", "email"),
    name: "Shipping Notification",
    description: "Sent when the customer's order has been dispatched.",
    key: "order.shipped",
    channel: "email",
    variables: SHIP_VARS,
    defaultSubject: "Your order {{ model.orderId }} is on its way",
    defaultBody:
      "<p>Hi {{ model.customerName }},</p>" +
      "<p>Good news — order <b>{{ model.orderId }}</b> has shipped with {{ model.carrier }}.</p>" +
      "<p>Tracking number: <b>{{ model.trackingNumber }}</b></p>",
  },
  {
    id: id("order.cancelled", "email"),
    name: "Order Cancellation",
    description: "Sent when an order is cancelled.",
    key: "order.cancelled",
    channel: "email",
    variables: ORDER_VARS,
    defaultSubject: "Your order {{ model.orderId }} has been cancelled",
    defaultBody:
      "<p>Hi {{ model.customerName }},</p>" +
      "<p>Your order <b>{{ model.orderId }}</b> has been cancelled. " +
      "Any payment will be refunded shortly.</p>",
  },
  {
    id: id("account.welcome", "email"),
    name: "Welcome Email",
    description: "Sent when a customer creates a new account.",
    key: "account.welcome",
    channel: "email",
    variables: ACCOUNT_VARS,
    defaultSubject: "Welcome to {{ model.storeName }}!",
    defaultBody:
      "<p>Hi {{ model.customerName }},</p>" +
      "<p>Welcome to {{ model.storeName }}. We're glad to have you.</p>",
  },
  {
    id: id("password.reset", "email"),
    name: "Password Reset",
    description: "Sent when a customer requests a password reset.",
    key: "password.reset",
    channel: "email",
    variables: ACCOUNT_VARS,
    defaultSubject: "Reset your {{ model.storeName }} password",
    defaultBody:
      "<p>Hi {{ model.customerName }},</p>" +
      "<p>Click the link below to reset your password:</p>" +
      '<p><a href="{{ model.actionUrl }}">Reset password</a></p>',
  },
];

/** Superset of variables, used for custom templates that aren't in the catalog. */
export const ALL_VARS: TemplateVar[] = Array.from(
  new Map(
    [...ORDER_VARS, ...SHIP_VARS, ...ACCOUNT_VARS].map((v) => [v.path, v]),
  ).values(),
);

/**
 * Catalog augmented with any saved templates whose key/channel isn't in the
 * curated list, so nothing in the database is ever hidden from the operator.
 *
 * `base` defaults to the built-in static catalog (offline fallback); callers
 * normally pass the config-driven catalog fetched from the backend.
 */
export function buildEffectiveCatalog(
  rows: TemplateRow[],
  base: TemplateCatalogEntry[] = TEMPLATE_CATALOG,
): TemplateCatalogEntry[] {
  const known = new Set(base.map((c) => c.id));
  const seen = new Set<string>();
  const extras: TemplateCatalogEntry[] = [];
  for (const r of rows) {
    const eid = id(r.key, r.channel);
    if (known.has(eid) || seen.has(eid)) continue;
    seen.add(eid);
    extras.push({
      id: eid,
      name: r.key,
      description: "Custom template",
      key: r.key,
      channel: r.channel,
      variables: ALL_VARS,
    });
  }
  return [...base, ...extras];
}

/**
 * DAI-687: fetch the config-driven template catalog from notification-api.
 * Falls back to the built-in {@link TEMPLATE_CATALOG} on any error so the page
 * keeps working offline / when the service is unavailable.
 */
export async function fetchTemplateCatalog(
  tenantId: string,
  storeId?: string,
  token?: string,
): Promise<TemplateCatalogEntry[]> {
  const headers: Record<string, string> = { Accept: "application/json", "X-Tenant-Id": tenantId };
  if (storeId) headers["X-Store-Id"] = storeId;
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetch(`${GATEWAY.notifications}/templates/catalog`, { credentials: "same-origin", headers });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const body = await res.json();
  const entries = (body?.data ?? []) as Array<Record<string, unknown>>;
  if (!Array.isArray(entries) || entries.length === 0) return TEMPLATE_CATALOG;

  return entries.map((e) => {
    const key = String(e.key ?? "");
    const channel = (String(e.channel ?? "email") as TemplateRow["channel"]);
    const variables: TemplateVar[] = Array.isArray(e.variables)
      ? (e.variables as Array<Record<string, unknown>>).map((v) => ({
          path: String(v.path ?? ""),
          label: String(v.label ?? v.path ?? ""),
          sample: (v.sample as string | number) ?? "",
        }))
      : [];
    return {
      id: String(e.id ?? `${key}|${channel}`),
      name: String(e.name ?? key),
      description: String(e.description ?? ""),
      key,
      channel,
      variables,
      defaultSubject: e.defaultSubject ? String(e.defaultSubject) : undefined,
      defaultBody: e.defaultBody ? String(e.defaultBody) : undefined,
    };
  });
}
