import { useEffect, useMemo, useState } from "react";
import { eStoreHashForPage, parseEStorePageFromHash } from "./estoreHashRouting";
import { EStoreLayout, type EStorePageId, type EStoreSidebarScope } from "./layout/EStoreLayout";
import { LanguageProvider } from "./LanguageContext";
import type { BrandListRow } from "./pages/BrandsPage";
import { BrandsPage } from "./pages/BrandsPage";
import { CategoriesPage } from "./pages/CategoriesPage";
import { EditBrandPage } from "./pages/EditBrandPage";
import { EditProductPage } from "./pages/EditProductPage";
import { ProductImageImportPage } from "./pages/ProductImageImportPage";
import { ProductImportPage } from "./pages/ProductImportPage";
import { ProductInventoryImportPage } from "./pages/ProductInventoryImportPage";
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
import { DEMO_PRODUCT_ROWS, ProductsPage } from "./pages/ProductsPage";
import { BestSellerSettingsPage } from "./pages/BestSellerSettingsPage";
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
import type { RoleRow } from "./pages/access/RolesPage";

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
  | { mode: "inventory-import" };

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
  | { mode: "edit"; tenantId: string };

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
}: {
  languages?: import("./useUmbracoLanguages").UmbracoLanguage[];
  sidebarScope?: EStoreSidebarScope;
}) {
  const [page, setPage] = useState<EStorePageId>(() => initialPageForScope(sidebarScope));
  const [brandForm, setBrandForm] = useState<BrandFormState>({ mode: "idle" });
  const [productForm, setProductForm] = useState<ProductFormState>({ mode: "idle" });
  const [attributeForm, setAttributeForm] = useState<AttributeFormState>({ mode: "idle" });
  const [promoForm, setPromoForm] = useState<PromoFormState>({ mode: "idle" });
  const [campaignForm, setCampaignForm] = useState<CampaignFormState>({ mode: "idle" });
  const [fulfillmentForm, setFulfillmentForm] = useState<FulfillmentFormState>({ mode: "idle" });
  const [accessUserForm, setAccessUserForm] = useState<AccessUserFormState>({ mode: "idle" });
  const [accessRoleForm, setAccessRoleForm] = useState<AccessRoleFormState>({ mode: "idle" });
  const [accessTenantForm, setAccessTenantForm] = useState<AccessTenantFormState>({ mode: "idle" });

  // Demo role data shared between RolesPage and ManageModulesPage for roleName lookup
  const [accessRoles] = useState<RoleRow[]>([
    { alias: "it-admin", name: "IT Administrator", description: "", isTenantRole: false, memberCount: 2 },
    { alias: "sys-admin", name: "System Administrator", description: "", isTenantRole: false, memberCount: 3 },
    { alias: "ecom-mgr", name: "Ecommerce Manager", description: "", isTenantRole: false, memberCount: 5 },
    { alias: "tenant-pm", name: "Tenant Product Manager", description: "", isTenantRole: true, memberCount: 12 },
    { alias: "tenant-inv-mgr", name: "Tenant Inventory Manager", description: "", isTenantRole: true, memberCount: 8 },
    { alias: "operations", name: "Operations", description: "", isTenantRole: false, memberCount: 7 },
    { alias: "finance", name: "Finance", description: "", isTenantRole: false, memberCount: 4 },
    { alias: "brand-mgr", name: "Brand Manager", description: "", isTenantRole: true, memberCount: 15 },
    { alias: "product-upload", name: "Product Upload", description: "", isTenantRole: true, memberCount: 10 },
    { alias: "guest", name: "Guest", description: "", isTenantRole: false, memberCount: 3 },
  ]);

  const [brands, setBrands] = useState<BrandListRow[]>(DEFAULT_BRANDS);

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

  const [predefinedFieldSettings, setPredefinedFieldSettings] = useState<FulfillmentPredefinedFieldSetting[]>(
    () => safeJsonParse<FulfillmentPredefinedFieldSetting[]>(localStorage.getItem(LS_KEYS.fulfillmentPredefinedFields)) ?? DEFAULT_PREDEFINED_FIELDS
  );

  const [logisticPartners, setLogisticPartners] = useState<LogisticPartner[]>(
    () =>
      safeJsonParse<LogisticPartner[]>(localStorage.getItem(LS_KEYS.logisticPartners)) ?? [
        { id: "lp-1", name: "DHL", code: "DHL", enabled: true, integratedLogistic: true },
        { id: "lp-2", name: "FedEx", code: "FEDEX", enabled: true, integratedLogistic: true },
        { id: "lp-3", name: "Ninja Van", code: "NINJA_VAN", enabled: false, integratedLogistic: false },
        { id: "lp-4", name: "J&T Express", code: "JNT", enabled: true, integratedLogistic: false },
      ]
  );

  const [fulfillmentGroupings, setFulfillmentGroupings] = useState<FulfillmentGrouping[]>([
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
  ]);

  const defaultCollectionLocations = useMemo<CollectionLocation[]>(
    () => [
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
    ],
    []
  );

  const [collectionLocations, setCollectionLocations] = useState<CollectionLocation[]>(
    () => safeJsonParse<CollectionLocation[]>(localStorage.getItem(LS_KEYS.collectionLocations)) ?? defaultCollectionLocations
  );

  useEffect(() => {
    localStorage.setItem(LS_KEYS.collectionLocations, JSON.stringify(collectionLocations));
  }, [collectionLocations]);

  useEffect(() => {
    localStorage.setItem(LS_KEYS.fulfillmentPredefinedFields, JSON.stringify(predefinedFieldSettings));
  }, [predefinedFieldSettings]);

  useEffect(() => {
    localStorage.setItem(LS_KEYS.logisticPartners, JSON.stringify(logisticPartners));
  }, [logisticPartners]);

  useEffect(() => {
    localStorage.setItem(LS_KEYS.brandAdditionalFields, JSON.stringify(brandAdditionalFields));
  }, [brandAdditionalFields]);

  useEffect(() => {
    localStorage.setItem(LS_KEYS.bestSellerFlags, JSON.stringify(bestSellerFlags));
  }, [bestSellerFlags]);

  const [stockLocations, setStockLocations] = useState<StockLocation[]>([
    { id: "dc-1", name: "DC - Central", code: "DC_CENTRAL", active: true },
    { id: "dc-2", name: "DC - East", code: "DC_EAST", active: true },
    { id: "dc-3", name: "DC - West", code: "DC_WEST", active: false },
  ]);

  const [fulfillmentSlots, setFulfillmentSlots] = useState<FulfillmentSlot[]>([
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
  ]);

  const [dynamicFields, setDynamicFields] = useState<FulfillmentDynamicField[]>([
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
  ]);

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
            onBack={() => setBrandForm({ mode: "idle" })}
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
            onRowsChange={setBrands}
          />
        ))}
      {page === "brand-configuration" && (
        <BrandConfigPage
          savedFields={brandAdditionalFields}
          onSave={setBrandAdditionalFields}
          onNavigateToBrands={() => handlePageChange("brands")}
        />
      )}
      {page === "categories" && <CategoriesPage />}
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
      {page === "product-configuration" && (
        <ProductConfigPage onNavigateToProducts={() => handlePageChange("products")} />
      )}
      {page === "products" &&
        (productForm.mode === "inventory-import" ? (
          <ProductInventoryImportPage onBack={() => setProductForm({ mode: "idle" })} />
        ) : productForm.mode === "image-import" ? (
          <ProductImageImportPage onBack={() => setProductForm({ mode: "idle" })} />
        ) : productForm.mode === "import" ? (
          <ProductImportPage onBack={() => setProductForm({ mode: "idle" })} />
        ) : productForm.mode !== "idle" ? (
          <EditProductPage
            mode={productForm.mode === "add" ? "add" : "edit"}
            product={productForm.mode === "edit" ? productForm.data : undefined}
            onBack={() => setProductForm({ mode: "idle" })}
          />
        ) : (
          <ProductsPage
            onAddProduct={() => setProductForm({ mode: "add" })}
            onEditProduct={(row) => setProductForm({ mode: "edit", data: row })}
            onImportProduct={() => setProductForm({ mode: "import" })}
            onImageImport={() => setProductForm({ mode: "image-import" })}
            onInventoryImport={() => setProductForm({ mode: "inventory-import" })}
          />
        ))}
      {page === "campaigns" &&
        (campaignForm.mode !== "idle" ? (
          <EditCampaignPage
            mode={campaignForm.mode === "add" ? "add" : "edit"}
            campaign={campaignForm.mode === "edit" ? campaignForm.data : undefined}
            onBack={() => setCampaignForm({ mode: "idle" })}
          />
        ) : (
          <CampaignsPage
            onCreateCampaign={() => setCampaignForm({ mode: "add" })}
            onEditCampaign={(row) => setCampaignForm({ mode: "edit", data: row })}
            onViewCampaign={(row) => setCampaignForm({ mode: "edit", data: row })}
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
          />
        ) : (
          <AttributesPage
            onCreateAttribute={() => setAttributeForm({ mode: "add" })}
            onEditAttribute={(row) => setAttributeForm({ mode: "edit", data: row })}
            onImportValues={() => setAttributeForm({ mode: "attr-import" })}
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
          />
        ) : (
          <PromotionsPage
            onCreatePromo={(type) => setPromoForm({ mode: "add", promoType: type })}
            onEditPromo={(row) => setPromoForm({ mode: "edit", data: row })}
            onViewCodes={(row) => setPromoForm({ mode: "grouped", parent: row })}
            onOpenExclusionList={() => setPromoForm({ mode: "exclusion-list" })}
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
          onChange={setCollectionLocations}
          brands={brands}
          onNavigateToFulfillmentOptions={() => handlePageChange("fulfillment-options")}
        />
      )}
      {page === "fulfillment-options" &&
        (fulfillmentForm.mode === "logistic-partners" ? (
          <LogisticPartnerManagementPage
            partners={logisticPartners}
            onChange={setLogisticPartners}
            onBack={() => setFulfillmentForm({ mode: "idle" })}
          />
        ) : fulfillmentForm.mode === "slots" ? (
          <FulfillmentSlotsPage
            grouping={fulfillmentGroupings.find((g) => g.id === fulfillmentForm.groupingId)!}
            slots={fulfillmentSlots.filter((s) => s.groupingId === fulfillmentForm.groupingId)}
            onBack={() => setFulfillmentForm({ mode: "idle" })}
            onCreate={() => {
              const slotId = `slot-${Math.random().toString(36).slice(2, 8)}`;
              setFulfillmentSlots((prev) => [
                {
                  id: slotId,
                  groupingId: fulfillmentForm.groupingId,
                  name: "New Slot",
                  code: "NEW_SLOT",
                  mode: fulfillmentGroupings.find((g) => g.id === fulfillmentForm.groupingId)?.deliveryMode ?? "Local Delivery",
                  startingDate: "",
                  endingDate: "",
                  price: "0.00",
                  updatedAt: new Date().toISOString().slice(0, 16).replace("T", " "),
                },
                ...prev,
              ]);
              setFulfillmentForm({ mode: "edit-slot", groupingId: fulfillmentForm.groupingId, slotId });
            }}
            onEdit={(slotId) => setFulfillmentForm({ mode: "edit-slot", groupingId: fulfillmentForm.groupingId, slotId })}
            onDelete={(slotId) => setFulfillmentSlots((prev) => prev.filter((s) => s.id !== slotId))}
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
              setFulfillmentSlots((prev) => {
                const exists = prev.some((s) => s.id === slot.id);
                return exists ? prev.map((s) => (s.id === slot.id ? slot : s)) : [slot, ...prev];
              });
            }}
            onBack={() => setFulfillmentForm({ mode: "idle" })}
          />
        ) : (
          <FulfillmentOptionsPage
            groupings={fulfillmentGroupings}
            slots={fulfillmentSlots}
            stockLocations={stockLocations}
            dynamicFields={dynamicFields}
            onDynamicFieldsChange={setDynamicFields}
            predefinedFieldSettings={predefinedFieldSettings}
            onPredefinedFieldSettingsChange={setPredefinedFieldSettings}
            onOpenLogisticPartners={() => setFulfillmentForm({ mode: "logistic-partners" })}
            onCreateGrouping={(next) => {
              const id = `grp-${Math.random().toString(36).slice(2, 8)}`;
              setFulfillmentGroupings((prev) => [{ ...next, id }, ...prev]);
              return id;
            }}
            onUpdateGrouping={(id, next) =>
              setFulfillmentGroupings((prev) => prev.map((g) => (g.id === id ? { ...next, id } : g)))
            }
            onDeleteGrouping={(id) => setFulfillmentGroupings((prev) => prev.filter((g) => g.id !== id))}
            onViewDeliverySlots={(groupingId) => setFulfillmentForm({ mode: "slots", groupingId })}
            onCreateTimeSlot={(groupingId) => {
              const slotId = `slot-${Math.random().toString(36).slice(2, 8)}`;
              const grp = fulfillmentGroupings.find((g) => g.id === groupingId);
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
          />
        ) : accessRoleForm.mode !== "idle" ? (
          <RoleFormPage
            mode={accessRoleForm.mode === "add" ? "add" : "edit"}
            roleAlias={accessRoleForm.mode === "edit" ? accessRoleForm.roleAlias : undefined}
            onSave={() => setAccessRoleForm({ mode: "idle" })}
            onCancel={() => setAccessRoleForm({ mode: "idle" })}
          />
        ) : (
          <RolesPage
            onAddRole={() => setAccessRoleForm({ mode: "add" })}
            onEditRole={(roleAlias) => setAccessRoleForm({ mode: "edit", roleAlias })}
            onManageModules={(roleAlias) => {
              const roleName = accessRoles.find((r) => r.alias === roleAlias)?.name ?? roleAlias;
              setAccessRoleForm({ mode: "manage-modules", roleAlias, roleName });
            }}
          />
        ))}

      {/* ── Access: Tenants ── */}
      {page === "access-tenants" &&
        (accessTenantForm.mode !== "idle" ? (
          <TenantFormPage
            mode={accessTenantForm.mode === "add" ? "add" : "edit"}
            tenantId={accessTenantForm.mode === "edit" ? accessTenantForm.tenantId : undefined}
            onSave={() => setAccessTenantForm({ mode: "idle" })}
            onCancel={() => setAccessTenantForm({ mode: "idle" })}
          />
        ) : (
          <TenantsPage
            onAddTenant={() => setAccessTenantForm({ mode: "add" })}
            onEditTenant={(tenantId) => setAccessTenantForm({ mode: "edit", tenantId })}
          />
        ))}
    </EStoreLayout>
    </LanguageProvider>
  );
}
