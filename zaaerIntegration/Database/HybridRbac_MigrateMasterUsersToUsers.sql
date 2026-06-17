/*
Migrate legacy MasterUsers -> pms_users + pms_user_hotels + pms_user_roles.
Run after HybridRbac_MasterDB.sql, HybridRbac_SimplifyPmsSchema.sql, HybridRbac_SeedPermissions.sql.
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.pms_users', N'U') IS NULL
BEGIN
    RAISERROR(N'dbo.pms_users does not exist. Run HybridRbac_MasterDB.sql first.', 16, 1);
    RETURN;
END;

INSERT INTO dbo.pms_users (
    master_user_id, username, employee_number, status, user_type,
    first_name, last_name, email, phone_number, password_hash, is_active, created_at, updated_at
)
SELECT
    mu.Id, mu.Username, mu.EmployeeNumber,
    CASE WHEN mu.IsActive = 1 THEN 1 ELSE 0 END, N'employee',
    CASE WHEN CHARINDEX(N' ', LTRIM(RTRIM(mu.FullName))) > 0
        THEN LTRIM(RTRIM(LEFT(LTRIM(RTRIM(mu.FullName)), CHARINDEX(N' ', LTRIM(RTRIM(mu.FullName))) - 1)))
        ELSE ISNULL(NULLIF(LTRIM(RTRIM(mu.FullName)), N''), mu.Username) END,
    CASE WHEN CHARINDEX(N' ', LTRIM(RTRIM(mu.FullName))) > 0
        THEN LTRIM(RTRIM(SUBSTRING(LTRIM(RTRIM(mu.FullName)), CHARINDEX(N' ', LTRIM(RTRIM(mu.FullName))) + 1, 4000)))
        ELSE N'' END,
    ISNULL(NULLIF(LTRIM(RTRIM(mu.Email)), N''), mu.Username + N'@local'),
    mu.PhoneNumber, ISNULL(mu.PasswordHash, N''), mu.IsActive,
    ISNULL(mu.CreatedAt, SYSUTCDATETIME()), mu.UpdatedAt
FROM dbo.MasterUsers mu
WHERE NOT EXISTS (SELECT 1 FROM dbo.pms_users u WHERE u.master_user_id = mu.Id);

PRINT CONCAT(N'Migrated pms_users: ', @@ROWCOUNT);

IF OBJECT_ID(N'dbo.pms_user_hotels', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.pms_user_hotels (user_id, tenant_id, is_active, created_at)
    SELECT u.user_id, ut.TenantId, 1, SYSUTCDATETIME()
    FROM dbo.UserTenants ut
    INNER JOIN dbo.pms_users u ON u.master_user_id = ut.UserId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.pms_user_hotels h WHERE h.user_id = u.user_id AND h.tenant_id = ut.TenantId);

    INSERT INTO dbo.pms_user_hotels (user_id, tenant_id, is_active, created_at)
    SELECT u.user_id, mu.TenantId, 1, SYSUTCDATETIME()
    FROM dbo.MasterUsers mu
    INNER JOIN dbo.pms_users u ON u.master_user_id = mu.Id
    WHERE mu.TenantId IS NOT NULL AND mu.TenantId > 0
      AND NOT EXISTS (SELECT 1 FROM dbo.pms_user_hotels h WHERE h.user_id = u.user_id AND h.tenant_id = mu.TenantId);
END;

DECLARE @adminRoleId INT;
SELECT @adminRoleId = role_id FROM dbo.pms_roles WHERE role_code = N'system_admin';

IF @adminRoleId IS NULL
BEGIN
    INSERT INTO dbo.pms_roles (role_name, role_name_en, role_name_ar, role_description, role_code, is_active, created_at)
    VALUES (N'System Administrator', N'System Administrator', N'مدير النظام', N'Full PMS access', N'system_admin', 1, SYSUTCDATETIME());
    SET @adminRoleId = SCOPE_IDENTITY();
END;

INSERT INTO dbo.pms_role_permissions (role_id, permission_id, granted, created_at)
SELECT @adminRoleId, p.permission_id, 1, SYSUTCDATETIME()
FROM dbo.pms_permissions p
WHERE p.is_active = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.pms_role_permissions rp WHERE rp.role_id = @adminRoleId AND rp.permission_id = p.permission_id);

INSERT INTO dbo.pms_user_roles (user_id, role_id, is_active, created_at)
SELECT u.user_id, @adminRoleId, 1, SYSUTCDATETIME()
FROM dbo.pms_users u
WHERE u.is_active = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.pms_user_roles ur WHERE ur.user_id = u.user_id AND ur.role_id = @adminRoleId);

PRINT N'Migration complete.';
