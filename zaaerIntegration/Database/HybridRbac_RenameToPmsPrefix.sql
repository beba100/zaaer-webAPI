/*
Rename Hybrid PMS RBAC tables on Master DB to pms_* prefix.
Safe to re-run. Does not touch legacy MasterUsers / Roles / UserRoles.

Run after HybridRbac_MasterDB.sql (old names) or on DB that already has pms_* (no-op).
*/

SET NOCOUNT ON;

PRINT N'=== Rename PMS RBAC tables to pms_* prefix ===';

IF OBJECT_ID(N'dbo.users', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.users', N'user_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_users', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.users', N'pms_users';
    PRINT N'users -> pms_users';
END;

IF OBJECT_ID(N'dbo.rbac_roles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.rbac_roles', N'pms_roles';
    PRINT N'rbac_roles -> pms_roles';
END;

IF OBJECT_ID(N'dbo.permissions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.permissions', N'permission_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_permissions', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.permissions', N'pms_permissions';
    PRINT N'permissions -> pms_permissions';
END;

IF OBJECT_ID(N'dbo.role_permissions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.role_permissions', N'role_permission_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_role_permissions', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.role_permissions', N'pms_role_permissions';
    PRINT N'role_permissions -> pms_role_permissions';
END;

IF OBJECT_ID(N'dbo.hotel_groups', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.hotel_groups', N'hotel_group_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_hotel_groups', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.hotel_groups', N'pms_hotel_groups';
    PRINT N'hotel_groups -> pms_hotel_groups';
END;

IF OBJECT_ID(N'dbo.hotel_group_hotels', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_hotel_group_hotels', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.hotel_group_hotels', N'pms_hotel_group_hotels';
    PRINT N'hotel_group_hotels -> pms_hotel_group_hotels';
END;

IF OBJECT_ID(N'dbo.pms_user_hotel_access', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.pms_user_hotels', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.pms_user_hotel_access', N'pms_user_hotels';
    PRINT N'pms_user_hotel_access -> pms_user_hotels';
END
ELSE IF OBJECT_ID(N'dbo.user_hotel_access', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.user_hotel_access', N'user_hotel_access_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_user_hotels', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.user_hotel_access', N'pms_user_hotels';
    PRINT N'user_hotel_access -> pms_user_hotels';
END;

IF OBJECT_ID(N'dbo.user_roles', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.user_roles', N'user_role_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_user_roles', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.user_roles', N'pms_user_roles';
    PRINT N'user_roles -> pms_user_roles';
END;

IF OBJECT_ID(N'dbo.tenant_auth_modes', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.tenant_auth_modes', N'tenant_auth_mode_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_tenant_auth_modes', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.tenant_auth_modes', N'pms_tenant_auth_modes';
    PRINT N'tenant_auth_modes -> pms_tenant_auth_modes';
END;

PRINT N'';
PRINT N'PMS Master tables: pms_users, pms_roles, pms_permissions, pms_role_permissions,';
PRINT N'  pms_user_roles, pms_user_hotels';
PRINT N'Run HybridRbac_SimplifyPmsSchema.sql next to drop hotel groups / default hotel.';
PRINT N'Legacy (myMainProject): MasterUsers, Roles, UserRoles — unchanged.';
