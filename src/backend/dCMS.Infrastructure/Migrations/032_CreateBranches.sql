-- DAI-750 / US-3: Branches — physical retail/pickup locations under a Client.
-- One branch per (ClientId, TenantId): TenantId on Branches is the same domain identifier
-- the rest of the system uses (Brands.TenantId etc.), but here it represents the branch
-- the storefront should pin scope to once geolocation resolves.
--
-- Geo: stored as DOUBLE PRECISION (Lat, Lng); Haversine in SQL — no PostGIS dependency
-- (only ~10s of branches per client expected, see DAI-750 scope).
-- IsDefault: exactly one row per ClientId may carry IsDefault=true (partial unique index).

CREATE TABLE IF NOT EXISTS "Branches"
(
    "Id"        UUID             NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
    "ClientId"  VARCHAR(64)      NOT NULL,
    "TenantId"  VARCHAR(64)      NOT NULL,
    "Name"      VARCHAR(200)     NOT NULL,
    "Address"   TEXT             NOT NULL DEFAULT '',
    "Lat"       DOUBLE PRECISION NOT NULL,
    "Lng"       DOUBLE PRECISION NOT NULL,
    "IsDefault" BOOLEAN          NOT NULL DEFAULT FALSE,
    "IsActive"  BOOLEAN          NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    CONSTRAINT "UQ_Branches_Client_Tenant" UNIQUE ("ClientId", "TenantId"),
    CONSTRAINT "CK_Branches_Lat" CHECK ("Lat" BETWEEN -90 AND 90),
    CONSTRAINT "CK_Branches_Lng" CHECK ("Lng" BETWEEN -180 AND 180)
);

-- Exactly one default per client.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Branches_Client_Default"
    ON "Branches" ("ClientId")
    WHERE "IsDefault" = TRUE;

CREATE INDEX IF NOT EXISTS "IX_Branches_Client_Active"
    ON "Branches" ("ClientId", "IsActive");

COMMENT ON TABLE  "Branches"            IS 'Physical retail/pickup locations under a Client. One branch maps 1:1 to a Tenant scope (Brands/Products belong to that tenant).';
COMMENT ON COLUMN "Branches"."TenantId" IS 'Same identifier used in Brands.TenantId — the storefront pins to this tenant once nearest-branch resolves.';
COMMENT ON COLUMN "Branches"."IsDefault" IS 'Fallback branch returned by /branches:nearest when no branch is within maxKm. Exactly one per Client (enforced by partial unique index).';

-- Seed: 3 sample branches for the 'aeon' client (Bình Tân default).
INSERT INTO "Branches" ("ClientId", "TenantId", "Name", "Address", "Lat", "Lng", "IsDefault", "IsActive")
VALUES
    ('aeon', 'aeon-bt', 'Aeon Mall Bình Tân',  'No.1, đường số 17A, Bình Trị Đông B, Bình Tân, TP.HCM', 10.7449, 106.6188, TRUE,  TRUE),
    ('aeon', 'aeon-tp', 'Aeon Mall Tân Phú',   '30 Bờ Bao Tân Thắng, Sơn Kỳ, Tân Phú, TP.HCM',           10.8019, 106.6155, FALSE, TRUE),
    ('aeon', 'aeon-bd', 'Aeon Mall Bình Dương', '01 Đại lộ Bình Dương, Lái Thiêu, Thuận An, Bình Dương', 10.9051, 106.7220, FALSE, TRUE)
ON CONFLICT ("ClientId", "TenantId") DO NOTHING;
