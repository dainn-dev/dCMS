import { test, expect } from "@playwright/test";
import { e2eEnvReady, e2eTenantStore } from "./env";
import { gotoProductWizard, umbracoLogin } from "./umbraco-auth";

test.describe.configure({ mode: "serial" });

test.describe("DAI-300 — catalog wizard, stock, approval", () => {
  test.skip(!e2eEnvReady(), "Set E2E_BASE_URL, E2E_STORE_USER, E2E_STORE_PASSWORD, E2E_BRAND_USER, E2E_BRAND_PASSWORD");

  test("StoreManager: wizard → draft; stock +10; submit → BrandManager approve → active", async ({ page, browser }) => {
    const storeUser = process.env.E2E_STORE_USER!;
    const storePass = process.env.E2E_STORE_PASSWORD!;
    const brandUser = process.env.E2E_BRAND_USER!;
    const brandPass = process.env.E2E_BRAND_PASSWORD!;
    const { tenantId, storeId } = e2eTenantStore();
    const suffix = `${Date.now()}`;
    const productName = `E2E PW ${suffix}`;
    const slug = `e2e-pw-${suffix}`;

    await umbracoLogin(page, storeUser, storePass);
    await gotoProductWizard(page);

    await page.getByTestId("dcms-pw-tenant").fill(tenantId);
    await page.getByTestId("dcms-pw-store").fill(storeId);
    await page.getByRole("button", { name: "Reload categories" }).click();
    await expect(page.locator('input[name="dcmsLeafCat"]').first()).toBeVisible({ timeout: 120_000 });
    await page.getByRole("button", { name: "Expand all" }).click();
    await page.locator('input[name="dcmsLeafCat"]').first().check();
    await page.getByTestId("dcms-pw-next").click();

    await page.getByTestId("dcms-pw-name-vi").fill(productName);
    await page.getByTestId("dcms-pw-slug").fill(slug);
    await expect(page.getByText("Slug available.")).toBeVisible({ timeout: 120_000 });
    await page.getByTestId("dcms-pw-next").click();

    const step3 = page.locator('[ng-show="vm.step === 3"]');
    await step3.waitFor({ state: "visible", timeout: 60_000 });
    const axisGroups = step3.locator('[ng-repeat*="attr in vm.variantAxesDefinitions"]');
    const groupCount = await axisGroups.count();
    if (groupCount === 0) {
      test.skip(true, "No variant-axis definitions — seed CatalogAttributes / CatalogAttributeValues for tenant/store.");
    }
    for (let i = 0; i < groupCount; i++) {
      const cb = axisGroups.nth(i).locator('input[type="checkbox"]').first();
      if ((await cb.count()) === 0) {
        test.skip(true, "Axis group has no checkboxes.");
      }
      await cb.check();
    }
    await page.getByTestId("dcms-pw-next").click();

    await page.locator('[ng-show="vm.step === 4"]').waitFor({ state: "visible" });
    await page.getByTestId("dcms-pw-next").click();

    await expect(page.getByTestId("dcms-pw-catalog-status")).toBeVisible({ timeout: 120_000 });
    await expect(page.getByTestId("dcms-pw-catalog-status")).toHaveText(/draft/i);

    const productId = (await page.getByTestId("dcms-pw-product-id").innerText()).trim();
    expect(productId.length).toBeGreaterThan(3);

    await page.getByTestId("dcms-pw-tab-stock").click();
    const stockTable = page.getByTestId("dcms-pw-stock-table");
    await expect(stockTable).toBeVisible({ timeout: 120_000 });
    const firstRow = stockTable.locator("tbody tr").first();
    const qtyCell = firstRow.locator("td").nth(2);
    await expect(qtyCell).not.toHaveText("—");
    const beforeText = (await qtyCell.innerText()).trim();
    const before = Number.parseInt(beforeText, 10);
    expect(Number.isFinite(before)).toBeTruthy();

    await firstRow.getByRole("button", { name: /Adjust/i }).click();
    await page.getByTestId("dcms-pw-stock-delta").fill("10");
    await page.getByTestId("dcms-pw-stock-confirm").click();
    await expect.poll(async () => Number.parseInt((await qtyCell.innerText()).trim(), 10)).toBe(before + 10);

    await page.getByTestId("dcms-pw-publish-mode-approval").check();
    await page.getByTestId("dcms-pw-submit-approval").click();
    await expect(page.getByTestId("dcms-pw-catalog-status")).toHaveText(/pending_approval/i, { timeout: 120_000 });

    const brandContext = await browser.newContext();
    const brandPage = await brandContext.newPage();
    await umbracoLogin(brandPage, brandUser, brandPass);
    await brandPage.addInitScript((id) => {
      localStorage.setItem("dcmsCatalogWizard_navigateProductId", id);
    }, productId);
    await gotoProductWizard(brandPage);
    await brandPage.getByTestId("dcms-pw-tenant").fill(tenantId);
    await brandPage.getByTestId("dcms-pw-store").fill(storeId);
    await brandPage.getByTestId("dcms-pw-approve").waitFor({ state: "visible", timeout: 120_000 });
    await brandPage.getByTestId("dcms-pw-approve").click();
    await expect(brandPage.getByTestId("dcms-pw-catalog-status")).toHaveText(/active/i, { timeout: 120_000 });
    await brandContext.close();
  });
});
