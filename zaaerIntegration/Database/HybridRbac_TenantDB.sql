/*
Hybrid PMS RBAC - Tenant DB schema additions.
Run on each tenant database that should support local RBAC or offline/local overrides.
*/

IF OBJECT_ID(N'dbo.role_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.role_permissions (
        role_permission_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_role_permissions PRIMARY KEY,
        role_id INT NOT NULL,
        permission_id INT NOT NULL,
        granted BIT NOT NULL CONSTRAINT DF_role_permissions_granted DEFAULT (1),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_role_permissions_created_at DEFAULT (SYSUTCDATETIME()),
        created_by INT NULL,
        zaaer_id INT NULL
    );
END;

IF COL_LENGTH('dbo.users', 'master_user_id') IS NULL
    ALTER TABLE dbo.users ADD master_user_id INT NULL;

IF COL_LENGTH('dbo.users', 'sync_source') IS NULL
    ALTER TABLE dbo.users ADD sync_source NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.users', 'sync_version') IS NULL
    ALTER TABLE dbo.users ADD sync_version INT NULL;

IF COL_LENGTH('dbo.users', 'last_synced_at') IS NULL
    ALTER TABLE dbo.users ADD last_synced_at DATETIME2 NULL;

IF COL_LENGTH('dbo.users', 'is_local_override') IS NULL
    ALTER TABLE dbo.users ADD is_local_override BIT NOT NULL CONSTRAINT DF_users_is_local_override DEFAULT (0);

IF COL_LENGTH('dbo.roles', 'master_role_id') IS NULL
    ALTER TABLE dbo.roles ADD master_role_id INT NULL;

IF COL_LENGTH('dbo.roles', 'sync_source') IS NULL
    ALTER TABLE dbo.roles ADD sync_source NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.roles', 'sync_version') IS NULL
    ALTER TABLE dbo.roles ADD sync_version INT NULL;

IF COL_LENGTH('dbo.roles', 'last_synced_at') IS NULL
    ALTER TABLE dbo.roles ADD last_synced_at DATETIME2 NULL;

IF COL_LENGTH('dbo.roles', 'is_local_override') IS NULL
    ALTER TABLE dbo.roles ADD is_local_override BIT NOT NULL CONSTRAINT DF_roles_is_local_override DEFAULT (0);

IF COL_LENGTH('dbo.permissions', 'master_permission_id') IS NULL
    ALTER TABLE dbo.permissions ADD master_permission_id INT NULL;

IF COL_LENGTH('dbo.permissions', 'sync_source') IS NULL
    ALTER TABLE dbo.permissions ADD sync_source NVARCHAR(40) NULL;

IF COL_LENGTH('dbo.permissions', 'sync_version') IS NULL
    ALTER TABLE dbo.permissions ADD sync_version INT NULL;

IF COL_LENGTH('dbo.permissions', 'last_synced_at') IS NULL
    ALTER TABLE dbo.permissions ADD last_synced_at DATETIME2 NULL;

IF COL_LENGTH('dbo.permissions', 'is_local_override') IS NULL
    ALTER TABLE dbo.permissions ADD is_local_override BIT NOT NULL CONSTRAINT DF_permissions_is_local_override DEFAULT (0);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_role_permissions_role_permission')
    CREATE UNIQUE INDEX UX_role_permissions_role_permission ON dbo.role_permissions(role_id, permission_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_users_master_user_id')
    CREATE INDEX IX_users_master_user_id ON dbo.users(master_user_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_roles_master_role_id')
    CREATE INDEX IX_roles_master_role_id ON dbo.roles(master_role_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_permissions_master_permission_id')
    CREATE INDEX IX_permissions_master_permission_id ON dbo.permissions(master_permission_id);
