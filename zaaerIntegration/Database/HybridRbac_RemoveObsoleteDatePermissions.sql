/*
  Collapse granular date permissions into reservations.edit_stay_dates_after_checkin.
  Keeps: reservations.edit_stay_dates_after_checkin, reservations.auto_extend

  Run on Master DB after HybridRbac_SeedPermissions.sql
*/

SET NOCOUNT ON;

DECLARE @obsolete TABLE (permission_code NVARCHAR(150) NOT NULL PRIMARY KEY);

INSERT INTO @obsolete (permission_code)
VALUES
    (N'reservations.edit_checkin_datetime'),
    (N'reservations.edit_checkout_datetime'),
    (N'reservations.edit_checkin_time'),
    (N'reservations.edit_checkout_time'),
    (N'reservations.edit_arrival_past_after_checkin'),
    (N'reservations.edit_departure_past_after_checkin');

DECLARE @newCode NVARCHAR(150) = N'reservations.edit_stay_dates_after_checkin';

DECLARE @newPermissionId INT = (
    SELECT permission_id FROM dbo.pms_permissions WHERE permission_code = @newCode
);

IF @newPermissionId IS NULL
BEGIN
    RAISERROR(N'Missing permission %s — run HybridRbac_SeedPermissions.sql first.', 16, 1, @newCode);
    RETURN;
END

INSERT INTO dbo.pms_role_permissions (role_id, permission_id, granted, created_at)
SELECT DISTINCT rp.role_id, @newPermissionId, 1, SYSUTCDATETIME()
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
INNER JOIN @obsolete o ON o.permission_code = p.permission_code
WHERE rp.granted = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.pms_role_permissions x
      WHERE x.role_id = rp.role_id AND x.permission_id = @newPermissionId
  );

PRINT CONCAT(N'Granted ', @newCode, N' to roles that had obsolete date permissions: ', @@ROWCOUNT);

DECLARE @ids TABLE (permission_id INT NOT NULL PRIMARY KEY);

INSERT INTO @ids (permission_id)
SELECT p.permission_id
FROM dbo.pms_permissions p
INNER JOIN @obsolete o ON o.permission_code = p.permission_code;

DELETE rp
FROM dbo.pms_role_permissions rp
INNER JOIN @ids i ON i.permission_id = rp.permission_id;

PRINT CONCAT(N'Removed role_permission rows: ', @@ROWCOUNT);

DELETE p
FROM dbo.pms_permissions p
INNER JOIN @ids i ON i.permission_id = p.permission_id;

PRINT CONCAT(N'Removed permission rows: ', @@ROWCOUNT);
