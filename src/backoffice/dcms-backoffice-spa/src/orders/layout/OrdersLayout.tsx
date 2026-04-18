import type { ReactNode } from "react";
import type { ComponentType } from "react";
import { IconHelp, IconReceipt, IconRefund, IconWarning } from "../icons";

export type OrdersPageId =
  | "order-processing"
  | "failed-orders"
  | "refund-cases"
  | "order-detail"
  | "failed-order-detail";

type NavItem = { id: OrdersPageId; label: string; Icon: ComponentType<{ className?: string }> };

const navItems: NavItem[] = [
  { id: "order-processing", label: "Orders Processing", Icon: IconReceipt },
  { id: "failed-orders", label: "Failed Orders", Icon: IconWarning },
  { id: "refund-cases", label: "Refund Cases", Icon: IconRefund },
];

type Props = {
  page: OrdersPageId;
  onPageChange: (id: OrdersPageId) => void;
  children: ReactNode;
};

export function OrdersLayout({ page, onPageChange, children }: Props) {
  return (
    /*
     * dcmsOrdersSpa root: fill the host element fully.
     * position:absolute + inset-0 ensures it stretches to whatever
     * height Umbraco gives the section, regardless of content.
     */
    <div
      className="dcmsOrdersSpa font-body text-xs font-medium text-on-background bg-background"
      style={{ position: "absolute", inset: 0, display: "flex", overflow: "hidden" }}
    >
      {/* Sidebar */}
      <aside
        className="flex flex-col shrink-0 bg-stone-50 border-r border-outline-variant/20"
        style={{ width: 240, overflowY: "auto" }}
      >
        <nav className="flex-1 space-y-1 pb-4" aria-label="Orders navigation">
          {navItems.map((item) => {
            const active =
              item.id === page || (item.id === "failed-orders" && page === "failed-order-detail");
            const ItemIcon = item.Icon;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => onPageChange(item.id)}
                aria-current={active ? "page" : undefined}
                className={`flex w-full items-center px-6 py-3 transition-all duration-200 ease-in-out border-r-4 text-xs font-medium ${
                  active
                    ? "text-red-700 bg-red-50 font-bold border-red-700"
                    : "text-stone-500 hover:bg-stone-200 border-transparent"
                }`}
              >
                <ItemIcon className="mr-3 h-5 w-5 shrink-0 opacity-80" />
                <span>{item.label}</span>
              </button>
            );
          })}
        </nav>

        <div className="px-6 pt-4 border-t border-outline-variant/20 pb-4">
          <button
            type="button"
            className="flex w-full items-center py-3 text-xs text-stone-500 hover:bg-stone-200 transition-all duration-200 ease-in-out"
            onClick={() => { /* placeholder */ }}
          >
            <IconHelp className="mr-3 h-5 w-5 shrink-0" />
            <span>Help Center</span>
          </button>
        </div>
      </aside>

      {/* Main scrollable area */}
      <main
        className="flex-1 min-w-0 bg-surface"
        style={{ overflowY: "auto" }}
        aria-label="Main content"
      >
        <div className="p-6 space-y-6">
          {children}
        </div>
      </main>
    </div>
  );
}
