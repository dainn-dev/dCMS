export interface CartLine {
  productId: string;
  variantId: string;
  sku: string;
  name: string;
  unitPrice: number;
  currency: string;
  quantity: number;
  warehouseId: string;
}

function cartKey(tenantId: string, storeId: string): string {
  return `dcms.cart.${tenantId}.${storeId}`;
}

function safeGet(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function safeSet(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    /* private mode */
  }
}

export function readCart(tenantId: string, storeId: string): CartLine[] {
  const raw = safeGet(cartKey(tenantId, storeId));
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isCartLine);
  } catch {
    return [];
  }
}

export function writeCart(tenantId: string, storeId: string, lines: CartLine[]): void {
  safeSet(cartKey(tenantId, storeId), JSON.stringify(lines));
}

export function addOrUpdateLine(
  tenantId: string,
  storeId: string,
  line: CartLine,
): CartLine[] {
  const lines = readCart(tenantId, storeId);
  const idx = lines.findIndex(l => l.variantId === line.variantId);
  if (idx >= 0) {
    lines[idx] = { ...lines[idx], quantity: lines[idx].quantity + line.quantity };
  } else {
    lines.push(line);
  }
  writeCart(tenantId, storeId, lines);
  return lines;
}

export function updateLineQuantity(
  tenantId: string,
  storeId: string,
  variantId: string,
  quantity: number,
): CartLine[] {
  const lines = readCart(tenantId, storeId)
    .map(l => (l.variantId === variantId ? { ...l, quantity } : l))
    .filter(l => l.quantity > 0);
  writeCart(tenantId, storeId, lines);
  return lines;
}

export function removeLine(tenantId: string, storeId: string, variantId: string): CartLine[] {
  const lines = readCart(tenantId, storeId).filter(l => l.variantId !== variantId);
  writeCart(tenantId, storeId, lines);
  return lines;
}

export function clearCart(tenantId: string, storeId: string): void {
  writeCart(tenantId, storeId, []);
}

export function cartItemCount(lines: CartLine[]): number {
  return lines.reduce((sum, l) => sum + l.quantity, 0);
}

export function cartSubtotal(lines: CartLine[]): number {
  return lines.reduce((sum, l) => sum + l.unitPrice * l.quantity, 0);
}

function isCartLine(value: unknown): value is CartLine {
  if (!value || typeof value !== "object") return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.productId === "string" &&
    typeof v.variantId === "string" &&
    typeof v.sku === "string" &&
    typeof v.name === "string" &&
    typeof v.unitPrice === "number" &&
    typeof v.currency === "string" &&
    typeof v.quantity === "number" &&
    typeof v.warehouseId === "string"
  );
}
