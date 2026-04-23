-- DAI-612: Fulfillment configuration (eStore) — groupings, slots, locations, partners, tenant JSON config.

CREATE TABLE IF NOT EXISTS "FulfillmentGroupings"
(
    "Id"                               VARCHAR(40)  NOT NULL PRIMARY KEY,
    "TenantId"                         VARCHAR(64)  NOT NULL,
    "GroupName"                        TEXT         NOT NULL,
    "Code"                             VARCHAR(100) NOT NULL,
    "StartDate"                        DATE         NOT NULL,
    "EndDate"                          DATE         NOT NULL,
    "Priority"                         INTEGER      NOT NULL DEFAULT 0,
    "Active"                           BOOLEAN      NOT NULL DEFAULT TRUE,
    "TenantEnabled"                    BOOLEAN      NOT NULL DEFAULT TRUE,
    "MaxPerTenant"                     INTEGER      NULL,
    "DeliveryMode"                     VARCHAR(40)  NOT NULL,
    "LimitSelectedDistributionCenter" BOOLEAN      NOT NULL DEFAULT FALSE,
    "StockLocation"                    TEXT         NOT NULL DEFAULT '',
    "CreatedAt"                        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"                        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "UX_FulfillmentGroupings_Tenant_Code" UNIQUE ("TenantId", "Code")
);

CREATE INDEX IF NOT EXISTS "IX_FulfillmentGroupings_Tenant"
    ON "FulfillmentGroupings" ("TenantId");

CREATE TABLE IF NOT EXISTS "FulfillmentSlots"
(
    "Id"           VARCHAR(40)  NOT NULL PRIMARY KEY,
    "TenantId"     VARCHAR(64)  NOT NULL,
    "GroupingId"   VARCHAR(40)  NOT NULL,
    "Name"         TEXT         NOT NULL,
    "Code"         VARCHAR(100) NOT NULL,
    "Mode"         VARCHAR(40)  NOT NULL,
    "StartingDate" DATE         NOT NULL,
    "EndingDate"   DATE         NOT NULL,
    "Price"        TEXT         NOT NULL DEFAULT '',
    "UpdatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_FulfillmentSlots_Grouping"
        FOREIGN KEY ("GroupingId") REFERENCES "FulfillmentGroupings" ("Id") ON DELETE CASCADE,
    CONSTRAINT "UX_FulfillmentSlots_Tenant_Grouping_Code" UNIQUE ("TenantId", "GroupingId", "Code")
);

CREATE INDEX IF NOT EXISTS "IX_FulfillmentSlots_Tenant_Grouping"
    ON "FulfillmentSlots" ("TenantId", "GroupingId");

CREATE TABLE IF NOT EXISTS "CollectionLocations"
(
    "Id"              VARCHAR(40)  NOT NULL PRIMARY KEY,
    "TenantId"        VARCHAR(64)  NOT NULL,
    "Name"            TEXT         NOT NULL,
    "BrandCodesJson"  TEXT         NOT NULL DEFAULT '[]',
    "Address1"        TEXT         NULL,
    "Address2"        TEXT         NULL,
    "Address3"        TEXT         NULL,
    "PostalCode"      TEXT         NULL,
    "Country"         TEXT         NULL,
    "GeoLat"          TEXT         NULL,
    "GeoLng"          TEXT         NULL,
    "DesktopImageSrc" TEXT         NULL,
    "DesktopImageName" TEXT        NULL,
    "MobileImageSrc"   TEXT         NULL,
    "MobileImageName"  TEXT         NULL,
    "Active"           BOOLEAN      NOT NULL DEFAULT TRUE,
    "OpeningHours"     TEXT         NULL,
    "ClosingHours"     TEXT         NULL,
    "CreatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS "IX_CollectionLocations_Tenant"
    ON "CollectionLocations" ("TenantId");

CREATE TABLE IF NOT EXISTS "LogisticPartners"
(
    "Id"                 VARCHAR(40)  NOT NULL PRIMARY KEY,
    "TenantId"           VARCHAR(64)  NOT NULL,
    "Name"               TEXT         NOT NULL,
    "Code"               VARCHAR(100) NOT NULL,
    "Enabled"            BOOLEAN      NOT NULL DEFAULT TRUE,
    "IntegratedLogistic" BOOLEAN      NOT NULL DEFAULT FALSE,
    "CreatedAt"          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "UX_LogisticPartners_Tenant_Code" UNIQUE ("TenantId", "Code")
);

CREATE INDEX IF NOT EXISTS "IX_LogisticPartners_Tenant"
    ON "LogisticPartners" ("TenantId");

CREATE TABLE IF NOT EXISTS "FulfillmentTenantSettings"
(
    "TenantId"           VARCHAR(64) NOT NULL PRIMARY KEY,
    "PredefinedFieldsJson" TEXT      NOT NULL DEFAULT '[]',
    "DynamicFieldsJson"  TEXT       NOT NULL DEFAULT '[]',
    "StockLocationsJson" TEXT       NOT NULL DEFAULT '[]',
    "UpdatedAt"          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
