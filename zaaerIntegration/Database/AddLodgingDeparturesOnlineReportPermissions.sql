SET NOCOUNT ON;

/*
  Lodging report permissions: departures + online bookings (hotel + resort).
  Run on Master DB after AddPropertyTypeReportPermissions.sql / AddPmsNavMenuPermissions.sql.
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
    (N'hotel.reports.departures', N'Hotel: departures report', N'الفندق: تقرير المغادرات', N'hotel', N'reports', N'departures', 1120),
    (N'hotel.reports.online_bookings', N'Hotel: online bookings report', N'الفندق: تقرير الحجوزات الأونلاين', N'hotel', N'reports', N'online_bookings', 1121),
    (N'resort.reports.departures', N'Resort: departures report', N'المنتجع: تقرير المغادرات', N'resort', N'reports', N'departures', 1211),
    (N'resort.reports.online_bookings', N'Resort: online bookings report', N'المنتجع: تقرير الحجوزات الأونلاين', N'resort', N'reports', N'online_bookings', 1212),
    (N'nav.menu.hotel.report.departures', N'Menu: departures report', N'القائمة: تقرير المغادرات', N'nav_menu', N'hotel', N'report_departures', 91),
    (N'nav.menu.hotel.report.online_bookings', N'Menu: online bookings report', N'القائمة: تقرير الحجوزات الأونلاين', N'nav_menu', N'hotel', N'report_online_bookings', 92),
    (N'nav.menu.resort.report.departures', N'Menu: resort departures report', N'القائمة: تقرير المغادرات (منتجع)', N'nav_menu', N'resort', N'report_departures', 1261),
    (N'nav.menu.resort.report.online_bookings', N'Menu: resort online bookings report', N'القائمة: تقرير الحجوزات الأونلاين (منتجع)', N'nav_menu', N'resort', N'report_online_bookings', 1262);

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
    (N'hotel.reports.bookings', N'hotel.reports.departures'),
    (N'hotel.reports.bookings', N'hotel.reports.online_bookings'),
    (N'hotel.reports', N'hotel.reports.departures'),
    (N'hotel.reports', N'hotel.reports.online_bookings'),
    (N'resort.reports.bookings', N'resort.reports.departures'),
    (N'resort.reports.bookings', N'resort.reports.online_bookings'),
    (N'resort.reports', N'resort.reports.departures'),
    (N'resort.reports', N'resort.reports.online_bookings'),
    (N'nav.menu.hotel.report.bookings', N'nav.menu.hotel.report.departures'),
    (N'nav.menu.hotel.report.bookings', N'nav.menu.hotel.report.online_bookings'),
    (N'nav.menu.resort.report.bookings', N'nav.menu.resort.report.departures'),
    (N'nav.menu.resort.report.bookings', N'nav.menu.resort.report.online_bookings'),
    (N'hotel.reports.departures', N'resort.reports.departures'),
    (N'hotel.reports.online_bookings', N'resort.reports.online_bookings'),
    (N'nav.menu.hotel.report.departures', N'nav.menu.resort.report.departures'),
    (N'nav.menu.hotel.report.online_bookings', N'nav.menu.resort.report.online_bookings');

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

PRINT CONCAT(N'Role grants mirrored for departures/online bookings: ', @@ROWCOUNT);
PRINT N'Lodging departures + online bookings report permissions ensured.';
