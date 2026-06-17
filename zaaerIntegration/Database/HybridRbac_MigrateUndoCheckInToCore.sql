/*
  Move reservations.undo_check_in from stay → core (أساسي) for role-permissions UI.
  Run on Master DB after HybridRbac_SeedPermissions.sql (safe to re-run).
*/

SET NOCOUNT ON;

UPDATE dbo.pms_permissions
SET
    submodule_name = N'core',
    sort_order = 242,
    is_active = 1
WHERE permission_code = N'reservations.undo_check_in';

PRINT CONCAT(N'reservations.undo_check_in → core: ', @@ROWCOUNT, N' row(s)');
