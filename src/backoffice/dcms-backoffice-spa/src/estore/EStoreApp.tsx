import { useState } from "react";
import { EStoreLayout, type EStorePageId } from "./layout/EStoreLayout";
import type { BrandListRow } from "./pages/BrandsPage";
import { BrandsPage } from "./pages/BrandsPage";
import { CategoriesPage } from "./pages/CategoriesPage";
import { EditBrandPage } from "./pages/EditBrandPage";
import { ProductsPage } from "./pages/ProductsPage";
import { AttributesPage } from "./pages/AttributesPage";
import { FulfillmentOptionsPage } from "./pages/FulfillmentOptionsPage";
import { PromotionsPage } from "./pages/PromotionsPage";

type BrandEditData = Pick<BrandListRow, "code" | "name" | "active" | "imageSrc" | "imageAlt">;

type BrandFormState =
  | { mode: "idle" }
  | { mode: "add" }
  | { mode: "edit"; data: BrandEditData };

export function EStoreApp() {
  const [page, setPage] = useState<EStorePageId>("brands");
  const [brandForm, setBrandForm] = useState<BrandFormState>({ mode: "idle" });

  function handlePageChange(id: EStorePageId) {
    if (id !== "brands") setBrandForm({ mode: "idle" });
    setPage(id);
  }

  return (
    <EStoreLayout page={page} onPageChange={handlePageChange}>
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
          />
        ))}
      {page === "categories" && <CategoriesPage />}
      {page === "products" && <ProductsPage />}
      {page === "attributes" && <AttributesPage />}
      {page === "promo-codes" && <PromotionsPage />}
      {page === "fulfillment-options" && <FulfillmentOptionsPage />}
    </EStoreLayout>
  );
}
