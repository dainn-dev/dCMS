import type { ReactNode } from "react";
import type { ComponentType } from "react";
import { useEffect, useState } from "react";
import {
  IconAccountTree,
  IconBolt,
  IconBox,
  IconChevronDown,
  IconGrid,
  IconHelp,
  IconLocalOffer,
  IconLocationOn,
  IconMap,
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
  | "product-category-assignment"
  | "product-configuration"
  | "attributes"
  | "campaigns"
  | "promo-codes"
  | "fulfillment-options"
  | "fulfillment-delivery-allocation"
  | "fulfillment-collection-locations";

type NavItem = {
  id: EStorePageId;
  label: string;
  Icon: ComponentType<{ className?: string }>;
  /** Visually group under Brands (US-18 / §4.2). */
  nestUnderBrands?: boolean;
  /** Visually group under Products (US-20 / §4.4). */
  nestUnderProducts?: boolean;
  nestUnderFulfillment?: boolean;
};

const navItems: NavItem[] = [
  { id: "brands", label: "Brands", Icon: IconStore },
  { id: "brand-configuration", label: "Brand Configuration", Icon: IconTune, nestUnderBrands: true },
  { id: "categories", label: "Categories", Icon: IconGrid },
  { id: "products", label: "Products", Icon: IconBox },
  { id: "product-best-sellers", label: "Best Seller Settings", Icon: IconTrendingUp, nestUnderProducts: true },
  { id: "product-category-assignment", label: "Category Assignment", Icon: IconAccountTree, nestUnderProducts: true },
  { id: "product-configuration", label: "Product Configuration", Icon: IconTune, nestUnderProducts: true },
  { id: "attributes", label: "Attributes", Icon: IconTag },
  { id: "campaigns", label: "Campaigns", Icon: IconBolt },
  { id: "promo-codes", label: "Promo Codes", Icon: IconLocalOffer },
  { id: "fulfillment-options", label: "Fulfillment Options", Icon: IconShipping },
  { id: "fulfillment-delivery-allocation", label: "Delivery Allocation", Icon: IconMap, nestUnderFulfillment: true },
  { id: "fulfillment-collection-locations", label: "Collection Location", Icon: IconLocationOn, nestUnderFulfillment: true },
];

/** Top-level eStore routes — keep in sync with `navItems` for hash URL parsing. */
export const ESTORE_HASH_PAGE_IDS: EStorePageId[] = navItems.map((item) => item.id);

function nestParentOf(item: NavItem): EStorePageId | null {
  if (item.nestUnderBrands) return "brands";
  if (item.nestUnderProducts) return "products";
  if (item.nestUnderFulfillment) return "fulfillment-options";
  return null;
}

function itemHasSubmenu(parentId: EStorePageId): boolean {
  return navItems.some((i) => nestParentOf(i) === parentId);
}

type Props = {
  page: EStorePageId;
  onPageChange: (id: EStorePageId) => void;
  children: ReactNode;
};

export function EStoreLayout({ page, onPageChange, children }: Props) {
  const [expandedSubmenus, setExpandedSubmenus] = useState<Set<EStorePageId>>(() => new Set());

  useEffect(() => {
    const current = navItems.find((i) => i.id === page);
    const parent = current ? nestParentOf(current) : null;
    if (parent) {
      setExpandedSubmenus((prev) => {
        if (prev.has(parent)) return prev;
        const next = new Set(prev);
        next.add(parent);
        return next;
      });
    }
  }, [page]);

  function toggleSubmenu(parentId: EStorePageId) {
    setExpandedSubmenus((prev) => {
      const next = new Set(prev);
      if (next.has(parentId)) next.delete(parentId);
      else next.add(parentId);
      return next;
    });
  }

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
            const ItemIcon = item.Icon;
            const nested = item.nestUnderBrands || item.nestUnderProducts || item.nestUnderFulfillment;
            const parentId = nestParentOf(item);
            if (parentId && !expandedSubmenus.has(parentId)) {
              return null;
            }

            const hasSubmenu = itemHasSubmenu(item.id);
            const submenuOpen = expandedSubmenus.has(item.id);
            const childOfCurrent = navItems.some((i) => nestParentOf(i) === item.id && i.id === page);
            const rowActive = item.id === page || childOfCurrent;

            const rowTone =
              rowActive
                ? "text-red-700 bg-red-50 font-bold border-red-700"
                : "text-stone-500 border-transparent hover:bg-stone-200";

            if (hasSubmenu) {
              return (
                <div
                  key={item.id}
                  className={`flex w-full items-stretch border-r-4 text-xs font-medium transition-all duration-200 ease-in-out ${rowTone}`}
                >
                  <button
                    type="button"
                    aria-expanded={submenuOpen}
                    aria-label={submenuOpen ? "Collapse submenu" : "Expand submenu"}
                    className="shrink-0 flex w-9 items-center justify-center text-inherit hover:bg-black/[0.04]"
                    onClick={(e) => {
                      e.preventDefault();
                      toggleSubmenu(item.id);
                    }}
                  >
                    <IconChevronDown
                      className={`h-4 w-4 shrink-0 transition-transform duration-200 ${submenuOpen ? "" : "-rotate-90"}`}
                      aria-hidden
                    />
                  </button>
                  <button
                    type="button"
                    onClick={() => onPageChange(item.id)}
                    aria-current={item.id === page ? "page" : undefined}
                    className="min-w-0 flex-1 flex items-center py-3 pr-4 pl-0 text-left text-inherit"
                  >
                    <ItemIcon className="mr-3 h-5 w-5 shrink-0 opacity-80" />
                    <span className="truncate">{item.label}</span>
                  </button>
                </div>
              );
            }

            const pad = nested ? "pl-10 pr-6" : "pl-6 pr-6";
            return (
              <div key={item.id}>
                <button
                  type="button"
                  onClick={() => onPageChange(item.id)}
                  aria-current={item.id === page ? "page" : undefined}
                  className={`flex w-full items-center border-r-4 py-3 text-xs font-medium transition-all duration-200 ease-in-out ${pad} ${rowTone}`}
                >
                  <ItemIcon className="mr-3 h-5 w-5 shrink-0 opacity-80" />
                  <span className="truncate text-left">{item.label}</span>
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
