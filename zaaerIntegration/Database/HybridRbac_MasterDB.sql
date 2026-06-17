/*
Hybrid PMS RBAC — Master DB (6 tables, Zaaer-style):
  pms_users, pms_roles, pms_permissions, pms_role_permissions, pms_user_roles, pms_user_hotels

Does not touch legacy MasterUsers / Roles / UserRoles.
On existing DBs run scripts in HybridRbac_MasterDB_RunOrder.txt (Rename -> MasterDB -> AddPmsNewColumns -> Simplify -> Seed -> ...).
*/

SET NOCOUNT ON;

-- inline rename old names -> pms_* (see HybridRbac_RenameToPmsPrefix.sql)
IF OBJECT_ID(N'dbo.users', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.users', N'user_id') IS NOT NULL AND OBJECT_ID(N'dbo.pms_users', N'U') IS NULL
    EXEC sp_rename N'dbo.users', N'pms_users';

IF OBJECT_ID(N'dbo.pms_users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_users (
        user_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_users PRIMARY KEY,
        zaaer_id INT NULL,
        master_user_id INT NULL,
        username NVARCHAR(100) NOT NULL CONSTRAINT DF_pms_users_username DEFAULT (N''),
        employee_number NVARCHAR(50) NULL,
        status BIT NOT NULL CONSTRAINT DF_pms_users_status DEFAULT (1),
        user_type NVARCHAR(50) NOT NULL CONSTRAINT DF_pms_users_user_type DEFAULT (N'employee'),
        first_name NVARCHAR(100) NOT NULL CONSTRAINT DF_pms_users_first_name DEFAULT (N''),
        last_name NVARCHAR(100) NOT NULL CONSTRAINT DF_pms_users_last_name DEFAULT (N''),
        title NVARCHAR(100) NULL,
        email NVARCHAR(255) NOT NULL CONSTRAINT DF_pms_users_email DEFAULT (N''),
        phone_number NVARCHAR(30) NULL,
        department NVARCHAR(100) NULL,
        password_hash NVARCHAR(500) NOT NULL CONSTRAINT DF_pms_users_password_hash DEFAULT (N''),
        change_password BIT NOT NULL CONSTRAINT DF_pms_users_change_password DEFAULT (0),
        is_active BIT NOT NULL CONSTRAINT DF_pms_users_is_active DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_pms_users_created_at DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2 NULL,
        last_login DATETIME2 NULL
    );
END;

IF OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_roles (
        role_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_roles PRIMARY KEY,
        role_name NVARCHAR(150) NOT NULL,
        role_name_ar NVARCHAR(150) NULL,
        role_name_en NVARCHAR(150) NULL,
        role_description NVARCHAR(500) NULL,
        role_code NVARCHAR(100) NULL,
        is_active BIT NOT NULL CONSTRAINT DF_pms_roles_is_active DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_pms_roles_created_at DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2 NULL,
        created_by INT NULL,
        updated_by INT NULL,
        zaaer_id INT NULL
    );
END;

IF OBJECT_ID(N'dbo.pms_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_permissions (
        permission_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_permissions PRIMARY KEY,
        permission_name NVARCHAR(150) NOT NULL,
        permission_name_ar NVARCHAR(200) NULL,
        permission_name_en NVARCHAR(200) NULL,
        permission_code NVARCHAR(150) NOT NULL,
        module_name NVARCHAR(80) NOT NULL,
        submodule_name NVARCHAR(80) NULL,
        action_name NVARCHAR(80) NOT NULL,
        sort_order INT NOT NULL CONSTRAINT DF_pms_permissions_sort_order DEFAULT (0),
        description NVARCHAR(500) NULL,
        is_active BIT NOT NULL CONSTRAINT DF_pms_permissions_is_active DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_pms_permissions_created_at DEFAULT (SYSUTCDATETIME()),
        zaaer_id INT NULL
    );
END;

IF OBJECT_ID(N'dbo.pms_role_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_role_permissions (
        role_permission_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_role_permissions PRIMARY KEY,
        role_id INT NOT NULL,
        permission_id INT NOT NULL,
        granted BIT NOT NULL CONSTRAINT DF_pms_role_permissions_granted DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_pms_role_permissions_created_at DEFAULT (SYSUTCDATETIME()),
        created_by INT NULL,
        zaaer_id INT NULL
    );
END;

IF OBJECT_ID(N'dbo.pms_user_roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_user_roles (
        user_role_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_user_roles PRIMARY KEY,
        user_id INT NOT NULL,
        role_id INT NOT NULL,
        is_active BIT NOT NULL CONSTRAINT DF_pms_user_roles_is_active DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_pms_user_roles_created_at DEFAULT (SYSUTCDATETIME()),
        created_by INT NULL
    );
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
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pms_permissions_code' AND object_id = OBJECT_ID(N'dbo.pms_permissions'))
    CREATE UNIQUE INDEX UX_pms_permissions_code ON dbo.pms_permissions(permission_code);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pms_role_permissions_role_permission' AND object_id = OBJECT_ID(N'dbo.pms_role_permissions'))
    CREATE UNIQUE INDEX UX_pms_role_permissions_role_permission ON dbo.pms_role_permissions(role_id, permission_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pms_user_roles_user_role' AND object_id = OBJECT_ID(N'dbo.pms_user_roles'))
    CREATE UNIQUE INDEX UX_pms_user_roles_user_role ON dbo.pms_user_roles(user_id, role_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pms_user_hotels_user_tenant' AND object_id = OBJECT_ID(N'dbo.pms_user_hotels'))
    CREATE UNIQUE INDEX UX_pms_user_hotels_user_tenant ON dbo.pms_user_hotels(user_id, tenant_id);

PRINT N'PMS Master RBAC schema ready (6 tables).';
PRINT N'Run HybridRbac_SeedPermissions.sql and HybridRbac_AddFinanceCancelPermissions.sql for permission rows.';
