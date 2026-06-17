-- =============================================================================
-- Run on: MASTER database (MasterDb — e.g. db54638_Master)
-- NOT on tenant / resort property databases
-- After merge: assign new permissions to roles in PMS → Roles
-- =============================================================================
SET NOCOUNT ON;

MERGE dbo.pms_permissions AS target
USING (VALUES
    (N'resort_tickets.pay_now', N'Use pay-now switch on ticket cashier', N'استخدام سويتش الدفع الآن في كاشير التذاكر', N'resort', N'tickets', N'pay_now', 907),
    (N'resort_tickets.service_date', N'Edit service date on ticket cashier', N'تعديل تاريخ الخدمة في كاشير التذاكر', N'resort', N'tickets', N'service_date', 908),
    (N'resort_tickets.manage_settings', N'Manage resort ticket business settings', N'إدارة إعدادات أوقات التذاكر', N'resort', N'tickets', N'manage_settings', 909),
    (N'resort_tickets.finance', N'Manage resort ticket invoices and ZATCA', N'إدارة فواتير تذاكر المنتجع والزكاة', N'resort', N'tickets', N'finance', 910)
) AS source(permission_code, permission_name_en, permission_name_ar, module_name, submodule_name, action_name, sort_order)
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

PRINT N'Resort ticket permissions synced on master database.';
