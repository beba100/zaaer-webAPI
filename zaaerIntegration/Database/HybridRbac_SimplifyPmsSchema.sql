/*
Step B — Simplify PMS RBAC schema (drops / renames / indexes).

PREREQUISITE: HybridRbac_AddPmsNewColumns.sql (adds role_name_ar/en, permission AR/EN, etc.)

Keeps:  pms_users, pms_roles, pms_permissions, pms_role_permissions, pms_user_roles, pms_user_hotels
Drops:  pms_hotel_groups, pms_hotel_group_hotels, pms_tenant_auth_modes
Removes: pms_users.default_hotel_id, pms_roles.hotel_id, scoped columns on pms_user_roles
*/

SET NOCOUNT ON;

-- --- pms_user_hotels (from pms_user_hotel_access) ---
IF OBJECT_ID(N'dbo.pms_user_hotels', N'U') IS NULL
   AND OBJECT_ID(N'dbo.pms_user_hotel_access', N'U') IS NOT NULL
BEGIN
    EXEC sp_rename N'dbo.pms_user_hotel_access', N'pms_user_hotels';
    PRINT N'Renamed pms_user_hotel_access -> pms_user_hotels';
END;

IF OBJECT_ID(N'dbo.pms_user_hotels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_user_hotels (
        user_hotel_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_user_hotels PRIMARY KEY,
        user_id INT NOT NULL,
        tenant_id INT NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_pms_user_hotels_is_active DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_pms_user_hotels_created_at DEFAULT (SYSUTCDATETIME()),
        created_by INT NULL
    );
    CREATE UNIQUE INDEX UX_pms_user_hotels_user_tenant ON dbo.pms_user_hotels(user_id, tenant_id);
END;

IF COL_LENGTH(N'dbo.pms_user_hotels', N'hotel_group_id') IS NOT NULL
BEGIN
    DELETE FROM dbo.pms_user_hotels WHERE tenant_id IS NULL OR tenant_id <= 0;
END;

IF COL_LENGTH(N'dbo.pms_user_hotels', N'hotel_group_id') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.pms_user_hotels') AND name LIKE N'%hotel_group%'
   )
BEGIN
    ALTER TABLE dbo.pms_user_hotels DROP COLUMN hotel_group_id;
    PRINT N'Dropped pms_user_hotels.hotel_group_id';
END;

IF COL_LENGTH(N'dbo.pms_user_hotels', N'user_hotel_access_id') IS NOT NULL
    EXEC sp_rename N'dbo.pms_user_hotels.user_hotel_access_id', N'user_hotel_id', N'COLUMN';

IF COL_LENGTH(N'dbo.pms_user_hotels', N'tenant_id') IS NOT NULL
   AND EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.pms_user_hotels') AND name = N'tenant_id' AND is_nullable = 1
   )
BEGIN
    UPDATE dbo.pms_user_hotels SET tenant_id = 0 WHERE tenant_id IS NULL;
    ALTER TABLE dbo.pms_user_hotels ALTER COLUMN tenant_id INT NOT NULL;
END;

-- --- pms_users ---
IF COL_LENGTH(N'dbo.pms_users', N'default_hotel_id') IS NOT NULL
BEGIN
    ALTER TABLE dbo.pms_users DROP COLUMN default_hotel_id;
    PRINT N'Dropped pms_users.default_hotel_id';
END;

-- --- pms_user_roles ---
IF COL_LENGTH(N'dbo.pms_user_roles', N'tenant_id') IS NOT NULL
BEGIN
    ALTER TABLE dbo.pms_user_roles DROP COLUMN tenant_id;
    PRINT N'Dropped pms_user_roles.tenant_id';
END;

IF COL_LENGTH(N'dbo.pms_user_roles', N'hotel_group_id') IS NOT NULL
BEGIN
    ALTER TABLE dbo.pms_user_roles DROP COLUMN hotel_group_id;
    PRINT N'Dropped pms_user_roles.hotel_group_id';
END;

IF OBJECT_ID(N'dbo.pms_user_roles', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pms_user_roles_user_role' AND object_id = OBJECT_ID(N'dbo.pms_user_roles'))
    CREATE UNIQUE INDEX UX_pms_user_roles_user_role ON dbo.pms_user_roles(user_id, role_id);

-- --- pms_roles ---
IF COL_LENGTH(N'dbo.pms_roles', N'hotel_id') IS NOT NULL
BEGIN
    ALTER TABLE dbo.pms_roles DROP COLUMN hotel_id;
    PRINT N'Dropped pms_roles.hotel_id';
END;

-- --- drop unused tables ---
IF OBJECT_ID(N'dbo.pms_hotel_group_hotels', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.pms_hotel_group_hotels;
    PRINT N'Dropped pms_hotel_group_hotels';
END;

IF OBJECT_ID(N'dbo.pms_hotel_groups', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.pms_hotel_groups;
    PRINT N'Dropped pms_hotel_groups';
END;

IF OBJECT_ID(N'dbo.pms_tenant_auth_modes', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.pms_tenant_auth_modes;
    PRINT N'Dropped pms_tenant_auth_modes';
END;

PRINT N'Simplify complete. Next: HybridRbac_SeedPermissions.sql';
