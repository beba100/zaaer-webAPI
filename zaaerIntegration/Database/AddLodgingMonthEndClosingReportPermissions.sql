SET NOCOUNT ON;

/*
  Lodging report permissions: month-end closing report (hotel + resort).
  Run on Master DB after AddLodgingUnitTransfersReportPermissions.sql.
*/

DECLARE @p TABLE (
    permission_code NVARCHAR(150),
    permission_name_en NVARCHAR(200),
    permission_name_ar NVARCHAR(200),
    module_name NVARCHAR(80),
    submodule_name NVARCHAR(80),
    action_name NVARCHAR(80),
    sort_order INT
);

INSERT INTO @p VALUES
    (N'hotel.reports.month_end_closing', N'Hotel: month-end closing report', N'الفندق: تقرير تقفيل الشهر', N'hotel', N'reports', N'month_end_closing', 1123),
    (N'resort.reports.month_end_closing', N'Resort: month-end closing report', N'المنتجع: تقرير تقفيل الشهر', N'resort', N'reports', N'month_end_closing', 1214),
    (N'nav.menu.hotel.report.month_end_closing', N'Menu: month-end closing report', N'القائمة: تقفيل الشهر', N'nav_menu', N'hotel', N'report_month_end_closing', 94),
    (N'nav.menu.resort.report.month_end_closing', N'Menu: resort month-end closing report', N'القائمة: تقفيل الشهر (منتجع)', N'nav_menu', N'resort', N'report_month_end_closing', 1264);

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

PRINT CONCAT(N'Permission catalog rows inserted/updated: ', @@ROWCOUNT);

DECLARE @map TABLE (legacy_code NVARCHAR(150), new_code NVARCHAR(150));
INSERT INTO @map VALUES
    (N'hotel.reports.bookings', N'hotel.reports.month_end_closing'),
    (N'hotel.reports', N'hotel.reports.month_end_closing'),
    (N'resort.reports.bookings', N'resort.reports.month_end_closing'),
    (N'resort.reports', N'resort.reports.month_end_closing'),
    (N'nav.menu.hotel.report.bookings', N'nav.menu.hotel.report.month_end_closing'),
    (N'nav.menu.resort.report.bookings', N'nav.menu.resort.report.month_end_closing'),
    (N'hotel.reports.month_end_closing', N'resort.reports.month_end_closing'),
    (N'nav.menu.hotel.report.month_end_closing', N'nav.menu.resort.report.month_end_closing');

;WITH grants AS (
    SELECT DISTINCT rp.role_id, newp.permission_id
    FROM dbo.pms_role_permissions rp
    INNER JOIN dbo.pms_permissions legacy ON legacy.permission_id = rp.permission_id
    INNER JOIN @map m ON m.legacy_code = legacy.permission_code
    INNER JOIN dbo.pms_permissions newp ON newp.permission_code = m.new_code
    WHERE rp.granted = 1
      AND legacy.is_active = 1
      AND newp.is_active = 1
)
MERGE dbo.pms_role_permissions AS target
USING grants AS source
ON target.role_id = source.role_id AND target.permission_id = source.permission_id
WHEN MATCHED AND target.granted = 0 THEN
    UPDATE SET granted = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, granted, created_at)
    VALUES (source.role_id, source.permission_id, 1, SYSUTCDATETIME());

PRINT CONCAT(N'Role grants mirrored for month-end closing report: ', @@ROWCOUNT);
PRINT N'Lodging month-end closing report permissions ensured.';
