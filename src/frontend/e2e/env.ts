export function e2eEnvReady(): boolean {
  return !!(
    process.env.E2E_BASE_URL &&
    process.env.E2E_STORE_USER &&
    process.env.E2E_STORE_PASSWORD &&
    process.env.E2E_BRAND_USER &&
    process.env.E2E_BRAND_PASSWORD
  );
}

export function e2eTenantStore(): { tenantId: string; storeId: string } {
  return {
    tenantId: process.env.E2E_TENANT_ID || "t1",
    storeId: process.env.E2E_STORE_ID || "s1",
  };
}
