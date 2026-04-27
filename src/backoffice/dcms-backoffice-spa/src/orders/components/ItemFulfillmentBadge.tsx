import { ITEM_STATUS_LABEL, type ItemFulfillmentStatus } from "../types";

const STYLE: Record<ItemFulfillmentStatus, string> = {
  open: "bg-secondary-container/20 text-on-secondary-container",
  allocated: "bg-blue-100 text-blue-700",
  ready_for_delivery: "bg-cyan-100 text-cyan-700",
  shipped: "bg-indigo-100 text-indigo-700",
  delivered: "bg-green-100 text-green-700",
  picked_up: "bg-teal-100 text-teal-700",
  returned: "bg-orange-100 text-orange-700",
  cancelled: "bg-red-100 text-red-700",
};

export function ItemFulfillmentBadge({ status }: { status: ItemFulfillmentStatus }) {
  const cls = STYLE[status] ?? "bg-surface-container-high text-on-surface-variant";
  return (
    <span className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold uppercase ${cls}`}>
      {ITEM_STATUS_LABEL[status] ?? status}
    </span>
  );
}
