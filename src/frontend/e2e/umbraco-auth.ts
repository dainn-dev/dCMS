import type { Page } from "@playwright/test";

/**
 * Umbraco 13+ backoffice local login (username/password).
 */
export async function umbracoLogin(page: Page, username: string, password: string): Promise<void> {
  await page.goto("/umbraco/login", { waitUntil: "domcontentloaded" });
  const userField = page.locator('input[name="username"]');
  await userField.waitFor({ state: "visible", timeout: 120_000 });
  await userField.fill(username);
  await page.locator('input[name="password"]').fill(password);
  await page.locator('button[type="submit"]').click();
  await page.locator('input[name="username"]').waitFor({ state: "detached", timeout: 120_000 });
}

export async function gotoProductWizard(page: Page): Promise<void> {
  await page.goto("/umbraco/#/dCMSCatalog/dCMSProductWizard", { waitUntil: "domcontentloaded" });
  await page.getByTestId("dcms-pw-wizard-root").waitFor({ state: "visible", timeout: 120_000 });
}
