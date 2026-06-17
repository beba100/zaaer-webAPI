-- Revoke orphaned legacy system grants when the matching nav.menu.* permission is not granted.
-- Run on Master DB after deploying nav-menu RBAC fix.

SET NOCOUNT ON;

DECLARE @map TABLE (legacy_code NVARCHAR(150), nav_code NVARCHAR(150));
INSERT INTO @map VALUES
    (N'rbac.users.manage', N'nav.menu.system.users'),
    (N'rbac.roles.manage', N'nav.menu.system.roles'),
    (N'rbac.permissions.view', N'nav.menu.system.permissions'),
    (N'admin.numbering.manage', N'nav.menu.system.numbering');

UPDATE rp
SET granted = 0
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions legacy ON legacy.permission_id = rp.permission_id
INNER JOIN @map m ON m.legacy_code = legacy.permission_code
INNER JOIN dbo.pms_permissions nav ON nav.permission_code = m.nav_code
WHERE rp.granted = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.pms_role_permissions nav_rp
      WHERE nav_rp.role_id = rp.role_id
        AND nav_rp.permission_id = nav.permission_id
        AND nav_rp.granted = 1
  );

PRINT CONCAT(N'Legacy system grants revoked (no matching nav.menu): ', @@ROWCOUNT);

-- Grant legacy functional codes when nav.menu is granted (keeps API/page guards working).
;WITH desired AS (
    SELECT nav_rp.role_id, legacy.permission_id
    FROM dbo.pms_role_permissions nav_rp
    INNER JOIN dbo.pms_permissions nav ON nav.permission_id = nav_rp.permission_id
    INNER JOIN @map m ON m.nav_code = nav.permission_code
    INNER JOIN dbo.pms_permissions legacy ON legacy.permission_code = m.legacy_code
    WHERE nav_rp.granted = 1
      AND nav.is_active = 1
      AND legacy.is_active = 1
)
MERGE dbo.pms_role_permissions AS target
USING desired AS source
ON target.role_id = source.role_id AND target.permission_id = source.permission_id
WHEN MATCHED AND target.granted = 0 THEN
    UPDATE SET granted = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, granted, created_at)
    VALUES (source.role_id, source.permission_id, 1, SYSUTCDATETIME());

PRINT CONCAT(N'Legacy system grants synced from nav.menu: ', @@ROWCOUNT);
