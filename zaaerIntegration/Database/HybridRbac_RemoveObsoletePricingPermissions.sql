/*
  Remove obsolete reservations.pricing_* permissions (pricing submodule cleanup).
  Keeps only:
    - reservations.pricing_edit_after_checkin
    - reservations.pricing_below_minimum

  Run on Master DB after deploying UI/API changes.
  Review role grants before/after; re-grant the two kept permissions on roles that had any pricing grant.
*/

SET NOCOUNT ON;

DECLARE @obsolete TABLE (permission_code NVARCHAR(150) NOT NULL PRIMARY KEY);

INSERT INTO @obsolete (permission_code)
VALUES
    (N'reservations.pricing_view'),
    (N'reservations.pricing_edit'),
    (N'reservations.pricing_edit_total_rent'),
    (N'reservations.pricing_edit_rate_type'),
    (N'reservations.pricing_bulk_edit');

DECLARE @ids TABLE (permission_id INT NOT NULL PRIMARY KEY);

INSERT INTO @ids (permission_id)
SELECT p.permission_id
FROM dbo.pms_permissions p
INNER JOIN @obsolete o ON o.permission_code = p.permission_code;

IF NOT EXISTS (SELECT 1 FROM @ids)
BEGIN
    PRINT N'No obsolete pricing permissions found — nothing to delete.';
    RETURN;
END

DELETE rp
FROM dbo.pms_role_permissions rp
INNER JOIN @ids i ON i.permission_id = rp.permission_id;

PRINT CONCAT(N'Removed role_permission rows: ', @@ROWCOUNT);

DELETE p
FROM dbo.pms_permissions p
INNER JOIN @ids i ON i.permission_id = p.permission_id;

PRINT CONCAT(N'Removed permission rows: ', @@ROWCOUNT);

PRINT N'Kept: reservations.pricing_edit_after_checkin, reservations.pricing_below_minimum';
