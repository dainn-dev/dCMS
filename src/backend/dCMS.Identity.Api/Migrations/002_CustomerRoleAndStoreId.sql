-- Storefront Commerce MVP: Customer role + StoreId for JWT store_id claim (order header auth).

ALTER TABLE "AuthUsers" ADD COLUMN IF NOT EXISTS "StoreId" VARCHAR(64) NULL;

ALTER TABLE "AuthUsers" DROP CONSTRAINT IF EXISTS "CK_AuthUsers_Role";
ALTER TABLE "AuthUsers" ADD CONSTRAINT "CK_AuthUsers_Role"
    CHECK ("Role" IN (
        'SuperAdmin','ClientAdmin','TenantAdmin','BrandManager',
        'StoreManager','StoreStaff','CustomerSupport','Customer'));

ALTER TABLE "AuthUsers" DROP CONSTRAINT IF EXISTS "CK_AuthUsers_Tenant_RoleConsistency";
ALTER TABLE "AuthUsers" ADD CONSTRAINT "CK_AuthUsers_Tenant_RoleConsistency"
    CHECK (
        ("Role" IN ('SuperAdmin','ClientAdmin') AND "TenantId" IS NULL)
        OR ("Role" NOT IN ('SuperAdmin','ClientAdmin') AND "TenantId" IS NOT NULL)
    );

ALTER TABLE "AuthUsers" DROP CONSTRAINT IF EXISTS "CK_AuthUsers_Customer_StoreId";
ALTER TABLE "AuthUsers" ADD CONSTRAINT "CK_AuthUsers_Customer_StoreId"
    CHECK ("Role" <> 'Customer' OR ("StoreId" IS NOT NULL AND LENGTH(TRIM("StoreId")) > 0));

-- Dev seed: customer@aeon.test / ChangeMe123! (tenant aeon-bt, store s1)
INSERT INTO "AuthUsers" ("Id","ClientId","TenantId","StoreId","Email","DisplayName","PasswordHash","Role","IsActive")
VALUES
    ('33333333-3333-3333-3333-333333333333','aeon','aeon-bt','s1','customer@aeon.test','Storefront Customer',
     'pbkdf2_sha256$100000$JhaERNWLsfzXyP9RZ+QYsQ==$SINuAmQcsZ0CHyPQEhDsh4I1DdoqDb2Cv/+ud2F3Upc=','Customer',TRUE)
ON CONFLICT ("Id") DO NOTHING;
