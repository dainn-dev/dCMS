import type { ReactNode } from "react";
import type { ComponentType } from "react";
import {
  IconBolt,
  IconBox,
  IconGrid,
  IconHelp,
  IconLocalOffer,
  IconShipping,
  IconStore,
  IconTag,
  IconTrendingUp,
  IconTune,
} from "../../orders/icons";

export type EStorePageId =
  | "brands"
  | "brand-configuration"
  | "categories"
  | "products"
  | "product-best-sellers"
  | "attributes"
  | "campaigns"
  | "promo-codes"
  | "fulfillment-options";

type NavItem = {
  id: EStorePageId;
  label: string;
  Icon: ComponentType<{ className?: string }>;
  /** Visually group under Brands (US-18 / §4.2). */
  nestUnderBrands?: boolean;
  /** Visually group under Products (US-20 / §4.4). */
  nestUnderProducts?: boolean;
};

const navItems: NavItem[] = [
  { id: "brands", label: "Brands", Icon: IconStore },
  { id: "brand-configuration", label: "Brand Configuration", Icon: IconTune, nestUnderBrands: true },
  { id: "categories", label: "Categories", Icon: IconGrid },
  { id: "products", label: "Products", Icon: IconBox },
  { id: "product-best-sellers", label: "Best Seller Settings", Icon: IconTrendingUp, nestUnderProducts: true },
  { id: "attributes", label: "Attributes", Icon: IconTag },
  { id: "campaigns", label: "Campaigns", Icon: IconBolt },
  { id: "promo-codes", label: "Promo Codes", Icon: IconLocalOffer },
  { id: "fulfillment-options", label: "Fulfillment Options", Icon: IconShipping },
];

type Props = {
  page: EStorePageId;
  onPageChange: (id: EStorePageId) => void;
  children: ReactNode;
};

export function EStoreLayout({ page, onPageChange, children }: Props) {
  return (
    <div
      className="dcmsEStoreSpa font-body text-xs font-medium text-on-background bg-background"
      style={{ position: "absolute", inset: 0, display: "flex", overflow: "hidden" }}
    >
      {/* Sidebar */}
      <aside
        className="flex flex-col shrink-0 bg-stone-50 border-r border-outline-variant/20"
        style={{ width: 240, overflowY: "auto" }}
      >
        <nav className="flex-1 space-y-1 pb-4" aria-label="eStore navigation">
          {navItems.map((item) => {
            const active = item.id === page;
            const ItemIcon = item.Icon;
            const nested = item.nestUnderBrands || item.nestUnderProducts;
            const pad = nested ? "pl-10 pr-6" : "px-6";
            return (
              <div key={item.id}>
                <button
                  type="button"
                  onClick={() => onPageChange(item.id)}
                  aria-current={active ? "page" : undefined}
                  className={`flex w-full items-center ${pad} py-3 transition-all duration-200 ease-in-out border-r-4 text-xs font-medium ${
                    active
                      ? "text-red-700 bg-red-50 font-bold border-red-700"
                      : "text-stone-500 hover:bg-stone-200 border-transparent"
                  }`}
                >
                  <ItemIcon className="mr-3 h-5 w-5 shrink-0 opacity-80" />
                  <span>{item.label}</span>
                </button>
              </div>
            );
          })}
        </nav>

        <div className="px-6 pt-4 border-t border-outline-variant/20 pb-4">
          <button
            type="button"
            className="flex w-full items-center py-3 text-xs text-stone-500 hover:bg-stone-200 transition-all duration-200 ease-in-out"
            onClick={() => {
              /* placeholder */
            }}
          >
            <IconHelp className="mr-3 h-5 w-5 shrink-0" />
            <span>Help Center</span>
          </button>
        </div>
      </aside>

      {/* Main scrollable area */}
      <main className="flex-1 min-w-0 bg-surface" style={{ overflowY: "auto" }} aria-label="Main content">
        <div className="p-6 space-y-6">{children}</div>
      </main>
    </div>
  );
}
