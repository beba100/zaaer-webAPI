SET NOCOUNT ON;

/*
  PMS sidebar (tree view) menu permissions — clear AR/EN labels for role assignment.
  Run on Master DB after HybridRbac_SeedPermissions.sql.

  Nav visibility uses nav.menu.* codes (see wwwroot/js/core/pms-rbac-nav.js).
  Legacy functional codes still work until roles are migrated.
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
    -- Home
    (N'nav.menu.board', N'Menu: room board / home screen', N'القائمة: شاشة الغرف', N'nav_menu', N'home', N'view', 10),

    -- Property & units
    (N'nav.menu.property', N'Menu: property & units group', N'القائمة: الوحدات والأسعار', N'nav_menu', N'property', N'view', 20),
    (N'nav.menu.property.settings', N'Menu: unit settings', N'القائمة: إعدادات الوحدات', N'nav_menu', N'property', N'settings', 21),
    (N'nav.menu.property.rates', N'Menu: unit rates', N'القائمة: أسعار الوحدات', N'nav_menu', N'property', N'rates', 22),

    -- Booking engine
    (N'nav.menu.booking_engine', N'Menu: booking website group', N'القائمة: الموقع الإلكتروني', N'nav_menu', N'booking_engine', N'view', 30),
    (N'nav.menu.booking_engine.settings', N'Menu: booking website settings', N'القائمة: إعدادات الموقع', N'nav_menu', N'booking_engine', N'settings', 31),
    (N'nav.menu.booking_engine.preview', N'Menu: preview booking website', N'القائمة: معاينة الموقع', N'nav_menu', N'booking_engine', N'preview', 32),

    -- POS
    (N'nav.menu.pos', N'Menu: point of sale group', N'القائمة: نقاط البيع', N'nav_menu', N'pos', N'view', 40),
    (N'nav.menu.pos.terminal', N'Menu: POS terminal', N'القائمة: نقطة البيع', N'nav_menu', N'pos', N'terminal', 41),
    (N'nav.menu.pos.orders', N'Menu: POS orders', N'القائمة: طلبات نقاط البيع', N'nav_menu', N'pos', N'orders', 42),
    (N'nav.menu.pos.settings', N'Menu: POS settings', N'القائمة: إعدادات نقاط البيع', N'nav_menu', N'pos', N'settings', 43),

    -- Resort tickets
    (N'nav.menu.resort.tickets', N'Menu: resort tickets group', N'القائمة: تذاكر المنتجع', N'nav_menu', N'resort_tickets', N'view', 50),
    (N'nav.menu.resort.tickets.cashier', N'Menu: ticket cashier', N'القائمة: كاشير التذاكر', N'nav_menu', N'resort_tickets', N'cashier', 51),
    (N'nav.menu.resort.tickets.scanner', N'Menu: ticket scanner', N'القائمة: ماسح التذاكر', N'nav_menu', N'resort_tickets', N'scanner', 52),
    (N'nav.menu.resort.tickets.gate', N'Menu: ticket gate', N'القائمة: بوابة التذاكر', N'nav_menu', N'resort_tickets', N'gate', 53),
    (N'nav.menu.resort.tickets.settings', N'Menu: ticket type settings', N'القائمة: إعدادات أنواع التذاكر', N'nav_menu', N'resort_tickets', N'settings', 54),
    (N'nav.menu.resort.tickets.finance', N'Menu: resort ticket finance group', N'القائمة: مالية التذاكر', N'nav_menu', N'resort_tickets', N'finance', 55),
    (N'nav.menu.resort.tickets.receipts', N'Menu: ticket receipts', N'القائمة: سندات التذاكر', N'nav_menu', N'resort_tickets', N'receipts', 56),
    (N'nav.menu.resort.tickets.invoices', N'Menu: ticket invoices', N'القائمة: فواتير التذاكر', N'nav_menu', N'resort_tickets', N'invoices', 57),

    -- Hall operations & reports
    (N'nav.menu.hall', N'Menu: hall operations group', N'القائمة: عمليات القاعات', N'nav_menu', N'hall', N'view', 60),
    (N'nav.menu.hall.operations', N'Menu: hall operations board', N'القائمة: لوحة القاعات', N'nav_menu', N'hall', N'operations', 61),
    (N'nav.menu.hall.reports', N'Menu: hall reports group', N'القائمة: تقارير القاعات', N'nav_menu', N'hall', N'reports', 62),
    (N'nav.menu.hall.report.daily_journal', N'Menu: hall daily journal report', N'القائمة: تقرير اليومية (قاعات)', N'nav_menu', N'hall', N'report_daily_journal', 63),
    (N'nav.menu.hall.report.cash_ledger', N'Menu: hall cash ledger report', N'القائمة: كشف حساب النقدية (قاعات)', N'nav_menu', N'hall', N'report_cash_ledger', 64),
    (N'nav.menu.hall.report.network_cash', N'Menu: hall network & cash payments', N'القائمة: مدفوعات الشبكة والنقدي (قاعات)', N'nav_menu', N'hall', N'report_network_cash', 65),
    (N'nav.menu.hall.report.bookings', N'Menu: hall bookings report', N'القائمة: تقرير الحجوزات (قاعات)', N'nav_menu', N'hall', N'report_bookings', 66),
    (N'nav.menu.hall.report.receipts', N'Menu: hall receipt vouchers', N'القائمة: سندات القبض (قاعات)', N'nav_menu', N'hall', N'report_receipts', 67),
    (N'nav.menu.hall.report.disbursements', N'Menu: hall payment vouchers', N'القائمة: سندات الصرف (قاعات)', N'nav_menu', N'hall', N'report_disbursements', 68),
    (N'nav.menu.hall.report.deposits', N'Menu: hall bank deposits', N'القائمة: إيداعات بنك (قاعات)', N'nav_menu', N'hall', N'report_deposits', 69),
    (N'nav.menu.hall.report.expenses', N'Menu: hall expenses', N'القائمة: المصروفات (قاعات)', N'nav_menu', N'hall', N'report_expenses', 70),
    (N'nav.menu.hall.report.invoices', N'Menu: hall invoices', N'القائمة: الفواتير (قاعات)', N'nav_menu', N'hall', N'report_invoices', 71),
    (N'nav.menu.hall.report.credit_notes', N'Menu: hall credit notes', N'القائمة: الإشعارات الدائنة (قاعات)', N'nav_menu', N'hall', N'report_credit_notes', 72),

    -- Hotel & resort reports
    (N'nav.menu.hotel.reports', N'Menu: hotel & resort reports group', N'القائمة: تقارير الفندق والمنتجع', N'nav_menu', N'hotel', N'reports', 80),
    (N'nav.menu.hotel.report.daily_journal', N'Menu: daily journal report', N'القائمة: تقرير اليومية', N'nav_menu', N'hotel', N'report_daily_journal', 81),
    (N'nav.menu.hotel.report.cash_ledger', N'Menu: cash ledger report', N'القائمة: كشف حساب النقدية', N'nav_menu', N'hotel', N'report_cash_ledger', 82),
    (N'nav.menu.hotel.report.network_cash', N'Menu: network & cash payments', N'القائمة: مدفوعات الشبكة والنقدي', N'nav_menu', N'hotel', N'report_network_cash', 83),
    (N'nav.menu.hotel.report.bookings', N'Menu: bookings report', N'القائمة: تقرير الحجوزات', N'nav_menu', N'hotel', N'report_bookings', 84),
    (N'nav.menu.hotel.report.receipts', N'Menu: receipt vouchers report', N'القائمة: سندات القبض', N'nav_menu', N'hotel', N'report_receipts', 85),
    (N'nav.menu.hotel.report.disbursements', N'Menu: payment vouchers report', N'القائمة: سندات الصرف', N'nav_menu', N'hotel', N'report_disbursements', 86),
    (N'nav.menu.hotel.report.deposits', N'Menu: bank deposits report', N'القائمة: إيداعات بنك', N'nav_menu', N'hotel', N'report_deposits', 87),
    (N'nav.menu.hotel.report.expenses', N'Menu: expenses report', N'القائمة: المصروفات', N'nav_menu', N'hotel', N'report_expenses', 88),
    (N'nav.menu.hotel.report.invoices', N'Menu: invoices report', N'القائمة: الفواتير', N'nav_menu', N'hotel', N'report_invoices', 89),
    (N'nav.menu.hotel.report.credit_notes', N'Menu: credit notes report', N'القائمة: الإشعارات الدائنة', N'nav_menu', N'hotel', N'report_credit_notes', 90),

    -- Finance (cash module)
    (N'nav.menu.finance', N'Menu: finance group', N'القائمة: المالية', N'nav_menu', N'finance', N'view', 100),
    (N'nav.menu.finance.expenses', N'Menu: hotel expenses', N'القائمة: المصروفات', N'nav_menu', N'finance', N'expenses', 101),
    (N'nav.menu.finance.deposits', N'Menu: bank deposits', N'القائمة: الإيداعات البنكية', N'nav_menu', N'finance', N'deposits', 102),

    -- Integrations
    (N'nav.menu.integrations', N'Menu: platform integrations group', N'القائمة: تكامل المنصات', N'nav_menu', N'integrations', N'view', 110),
    (N'nav.menu.integrations.ntmp', N'Menu: NTMP integration', N'القائمة: NTMP', N'nav_menu', N'integrations', N'ntmp', 111),
    (N'nav.menu.integrations.shomoos', N'Menu: Shomoos integration', N'القائمة: شموس', N'nav_menu', N'integrations', N'shomoos', 112),
    (N'nav.menu.integrations.zatca', N'Menu: ZATCA integration', N'القائمة: الزكاة والضريبة', N'nav_menu', N'integrations', N'zatca', 113),
    (N'nav.menu.integrations.balady', N'Menu: Balady report', N'القائمة: تقرير بلدي', N'nav_menu', N'integrations', N'balady', 114),
    (N'nav.menu.integrations.responses', N'Menu: integration responses log', N'القائمة: سجل الاستجابات', N'nav_menu', N'integrations', N'responses', 115),

    -- System settings
    (N'nav.menu.system', N'Menu: system settings group', N'القائمة: إعدادات النظام', N'nav_menu', N'system', N'view', 120),
    (N'nav.menu.system.users', N'Menu: users management', N'القائمة: المستخدمون', N'nav_menu', N'system', N'users', 121),
    (N'nav.menu.system.roles', N'Menu: roles management', N'القائمة: الأدوار', N'nav_menu', N'system', N'roles', 122),
    (N'nav.menu.system.permissions', N'Menu: permissions catalog', N'القائمة: الصلاحيات', N'nav_menu', N'system', N'permissions', 123),
    (N'nav.menu.system.numbering', N'Menu: numbering settings (admin)', N'القائمة: إعدادات الترقيم', N'nav_menu', N'system', N'numbering', 124);

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

-- Mirror legacy functional grants onto matching nav.menu.* codes.
DECLARE @map TABLE (legacy_code NVARCHAR(150), nav_code NVARCHAR(150));
INSERT INTO @map VALUES
    (N'room_board.view', N'nav.menu.board'),
    (N'property.settings.view', N'nav.menu.property.settings'),
    (N'property.rates.view', N'nav.menu.property.rates'),
    (N'booking_engine.settings.view', N'nav.menu.booking_engine.settings'),
    (N'booking_engine.settings.view', N'nav.menu.booking_engine.preview'),
    (N'pos.view', N'nav.menu.pos.terminal'),
    (N'pos.view', N'nav.menu.pos.orders'),
    (N'pos.settings.view', N'nav.menu.pos.settings'),
    (N'resort_tickets.view', N'nav.menu.resort.tickets.cashier'),
    (N'resort_tickets.validate', N'nav.menu.resort.tickets.scanner'),
    (N'resort_tickets.validate', N'nav.menu.resort.tickets.gate'),
    (N'resort_tickets.manage_types', N'nav.menu.resort.tickets.settings'),
    (N'resort_tickets.finance', N'nav.menu.resort.tickets.finance'),
    (N'resort_tickets.finance', N'nav.menu.resort.tickets.receipts'),
    (N'resort_tickets.finance', N'nav.menu.resort.tickets.invoices'),
    (N'hall.events.view', N'nav.menu.hall.operations'),
    (N'hall.reports', N'nav.menu.hall.report.daily_journal'),
    (N'hall.reports', N'nav.menu.hall.report.cash_ledger'),
    (N'hall.reports', N'nav.menu.hall.report.network_cash'),
    (N'hall.reports', N'nav.menu.hall.report.bookings'),
    (N'hall.reports', N'nav.menu.hall.report.receipts'),
    (N'hall.reports', N'nav.menu.hall.report.disbursements'),
    (N'hall.reports', N'nav.menu.hall.report.deposits'),
    (N'hall.reports', N'nav.menu.hall.report.expenses'),
    (N'hall.reports', N'nav.menu.hall.report.invoices'),
    (N'hall.reports', N'nav.menu.hall.report.credit_notes'),
    (N'hotel.reports', N'nav.menu.hotel.report.daily_journal'),
    (N'hotel.reports', N'nav.menu.hotel.report.cash_ledger'),
    (N'hotel.reports', N'nav.menu.hotel.report.network_cash'),
    (N'hotel.reports', N'nav.menu.hotel.report.bookings'),
    (N'hotel.reports', N'nav.menu.hotel.report.receipts'),
    (N'hotel.reports', N'nav.menu.hotel.report.disbursements'),
    (N'hotel.reports', N'nav.menu.hotel.report.deposits'),
    (N'hotel.reports', N'nav.menu.hotel.report.expenses'),
    (N'hotel.reports', N'nav.menu.hotel.report.invoices'),
    (N'hotel.reports', N'nav.menu.hotel.report.credit_notes'),
    (N'finance.expense.view', N'nav.menu.finance.expenses'),
    (N'finance.deposit.view', N'nav.menu.finance.deposits'),
    (N'integrations.view', N'nav.menu.integrations.ntmp'),
    (N'integrations.view', N'nav.menu.integrations.shomoos'),
    (N'integrations.view', N'nav.menu.integrations.zatca'),
    (N'integrations.view', N'nav.menu.integrations.responses'),
    (N'integrations.balady.view', N'nav.menu.integrations.balady'),
    (N'rbac.users.manage', N'nav.menu.system.users'),
    (N'rbac.roles.manage', N'nav.menu.system.roles'),
    (N'rbac.permissions.view', N'nav.menu.system.permissions'),
    (N'admin.numbering.manage', N'nav.menu.system.numbering');

;WITH nav_grants AS (
    SELECT DISTINCT rp.role_id, nav.permission_id
    FROM dbo.pms_role_permissions rp
    INNER JOIN dbo.pms_permissions legacy ON legacy.permission_id = rp.permission_id
    INNER JOIN @map m ON m.legacy_code = legacy.permission_code
    INNER JOIN dbo.pms_permissions nav ON nav.permission_code = m.nav_code
    WHERE rp.granted = 1
      AND legacy.is_active = 1
      AND nav.is_active = 1
)
MERGE dbo.pms_role_permissions AS target
USING nav_grants AS source
ON target.role_id = source.role_id AND target.permission_id = source.permission_id
WHEN MATCHED AND target.granted = 0 THEN
    UPDATE SET granted = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, granted, created_at)
    VALUES (source.role_id, source.permission_id, 1, SYSUTCDATETIME());

PRINT CONCAT(N'Nav menu permissions mirrored from legacy grants: ', @@ROWCOUNT);
