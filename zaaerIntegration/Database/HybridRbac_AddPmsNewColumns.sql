/*
Step A — Add new PMS columns on existing tables (run BEFORE HybridRbac_SimplifyPmsSchema.sql).

Uses GO so UPDATE runs after ALTER (avoids Msg 207 invalid column name at compile time).
Safe to re-run.
*/

SET NOCOUNT ON;

-- =============================================================================
-- pms_roles — new columns
-- =============================================================================
IF OBJECT_ID(N'dbo.pms_roles', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.pms_roles', N'role_name_ar') IS NULL
        ALTER TABLE dbo.pms_roles ADD role_name_ar NVARCHAR(150) NULL;

    IF COL_LENGTH(N'dbo.pms_roles', N'role_name_en') IS NULL
        ALTER TABLE dbo.pms_roles ADD role_name_en NVARCHAR(150) NULL;

    PRINT N'pms_roles: role_name_ar, role_name_en ready.';
END
ELSE
    PRINT N'SKIP pms_roles (table not found).';
GO

IF OBJECT_ID(N'dbo.pms_roles', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pms_roles', N'role_name_en') IS NOT NULL
BEGIN
    UPDATE dbo.pms_roles
    SET role_name_en = ISNULL(NULLIF(LTRIM(role_name_en), N''), role_name),
        role_name_ar = ISNULL(NULLIF(LTRIM(role_name_ar), N''), role_name)
    WHERE role_name IS NOT NULL;

    PRINT CONCAT(N'pms_roles backfill rows: ', @@ROWCOUNT);
END
GO

-- =============================================================================
-- pms_permissions — new columns
-- =============================================================================
IF OBJECT_ID(N'dbo.pms_permissions', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.pms_permissions', N'permission_name_ar') IS NULL
        ALTER TABLE dbo.pms_permissions ADD permission_name_ar NVARCHAR(200) NULL;

    IF COL_LENGTH(N'dbo.pms_permissions', N'permission_name_en') IS NULL
        ALTER TABLE dbo.pms_permissions ADD permission_name_en NVARCHAR(200) NULL;

    IF COL_LENGTH(N'dbo.pms_permissions', N'submodule_name') IS NULL
        ALTER TABLE dbo.pms_permissions ADD submodule_name NVARCHAR(80) NULL;

    IF COL_LENGTH(N'dbo.pms_permissions', N'sort_order') IS NULL
        ALTER TABLE dbo.pms_permissions ADD sort_order INT NOT NULL
            CONSTRAINT DF_pms_permissions_sort_order DEFAULT (0);

    PRINT N'pms_permissions: permission_name_ar, permission_name_en, submodule_name, sort_order ready.';
END
ELSE
    PRINT N'SKIP pms_permissions (table not found).';
GO

IF OBJECT_ID(N'dbo.pms_permissions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pms_permissions', N'permission_name_en') IS NOT NULL
BEGIN
    UPDATE dbo.pms_permissions
    SET permission_name_en = ISNULL(NULLIF(permission_name_en, N''), permission_name),
        permission_name_ar = ISNULL(NULLIF(permission_name_ar, N''), permission_name),
        submodule_name = ISNULL(NULLIF(submodule_name, N''), module_name)
    WHERE permission_name IS NOT NULL;

    PRINT CONCAT(N'pms_permissions backfill rows: ', @@ROWCOUNT);
END
GO

PRINT N'HybridRbac_AddPmsNewColumns.sql complete. Next: HybridRbac_SimplifyPmsSchema.sql';
