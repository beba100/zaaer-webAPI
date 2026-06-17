SET NOCOUNT ON;

MERGE dbo.pms_permissions AS target
USING (VALUES
    (N'resort_tickets.validate', N'Validate resort tickets at gate', N'التحقق من التذاكر عند البوابة', N'resort', N'tickets', N'validate', 906)
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

PRINT N'Resort ticket validate permission synced.';
