import { useEffect, useRef, useState } from "react";
import { eStoreHashForPage, parseEStorePageFromHash } from "./estoreHashRouting";
import { EStoreLayout, type EStorePageId, type EStoreSidebarScope } from "./layout/EStoreLayout";
import { LanguageProvider } from "./LanguageContext";
import type { BrandListRow } from "./pages/BrandsPage";
import { BrandsPage } from "./pages/BrandsPage";
import { CategoriesPage } from "./pages/CategoriesPage";
import { EditBrandPage } from "./pages/EditBrandPage";
import { EditProductPage } from "./pages/EditProductPage";
import { ProductImageImportPage } from "./pages/ProductImageImportPage";
import { createBrand, deleteBrand, fetchBrands, updateBrand } from "./api/brandsApi";
import {
  createCollectionLocationApi,
  createFulfillmentGrouping,
  createFulfillmentSlot,
  createLogisticPartnerApi,
  deleteCollectionLocationApi,
  deleteFulfillmentGrouping,
  deleteFulfillmentSlot,
  deleteLogisticPartnerApi,
  fetchCollectionLocations,
  fetchFulfillmentGroupings,
  fetchFulfillmentSettings,
  fetchFulfillmentSlots,
  fetchLogisticPartnersApi,
  putFulfillmentSettings,
  updateCollectionLocationApi,
  updateFulfillmentGrouping,
  updateFulfillmentSlot,
  updateLogisticPartnerApi,
} from "./api/fulfillmentApi";
import { ProductImportPage } from "./pages/ProductImportPage";
import { ProductInventoryImportPage } from "./pages/ProductInventoryImportPage";
import { AdvancePriceImportPage } from "./pages/AdvancePriceImportPage";
import { CategoryAssignmentPage } from "./pages/CategoryAssignmentPage";
import { ProductConfigPage } from "./pages/ProductConfigPage";
import { EditAttributePage } from "./pages/EditAttributePage";
import { AttributeImportPage } from "./pages/AttributeImportPage";
import { EditPromoPage } from "./pages/EditPromoPage";
import { GroupedPromoCodesPage } from "./pages/GroupedPromoCodesPage";
import { PromoExclusionListPage } from "./pages/PromoExclusionListPage";
import type { PromoListRow } from "./promotions-columns";
import type { AttributeListRow } from "./attributes-columns";
import type { ProductListRow } from "./pages/ProductsPage";
import { ProductsPage } from "./pages/ProductsPage";
import { BestSellerSettingsPage } from "./pages/BestSellerSettingsPage";
import { ProductQuantityLimitSettingsPage } from "./pages/ProductQuantityLimitSettingsPage";
import { DEMO_PRODUCT_ROWS } from "./demo/demoProductRows";
import { AttributesPage } from "./pages/AttributesPage";
import {
  BrandConfigPage,
  DEFAULT_BRAND_ADDITIONAL_FIELDS,
  type BrandAdditionalField,
} from "./pages/BrandConfigPage";
import { FulfillmentOptionsPage } from "./pages/FulfillmentOptionsPage";
import { EditFulfillmentOptionPage } from "./pages/EditFulfillmentOptionPage";
import { CollectionLocationsPage } from "./pages/CollectionLocationsPage";
import { DeliveryAllocationPage } from "./pages/DeliveryAllocationPage";
import { FulfillmentSlotsPage } from "./pages/FulfillmentSlotsPage";
import type { CampaignListRow } from "./campaigns-columns";
import { CampaignsPage } from "./pages/CampaignsPage";
import { EditCampaignPage } from "./pages/EditCampaignPage";
import { PromotionsPage } from "./pages/PromotionsPage";
import { LogisticPartnerManagementPage } from "./pages/LogisticPartnerManagementPage";
import { UsersPage } from "./pages/access/UsersPage";
import { UserFormPage } from "./pages/access/UserFormPage";
import { ChangePasswordModal } from "./pages/access/ChangePasswordModal";
import { RolesPage } from "./pages/access/RolesPage";
import { RoleFormPage } from "./pages/access/RoleFormPage";
import { ManageModulesPage } from "./pages/access/ManageModulesPage";
import { TenantsPage } from "./pages/access/TenantsPage";
import { TenantFormPage } from "./pages/access/TenantFormPage";
import { TemplatesPage } from "./pages/TemplatesPage";
import type { TenantRow } from "./api/tenantsApi";

type BrandEditData = Pick<BrandListRow, "code" | "name" | "active" | "imageSrc" | "imageAlt">;

type BrandFormState =
  | { mode: "idle" }
  | { mode: "add" }
  | { mode: "edit"; data: BrandEditData };

type PromoType = "standard" | "shareable" | "account-bound";

type PromoFormState =
  | { mode: "idle" }
  | { mode: "add"; promoType: PromoType }
  | { mode: "edit"; data: PromoListRow }
  | { mode: "grouped"; parent: PromoListRow }
  | { mode: "exclusion-list" };

type CampaignFormState = { mode: "idle" } | { mode: "add" } | { mode: "edit"; data: CampaignListRow };

type AttributeFormState =
  | { mode: "idle" }
  | { mode: "add" }
  | { mode: "edit"; data: AttributeListRow }
  | { mode: "attr-import" };

type ProductFormState =
  | { mode: "idle" }
  | { mode: "add" }
  | { mode: "edit"; data: ProductListRow }
  | { mode: "import" }
  | { mode: "image-import" }
  | { mode: "inventory-import" }
  | { mode: "advance-price-import" };

type FulfillmentFormState =
  | { mode: "idle" }
  | { mode: "slots"; groupingId: string }
  | { mode: "edit-slot"; groupingId: string; slotId?: string }
  | { mode: "logistic-partners" };

type AccessUserFormState =
  | { mode: "idle" }
  | { mode: "add" }
  | { mode: "edit"; userId: string }
  | { mode: "change-password"; userId: string; userName: string };

type AccessRoleFormState =
  | { mode: "idle" }
  | { mode: "add" }
  | { mode: "edit"; roleAlias: string }
  | { mode: "manage-modules"; roleAlias: string; roleName: string };

type AccessTenantFormState =
  | { mode: "idle" }
  | { mode: "add" }
  | { mode: "edit"; row: TenantRow };

export type FulfillmentDeliveryMode = "Store Collection" | "Local Delivery" | "Overseas Delivery";

export type FulfillmentGrouping = {
  id: string;
  groupName: string;
  code: string;
  startDate: string;
  endDate: string;
  priority: number;
  active: boolean;
  tenantEnabled: boolean;
  maxPerTenant: number | "";
  deliveryMode: FulfillmentDeliveryMode;
  limitSelectedDistributionCenter: boolean;
  stockLocation: string;
};

export type CollectionLocation = {
  id: string;
  name: string;
  brandCodes?: string[];
  address1?: string;
  address2?: string;
  address3?: string;
  postalCode?: string;
  country?: string;
  geoLat?: string;
  geoLng?: string;
  desktopImageSrc?: string;
  desktopImageName?: string;
  mobileImageSrc?: string;
  mobileImageName?: string;
  active: boolean;
  openingHours?: string;
  closingHours?: string;
};

export type FulfillmentPredefinedFieldKey =
  | "blockOffDatesAndTimes"
  | "blockedDates"
  | "collectionLocations"
  | "excludedBrands"
  | "excludedCategories"
  | "excludedProducts"
  | "freeDeliveryMinSpend"
  | "includedBrands"
  | "includedCategories"
  | "includedProducts"
  | "logisticPartner"
  | "numberOfDeliverySlots"
  | "openingAndClosingHours"
  | "recipientNotificationEmails"
  | "timeslot";

export type FulfillmentPredefinedFieldSetting = {
  key: FulfillmentPredefinedFieldKey;
  label: string;
  enabled: boolean;
};

export type LogisticPartner = {
  id: string;
  name: string;
  code: string;
  enabled: boolean;
  integratedLogistic: boolean;
};

export type StockLocation = {
  id: string;
  name: string;
  code: string;
  active: boolean;
};

export type FulfillmentSlot = {
  id: string;
  groupingId: string;
  name: string;
  code: string;
  mode: FulfillmentDeliveryMode;
  startingDate: string;
  endingDate: string;
  price: string;
  updatedAt: string;
};

export type FulfillmentFieldType =
  | "Text Box"
  | "WYSIWYG"
  | "Geo Location"
  | "Dropdown List"
  | "Checkbox"
  | "Date Picker"
  | "Multiple Select";

export type FulfillmentFieldSection =
  | "Additional Info"
  | "General Information"
  | "Qualifier"
  | "Fulfillment Settings"
  | "Advanced Settings";

export type FulfillmentDynamicField = {
  id: string;
  enabled: boolean;
  required: boolean;
  property: string;
  columnLabel: string;
  fieldName: string;
  fieldType: FulfillmentFieldType;
  section: FulfillmentFieldSection;
};

const LS_KEYS = {
  fulfillmentPredefinedFields: "dcms.estore.fulfillment.predefinedFields.v1",
  collectionLocations: "dcms.estore.fulfillment.collectionLocations.v1",
  logisticPartners: "dcms.estore.fulfillment.logisticPartners.v1",
  brandAdditionalFields: "dcms.estore.brandAdditionalFields.v1",
  bestSellerFlags: "dcms.estore.bestSellerFlags.v1",
};

const DEFAULT_BEST_SELLER_FLAGS: Record<string, boolean> = {
  "1": true,
  "2": true,
  "3": false,
  "4": true,
};

const DEFAULT_BRANDS: BrandListRow[] = [
  {
    code: "CAS-7721",
    name: "Luxe Heritage Group",
    imageAlt: "Minimalist abstract geometric logo with sharp angles and clean lines on a neutral background",
    imageSrc:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuCE3gnGOwxCBJd-O91kFWC9Ys7MmX3uUyKEP-Uue7NKGilCk0aTlec5YS0d89W8x9X8lkFFpgpHVGV3OTFE7z1Kf6xTIMgldQEaj23BDB0Mtbv9BM66FWGZxZ6pMOJmbzOcMIXlCDJHHewraamlXGLG10IQ4Dj0iVkie9tj7INZzIeLlaFZ7NAzTRb_bi7sWHuiRmYFDCePqwhSc4knDdag68Xf1rqs61T6Rcndy0QGtMQFBuMRLTgEX1fBqRnBJfJV1E1j8AvJKSA",
    active: true,
  },
  {
    code: "VEL-4490",
    name: "Velocity Tech Systems",
    imageAlt: "Modern corporate emblem with a circular motif and elegant serif typography in a dark muted tone",
    imageSrc:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuBOW2Bfv61jFiGsVEhs7pzGpS2bL8mHHiCdt3wGBYJ_ood63gKxVLi90yWNmxSQ7mhrlQ3p9VmqGoBTixTSsBi9zOrCnT1n0TSew0T3VEsqKqtdiUPOkvLmI6-5nodDQxlZpHnpWLE6aa1tw7yCiISzAgIDdR22bNseDLp-UUPH7h465EwLX5K5cuYiIyZrUDNNsjZ6xe9s4Frg-I-4hjmFYXRHpU-42kVk5L0mwUp9xsrKgyqj0KTv29mjGsNBAKQDGn9F0vWs0i4",
    active: true,
  },
  {
    code: "NOM-1022",
    name: "Nomad Consulting Ltd.",
    imageAlt: "Monochrome architectural brand logo using bold slab-serif lettering for a professional consulting firm",
    imageSrc:
      "https://lh3.googleusercontent.com/aida-public/AB6AXuB7yIQm-Rz09NCKbfnAZSD_9byiIdPPRNMvM1zXkTvyIowg7YuBmlKlQnwxaS8E0aITXNzhLloRDUYZljsWGyHB7ExQysjzKkTNPOg7NHucWjXDqdtDDb658MXRzFMDnyOCI4yCJgMy7tOks2pnxS_yLLsL1-FHXBqE2YhkrZkb5ym2Nbv-Zj_QohTQ7PfY-dFy7p0DiUxzLm9-wRbWIq-ilG5Sh0amZH3_jZmD0KfT0LuHvXN05S7bOnalnXlWaroTbAAijJdKV_I",
    active: false,
  },
  {
    code: "AUR-5501",
    name: "Aura Essentials",
    imageAlt: "Stylized floral emblem logo in soft golden tones on a clean white field for a luxury skincare brand",
    imageSrc: "https://lh3.googleusercontent.com/aida-public/AB6AXuAMgUTMGnlA5lx3Ejy3wYKSGMHWnJv8lCV_jSKJ2sVYXFo94qLsU4VjhhZLcgBNfst7UdqOk45Ld4PSbA4U9mtdFPfI6iXkwbrSlXMkg_UdlONO9bGP6roflznlaLK3HdEAqeL9ef6KZmVW4uptu5pb_PNpTgkX_8JYm4Di2d2JaUHbOQ_SzXCCzLbeyb0I67i5nfwf1u2gNeJiSJk5IB0nweV7Xwm2hT-_bIw",
    active: true,
  },
];

const DEFAULT_PREDEFINED_FIELDS: FulfillmentPredefinedFieldSetting[] = [
  { key: "blockOffDatesAndTimes", label: "Block Off Dates and Times", enabled: true },
  { key: "blockedDates", label: "Blocked Dates", enabled: true },
  { key: "collectionLocations", label: "Collection Locations", enabled: true },
  { key: "excludedBrands", label: "Excluded Brands", enabled: true },
  { key: "excludedCategories", label: "Excluded Categories", enabled: true },
  { key: "excludedProducts", label: "Excluded Products", enabled: true },
  { key: "freeDeliveryMinSpend", label: "Free Delivery Min Spend", enabled: true },
  { key: "includedBrands", label: "Included Brands", enabled: true },
  { key: "includedCategories", label: "Included Categories", enabled: true },
  { key: "includedProducts", label: "Included Products", enabled: true },
  { key: "logisticPartner", label: "Logistic Partner", enabled: true },
  { key: "numberOfDeliverySlots", label: "Number of Delivery Slots", enabled: true },
  { key: "openingAndClosingHours", label: "Opening and Closing Hours", enabled: true },
  { key: "recipientNotificationEmails", label: "Recipient Notification Emails", enabled: true },
  { key: "timeslot", label: "Timeslot", enabled: true },
];

/** Demo / offline fallback when `tenantId` is absent (DAI-615). Never used as initial data for API mode. */
const DEMO_FULFILLMENT_GROUPINGS: FulfillmentGrouping[] = [
  {
    id: "grp-1",
    groupName: "Standard Delivery",
    code: "STD_DELIVERY",
    startDate: "2026-04-01T00:00",
    endDate: "",
    priority: 10,
    active: true,
    tenantEnabled: false,
    maxPerTenant: "",
    deliveryMode: "Local Delivery",
    limitSelectedDistributionCenter: false,
    stockLocation: "",
  },
];

const DEMO_FULFILLMENT_SLOTS: FulfillmentSlot[] = [
  {
    id: "slot-1",
    groupingId: "grp-1",
    name: "10am – 4pm",
    code: "SLOT_10_16",
    mode: "Local Delivery",
    startingDate: "2026-04-01T00:00",
    endingDate: "",
    price: "5.99",
    updatedAt: "2026-04-10 09:30",
  },
  {
    id: "slot-2",
    groupingId: "grp-1",
    name: "4pm – 10pm",
    code: "SLOT_16_22",
    mode: "Local Delivery",
    startingDate: "2026-04-01T00:00",
    endingDate: "",
    price: "7.99",
    updatedAt: "2026-04-12 15:20",
  },
];

const DEMO_STOCK_LOCATIONS: StockLocation[] = [
  { id: "dc-1", name: "DC - Central", code: "DC_CENTRAL", active: true },
  { id: "dc-2", name: "DC - East", code: "DC_EAST", active: true },
  { id: "dc-3", name: "DC - West", code: "DC_WEST", active: false },
];

const DEMO_DYNAMIC_FIELDS: FulfillmentDynamicField[] = [
  {
    id: "df-1",
    enabled: true,
    required: false,
    property: "deliveryNote",
    columnLabel: "Delivery Note",
    fieldName: "delivery_note",
    fieldType: "Text Box",
    section: "Additional Info",
  },
  {
    id: "df-2",
    enabled: false,
    required: false,
    property: "pickupInstructions",
    columnLabel: "Pickup Instructions",
    fieldName: "pickup_instructions",
    fieldType: "WYSIWYG",
    section: "Additional Info",
  },
];

const DEMO_LOGISTIC_PARTNERS: LogisticPartner[] = [
  { id: "lp-1", name: "DHL", code: "DHL", enabled: true, integratedLogistic: true },
  { id: "lp-2", name: "FedEx", code: "FEDEX", enabled: true, integratedLogistic: true },
  { id: "lp-3", name: "Ninja Van", code: "NINJA_VAN", enabled: false, integratedLogistic: false },
  { id: "lp-4", name: "J&T Express", code: "JNT", enabled: true, integratedLogistic: false },
];

const DEMO_COLLECTION_LOCATIONS: CollectionLocation[] = [
  {
    id: "LOC_MAIN",
    name: "Main Store",
    brandCodes: ["CAS-7721", "VEL-4490"],
    address1: "123 Market Street",
    address2: "",
    address3: "",
    postalCode: "000000",
    country: "Singapore",
    geoLat: "1.3000",
    geoLng: "103.8000",
    active: true,
    openingHours: "10:00",
    closingHours: "22:00",
  },
  {
    id: "LOC_WAREHOUSE",
    name: "Warehouse Pickup",
    brandCodes: ["AUR-5501"],
    address1: "88 Industrial Ave",
    address2: "",
    address3: "",
    postalCode: "000000",
    country: "Singapore",
    geoLat: "1.3100",
    geoLng: "103.8200",
    active: true,
    openingHours: "09:00",
    closingHours: "18:00",
  },
];

function safeJsonParse<T>(raw: string | null): T | null {
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;
  }
}

function initialPageForScope(sidebarScope: EStoreSidebarScope): EStorePageId {
  const parsed = parseEStorePageFromHash();
  if (sidebarScope === "access") {
    if (parsed && parsed.startsWith("access-")) return parsed;
    return "access-users";
  }
  if (parsed && parsed.startsWith("access-")) return "brands";
  return parsed ?? "brands";
}

export function EStoreApp({
  languages,
  sidebarScope = "estore",
  tenantId,
  storeId,
  authToken,
}: {
  languages?: import("./useUmbracoLanguages").UmbracoLanguage[];
  sidebarScope?: EStoreSidebarScope;
  /** Umbraco tenant ID for Catalog API calls. Falls back to demo data when absent. */
  tenantId?: string;
  /** Store ID for store-scoped catalog settings (DAI-616). */
  storeId?: string;
  /** Bearer token from Umbraco auth context. */
  authToken?: string;
}) {
  const [page, setPage] = useState<EStorePageId>(() => initialPageForScope(sidebarScope));
  const [brandForm, setBrandForm] = useState<BrandFormState>({ mode: "idle" });
  const [productForm, setProductForm] = useState<ProductFormState>({ mode: "idle" });
  const [attributeForm, setAttributeForm] = useState<AttributeFormState>({ mode: "idle" });
  const [promoForm, setPromoForm] = useState<PromoFormState>({ mode: "idle" });
  const [campaignForm, setCampaignForm] = useState<CampaignFormState>({ mode: "idle" });
  const [fulfillmentForm, setFulfillmentForm] = useState<FulfillmentFormState>({ mode: "idle" });
  /** When tenantId is set: false until Fulfillment API hydration completes (avoid persisting demo defaults). */
  const [fulfillmentSynced, setFulfillmentSynced] = useState(() => !tenantId);
  const [fulfillmentLoading, setFulfillmentLoading] = useState(false);
  const [fulfillmentError, setFulfillmentError] = useState<string | null>(null);
  /** DAI-615: true after successful tenant-scoped fulfillment fetch (settings PUT waits for this). */
  const fulfillmentLoadedRef = useRef(false);
  const [fulfillmentToast, setFulfillmentToast] = useState<string | null>(null);
  const [accessUserForm, setAccessUserForm] = useState<AccessUserFormState>({ mode: "idle" });
  const [accessRoleForm, setAccessRoleForm] = useState<AccessRoleFormState>({ mode: "idle" });
  const [accessTenantForm, setAccessTenantForm] = useState<AccessTenantFormState>({ mode: "idle" });

  // ── Brands: API-backed when tenantId provided, fallback to DEFAULT_BRANDS ──
  const [brands, setBrands] = useState<BrandListRow[]>(DEFAULT_BRANDS);
  const [brandsLoading, setBrandsLoading] = useState(false);
  const [brandsError, setBrandsError] = useState<string | null>(null);
  const brandsLoadedRef = useRef(false);

  useEffect(() => {
    if (!tenantId) return;
    if (brandsLoadedRef.current) return;
    brandsLoadedRef.current = true;
    setBrandsLoading(true);
    fetchBrands(tenantId, {}, authToken)
      .then(({ rows }) => {
        setBrands(rows);
        setBrandsError(null);
      })
      .catch((err: unknown) => {
        setBrandsError(err instanceof Error ? err.message : "Failed to load brands");
      })
      .finally(() => setBrandsLoading(false));
  }, [tenantId, authToken]);

  const [brandAdditionalFields, setBrandAdditionalFields] = useState<BrandAdditionalField[]>(
    () =>
      safeJsonParse<BrandAdditionalField[]>(localStorage.getItem(LS_KEYS.brandAdditionalFields)) ??
      DEFAULT_BRAND_ADDITIONAL_FIELDS
  );

  const [bestSellerFlags, setBestSellerFlags] = useState<Record<string, boolean>>(
    () =>
      safeJsonParse<Record<string, boolean>>(localStorage.getItem(LS_KEYS.bestSellerFlags)) ??
      DEFAULT_BEST_SELLER_FLAGS
  );

  const [predefinedFieldSettings, setPredefinedFieldSettings] = useState<FulfillmentPredefinedFieldSetting[]>(() =>
    tenantId
      ? DEFAULT_PREDEFINED_FIELDS
      : safeJsonParse<FulfillmentPredefinedFieldSetting[]>(localStorage.getItem(LS_KEYS.fulfillmentPredefinedFields)) ??
        DEFAULT_PREDEFINED_FIELDS
  );

  const [logisticPartners, setLogisticPartners] = useState<LogisticPartner[]>(() =>
    tenantId
      ? []
      : safeJsonParse<LogisticPartner[]>(localStorage.getItem(LS_KEYS.logisticPartners)) ?? DEMO_LOGISTIC_PARTNERS
  );

  const [fulfillmentGroupings, setFulfillmentGroupings] = useState<FulfillmentGrouping[]>(() =>
    tenantId ? [] : DEMO_FULFILLMENT_GROUPINGS
  );

  const [collectionLocations, setCollectionLocations] = useState<CollectionLocation[]>(() =>
    tenantId
      ? []
      : safeJsonParse<CollectionLocation[]>(localStorage.getItem(LS_KEYS.collectionLocations)) ?? DEMO_COLLECTION_LOCATIONS
  );

  useEffect(() => {
    if (tenantId) return;
    localStorage.setItem(LS_KEYS.collectionLocations, JSON.stringify(collectionLocations));
  }, [collectionLocations, tenantId]);

  useEffect(() => {
    if (tenantId) return;
    localStorage.setItem(LS_KEYS.fulfillmentPredefinedFields, JSON.stringify(predefinedFieldSettings));
  }, [predefinedFieldSettings, tenantId]);

  useEffect(() => {
    if (tenantId) return;
    localStorage.setItem(LS_KEYS.logisticPartners, JSON.stringify(logisticPartners));
  }, [logisticPartners, tenantId]);

  useEffect(() => {
    localStorage.setItem(LS_KEYS.brandAdditionalFields, JSON.stringify(brandAdditionalFields));
  }, [brandAdditionalFields]);

  useEffect(() => {
    localStorage.setItem(LS_KEYS.bestSellerFlags, JSON.stringify(bestSellerFlags));
  }, [bestSellerFlags]);

  const [stockLocations, setStockLocations] = useState<StockLocation[]>(() => (tenantId ? [] : DEMO_STOCK_LOCATIONS));

  const [fulfillmentSlots, setFulfillmentSlots] = useState<FulfillmentSlot[]>(() =>
    tenantId ? [] : DEMO_FULFILLMENT_SLOTS
  );

  const [dynamicFields, setDynamicFields] = useState<FulfillmentDynamicField[]>(() =>
    tenantId ? [] : DEMO_DYNAMIC_FIELDS
  );

  useEffect(() => {
    if (!fulfillmentError) return;
    setFulfillmentToast(fulfillmentError);
    const t = window.setTimeout(() => setFulfillmentToast(null), 5000);
    return () => window.clearTimeout(t);
  }, [fulfillmentError]);

  useEffect(() => {
    if (!tenantId) {
      fulfillmentLoadedRef.current = false;
      setFulfillmentSynced(true);
      setFulfillmentLoading(false);
      setFulfillmentError(null);
      setFulfillmentGroupings(DEMO_FULFILLMENT_GROUPINGS);
      setFulfillmentSlots(DEMO_FULFILLMENT_SLOTS);
      setStockLocations(DEMO_STOCK_LOCATIONS);
      setDynamicFields(DEMO_DYNAMIC_FIELDS);
      setPredefinedFieldSettings(
        safeJsonParse<FulfillmentPredefinedFieldSetting[]>(localStorage.getItem(LS_KEYS.fulfillmentPredefinedFields)) ??
          DEFAULT_PREDEFINED_FIELDS
      );
      setLogisticPartners(
        safeJsonParse<LogisticPartner[]>(localStorage.getItem(LS_KEYS.logisticPartners)) ?? DEMO_LOGISTIC_PARTNERS
      );
      setCollectionLocations(
        safeJsonParse<CollectionLocation[]>(localStorage.getItem(LS_KEYS.collectionLocations)) ??
          DEMO_COLLECTION_LOCATIONS
      );
      return;
    }

    fulfillmentLoadedRef.current = false;
    setFulfillmentSynced(false);
    setFulfillmentLoading(true);
    setFulfillmentError(null);
    setFulfillmentGroupings([]);
    setFulfillmentSlots([]);
    setStockLocations([]);
    setDynamicFields([]);
    setCollectionLocations([]);
    setLogisticPartners([]);
    setPredefinedFieldSettings(DEFAULT_PREDEFINED_FIELDS);
    let cancelled = false;
    (async () => {
      try {
        const [groupings, locations, partners, settings] = await Promise.all([
          fetchFulfillmentGroupings(tenantId, authToken),
          fetchCollectionLocations(tenantId, authToken),
          fetchLogisticPartnersApi(tenantId, authToken),
          fetchFulfillmentSettings(tenantId, authToken),
        ]);
        if (cancelled) return;
        const slotLists = await Promise.all(
          groupings.map((g) => fetchFulfillmentSlots(tenantId, g.id, authToken))
        );
        if (cancelled) return;
        setFulfillmentGroupings(groupings);
        setFulfillmentSlots(slotLists.flat());
        setCollectionLocations(locations);
        setLogisticPartners(partners);
        setPredefinedFieldSettings(
          settings.predefinedFields.length > 0 ? settings.predefinedFields : DEFAULT_PREDEFINED_FIELDS
        );
        setDynamicFields(settings.dynamicFields);
        setStockLocations(settings.stockLocations);
        fulfillmentLoadedRef.current = true;
      } catch (err) {
        fulfillmentLoadedRef.current = false;
        if (!cancelled) {
          setFulfillmentError(err instanceof Error ? err.message : "Failed to load fulfillment data");
        }
      } finally {
        if (!cancelled) {
          setFulfillmentLoading(false);
          setFulfillmentSynced(true);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [tenantId, authToken]);

  useEffect(() => {
    if (!tenantId || !fulfillmentSynced || !fulfillmentLoadedRef.current) return;
    const t = window.setTimeout(() => {
      void putFulfillmentSettings(
        tenantId,
        {
          predefinedFields: predefinedFieldSettings,
          dynamicFields,
          stockLocations,
        },
        authToken
      ).catch((e) =>
        setFulfillmentError(e instanceof Error ? e.message : "Failed to save fulfillment settings")
      );
    }, 600);
    return () => window.clearTimeout(t);
  }, [tenantId, fulfillmentSynced, predefinedFieldSettings, dynamicFields, stockLocations, authToken]);

  async function handleCollectionLocationsChange(next: CollectionLocation[]) {
    if (!tenantId) {
      setCollectionLocations(next);
      return;
    }
    const prev = collectionLocations;
    try {
      setFulfillmentError(null);
      if (next.length < prev.length) {
        const removed = prev.find((p) => !next.some((n) => n.id === p.id));
        if (removed) await deleteCollectionLocationApi(tenantId, removed.id, authToken);
      } else if (next.length > prev.length) {
        const added = next.find((n) => !prev.some((p) => p.id === n.id));
        if (added) await createCollectionLocationApi(tenantId, added, authToken);
      } else {
        for (const n of next) {
          const o = prev.find((p) => p.id === n.id);
          if (!o) continue;
          const snap = ({ id: _id, ...r }: CollectionLocation) => JSON.stringify(r);
          if (snap(o) !== snap(n)) await updateCollectionLocationApi(tenantId, n.id, n, authToken);
        }
      }
      setCollectionLocations(await fetchCollectionLocations(tenantId, authToken));
    } catch (err) {
      setFulfillmentError(err instanceof Error ? err.message : "Collection location sync failed");
    }
  }

  async function handleLogisticPartnersChange(next: LogisticPartner[]) {
    if (!tenantId) {
      setLogisticPartners(next);
      return;
    }
    const prev = logisticPartners;
    try {
      setFulfillmentError(null);
      if (next.length < prev.length) {
        const removed = prev.find((p) => !next.some((n) => n.id === p.id));
        if (removed) await deleteLogisticPartnerApi(tenantId, removed.id, authToken);
      } else if (next.length > prev.length) {
        const added = next.find((n) => !prev.some((p) => p.id === n.id));
        if (added) await createLogisticPartnerApi(tenantId, added, authToken);
      } else {
        for (const n of next) {
          const o = prev.find((p) => p.id === n.id);
          if (!o) continue;
          if (
            o.name !== n.name ||
            o.code !== n.code ||
            o.enabled !== n.enabled ||
            o.integratedLogistic !== n.integratedLogistic
          ) {
            await updateLogisticPartnerApi(tenantId, n.id, n, authToken);
          }
        }
      }
      setLogisticPartners(await fetchLogisticPartnersApi(tenantId, authToken));
    } catch (err) {
      setFulfillmentError(err instanceof Error ? err.message : "Logistic partner sync failed");
    }
  }

  function resetNestedFormsForPage(id: EStorePageId) {
    if (id !== "brands") setBrandForm({ mode: "idle" });
    if (id !== "products") setProductForm({ mode: "idle" });
    if (id !== "attributes") setAttributeForm({ mode: "idle" });
    if (id !== "promo-codes") setPromoForm({ mode: "idle" });
    if (id !== "campaigns") setCampaignForm({ mode: "idle" });
    const inFulfillmentArea =
      id === "fulfillment-options" ||
      id === "fulfillment-delivery-allocation" ||
      id === "fulfillment-collection-locations";
    if (!inFulfillmentArea) {
      setFulfillmentForm({ mode: "idle" });
    }
    const inAccessArea =
      id === "access-users" || id === "access-roles" || id === "access-tenants";
    if (!inAccessArea) {
      setAccessUserForm({ mode: "idle" });
      setAccessRoleForm({ mode: "idle" });
      setAccessTenantForm({ mode: "idle" });
    }
  }

  function handlePageChange(id: EStorePageId) {
    resetNestedFormsForPage(id);
    setPage(id);
    const nextHash = eStoreHashForPage(id);
    if (window.location.hash !== nextHash) {
      window.location.hash = nextHash;
    }
  }

  useEffect(() => {
    function onHashChange() {
      const p = parseEStorePageFromHash();
      if (!p) return;
      if (sidebarScope === "access" && !p.startsWith("access-")) {
        window.location.hash = eStoreHashForPage("access-users");
        return;
      }
      if (sidebarScope === "estore" && p.startsWith("access-")) {
        window.location.hash = eStoreHashForPage("brands");
        return;
      }
      resetNestedFormsForPage(p);
      setPage(p);
    }
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, [sidebarScope]);

  useEffect(() => {
    const p = parseEStorePageFromHash();
    if (!p) {
      window.location.hash = eStoreHashForPage(sidebarScope === "access" ? "access-users" : "brands");
      return;
    }
    if (sidebarScope === "access" && !p.startsWith("access-")) {
      window.location.hash = eStoreHashForPage("access-users");
      return;
    }
    if (sidebarScope === "estore" && p.startsWith("access-")) {
      window.location.hash = eStoreHashForPage("brands");
      return;
    }
    const preferred = eStoreHashForPage(p);
    if (window.location.hash !== preferred) {
      window.location.hash = preferred;
    }
  }, [sidebarScope]);

  return (
    <LanguageProvider languages={languages}>
    <EStoreLayout page={page} onPageChange={handlePageChange} sidebarScope={sidebarScope}>
      {page === "brands" &&
        (brandForm.mode !== "idle" ? (
          <EditBrandPage
            mode={brandForm.mode === "add" ? "add" : "edit"}
            brandCode={brandForm.mode === "edit" ? brandForm.data.code : undefined}
            brandName={brandForm.mode === "edit" ? brandForm.data.name : undefined}
            active={brandForm.mode === "edit" ? brandForm.data.active : undefined}
            logoSrc={brandForm.mode === "edit" ? brandForm.data.imageSrc : undefined}
            logoAlt={brandForm.mode === "edit" ? brandForm.data.imageAlt : undefined}
            additionalFields={brandAdditionalFields}
            onBack={() => setBrandForm({ mode: "idle" })}
            onSave={async (row, additionalInfo) => {
              if (tenantId) {
                try {
                  const saved = brandForm.mode === "add"
                    ? await createBrand(tenantId, row.code, { name: row.name, active: row.active, imageUrl: row.imageSrc, imageAlt: row.imageAlt, additionalInfo }, authToken)
                    : await updateBrand(tenantId, row.code, { name: row.name, active: row.active, imageUrl: row.imageSrc, imageAlt: row.imageAlt, additionalInfo }, authToken);
                  setBrands((prev) => {
                    const exists = prev.some((b) => b.code === saved.code);
                    return exists ? prev.map((b) => (b.code === saved.code ? saved : b)) : [saved, ...prev];
                  });
                } catch {
                  // API error — still update local state optimistically so UI isn't broken
                  setBrands((prev) => {
                    const exists = prev.some((b) => b.code === row.code);
                    return exists ? prev.map((b) => (b.code === row.code ? row : b)) : [row, ...prev];
                  });
                }
              } else {
                // No tenantId — in-memory only (dev / demo mode), additionalInfo not persisted
                setBrands((prev) => {
                  const exists = prev.some((b) => b.code === row.code);
                  return exists ? prev.map((b) => (b.code === row.code ? row : b)) : [row, ...prev];
                });
              }
            }}
          />
        ) : (
          <BrandsPage
            onCreateBrand={() => setBrandForm({ mode: "add" })}
            onEditBrand={(row) =>
              setBrandForm({
                mode: "edit",
                data: {
                  code: row.code,
                  name: row.name,
                  active: row.active,
                  imageSrc: row.imageSrc,
                  imageAlt: row.imageAlt,
                },
              })
            }
            rows={brands}
            loading={brandsLoading}
            loadError={brandsError}
            onRowsChange={async (next) => {
              // Called on delete — determine which row was removed
              if (tenantId) {
                const removed = brands.find((b) => !next.some((n) => n.code === b.code));
                if (removed) {
                  try {
                    await deleteBrand(tenantId, removed.code, authToken);
                  } catch {
                    // ignore — row removed locally regardless
                  }
                }
              }
              setBrands(next);
            }}
          />
        ))}
      {page === "brand-configuration" && (
        <BrandConfigPage
          savedFields={brandAdditionalFields}
          onSave={setBrandAdditionalFields}
          onNavigateToBrands={() => handlePageChange("brands")}
        />
      )}
      {page === "categories" && <CategoriesPage tenantId={tenantId} authToken={authToken} />}
      {page === "product-best-sellers" && (
        <BestSellerSettingsPage
          rows={DEMO_PRODUCT_ROWS}
          bestSellerById={bestSellerFlags}
          onBestSellerChange={setBestSellerFlags}
          onNavigateToProducts={() => handlePageChange("products")}
        />
      )}
      {page === "product-category-assignment" && (
        <CategoryAssignmentPage onNavigateToProducts={() => handlePageChange("products")} />
      )}
      {page === "product-quantity-limit-settings" && (
        <ProductQuantityLimitSettingsPage onNavigateToProducts={() => handlePageChange("products")} />
      )}
      {page === "product-configuration" && (
        <ProductConfigPage
          onNavigateToProducts={() => handlePageChange("products")}
          tenantId={tenantId}
          storeId={storeId}
          authToken={authToken}
        />
      )}
      {page === "templates" && <TemplatesPage tenantId={tenantId} storeId={storeId} authToken={authToken} />}
      {page === "products" &&
        (productForm.mode === "inventory-import" ? (
          <ProductInventoryImportPage tenantId={tenantId} onBack={() => setProductForm({ mode: "idle" })} />
        ) : productForm.mode === "image-import" ? (
          <ProductImageImportPage tenantId={tenantId} onBack={() => setProductForm({ mode: "idle" })} />
        ) : productForm.mode === "advance-price-import" ? (
          <AdvancePriceImportPage tenantId={tenantId} onBack={() => setProductForm({ mode: "idle" })} />
        ) : productForm.mode === "import" ? (
          <ProductImportPage tenantId={tenantId} onBack={() => setProductForm({ mode: "idle" })} />
        ) : productForm.mode !== "idle" ? (
          <EditProductPage
            mode={productForm.mode === "add" ? "add" : "edit"}
            product={productForm.mode === "edit" ? productForm.data : undefined}
            onBack={() => setProductForm({ mode: "idle" })}
          />
        ) : (
          <ProductsPage
            tenantId={tenantId}
            storeId={storeId}
            authToken={authToken}
            onAddProduct={() => setProductForm({ mode: "add" })}
            onEditProduct={(row) => setProductForm({ mode: "edit", data: row })}
            onImportProduct={() => setProductForm({ mode: "import" })}
            onImageImport={() => setProductForm({ mode: "image-import" })}
            onInventoryImport={() => setProductForm({ mode: "inventory-import" })}
            onAdvancePriceImport={() => setProductForm({ mode: "advance-price-import" })}
            onCategoryAssignment={() => handlePageChange("product-category-assignment")}
          />
        ))}
      {page === "campaigns" &&
        (campaignForm.mode !== "idle" ? (
          <EditCampaignPage
            mode={campaignForm.mode === "add" ? "add" : "edit"}
            campaign={campaignForm.mode === "edit" ? campaignForm.data : undefined}
            onBack={() => setCampaignForm({ mode: "idle" })}
            tenantId={tenantId}
            authToken={authToken}
          />
        ) : (
          <CampaignsPage
            onCreateCampaign={() => setCampaignForm({ mode: "add" })}
            onEditCampaign={(row) => setCampaignForm({ mode: "edit", data: row })}
            onViewCampaign={(row) => setCampaignForm({ mode: "edit", data: row })}
            tenantId={tenantId}
            authToken={authToken}
          />
        ))}
      {page === "attributes" &&
        (attributeForm.mode === "attr-import" ? (
          <AttributeImportPage onBack={() => setAttributeForm({ mode: "idle" })} />
        ) : attributeForm.mode !== "idle" ? (
          <EditAttributePage
            mode={attributeForm.mode === "add" ? "add" : "edit"}
            attribute={attributeForm.mode === "edit" ? attributeForm.data : undefined}
            onBack={() => setAttributeForm({ mode: "idle" })}
            tenantId={tenantId}
            authToken={authToken}
          />
        ) : (
          <AttributesPage
            onCreateAttribute={() => setAttributeForm({ mode: "add" })}
            onEditAttribute={(row) => setAttributeForm({ mode: "edit", data: row })}
            onImportValues={() => setAttributeForm({ mode: "attr-import" })}
            tenantId={tenantId}
            authToken={authToken}
          />
        ))}
      {page === "promo-codes" &&
        (promoForm.mode === "exclusion-list" ? (
          <PromoExclusionListPage onBack={() => setPromoForm({ mode: "idle" })} />
        ) : promoForm.mode === "grouped" ? (
          <GroupedPromoCodesPage
            parentPromo={promoForm.parent}
            onBack={() => setPromoForm({ mode: "idle" })}
            onEditChild={(child) => setPromoForm({ mode: "edit", data: child })}
          />
        ) : promoForm.mode !== "idle" ? (
          <EditPromoPage
            mode={promoForm.mode === "add" ? "add" : "edit"}
            promoType={promoForm.mode === "add" ? promoForm.promoType : "standard"}
            promo={promoForm.mode === "edit" ? promoForm.data : undefined}
            onBack={() => setPromoForm({ mode: "idle" })}
            tenantId={tenantId}
            authToken={authToken}
          />
        ) : (
          <PromotionsPage
            onCreatePromo={(type) => setPromoForm({ mode: "add", promoType: type })}
            onEditPromo={(row) => setPromoForm({ mode: "edit", data: row })}
            onViewCodes={(row) => setPromoForm({ mode: "grouped", parent: row })}
            onOpenExclusionList={() => setPromoForm({ mode: "exclusion-list" })}
            tenantId={tenantId}
            authToken={authToken}
          />
        ))}
      {page === "fulfillment-delivery-allocation" && (
        <DeliveryAllocationPage
          stockLocations={stockLocations}
          onChange={setStockLocations}
          onNavigateToFulfillmentOptions={() => handlePageChange("fulfillment-options")}
        />
      )}
      {page === "fulfillment-collection-locations" && (
        <CollectionLocationsPage
          locations={collectionLocations}
          onChange={handleCollectionLocationsChange}
          brands={brands}
          onNavigateToFulfillmentOptions={() => handlePageChange("fulfillment-options")}
        />
      )}
      {page === "fulfillment-options" &&
        (fulfillmentForm.mode === "logistic-partners" ? (
          <LogisticPartnerManagementPage
            partners={logisticPartners}
            onChange={handleLogisticPartnersChange}
            onBack={() => setFulfillmentForm({ mode: "idle" })}
          />
        ) : fulfillmentForm.mode === "slots" ? (
          <FulfillmentSlotsPage
            grouping={fulfillmentGroupings.find((g) => g.id === fulfillmentForm.groupingId)!}
            slots={fulfillmentSlots.filter((s) => s.groupingId === fulfillmentForm.groupingId)}
            onBack={() => setFulfillmentForm({ mode: "idle" })}
            onCreate={() => {
              void (async () => {
                const groupingId = fulfillmentForm.groupingId;
                const grp = fulfillmentGroupings.find((g) => g.id === groupingId);
                if (tenantId) {
                  try {
                    setFulfillmentError(null);
                    const code = `NEW_${Date.now().toString(36).toUpperCase()}`;
                    const created = await createFulfillmentSlot(
                      tenantId,
                      groupingId,
                      {
                        groupingId,
                        name: "New Slot",
                        code,
                        mode: grp?.deliveryMode ?? "Local Delivery",
                        startingDate: "",
                        endingDate: "",
                        price: "0.00",
                      },
                      authToken
                    );
                    setFulfillmentSlots((prev) => [created, ...prev]);
                    setFulfillmentForm({ mode: "edit-slot", groupingId, slotId: created.id });
                  } catch (err) {
                    setFulfillmentError(err instanceof Error ? err.message : "Create slot failed");
                  }
                } else {
                  const slotId = `slot-${Math.random().toString(36).slice(2, 8)}`;
                  setFulfillmentSlots((prev) => [
                    {
                      id: slotId,
                      groupingId,
                      name: "New Slot",
                      code: "NEW_SLOT",
                      mode: grp?.deliveryMode ?? "Local Delivery",
                      startingDate: "",
                      endingDate: "",
                      price: "0.00",
                      updatedAt: new Date().toISOString().slice(0, 16).replace("T", " "),
                    },
                    ...prev,
                  ]);
                  setFulfillmentForm({ mode: "edit-slot", groupingId, slotId });
                }
              })();
            }}
            onEdit={(slotId) => setFulfillmentForm({ mode: "edit-slot", groupingId: fulfillmentForm.groupingId, slotId })}
            onDelete={(slotId) => {
              void (async () => {
                if (tenantId) {
                  try {
                    setFulfillmentError(null);
                    await deleteFulfillmentSlot(tenantId, fulfillmentForm.groupingId, slotId, authToken);
                  } catch (err) {
                    setFulfillmentError(err instanceof Error ? err.message : "Delete slot failed");
                  }
                }
                setFulfillmentSlots((prev) => prev.filter((s) => s.id !== slotId));
              })();
            }}
          />
        ) : fulfillmentForm.mode === "edit-slot" ? (
          <EditFulfillmentOptionPage
            grouping={fulfillmentGroupings.find((g) => g.id === fulfillmentForm.groupingId)}
            slot={fulfillmentSlots.find((s) => s.id === fulfillmentForm.slotId)}
            collectionLocations={collectionLocations}
            stockLocations={stockLocations}
            dynamicFields={dynamicFields}
            predefinedFieldSettings={predefinedFieldSettings}
            logisticPartners={logisticPartners}
            onSave={(slot) => {
              void (async () => {
                const gid = fulfillmentForm.groupingId;
                if (tenantId) {
                  try {
                    setFulfillmentError(null);
                    const { id, groupingId: _g, updatedAt: _u, ...rest } = slot;
                    const u = await updateFulfillmentSlot(tenantId, gid, id, rest, authToken);
                    setFulfillmentSlots((prev) => prev.map((s) => (s.id === u.id ? u : s)));
                  } catch (err) {
                    setFulfillmentError(err instanceof Error ? err.message : "Update slot failed");
                  }
                } else {
                  setFulfillmentSlots((prev) => {
                    const exists = prev.some((s) => s.id === slot.id);
                    return exists ? prev.map((s) => (s.id === slot.id ? slot : s)) : [slot, ...prev];
                  });
                }
              })();
            }}
            onBack={() => setFulfillmentForm({ mode: "idle" })}
          />
        ) : (
          <div className="flex min-h-0 flex-1 flex-col">
            {tenantId && fulfillmentLoading && (
              <div className="shrink-0 border-b border-outline-variant/10 bg-surface-container-high/50 px-6 py-2 text-xs text-on-surface-variant">
                Đang tải cấu hình fulfillment…
              </div>
            )}
            {fulfillmentError && (
              <div className="shrink-0 border-b border-error/30 bg-error/10 px-6 py-2 text-xs text-error">
                {fulfillmentError}
              </div>
            )}
            {fulfillmentToast && (
              <div
                className="fixed bottom-6 right-6 z-[100] max-w-sm rounded-lg border border-outline-variant/30 bg-inverse-surface px-4 py-3 text-xs text-inverse-on-surface shadow-xl"
                role="status"
              >
                {fulfillmentToast}
              </div>
            )}
            <div className="min-h-0 flex-1 overflow-auto">
              <FulfillmentOptionsPage
                groupings={fulfillmentGroupings}
                slots={fulfillmentSlots}
                stockLocations={stockLocations}
                dynamicFields={dynamicFields}
                onDynamicFieldsChange={setDynamicFields}
                predefinedFieldSettings={predefinedFieldSettings}
                onPredefinedFieldSettingsChange={setPredefinedFieldSettings}
                onOpenLogisticPartners={() => setFulfillmentForm({ mode: "logistic-partners" })}
                onCreateGrouping={async (next) => {
                  if (!tenantId) {
                    const id = `grp-${Math.random().toString(36).slice(2, 8)}`;
                    setFulfillmentGroupings((prev) => [{ ...next, id }, ...prev]);
                    return id;
                  }
                  try {
                    setFulfillmentError(null);
                    const created = await createFulfillmentGrouping(tenantId, next, authToken);
                    setFulfillmentGroupings((prev) => [created, ...prev]);
                    return created.id;
                  } catch (err) {
                    setFulfillmentError(err instanceof Error ? err.message : "Create grouping failed");
                    throw err;
                  }
                }}
                onUpdateGrouping={async (id, next) => {
                  if (!tenantId) {
                    setFulfillmentGroupings((prev) => prev.map((g) => (g.id === id ? { ...next, id } : g)));
                    return;
                  }
                  try {
                    setFulfillmentError(null);
                    const u = await updateFulfillmentGrouping(tenantId, id, next, authToken);
                    setFulfillmentGroupings((prev) => prev.map((g) => (g.id === id ? u : g)));
                  } catch (err) {
                    setFulfillmentError(err instanceof Error ? err.message : "Update grouping failed");
                  }
                }}
                onDeleteGrouping={async (id) => {
                  if (!tenantId) {
                    setFulfillmentGroupings((prev) => prev.filter((g) => g.id !== id));
                    setFulfillmentSlots((prev) => prev.filter((s) => s.groupingId !== id));
                    return;
                  }
                  try {
                    setFulfillmentError(null);
                    await deleteFulfillmentGrouping(tenantId, id, authToken);
                    setFulfillmentGroupings((prev) => prev.filter((g) => g.id !== id));
                    setFulfillmentSlots((prev) => prev.filter((s) => s.groupingId !== id));
                  } catch (err) {
                    setFulfillmentError(err instanceof Error ? err.message : "Delete grouping failed");
                  }
                }}
                onViewDeliverySlots={(groupingId) => setFulfillmentForm({ mode: "slots", groupingId })}
                onCreateTimeSlot={(groupingId) => {
                  const grp = fulfillmentGroupings.find((g) => g.id === groupingId);
                  if (tenantId) {
                    void (async () => {
                      try {
                        setFulfillmentError(null);
                        const code = `NEW_${Date.now().toString(36).toUpperCase()}`;
                        const created = await createFulfillmentSlot(
                          tenantId,
                          groupingId,
                          {
                            groupingId,
                            name: "New Slot",
                            code,
                            mode: grp?.deliveryMode ?? "Local Delivery",
                            startingDate: "",
                            endingDate: "",
                            price: "0.00",
                          },
                          authToken
                        );
                        setFulfillmentSlots((prev) => [created, ...prev]);
                        setFulfillmentForm({ mode: "edit-slot", groupingId, slotId: created.id });
                      } catch (err) {
                        setFulfillmentError(err instanceof Error ? err.message : "Create slot failed");
                      }
                    })();
                    return;
                  }
                  const slotId = `slot-${Math.random().toString(36).slice(2, 8)}`;
                  setFulfillmentSlots((prev) => [
                    {
                      id: slotId,
                      groupingId,
                      name: "New Slot",
                      code: "NEW_SLOT",
                      mode: grp?.deliveryMode ?? "Local Delivery",
                      startingDate: "",
                      endingDate: "",
                      price: "0.00",
                      updatedAt: new Date().toISOString().slice(0, 16).replace("T", " "),
                    },
                    ...prev,
                  ]);
                  setFulfillmentForm({ mode: "edit-slot", groupingId, slotId });
                }}
              />
            </div>
          </div>
        ))}
      {/* ── Access: Users ── */}
      {page === "access-users" &&
        (accessUserForm.mode === "change-password" ? (
          <ChangePasswordModal
            userId={accessUserForm.userId}
            userName={accessUserForm.userName}
            onSave={() => setAccessUserForm({ mode: "idle" })}
            onClose={() => setAccessUserForm({ mode: "idle" })}
          />
        ) : accessUserForm.mode !== "idle" ? (
          <UserFormPage
            mode={accessUserForm.mode === "add" ? "add" : "edit"}
            userId={accessUserForm.mode === "edit" ? accessUserForm.userId : undefined}
            onSave={() => setAccessUserForm({ mode: "idle" })}
            onCancel={() => setAccessUserForm({ mode: "idle" })}
            authToken={authToken}
          />
        ) : (
          <UsersPage
            onAddUser={() => setAccessUserForm({ mode: "add" })}
            onEditUser={(userId) => setAccessUserForm({ mode: "edit", userId })}
            onChangePassword={(userId) => {
              // Look up userName from demo data for the modal title
              const userName = userId;
              setAccessUserForm({ mode: "change-password", userId, userName });
            }}
          />
        ))}

      {/* ── Access: Roles ── */}
      {page === "access-roles" &&
        (accessRoleForm.mode === "manage-modules" ? (
          <ManageModulesPage
            roleAlias={accessRoleForm.roleAlias}
            roleName={accessRoleForm.roleName}
            onBack={() => setAccessRoleForm({ mode: "idle" })}
            authToken={authToken}
          />
        ) : accessRoleForm.mode !== "idle" ? (
          <RoleFormPage
            mode={accessRoleForm.mode === "add" ? "add" : "edit"}
            roleAlias={accessRoleForm.mode === "edit" ? accessRoleForm.roleAlias : undefined}
            onSave={() => setAccessRoleForm({ mode: "idle" })}
            onCancel={() => setAccessRoleForm({ mode: "idle" })}
            authToken={authToken}
          />
        ) : (
          <RolesPage
            onAddRole={() => setAccessRoleForm({ mode: "add" })}
            onEditRole={(roleAlias) => setAccessRoleForm({ mode: "edit", roleAlias })}
            onManageModules={(row) =>
              setAccessRoleForm({ mode: "manage-modules", roleAlias: row.alias, roleName: row.name })
            }
            authToken={authToken}
          />
        ))}

      {/* ── Access: Tenants ── */}
      {page === "access-tenants" &&
        (accessTenantForm.mode !== "idle" ? (
          <TenantFormPage
            mode={accessTenantForm.mode === "add" ? "add" : "edit"}
            tenant={accessTenantForm.mode === "edit" ? accessTenantForm.row : undefined}
            authToken={authToken}
            onSave={() => setAccessTenantForm({ mode: "idle" })}
            onCancel={() => setAccessTenantForm({ mode: "idle" })}
          />
        ) : (
          <TenantsPage
            onAddTenant={() => setAccessTenantForm({ mode: "add" })}
            onEditTenant={(row) => setAccessTenantForm({ mode: "edit", row })}
            authToken={authToken}
          />
        ))}
    </EStoreLayout>
    </LanguageProvider>
  );
}
