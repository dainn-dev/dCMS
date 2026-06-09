import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useOptionalStoreScope } from "../commerce/StoreContextProvider";
import {
  addOrUpdateLine,
  cartItemCount,
  cartSubtotal,
  clearCart,
  readCart,
  removeLine,
  updateLineQuantity,
  type CartLine,
} from "./cartStorage";

interface CartContextValue {
  lines: CartLine[];
  itemCount: number;
  subtotal: number;
  addLine: (line: CartLine) => void;
  setQuantity: (variantId: string, quantity: number) => void;
  remove: (variantId: string) => void;
  empty: () => void;
}

const CartContext = createContext<CartContextValue | null>(null);

export function CartProvider({ children }: { children: ReactNode }) {
  const scope = useOptionalStoreScope();
  const [lines, setLines] = useState<CartLine[]>([]);

  useEffect(() => {
    if (!scope) {
      setLines([]);
      return;
    }
    setLines(readCart(scope.tenantId, scope.storeId));
  }, [scope?.tenantId, scope?.storeId]);

  const sync = useCallback(
    (next: CartLine[]) => {
      setLines(next);
    },
    [],
  );

  const addLine = useCallback(
    (line: CartLine) => {
      if (!scope) return;
      sync(addOrUpdateLine(scope.tenantId, scope.storeId, line));
    },
    [scope, sync],
  );

  const setQuantity = useCallback(
    (variantId: string, quantity: number) => {
      if (!scope) return;
      sync(updateLineQuantity(scope.tenantId, scope.storeId, variantId, quantity));
    },
    [scope, sync],
  );

  const remove = useCallback(
    (variantId: string) => {
      if (!scope) return;
      sync(removeLine(scope.tenantId, scope.storeId, variantId));
    },
    [scope, sync],
  );

  const empty = useCallback(() => {
    if (!scope) return;
    clearCart(scope.tenantId, scope.storeId);
    sync([]);
  }, [scope, sync]);

  const value = useMemo<CartContextValue>(
    () => ({
      lines,
      itemCount: cartItemCount(lines),
      subtotal: cartSubtotal(lines),
      addLine,
      setQuantity,
      remove,
      empty,
    }),
    [lines, addLine, setQuantity, remove, empty],
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartContextValue {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart must be used inside CartProvider");
  return ctx;
}
