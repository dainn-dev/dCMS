-- DAI-743: relax Brand.Code constraint to support legacy bulk-import codes
-- (e.g. "acqua-di-parma", "10-DEEP"). Widens VARCHAR(20) → VARCHAR(64) and
-- drops the implicit upper-case assumption.
ALTER TABLE "Brands" ALTER COLUMN "Code" TYPE VARCHAR(64);

COMMENT ON COLUMN "Brands"."Code" IS
    'Brand code, unique per tenant. Legacy-friendly: 1–64 chars of letters, digits, dashes (case-preserved).';
