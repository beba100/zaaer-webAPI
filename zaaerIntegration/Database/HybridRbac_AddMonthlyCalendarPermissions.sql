/*
  Monthly rental calendar mode permissions (reservation detail — dates submodule).
  Run on Master DB after HybridRbac_SeedPermissions.sql

  reservations.monthly_calendar_thirty_day — 30-day month blocks (default)
  reservations.monthly_calendar_actual     — actual Gregorian calendar months

  Grant thirty_day to roles that can edit reservations (keeps current behaviour).
*/

SET NOCOUNT ON;

DECLARE @p TABLE (
    permission_code NVARCHAR(150),
    permission_name_en NVARCHAR(200),
    permission_name_ar NVARCHAR(200),
    module_name NVARCHAR(80),
    submodule_name NVARCHAR(80),
    action_name NVARCHAR(80),
    sort_order INT
);

INSERT INTO @p (permission_code, permission_name_en, permission_name_ar, module_name, submodule_name, action_name, sort_order)
VALUES
    (N'reservations.monthly_calendar_thirty_day', N'Monthly stay: 30-day months', N'الحجز الشهري: شهور 30 يوم', N'reservations', N'dates', N'monthly_calendar_thirty_day', 375),
    (N'reservations.monthly_calendar_actual', N'Monthly stay: actual calendar months', N'الحجز الشهري: شهور ميلادية فعلية', N'reservations', N'dates', N'monthly_calendar_actual', 380);

MERGE dbo.pms_permissions AS target
USING @p AS source
ON target.permission_code = source.permission_code
WHEN MATCHED THEN
    UPDATE SET
        permission_name = source.permission_name_en,
        permission_name_en = source.permission_name_en,
        permission_name_ar = source.permission_name_ar,
        module_name = source.module_name,
        submodule_name = source.submodule_name,
        action_name = source.action_name,
        sort_order = source.sort_order,
        is_active = 1
WHEN NOT MATCHED THEN
    INSERT (permission_code, permission_name, permission_name_en, permission_name_ar,
            module_name, submodule_name, action_name, sort_order, is_active, created_at)
    VALUES (source.permission_code, source.permission_name_en, source.permission_name_en, source.permission_name_ar,
            source.module_name, source.submodule_name, source.action_name, source.sort_order, 1, SYSUTCDATETIME());

PRINT CONCAT(N'Monthly calendar permissions synced: ', @@ROWCOUNT);

DECLARE @thirtyDayCode NVARCHAR(150) = N'reservations.monthly_calendar_thirty_day';
DECLARE @thirtyDayId INT = (
    SELECT permission_id FROM dbo.pms_permissions WHERE permission_code = @thirtyDayCode AND is_active = 1
);

IF @thirtyDayId IS NULL
BEGIN
    RAISERROR(N'Missing permission %s.', 16, 1, @thirtyDayCode);
    RETURN;
END

INSERT INTO dbo.pms_role_permissions (role_id, permission_id, granted, created_at)
SELECT DISTINCT rp.role_id, @thirtyDayId, 1, SYSUTCDATETIME()
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
WHERE rp.granted = 1
  AND p.permission_code IN (
      N'reservations.update',
      N'reservations.create',
      N'reservations.edit_stay_dates_after_checkin'
  )
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.pms_role_permissions x
      WHERE x.role_id = rp.role_id AND x.permission_id = @thirtyDayId
  );

PRINT CONCAT(N'Granted ', @thirtyDayCode, N' to roles with reservation edit/create: ', @@ROWCOUNT);
