-- DAI-718 / DAI-688: generic approval engine backing table.
CREATE TABLE IF NOT EXISTS "ApprovalRequests"
(
    "Id"              UUID        NOT NULL PRIMARY KEY,
    "TenantId"        VARCHAR(64) NOT NULL,
    "EntityType"      TEXT        NOT NULL, -- 'Product','Campaign','PromoCode','Content', ...
    "EntityId"        TEXT        NOT NULL,
    "State"           TEXT        NOT NULL CHECK ("State" IN ('Draft','PendingApproval','Approved','Rejected','ChangesRequested')),
    "RequestedBy"     TEXT        NOT NULL,
    "RequestedAt"     TIMESTAMPTZ NOT NULL DEFAULT now(),
    "CurrentApprover" TEXT        NULL,
    "History"         JSONB       NOT NULL DEFAULT '[]'::jsonb, -- [{ state, by, at, notes }]
    "PayloadSnapshot" JSONB       NOT NULL,
    "FinalizedAt"     TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS "IX_ApprovalRequests_Assigned"
    ON "ApprovalRequests" ("TenantId", "CurrentApprover", "State");

CREATE INDEX IF NOT EXISTS "IX_ApprovalRequests_Entity"
    ON "ApprovalRequests" ("TenantId", "EntityType", "EntityId");

CREATE INDEX IF NOT EXISTS "IX_ApprovalRequests_TenantRequestedAt"
    ON "ApprovalRequests" ("TenantId", "RequestedAt" DESC);

