/*
Move PMS role table off legacy name Roles/roles -> pms_roles (no synonyms).

For full legacy restore run HybridRbac_RestoreLegacyTableNames.sql
For full pms_* rename run HybridRbac_RenameToPmsPrefix.sql
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.roles', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.roles', N'role_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
    EXEC sp_rename N'dbo.roles', N'pms_roles';

IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Roles', N'role_id') IS NOT NULL
   AND OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
    EXEC sp_rename N'dbo.Roles', N'pms_roles';

IF OBJECT_ID(N'dbo.rbac_roles', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.pms_roles', N'U') IS NULL
    EXEC sp_rename N'dbo.rbac_roles', N'pms_roles';

IF OBJECT_ID(N'dbo.Roles', N'SN') IS NOT NULL
    DROP SYNONYM dbo.Roles;

IF OBJECT_ID(N'dbo.UserRoles', N'SN') IS NOT NULL
    DROP SYNONYM dbo.UserRoles;

PRINT N'Use HybridRbac_RenameToPmsPrefix.sql for remaining PMS tables.';
