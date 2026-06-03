import type { HistoryEntry } from "../api/campaignsApi";
import type { CampaignDetail } from "../api/campaignsApi";
import type { CampaignChannel, CampaignEditorKind, CampaignWorkflowState } from "../campaigns-columns";
import type { CampaignChangeHistoryEntry } from "../pages/campaign-sections/CampaignChangeHistoryPanel";
import { defaultAfterSalesMechanicsValue, type AfterSalesMechanicsValue } from "../pages/campaign-sections/AfterSalesMechanicsSection";
import { defaultMixMatchMechanicsValue, type MixMatchMechanicsValue } from "../pages/campaign-sections/MixMatchMechanicsSection";
import { defaultProductDiscountMechanicsValue, type ProductDiscountMechanicsValue } from "../pages/campaign-sections/ProductDiscountMechanicsSection";
import type { PwpDiscountMechanicsValue } from "../pages/campaign-sections/PwpDiscountMechanicsSection";
import type { PwpItemMechanicsValue } from "../pages/campaign-sections/PwpItemMechanicsSection";

const WORKFLOW_LABEL: Record<string, string> = {
  draft: "Draft",
  pending_approval: "Pending approval",
  approved: "Approved",
  active: "Active on eStore",
  deactivated: "Deactivated",
  archived: "Archived",
  rejected: "Rejected",
};

export const CAMPAIGN_CHANNELS: CampaignChannel[] = ["Email", "SMS", "Push", "Web"];

export function isoToDatetimeLocal(iso: string | null | undefined): string {
  if (!iso) return "";
  try {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return "";
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  } catch {
    return "";
  }
}

export function datetimeLocalToIso(value: string): string | null {
  if (!value.trim()) return null;
  try {
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? null : d.toISOString();
  } catch {
    return null;
  }
}

function parseJsonRecord(raw: string | undefined): Record<string, unknown> {
  if (!raw) return {};
  try {
    const v = JSON.parse(raw) as unknown;
    return v && typeof v === "object" && !Array.isArray(v) ? (v as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function parseJsonArray<T extends string>(raw: string | undefined): T[] {
  if (!raw) return [];
  try {
    const v = JSON.parse(raw) as unknown;
    return Array.isArray(v) ? (v.filter((x) => typeof x === "string") as T[]) : [];
  } catch {
    return [];
  }
}

function strArray(v: unknown): string[] {
  return Array.isArray(v) ? v.filter((x): x is string => typeof x === "string") : [];
}

function strField(v: unknown): string {
  return typeof v === "string" ? v : "";
}

function boolField(v: unknown): boolean {
  return Boolean(v);
}

export type QualifiersFormState = {
  inclusionEnabled: boolean;
  incBrands: string[];
  incCategories: string[];
  incPid1: string[];
  incPid2: string[];
  incProducts: string[];
  cartAllQualifiers: boolean;
  minSpend: string;
  minSpendNet: boolean;
  exclusionEnabled: boolean;
  excBrands: string[];
  excCategories: string[];
  excPid1: string[];
  excPid2: string[];
  excProducts: string[];
  excMembershipTypes: string[];
  excMembershipTiers: string[];
};

export function parseQualifiersJson(raw: string | undefined): QualifiersFormState {
  const root = parseJsonRecord(raw);
  const inclusion = (root.inclusion as Record<string, unknown> | undefined) ?? {};
  const exclusion = (root.exclusion as Record<string, unknown> | undefined) ?? {};
  const incBrands = strArray(inclusion.brands);
  const incCategories = strArray(inclusion.categories);
  const incPid1 = strArray(inclusion.pid1);
  const incPid2 = strArray(inclusion.pid2);
  const incProducts = strArray(inclusion.products);
  const excBrands = strArray(exclusion.brands);
  const excCategories = strArray(exclusion.categories);
  const excPid1 = strArray(exclusion.pid1);
  const excPid2 = strArray(exclusion.pid2);
  const excProducts = strArray(exclusion.products);
  const excMembershipTypes = strArray(exclusion.membershipTypes);
  const excMembershipTiers = strArray(exclusion.membershipTiers);
  const minSpend = strField(inclusion.minSpend);
  const hasInclusion =
    incBrands.length > 0 ||
    incCategories.length > 0 ||
    incPid1.length > 0 ||
    incPid2.length > 0 ||
    incProducts.length > 0 ||
    minSpend.length > 0;
  const hasExclusion =
    excBrands.length > 0 ||
    excCategories.length > 0 ||
    excPid1.length > 0 ||
    excPid2.length > 0 ||
    excProducts.length > 0 ||
    excMembershipTypes.length > 0 ||
    excMembershipTiers.length > 0;

  return {
    inclusionEnabled: hasInclusion || !hasExclusion,
    incBrands,
    incCategories,
    incPid1,
    incPid2,
    incProducts,
    cartAllQualifiers: boolField(inclusion.cartAllQualifiers),
    minSpend,
    minSpendNet: boolField(inclusion.minSpendNet),
    exclusionEnabled: hasExclusion,
    excBrands,
    excCategories,
    excPid1,
    excPid2,
    excProducts,
    excMembershipTypes,
    excMembershipTiers,
  };
}

export function serializeQualifiersJson(state: QualifiersFormState): string {
  return JSON.stringify({
    inclusion: state.inclusionEnabled
      ? {
          brands: state.incBrands,
          categories: state.incCategories,
          pid1: state.incPid1,
          pid2: state.incPid2,
          products: state.incProducts,
          cartAllQualifiers: state.cartAllQualifiers,
          minSpend: state.minSpend,
          minSpendNet: state.minSpendNet,
        }
      : {},
    exclusion: state.exclusionEnabled
      ? {
          brands: state.excBrands,
          categories: state.excCategories,
          pid1: state.excPid1,
          pid2: state.excPid2,
          products: state.excProducts,
          membershipTypes: state.excMembershipTypes,
          membershipTiers: state.excMembershipTiers,
        }
      : {},
  });
}

export type PromotionDetailsFormState = {
  orderDetailMessage: string;
  promotionDetails: string;
  showPromoOnProductPage: boolean;
  promotionMessagePriority: string;
  campaignPriority: string;
  blockOtherPromos: boolean;
  blockSamePriority: boolean;
  activationCodeEnabled: boolean;
  activationCode: string;
};

export function parsePromotionDetailsJson(raw: string | undefined): PromotionDetailsFormState {
  const p = parseJsonRecord(raw);
  const activationCode = strField(p.activationCode);
  return {
    orderDetailMessage: strField(p.orderDetailMessage),
    promotionDetails: strField(p.promotionDetails),
    showPromoOnProductPage: boolField(p.showPromoOnProductPage),
    promotionMessagePriority: strField(p.promotionMessagePriority),
    campaignPriority: strField(p.campaignPriority),
    blockOtherPromos: boolField(p.blockOtherPromos),
    blockSamePriority: boolField(p.blockSamePriority),
    activationCodeEnabled: activationCode.length > 0 || boolField(p.activationCodeEnabled),
    activationCode,
  };
}

export function serializePromotionDetailsJson(state: PromotionDetailsFormState): string {
  return JSON.stringify({
    orderDetailMessage: state.orderDetailMessage,
    promotionDetails: state.promotionDetails,
    showPromoOnProductPage: state.showPromoOnProductPage,
    promotionMessagePriority: state.promotionMessagePriority,
    campaignPriority: state.campaignPriority,
    blockOtherPromos: state.blockOtherPromos,
    blockSamePriority: state.blockSamePriority,
    activationCodeEnabled: state.activationCodeEnabled,
    activationCode: state.activationCodeEnabled ? state.activationCode : "",
  });
}

export function parseMechanicsJson(
  kind: CampaignEditorKind,
  raw: string | undefined
): {
  itemMechanics: PwpItemMechanicsValue;
  discountMechanics: PwpDiscountMechanicsValue;
  mixMatchMechanics: MixMatchMechanicsValue;
  productDiscountMechanics: ProductDiscountMechanicsValue;
  afterSalesMechanics: AfterSalesMechanicsValue;
} {
  const defaults = {
    itemMechanics: {
      promotionProducts: [] as string[],
      qualifyingProductsPerSet: "",
      maxPromotionalProductsPerUser: "",
    },
    discountMechanics: {
      promotionProducts: [] as string[],
      discountType: "fixed" as const,
      discountMode: "add" as const,
      discountValue: "",
      qualifyingProductsPerSet: "",
      maxPromotionalProductsPerUser: "",
      promotionMessageOnProducts: "",
    },
    mixMatchMechanics: defaultMixMatchMechanicsValue(),
    productDiscountMechanics: defaultProductDiscountMechanicsValue(),
    afterSalesMechanics: defaultAfterSalesMechanicsValue(),
  };

  if (!raw) return defaults;

  try {
    const parsed = JSON.parse(raw) as Record<string, unknown>;
    switch (kind) {
      case "pwp-item":
        return { ...defaults, itemMechanics: { ...defaults.itemMechanics, ...parsed } as PwpItemMechanicsValue };
      case "pwp-discount":
        return { ...defaults, discountMechanics: { ...defaults.discountMechanics, ...parsed } as PwpDiscountMechanicsValue };
      case "mix-match":
        return { ...defaults, mixMatchMechanics: { ...defaults.mixMatchMechanics, ...parsed } as MixMatchMechanicsValue };
      case "product-discount":
        return {
          ...defaults,
          productDiscountMechanics: { ...defaults.productDiscountMechanics, ...parsed } as ProductDiscountMechanicsValue,
        };
      case "after-sales":
        return { ...defaults, afterSalesMechanics: { ...defaults.afterSalesMechanics, ...parsed } as AfterSalesMechanicsValue };
      default:
        return defaults;
    }
  } catch {
    return defaults;
  }
}

export function parseNameJson(nameJson: string): Record<string, string> {
  try {
    const parsed = JSON.parse(nameJson) as Record<string, string>;
    return parsed && typeof parsed === "object" ? parsed : { en: nameJson };
  } catch {
    return { en: nameJson };
  }
}

export function historyFromApi(rows: HistoryEntry[]): CampaignChangeHistoryEntry[] {
  return rows.map((h) => ({
    id: String(h.id),
    at: h.createdAt,
    user: h.actorUserId || "system",
    field: h.comment?.trim() ? `Workflow (${h.comment})` : "Workflow",
    oldValue: WORKFLOW_LABEL[h.fromState] ?? h.fromState,
    newValue: WORKFLOW_LABEL[h.toState] ?? h.toState,
  }));
}

export type CampaignFormSnapshot = {
  campaignKind: CampaignEditorKind;
  code: string;
  nameLocales: Record<string, string>;
  channel: CampaignChannel;
  budget: string;
  audience: string;
  startDate: string;
  endDate: string;
  activeDays: string[];
  activeMonths: string[];
  qualifiers: QualifiersFormState;
  promotionDetails: PromotionDetailsFormState;
  itemMechanics: PwpItemMechanicsValue;
  discountMechanics: PwpDiscountMechanicsValue;
  mixMatchMechanics: MixMatchMechanicsValue;
  productDiscountMechanics: ProductDiscountMechanicsValue;
  afterSalesMechanics: AfterSalesMechanicsValue;
};

export function snapshotFromDetail(detail: CampaignDetail): CampaignFormSnapshot {
  const kind = detail.editorKind as CampaignEditorKind;
  const mechanics = parseMechanicsJson(kind, detail.mechanicsJson);
  const promotion = parsePromotionDetailsJson(detail.promotionDetailsJson);
  const channel = CAMPAIGN_CHANNELS.includes(detail.channel as CampaignChannel)
    ? (detail.channel as CampaignChannel)
    : "Email";

  return {
    campaignKind: kind,
    code: detail.code,
    nameLocales: parseNameJson(detail.nameJson),
    channel,
    budget: detail.budget ?? "",
    audience: detail.audience ?? "",
    startDate: isoToDatetimeLocal(detail.startDate),
    endDate: isoToDatetimeLocal(detail.endDate),
    activeDays: parseJsonArray(detail.activeDaysJson),
    activeMonths: parseJsonArray(detail.activeMonthsJson),
    qualifiers: parseQualifiersJson(detail.qualifiersJson),
    promotionDetails: promotion,
    ...mechanics,
  };
}

export function workflowFromDetail(detail: CampaignDetail): CampaignWorkflowState {
  const valid: CampaignWorkflowState[] = [
    "draft", "pending_approval", "approved", "active", "deactivated", "archived", "rejected",
  ];
  return valid.includes(detail.workflowState as CampaignWorkflowState)
    ? (detail.workflowState as CampaignWorkflowState)
    : "draft";
}
