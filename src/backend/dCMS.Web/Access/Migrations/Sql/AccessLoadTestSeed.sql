-- DAI-705 — idempotent seed for permission cache load test.
-- Inserts 'orders' module grants for the 'dcmsOperations' role and links Umbraco user 1 to that group.
-- Run only when Dcms:Access:LoadTestSeedEnabled=true (see LoadTestSeedComposer).

-- 1) Ensure baseline grants exist for dcmsOperations on the orders module.
IF NOT EXISTS (
    SELECT 1 FROM dcms_role_module_permissions
    WHERE role_alias = N'dcmsOperations' AND module = N'orders' AND action = N'view')
BEGIN
    INSERT INTO dcms_role_module_permissions (role_alias, module, action, granted)
    VALUES (N'dcmsOperations', N'orders', N'view', 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM dcms_role_module_permissions
    WHERE role_alias = N'dcmsOperations' AND module = N'orders' AND action = N'create')
BEGIN
    INSERT INTO dcms_role_module_permissions (role_alias, module, action, granted)
    VALUES (N'dcmsOperations', N'orders', N'create', 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM dcms_role_module_permissions
    WHERE role_alias = N'dcmsOperations' AND module = N'orders' AND action = N'update')
BEGIN
    INSERT INTO dcms_role_module_permissions (role_alias, module, action, granted)
    VALUES (N'dcmsOperations', N'orders', N'update', 1);
END;

-- 2) Link Umbraco user 1 (admin) to a UserGroup whose alias matches dcmsOperations,
--    so PermissionService.GetGroupAliases(1) returns the role above.
DECLARE @groupId INT = (SELECT TOP 1 id FROM umbracoUserGroup WHERE userGroupAlias = N'dcmsOperations');

IF @groupId IS NOT NULL AND EXISTS (SELECT 1 FROM umbracoUser WHERE id = 1)
   AND NOT EXISTS (SELECT 1 FROM umbracoUser2UserGroup WHERE userId = 1 AND userGroupId = @groupId)
BEGIN
    INSERT INTO umbracoUser2UserGroup (userId, userGroupId) VALUES (1, @groupId);
END;
