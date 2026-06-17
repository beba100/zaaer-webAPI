SET NOCOUNT ON;

/*
  Lodging report permissions: unit transfers / room switches (hotel + resort).
  Run on Master DB after AddLodgingDeparturesOnlineReportPermissions.sql.
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
    (N'hotel.reports.unit_transfers', N'Hotel: unit transfers report', N'الفندق: تقرير التحويل بين الغرف', N'hotel', N'reports', N'unit_transfers', 1122),
    (N'resort.reports.unit_transfers', N'Resort: unit transfers report', N'المنتجع: تقرير التحويل بين الغرف', N'resort', N'reports', N'unit_transfers', 1213),
    (N'nav.menu.hotel.report.unit_transfers', N'Menu: unit transfers report', N'القائمة: التحويل بين الغرف', N'nav_menu', N'hotel', N'report_unit_transfers', 93),
    (N'nav.menu.resort.report.unit_transfers', N'Menu: resort unit transfers report', N'القائمة: التحويل بين الغرف (منتجع)', N'nav_menu', N'resort', N'report_unit_transfers', 1263);

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
    (N'hotel.reports.bookings', N'hotel.reports.unit_transfers'),
    (N'hotel.reports', N'hotel.reports.unit_transfers'),
    (N'resort.reports.bookings', N'resort.reports.unit_transfers'),
    (N'resort.reports', N'resort.reports.unit_transfers'),
    (N'nav.menu.hotel.report.bookings', N'nav.menu.hotel.report.unit_transfers'),
    (N'nav.menu.resort.report.bookings', N'nav.menu.resort.report.unit_transfers'),
    (N'hotel.reports.unit_transfers', N'resort.reports.unit_transfers'),
    (N'nav.menu.hotel.report.unit_transfers', N'nav.menu.resort.report.unit_transfers');

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

PRINT CONCAT(N'Role grants mirrored for unit transfers report: ', @@ROWCOUNT);
PRINT N'Lodging unit transfers report permissions ensured.';
