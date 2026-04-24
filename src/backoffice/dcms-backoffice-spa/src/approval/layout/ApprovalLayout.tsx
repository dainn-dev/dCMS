import type { ComponentType, ReactNode } from "react";
import { IconBolt, IconBox, IconHelp, IconLocalOffer } from "../../orders/icons";

export type ApprovalPageId = "product-approval" | "campaign-approval" | "promo-approval";

type NavItem = {
  id: ApprovalPageId;
  label: string;
  Icon: ComponentType<{ className?: string }>;
  pendingCount: number;
};

const navItems: NavItem[] = [
  { id: "product-approval", label: "Product Approval", Icon: IconBox, pendingCount: 5 },
  { id: "campaign-approval", label: "Campaign Approval", Icon: IconBolt, pendingCount: 3 },
  { id: "promo-approval", label: "Promotion Approval", Icon: IconLocalOffer, pendingCount: 4 },
];

export const APPROVAL_HASH_PAGE_IDS: ApprovalPageId[] = navItems.map((item) => item.id);

type Props = {
  page: ApprovalPageId;
  onPageChange: (id: ApprovalPageId) => void;
  children: ReactNode;
  /** Live pending count for product approval (sidebar badge). Falls back to demo value when unset. */
  productApprovalPendingCount?: number;
  /** Live pending count for campaign approval (sidebar badge). Falls back to demo value when unset. */
  campaignApprovalPendingCount?: number;
  /** Live pending count for promotion / promo code approval (sidebar badge). Falls back to demo value when unset. */
  promoApprovalPendingCount?: number;
};

export function ApprovalLayout({
  page,
  onPageChange,
  children,
  productApprovalPendingCount,
  campaignApprovalPendingCount,
  promoApprovalPendingCount,
}: Props) {
  return (
    <div
      className="dcmsApprovalSpa font-body text-xs font-medium text-on-background bg-background"
      style={{ position: "absolute", inset: 0, display: "flex", overflow: "hidden" }}
    >
      <aside
        className="flex flex-col shrink-0 bg-stone-50 border-r border-outline-variant/20"
        style={{ width: 240, overflowY: "auto" }}
      >
        <nav className="flex-1 space-y-1 pb-4" aria-label="Approval navigation">
          {navItems.map((item) => {
            const active = item.id === page;
            const ItemIcon = item.Icon;
            let pendingCount = item.pendingCount;
            if (item.id === "product-approval" && productApprovalPendingCount !== undefined) {
              pendingCount = productApprovalPendingCount;
            } else if (item.id === "campaign-approval" && campaignApprovalPendingCount !== undefined) {
              pendingCount = campaignApprovalPendingCount;
            } else if (item.id === "promo-approval" && promoApprovalPendingCount !== undefined) {
              pendingCount = promoApprovalPendingCount;
            }
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => onPageChange(item.id)}
                aria-current={active ? "page" : undefined}
                className={`flex w-full items-center gap-2 px-6 py-3 transition-all duration-200 ease-in-out border-r-4 text-xs font-medium ${
                  active
                    ? "text-red-700 bg-red-50 font-bold border-red-700"
                    : "text-stone-500 hover:bg-stone-200 border-transparent"
                }`}
              >
                <ItemIcon className="h-5 w-5 shrink-0 opacity-80" />
                <span className="min-w-0 flex-1 text-left">{item.label}</span>
                {pendingCount > 0 && (
                  <span
                    className={`shrink-0 rounded-full px-2 py-0.5 text-[10px] font-extrabold tabular-nums ${
                      active ? "bg-red-200 text-red-900" : "bg-stone-200 text-stone-700"
                    }`}
                    title="Pending items"
                  >
                    {pendingCount > 99 ? "99+" : pendingCount}
                  </span>
                )}
              </button>
            );
          })}
        </nav>

        <div className="px-6 pt-4 border-t border-outline-variant/20 pb-4">
          <button
            type="button"
            className="flex w-full items-center py-3 text-xs text-stone-500 hover:bg-stone-200 transition-all duration-200 ease-in-out"
            onClick={() => {}}
          >
            <IconHelp className="mr-3 h-5 w-5 shrink-0" />
            <span>Help Center</span>
          </button>
        </div>
      </aside>

      <main className="flex-1 min-w-0 bg-surface" style={{ overflowY: "auto" }} aria-label="Approval main content">
        <div className="p-6 space-y-6">{children}</div>
      </main>
    </div>
  );
}
