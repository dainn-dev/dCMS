-- Dev seed: insert basic variant axes (Màu sắc + Kích cỡ) for tenant t1 if none exist.
-- Idempotent: skipped entirely when CatalogAttributes already has rows for t1.
DO $$
DECLARE
    attr_mau_sac  INT;
    attr_kich_co  INT;
BEGIN
    IF EXISTS (SELECT 1 FROM "CatalogAttributes" WHERE "TenantId" = 't1') THEN
        RETURN;
    END IF;

    -- Axis 1: Màu sắc
    INSERT INTO "CatalogAttributes" ("TenantId", "Name", "SortOrder")
    VALUES ('t1', 'Màu sắc', 1)
    RETURNING "Id" INTO attr_mau_sac;

    INSERT INTO "CatalogAttributeValues" ("AttributeId", "Name", "SortOrder")
    VALUES
        (attr_mau_sac, 'Đỏ',      1),
        (attr_mau_sac, 'Xanh lá', 2),
        (attr_mau_sac, 'Trắng',   3),
        (attr_mau_sac, 'Đen',     4);

    -- Axis 2: Kích cỡ
    INSERT INTO "CatalogAttributes" ("TenantId", "Name", "SortOrder")
    VALUES ('t1', 'Kích cỡ', 2)
    RETURNING "Id" INTO attr_kich_co;

    INSERT INTO "CatalogAttributeValues" ("AttributeId", "Name", "SortOrder")
    VALUES
        (attr_kich_co, 'S',  1),
        (attr_kich_co, 'M',  2),
        (attr_kich_co, 'L',  3),
        (attr_kich_co, 'XL', 4);
END $$;
