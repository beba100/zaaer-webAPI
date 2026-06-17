SET NOCOUNT ON;

/*
  Verify PMS permission scripts on MASTER database (read-only checks).

  Expected scripts (run order on Master):
    1. HybridRbac_MasterDB.sql + HybridRbac_SimplifyPmsSchema.sql (once, schema)
    2. HybridRbac_SeedPermissions.sql (base catalog)
    3. AddHallEventMasterSetup.sql (hall.*)
    4. AddHotelReportsSetup.sql (hotel.reports)
    5. AddPmsNavMenuPermissions.sql (nav.menu.*)
    6. AddPropertyTypeReportPermissions.sql (granular hotel/resort/hall reports)

  How to use:
    - Connect SSMS to MASTER DB
    - Open and execute this file
    - Read section "=== FINAL SUMMARY ==="
    - If any bundle = FAIL, re-run the listed script(s) and execute this verify again
*/

PRINT N'=== PMS Permissions Verify (Master DB) ===';
PRINT CONCAT(N'Database: ', DB_NAME());
PRINT CONCAT(N'Server time: ', CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126));
PRINT N'';

-- ---------------------------------------------------------------------------
-- 0) Schema sanity
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.pms_permissions', N'U') IS NULL
BEGIN
    RAISERROR(N'FAIL: table dbo.pms_permissions not found. Run HybridRbac_MasterDB.sql first.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.pms_role_permissions', N'U') IS NULL
BEGIN
    RAISERROR(N'FAIL: table dbo.pms_role_permissions not found. Run HybridRbac_MasterDB.sql first.', 16, 1);
    RETURN;
END;

PRINT N'[OK] RBAC tables exist (pms_permissions, pms_role_permissions)';
PRINT N'';

-- ---------------------------------------------------------------------------
-- 1) Expected permission codes (grouped by source script)
-- ---------------------------------------------------------------------------
DECLARE @expected TABLE (
    script_name NVARCHAR(120) NOT NULL,
    permission_code NVARCHAR(150) NOT NULL,
    PRIMARY KEY (script_name, permission_code)
);

-- Base (HybridRbac_SeedPermissions.sql) — sample anchors only
INSERT INTO @expected (script_name, permission_code) VALUES
    (N'HybridRbac_SeedPermissions.sql', N'room_board.view'),
    (N'HybridRbac_SeedPermissions.sql', N'rbac.users.manage'),
    (N'HybridRbac_SeedPermissions.sql', N'property.settings.view'),
    (N'HybridRbac_SeedPermissions.sql', N'finance.expense.view');

-- AddHallEventMasterSetup.sql
INSERT INTO @expected (script_name, permission_code) VALUES
    (N'AddHallEventMasterSetup.sql', N'hall.events.view'),
    (N'AddHallEventMasterSetup.sql', N'hall.events.manage'),
    (N'AddHallEventMasterSetup.sql', N'hall.function_sheet.edit'),
    (N'AddHallEventMasterSetup.sql', N'hall.finance.deposit'),
    (N'AddHallEventMasterSetup.sql', N'hall.reports');

-- AddHotelReportsSetup.sql
INSERT INTO @expected (script_name, permission_code) VALUES
    (N'AddHotelReportsSetup.sql', N'hotel.reports');

-- AddPmsNavMenuPermissions.sql — key nav.menu anchors
INSERT INTO @expected (script_name, permission_code) VALUES
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.board'),
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.hotel.reports'),
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.hotel.report.bookings'),
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.hall.reports'),
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.hall.report.bookings'),
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.resort.tickets.cashier'),
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.finance.expenses'),
    (N'AddPmsNavMenuPermissions.sql', N'nav.menu.system.users');

-- AddPropertyTypeReportPermissions.sql — full granular report + resort nav set
DECLARE @reportKeys TABLE (report_key NVARCHAR(40) NOT NULL PRIMARY KEY);
INSERT INTO @reportKeys VALUES
    (N'daily_journal'), (N'cash_ledger'), (N'network_cash'), (N'bookings'),
    (N'receipts'), (N'disbursements'), (N'deposits'), (N'expenses'),
    (N'invoices'), (N'credit_notes');

INSERT INTO @expected (script_name, permission_code)
SELECT N'AddPropertyTypeReportPermissions.sql', CONCAT(N'hall.reports.', rk.report_key)
FROM @reportKeys rk;

INSERT INTO @expected (script_name, permission_code)
SELECT N'AddPropertyTypeReportPermissions.sql', CONCAT(N'hotel.reports.', rk.report_key)
FROM @reportKeys rk;

INSERT INTO @expected (script_name, permission_code)
SELECT N'AddPropertyTypeReportPermissions.sql', CONCAT(N'resort.reports.', rk.report_key)
FROM @reportKeys rk;

INSERT INTO @expected (script_name, permission_code)
SELECT N'AddPropertyTypeReportPermissions.sql', CONCAT(N'nav.menu.resort.report.', rk.report_key)
FROM @reportKeys rk;

-- Parent codes (not covered by the loop above)
INSERT INTO @expected (script_name, permission_code) VALUES
    (N'AddPropertyTypeReportPermissions.sql', N'resort.reports'),
    (N'AddPropertyTypeReportPermissions.sql', N'nav.menu.resort.reports');

-- ---------------------------------------------------------------------------
-- 2) Per-script bundle status
-- ---------------------------------------------------------------------------
PRINT N'--- Bundle status (by script) ---';

SELECT
    e.script_name,
    COUNT(*) AS expected_count,
    SUM(CASE WHEN p.permission_id IS NOT NULL AND p.is_active = 1 THEN 1 ELSE 0 END) AS found_active_count,
    SUM(CASE WHEN p.permission_id IS NULL THEN 1 ELSE 0 END) AS missing_count,
    CASE
        WHEN SUM(CASE WHEN p.permission_id IS NULL OR p.is_active = 0 THEN 1 ELSE 0 END) = 0 THEN N'OK'
        ELSE N'FAIL'
    END AS bundle_status
FROM @expected e
LEFT JOIN dbo.pms_permissions p ON p.permission_code = e.permission_code
GROUP BY e.script_name
ORDER BY e.script_name;

-- ---------------------------------------------------------------------------
-- 3) Missing / inactive codes (action list)
-- ---------------------------------------------------------------------------
PRINT N'';
PRINT N'--- Missing or inactive permissions (fix by re-running script_name) ---';

SELECT
    e.script_name,
    e.permission_code,
    CASE
        WHEN p.permission_id IS NULL THEN N'MISSING'
        WHEN p.is_active = 0 THEN N'INACTIVE'
        ELSE N'OK'
    END AS issue
FROM @expected e
LEFT JOIN dbo.pms_permissions p ON p.permission_code = e.permission_code
WHERE p.permission_id IS NULL OR p.is_active = 0
ORDER BY e.script_name, e.permission_code;

-- ---------------------------------------------------------------------------
-- 4) Counts by module (quick overview)
-- ---------------------------------------------------------------------------
PRINT N'';
PRINT N'--- Permission counts by module ---';

SELECT
    module_name,
    COUNT(*) AS permission_count
FROM dbo.pms_permissions
WHERE is_active = 1
GROUP BY module_name
ORDER BY module_name;

PRINT N'';
PRINT N'--- nav.menu.* count (expect >= 60 after AddPmsNavMenuPermissions + resort nav) ---';
SELECT COUNT(*) AS nav_menu_permission_count
FROM dbo.pms_permissions
WHERE is_active = 1
  AND permission_code LIKE N'nav.menu.%';

PRINT N'';
PRINT N'--- Report permission counts (expect 10 children + 1 parent per domain) ---';
SELECT
    CASE
        WHEN permission_code = N'hall.reports' OR permission_code LIKE N'hall.reports.%' THEN N'hall'
        WHEN permission_code = N'hotel.reports' OR permission_code LIKE N'hotel.reports.%' THEN N'hotel'
        WHEN permission_code = N'resort.reports' OR permission_code LIKE N'resort.reports.%' THEN N'resort'
    END AS report_domain,
    COUNT(*) AS permission_count
FROM dbo.pms_permissions
WHERE is_active = 1
  AND (
        permission_code IN (N'hall.reports', N'hotel.reports', N'resort.reports')
        OR permission_code LIKE N'hall.reports.%'
        OR permission_code LIKE N'hotel.reports.%'
        OR permission_code LIKE N'resort.reports.%'
      )
GROUP BY
    CASE
        WHEN permission_code = N'hall.reports' OR permission_code LIKE N'hall.reports.%' THEN N'hall'
        WHEN permission_code = N'hotel.reports' OR permission_code LIKE N'hotel.reports.%' THEN N'hotel'
        WHEN permission_code = N'resort.reports' OR permission_code LIKE N'resort.reports.%' THEN N'resort'
    END
ORDER BY report_domain;

-- ---------------------------------------------------------------------------
-- 5) Role grants sanity (roles with room_board.view should have report/nav mirrors)
-- ---------------------------------------------------------------------------
PRINT N'';
PRINT N'--- Roles with room_board.view: grant coverage sample ---';

;WITH roles_with_board AS (
    SELECT DISTINCT r.role_id, r.role_name
    FROM dbo.pms_roles r
    INNER JOIN dbo.pms_role_permissions rp ON rp.role_id = r.role_id AND rp.granted = 1
    INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
    WHERE p.permission_code = N'room_board.view'
      AND p.is_active = 1
)
SELECT TOP (20)
    rwb.role_name,
    MAX(CASE WHEN p.permission_code = N'nav.menu.board' AND rp.granted = 1 THEN 1 ELSE 0 END) AS has_nav_menu_board,
    MAX(CASE WHEN p.permission_code = N'hall.reports' AND rp.granted = 1 THEN 1 ELSE 0 END) AS has_hall_reports,
    MAX(CASE WHEN p.permission_code = N'hotel.reports' AND rp.granted = 1 THEN 1 ELSE 0 END) AS has_hotel_reports,
    MAX(CASE WHEN p.permission_code = N'resort.reports' AND rp.granted = 1 THEN 1 ELSE 0 END) AS has_resort_reports,
    MAX(CASE WHEN p.permission_code = N'nav.menu.hotel.report.bookings' AND rp.granted = 1 THEN 1 ELSE 0 END) AS has_nav_hotel_bookings
FROM roles_with_board rwb
LEFT JOIN dbo.pms_role_permissions rp ON rp.role_id = rwb.role_id AND rp.granted = 1
LEFT JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id AND p.is_active = 1
GROUP BY rwb.role_name
ORDER BY rwb.role_name;

-- ---------------------------------------------------------------------------
-- 6) FINAL SUMMARY
-- ---------------------------------------------------------------------------
DECLARE @missingTotal INT;
DECLARE @expectedTotal INT;
DECLARE @navMenuCount INT;
DECLARE @resortReportsCount INT;
DECLARE @hotelReportsGranular INT;
DECLARE @hallReportsGranular INT;

SELECT @expectedTotal = COUNT(*) FROM @expected;

SELECT @missingTotal = COUNT(*)
FROM @expected e
LEFT JOIN dbo.pms_permissions p ON p.permission_code = e.permission_code AND p.is_active = 1
WHERE p.permission_id IS NULL;

SELECT @navMenuCount = COUNT(*)
FROM dbo.pms_permissions
WHERE is_active = 1 AND permission_code LIKE N'nav.menu.%';

SELECT @resortReportsCount = COUNT(*)
FROM dbo.pms_permissions
WHERE is_active = 1
  AND (permission_code = N'resort.reports' OR permission_code LIKE N'resort.reports.%');

SELECT @hotelReportsGranular = COUNT(*)
FROM dbo.pms_permissions
WHERE is_active = 1 AND permission_code LIKE N'hotel.reports.%';

SELECT @hallReportsGranular = COUNT(*)
FROM dbo.pms_permissions
WHERE is_active = 1 AND permission_code LIKE N'hall.reports.%';

PRINT N'';
PRINT N'=== FINAL SUMMARY ===';
PRINT CONCAT(N'Expected codes loaded in verify script: ', @expectedTotal);

IF @expectedTotal < 50
BEGIN
    PRINT N'STATUS: FAIL — verify script internal error (expected list incomplete).';
    PRINT N'Action: update VerifyPmsPermissionsOnMaster.sql and re-run.';
END
ELSE IF @missingTotal = 0
    AND @navMenuCount >= 60
    AND @resortReportsCount >= 11
    AND @hotelReportsGranular >= 10
    AND @hallReportsGranular >= 10
BEGIN
    PRINT N'STATUS: PASS — permission scripts look applied on Master DB.';
    PRINT CONCAT(N'  nav.menu.* count: ', @navMenuCount);
    PRINT CONCAT(N'  hall.reports.* count: ', @hallReportsGranular, N' (+ parent hall.reports)');
    PRINT CONCAT(N'  hotel.reports.* count: ', @hotelReportsGranular, N' (+ parent hotel.reports)');
    PRINT CONCAT(N'  resort.reports* count: ', @resortReportsCount);
END
ELSE
BEGIN
    PRINT N'STATUS: FAIL — one or more permission bundles are incomplete.';
    PRINT CONCAT(N'  Missing expected codes: ', @missingTotal);
    PRINT CONCAT(N'  nav.menu.* active count: ', @navMenuCount, N' (need >= 60)');
    PRINT CONCAT(N'  hall.reports.* active count: ', @hallReportsGranular, N' (need >= 10)');
    PRINT CONCAT(N'  hotel.reports.* active count: ', @hotelReportsGranular, N' (need >= 10)');
    PRINT CONCAT(N'  resort.reports* active count: ', @resortReportsCount, N' (need >= 11)');
    PRINT N'';
    PRINT N'Action: re-run failed script(s) from section "Missing or inactive permissions", then run this verify again.';
END;

PRINT N'=== End verify ===';
