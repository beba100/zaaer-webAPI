/*
استعادة جداول myMainProject / المصروفات بأسمائها الأصلية (جداول فعلية، بدون Synonyms):
  dbo.Roles, dbo.UserRoles, dbo.MasterUsers

جداول PMS (Hybrid RBAC) تبقى منفصلة ولا تمس:
  dbo.pms_users, dbo.pms_roles, dbo.pms_user_roles, dbo.pms_permissions, ...

شغّل على Master DB بعد أي LegacyRename أو CompatibilitySynonyms أو FixRolesNameConflict.
آمن للتكرار.
*/

SET NOCOUNT ON;

PRINT N'=== Hybrid RBAC: restore legacy table names ===';

-- 1) إزالة المرادفات — التطبيق القديم يحتاج جداول حقيقية باسم Roles / UserRoles
IF OBJECT_ID(N'dbo.Roles', N'SN') IS NOT NULL
BEGIN
    DROP SYNONYM dbo.Roles;
    PRINT N'Dropped synonym dbo.Roles';
END;

IF OBJECT_ID(N'dbo.UserRoles', N'SN') IS NOT NULL
BEGIN
    DROP SYNONYM dbo.UserRoles;
    PRINT N'Dropped synonym dbo.UserRoles';
END;

-- 2) إبعاد جدول أدوار PMS عن الاسم roles/Roles (يتعارض مع legacy)
IF OBJECT_ID(N'dbo.roles', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.roles', N'role_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.roles', N'pms_roles';
    PRINT N'PMS: renamed dbo.roles -> dbo.pms_roles';
END;

IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Roles', N'role_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.Roles', N'pms_roles';
    PRINT N'PMS: renamed dbo.Roles (RBAC) -> dbo.pms_roles';
END;

IF OBJECT_ID(N'dbo.rbac_roles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.rbac_roles', N'pms_roles';
    PRINT N'PMS: renamed dbo.rbac_roles -> dbo.pms_roles';
END;

-- 3) استعادة dbo.Roles (Id, Name, Code)
IF OBJECT_ID(N'dbo.Roles_Legacy', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.Roles_Legacy', N'Roles';
    PRINT N'Restored dbo.Roles_Legacy -> dbo.Roles';
END
ELSE IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
     AND COL_LENGTH(N'dbo.Roles', N'Id') IS NOT NULL
    PRINT N'Legacy dbo.Roles already present.';
ELSE IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
    PRINT N'WARNING: dbo.Roles exists but is not legacy (Id). Check schema manually.';

-- 4) استعادة dbo.UserRoles (Id, UserId, RoleId) — لا يتعارض مع dbo.pms_user_roles (PMS)
IF OBJECT_ID(N'dbo.UserRoles_Legacy', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.UserRoles_Legacy', N'UserRoles';
    PRINT N'Restored dbo.UserRoles_Legacy -> dbo.UserRoles';
END
ELSE IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NOT NULL
     AND COL_LENGTH(N'dbo.UserRoles', N'Id') IS NOT NULL
    PRINT N'Legacy dbo.UserRoles already present.';

-- 5) ملخص
PRINT N'';
PRINT N'Expected for OLD app (myMainProject):';
PRINT N'  MasterUsers, Roles, UserRoles, UserTenants, ...';
PRINT N'Expected for PMS (this project):';
PRINT N'  pms_users, pms_roles, pms_user_roles, pms_permissions, pms_role_permissions, pms_user_hotel_access, ...';
PRINT N'';
PRINT N'Restore complete. No redeploy to myMainProject required if schema matches above.';
