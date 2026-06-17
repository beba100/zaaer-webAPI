SET NOCOUNT ON;

/*
  Granular report permissions by property domain:
    - hall.reports.*     → hall properties only (API + nav)
    - hotel.reports.*    → hotel properties only
    - resort.reports.*   → resort properties only (same screens as hotel, separate role assignment)

  Parent codes (hall.reports / hotel.reports / resort.reports) grant all child reports.
  Run on Master DB after AddHotelReportsSetup.sql and AddPmsNavMenuPermissions.sql.
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
    -- Resort (mirrors hotel report set — assign to resort-only staff)
    (N'resort.reports', N'Resort: all reports', N'المنتجع: جميع التقارير', N'resort', N'reports', N'view', 1200),
    (N'resort.reports.daily_journal', N'Resort: daily journal report', N'المنتجع: تقرير اليومية', N'resort', N'reports', N'daily_journal', 1201),
    (N'resort.reports.cash_ledger', N'Resort: cash ledger report', N'المنتجع: كشف حساب النقدية', N'resort', N'reports', N'cash_ledger', 1202),
    (N'resort.reports.network_cash', N'Resort: network & cash payments', N'المنتجع: مدفوعات الشبكة والنقدي', N'resort', N'reports', N'network_cash', 1203),
    (N'resort.reports.bookings', N'Resort: bookings report', N'المنتجع: تقرير الحجوزات', N'resort', N'reports', N'bookings', 1204),
    (N'resort.reports.receipts', N'Resort: receipt vouchers', N'المنتجع: سندات القبض', N'resort', N'reports', N'receipts', 1205),
    (N'resort.reports.disbursements', N'Resort: payment vouchers', N'المنتجع: سندات الصرف', N'resort', N'reports', N'disbursements', 1206),
    (N'resort.reports.deposits', N'Resort: bank deposits', N'المنتجع: إيداعات بنك', N'resort', N'reports', N'deposits', 1207),
    (N'resort.reports.expenses', N'Resort: expenses report', N'المنتجع: المصروفات', N'resort', N'reports', N'expenses', 1208),
    (N'resort.reports.invoices', N'Resort: invoices report', N'المنتجع: الفواتير', N'resort', N'reports', N'invoices', 1209),
    (N'resort.reports.credit_notes', N'Resort: credit notes report', N'المنتجع: الإشعارات الدائنة', N'resort', N'reports', N'credit_notes', 1210),

    -- Hotel granular (hotel properties only)
    (N'hotel.reports.daily_journal', N'Hotel: daily journal report', N'الفندق: تقرير اليومية', N'hotel', N'reports', N'daily_journal', 1110),
    (N'hotel.reports.cash_ledger', N'Hotel: cash ledger report', N'الفندق: كشف حساب النقدية', N'hotel', N'reports', N'cash_ledger', 1111),
    (N'hotel.reports.network_cash', N'Hotel: network & cash payments', N'الفندق: مدفوعات الشبكة والنقدي', N'hotel', N'reports', N'network_cash', 1112),
    (N'hotel.reports.bookings', N'Hotel: bookings report', N'الفندق: تقرير الحجوزات', N'hotel', N'reports', N'bookings', 1113),
    (N'hotel.reports.receipts', N'Hotel: receipt vouchers', N'الفندق: سندات القبض', N'hotel', N'reports', N'receipts', 1114),
    (N'hotel.reports.disbursements', N'Hotel: payment vouchers', N'الفندق: سندات الصرف', N'hotel', N'reports', N'disbursements', 1115),
    (N'hotel.reports.deposits', N'Hotel: bank deposits', N'الفندق: إيداعات بنك', N'hotel', N'reports', N'deposits', 1116),
    (N'hotel.reports.expenses', N'Hotel: expenses report', N'الفندق: المصروفات', N'hotel', N'reports', N'expenses', 1117),
    (N'hotel.reports.invoices', N'Hotel: invoices report', N'الفندق: الفواتير', N'hotel', N'reports', N'invoices', 1118),
    (N'hotel.reports.credit_notes', N'Hotel: credit notes report', N'الفندق: الإشعارات الدائنة', N'hotel', N'reports', N'credit_notes', 1119),

    -- Hall granular (hall properties only)
    (N'hall.reports.daily_journal', N'Hall: daily journal report', N'القاعات: تقرير اليومية', N'hall', N'reports', N'daily_journal', 1010),
    (N'hall.reports.cash_ledger', N'Hall: cash ledger report', N'القاعات: كشف حساب النقدية', N'hall', N'reports', N'cash_ledger', 1011),
    (N'hall.reports.network_cash', N'Hall: network & cash payments', N'القاعات: مدفوعات الشبكة والنقدي', N'hall', N'reports', N'network_cash', 1012),
    (N'hall.reports.bookings', N'Hall: bookings report', N'القاعات: تقرير الحجوزات', N'hall', N'reports', N'bookings', 1013),
    (N'hall.reports.receipts', N'Hall: receipt vouchers', N'القاعات: سندات القبض', N'hall', N'reports', N'receipts', 1014),
    (N'hall.reports.disbursements', N'Hall: payment vouchers', N'القاعات: سندات الصرف', N'hall', N'reports', N'disbursements', 1015),
    (N'hall.reports.deposits', N'Hall: bank deposits', N'القاعات: إيداعات بنك', N'hall', N'reports', N'deposits', 1016),
    (N'hall.reports.expenses', N'Hall: expenses report', N'القاعات: المصروفات', N'hall', N'reports', N'expenses', 1017),
    (N'hall.reports.invoices', N'Hall: invoices report', N'القاعات: الفواتير', N'hall', N'reports', N'invoices', 1018),
    (N'hall.reports.credit_notes', N'Hall: credit notes report', N'القاعات: الإشعارات الدائنة', N'hall', N'reports', N'credit_notes', 1019),

    -- Resort nav menu (shown when property type = resort)
    (N'nav.menu.resort.reports', N'Menu: resort reports group', N'القائمة: تقارير المنتجع', N'nav_menu', N'resort', N'reports', 1250),
    (N'nav.menu.resort.report.daily_journal', N'Menu: resort daily journal', N'القائمة: تقرير اليومية (منتجع)', N'nav_menu', N'resort', N'report_daily_journal', 1251),
    (N'nav.menu.resort.report.cash_ledger', N'Menu: resort cash ledger', N'القائمة: كشف النقدية (منتجع)', N'nav_menu', N'resort', N'report_cash_ledger', 1252),
    (N'nav.menu.resort.report.network_cash', N'Menu: resort network & cash', N'القائمة: مدفوعات الشبكة (منتجع)', N'nav_menu', N'resort', N'report_network_cash', 1253),
    (N'nav.menu.resort.report.bookings', N'Menu: resort bookings report', N'القائمة: تقرير الحجوزات (منتجع)', N'nav_menu', N'resort', N'report_bookings', 1254),
    (N'nav.menu.resort.report.receipts', N'Menu: resort receipts', N'القائمة: سندات القبض (منتجع)', N'nav_menu', N'resort', N'report_receipts', 1255),
    (N'nav.menu.resort.report.disbursements', N'Menu: resort disbursements', N'القائمة: سندات الصرف (منتجع)', N'nav_menu', N'resort', N'report_disbursements', 1256),
    (N'nav.menu.resort.report.deposits', N'Menu: resort deposits', N'القائمة: إيداعات بنك (منتجع)', N'nav_menu', N'resort', N'report_deposits', 1257),
    (N'nav.menu.resort.report.expenses', N'Menu: resort expenses', N'القائمة: المصروفات (منتجع)', N'nav_menu', N'resort', N'report_expenses', 1258),
    (N'nav.menu.resort.report.invoices', N'Menu: resort invoices', N'القائمة: الفواتير (منتجع)', N'nav_menu', N'resort', N'report_invoices', 1259),
    (N'nav.menu.resort.report.credit_notes', N'Menu: resort credit notes', N'القائمة: الإشعارات الدائنة (منتجع)', N'nav_menu', N'resort', N'report_credit_notes', 1260);

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

DECLARE @permissionMergeCount INT = @@ROWCOUNT;
PRINT CONCAT(N'Permission catalog rows inserted/updated: ', @permissionMergeCount);

DECLARE @resortCatalogCount INT = (
    SELECT COUNT(*)
    FROM dbo.pms_permissions
    WHERE is_active = 1
      AND (permission_code = N'resort.reports' OR permission_code LIKE N'resort.reports.%')
);
PRINT CONCAT(N'Resort report codes in catalog (expect 11): ', @resortCatalogCount);

-- Mirror hotel.reports → resort.reports for roles that already have hotel report grants.
DECLARE @map TABLE (legacy_code NVARCHAR(150), new_code NVARCHAR(150));
INSERT INTO @map VALUES
    (N'hotel.reports', N'resort.reports'),
    (N'hotel.reports.daily_journal', N'resort.reports.daily_journal'),
    (N'hotel.reports.cash_ledger', N'resort.reports.cash_ledger'),
    (N'hotel.reports.network_cash', N'resort.reports.network_cash'),
    (N'hotel.reports.bookings', N'resort.reports.bookings'),
    (N'hotel.reports.receipts', N'resort.reports.receipts'),
    (N'hotel.reports.disbursements', N'resort.reports.disbursements'),
    (N'hotel.reports.deposits', N'resort.reports.deposits'),
    (N'hotel.reports.expenses', N'resort.reports.expenses'),
    (N'hotel.reports.invoices', N'resort.reports.invoices'),
    (N'hotel.reports.credit_notes', N'resort.reports.credit_notes'),
    (N'nav.menu.hotel.report.daily_journal', N'nav.menu.resort.report.daily_journal'),
    (N'nav.menu.hotel.report.cash_ledger', N'nav.menu.resort.report.cash_ledger'),
    (N'nav.menu.hotel.report.network_cash', N'nav.menu.resort.report.network_cash'),
    (N'nav.menu.hotel.report.bookings', N'nav.menu.resort.report.bookings'),
    (N'nav.menu.hotel.report.receipts', N'nav.menu.resort.report.receipts'),
    (N'nav.menu.hotel.report.disbursements', N'nav.menu.resort.report.disbursements'),
    (N'nav.menu.hotel.report.deposits', N'nav.menu.resort.report.deposits'),
    (N'nav.menu.hotel.report.expenses', N'nav.menu.resort.report.expenses'),
    (N'nav.menu.hotel.report.invoices', N'nav.menu.resort.report.invoices'),
    (N'nav.menu.hotel.report.credit_notes', N'nav.menu.resort.report.credit_notes');

;WITH resort_grants AS (
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
USING resort_grants AS source
ON target.role_id = source.role_id AND target.permission_id = source.permission_id
WHEN MATCHED AND target.granted = 0 THEN
    UPDATE SET granted = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, granted, created_at)
    VALUES (source.role_id, source.permission_id, 1, SYSUTCDATETIME());

DECLARE @mirroredFromHotel INT = @@ROWCOUNT;
PRINT CONCAT(N'Resort role grants mirrored from hotel/nav hotel grants: ', @mirroredFromHotel);

-- Fallback: same as AddHotelReportsSetup — grant resort reports to roles with room_board.view.
;WITH resort_anchor_grants AS (
    SELECT DISTINCT rp.role_id, resort_perm.permission_id
    FROM dbo.pms_role_permissions rp
    INNER JOIN dbo.pms_permissions anchor ON anchor.permission_id = rp.permission_id
    INNER JOIN dbo.pms_permissions resort_perm ON resort_perm.is_active = 1
        AND (
            resort_perm.permission_code = N'resort.reports'
            OR resort_perm.permission_code LIKE N'resort.reports.%'
            OR resort_perm.permission_code = N'nav.menu.resort.reports'
            OR resort_perm.permission_code LIKE N'nav.menu.resort.report.%'
        )
    WHERE anchor.permission_code = N'room_board.view'
      AND rp.granted = 1
      AND anchor.is_active = 1
)
MERGE dbo.pms_role_permissions AS target
USING resort_anchor_grants AS source
ON target.role_id = source.role_id AND target.permission_id = source.permission_id
WHEN MATCHED AND target.granted = 0 THEN
    UPDATE SET granted = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, granted, created_at)
    VALUES (source.role_id, source.permission_id, 1, SYSUTCDATETIME());

DECLARE @mirroredFromBoard INT = @@ROWCOUNT;
PRINT CONCAT(N'Resort role grants via room_board.view anchor: ', @mirroredFromBoard);

-- Update parent permission labels for clarity.
UPDATE dbo.pms_permissions
SET permission_name_en = N'Hotel: all reports',
    permission_name = N'Hotel: all reports',
    permission_name_ar = N'الفندق: جميع التقارير'
WHERE permission_code = N'hotel.reports';

UPDATE dbo.pms_permissions
SET permission_name_en = N'Hall: all reports',
    permission_name = N'Hall: all reports',
    permission_name_ar = N'القاعات: جميع التقارير'
WHERE permission_code = N'hall.reports';

PRINT N'Property-type report permissions ready.';
