-- §8 Access Module: role-permission matrix + user notification preferences.
-- Tables live in the shared Catalog PostgreSQL database (same DB as catalog/inventory).

CREATE TABLE IF NOT EXISTS role_module_permissions
(
    role_alias  VARCHAR(100) NOT NULL,
    module      VARCHAR(100) NOT NULL,
    action      VARCHAR(100) NOT NULL,
    granted     BOOLEAN      NOT NULL DEFAULT FALSE,
    PRIMARY KEY (role_alias, module, action)
);

COMMENT ON TABLE  role_module_permissions                  IS 'Maps Umbraco UserGroup aliases to module/action grants for dCMS RBAC.';
COMMENT ON COLUMN role_module_permissions.role_alias       IS 'Umbraco IUserGroup.Alias value.';
COMMENT ON COLUMN role_module_permissions.module           IS 'Module identifier, e.g. products, orders, access.';
COMMENT ON COLUMN role_module_permissions.action           IS 'Action identifier, e.g. read, write, delete, export.';
COMMENT ON COLUMN role_module_permissions.granted          IS 'True = permission granted; false = explicitly denied.';

CREATE INDEX IF NOT EXISTS ix_role_module_permissions_role_alias
    ON role_module_permissions (role_alias);

-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS user_notification_prefs
(
    id                SERIAL       PRIMARY KEY,
    user_id           VARCHAR(64)  NOT NULL,
    notification_type VARCHAR(100) NOT NULL,
    enabled           BOOLEAN      NOT NULL DEFAULT FALSE,
    tenant_ids        TEXT         NULL,    -- JSON array of tenant IDs, e.g. ["t1","t2"]
    UNIQUE (user_id, notification_type)
);

COMMENT ON TABLE  user_notification_prefs                        IS 'Per-user notification opt-in preferences.';
COMMENT ON COLUMN user_notification_prefs.user_id                IS 'Umbraco user key (string form of Guid).';
COMMENT ON COLUMN user_notification_prefs.notification_type      IS 'Notification channel/type identifier.';
COMMENT ON COLUMN user_notification_prefs.tenant_ids             IS 'Optional JSON array limiting which tenants trigger this notification.';

CREATE INDEX IF NOT EXISTS ix_user_notification_prefs_user_id
    ON user_notification_prefs (user_id);
