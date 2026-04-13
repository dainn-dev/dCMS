-- Dev seed: insert a basic category tree for tenant t1 if none exist.
-- Idempotent: skipped entirely when Categories already has rows for t1.
DO $$
DECLARE
    cat_thucpham  INT;
    cat_douong    INT;
    cat_giadung   INT;
BEGIN
    IF EXISTS (SELECT 1 FROM "Categories" WHERE "TenantId" = 't1') THEN
        RETURN;
    END IF;

    -- Root: Thực phẩm
    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
    VALUES ('t1', NULL, '/', 0, 'Thực phẩm', 'thuc-pham', 1)
    RETURNING "Id" INTO cat_thucpham;
    UPDATE "Categories" SET "Path" = '/' || cat_thucpham || '/' WHERE "Id" = cat_thucpham;

    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
    VALUES
        ('t1', cat_thucpham, '/' || cat_thucpham || '/', 1, 'Thịt & Hải sản',  'thit-hai-san', 1),
        ('t1', cat_thucpham, '/' || cat_thucpham || '/', 1, 'Rau củ quả',       'rau-cu-qua',   2),
        ('t1', cat_thucpham, '/' || cat_thucpham || '/', 1, 'Đồ khô',           'do-kho',       3);

    -- Root: Đồ uống
    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
    VALUES ('t1', NULL, '/', 0, 'Đồ uống', 'do-uong', 2)
    RETURNING "Id" INTO cat_douong;
    UPDATE "Categories" SET "Path" = '/' || cat_douong || '/' WHERE "Id" = cat_douong;

    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
    VALUES
        ('t1', cat_douong, '/' || cat_douong || '/', 1, 'Nước giải khát', 'nuoc-giai-khat', 1),
        ('t1', cat_douong, '/' || cat_douong || '/', 1, 'Bia & Rượu',     'bia-ruou',       2);

    -- Root: Gia dụng
    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
    VALUES ('t1', NULL, '/', 0, 'Gia dụng', 'gia-dung', 3)
    RETURNING "Id" INTO cat_giadung;
    UPDATE "Categories" SET "Path" = '/' || cat_giadung || '/' WHERE "Id" = cat_giadung;

    INSERT INTO "Categories" ("TenantId", "ParentId", "Path", "Depth", "Name", "Slug", "SortOrder")
    VALUES
        ('t1', cat_giadung, '/' || cat_giadung || '/', 1, 'Chăm sóc cá nhân', 'cham-soc-ca-nhan', 1),
        ('t1', cat_giadung, '/' || cat_giadung || '/', 1, 'Vệ sinh nhà cửa',  've-sinh-nha-cua',  2);
END $$;
