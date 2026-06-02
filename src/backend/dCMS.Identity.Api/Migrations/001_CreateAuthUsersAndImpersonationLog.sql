-- DAI-752 / US-5: Identity bootstrap.
-- AuthUsers — credentials + role + scope (single static client per deployment).
-- ImpersonationLog — every impersonation start (audited externally on every write).

CREATE TABLE IF NOT EXISTS "AuthUsers" (
    "Id"            UUID         PRIMARY KEY,
    "ClientId"      VARCHAR(64)  NOT NULL,
    "TenantId"      VARCHAR(64)  NULL,
    "Email"         VARCHAR(320) NOT NULL,
    "DisplayName"   VARCHAR(200) NOT NULL,
    "PasswordHash"  TEXT         NOT NULL,
    "Role"          VARCHAR(40)  NOT NULL,
    "IsActive"      BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_AuthUsers_Role"
        CHECK ("Role" IN ('SuperAdmin','ClientAdmin','TenantAdmin','BrandManager','StoreManager','StoreStaff','CustomerSupport')),
    CONSTRAINT "CK_AuthUsers_Tenant_RoleConsistency"
        CHECK (
            ("Role" IN ('SuperAdmin','ClientAdmin') AND "TenantId" IS NULL)
            OR ("Role" NOT IN ('SuperAdmin','ClientAdmin') AND "TenantId" IS NOT NULL)
        )
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_AuthUsers_Client_Email"
    ON "AuthUsers" ("ClientId", LOWER("Email"));

CREATE INDEX IF NOT EXISTS "IX_AuthUsers_Client_Tenant"
    ON "AuthUsers" ("ClientId", "TenantId");

CREATE TABLE IF NOT EXISTS "ImpersonationLog" (
    "Id"             UUID         PRIMARY KEY,
    "ClientId"       VARCHAR(64)  NOT NULL,
    "ActorUserId"    UUID         NOT NULL,
    "TargetTenantId" VARCHAR(64)  NOT NULL,
    "Reason"         VARCHAR(500) NOT NULL,
    "TokenJti"       UUID         NOT NULL,
    "IssuedAt"       TIMESTAMPTZ  NOT NULL,
    "ExpiresAt"      TIMESTAMPTZ  NOT NULL,
    "EndedAt"        TIMESTAMPTZ  NULL
);

CREATE INDEX IF NOT EXISTS "IX_ImpersonationLog_Client_Actor_Issued"
    ON "ImpersonationLog" ("ClientId", "ActorUserId", "IssuedAt" DESC);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ImpersonationLog_Jti"
    ON "ImpersonationLog" ("TokenJti");

-- Seed two users for verification (passwords hashed with PBKDF2-SHA256, 100k iters,
-- 16-byte salt — see DcmsPasswordHasher). Plaintext for dev:
--   hq.admin@aeon.vn       → "ChangeMe123!"  (ClientAdmin)
--   bt.admin@aeon.vn       → "ChangeMe123!"  (TenantAdmin tenant=aeon-bt)
-- Seeds are guarded by ON CONFLICT so re-running migration is safe.
INSERT INTO "AuthUsers" ("Id","ClientId","TenantId","Email","DisplayName","PasswordHash","Role","IsActive")
VALUES
    ('11111111-1111-1111-1111-111111111111','aeon',NULL,'hq.admin@aeon.vn','Aeon HQ Admin',
     'pbkdf2_sha256$100000$NS26sRfqAe5RvR5jpMvFGA==$qA5Kwj/rWnNnPawkTS9AGEWJmiZu7ezhAtMWNWG7DQM=','ClientAdmin',TRUE),
    ('22222222-2222-2222-2222-222222222222','aeon','aeon-bt','bt.admin@aeon.vn','Bình Tân Tenant Admin',
     'pbkdf2_sha256$100000$hK6T+yxkDrY3ddOXErVSiw==$kmSPJWm4T7m1HPFTtAYYV0+Fubn44f6RNdUljEMpvp8=','TenantAdmin',TRUE)
ON CONFLICT ("Id") DO NOTHING;
