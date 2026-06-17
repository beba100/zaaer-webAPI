SET NOCOUNT ON;

MERGE dbo.DocumentTypes AS target
USING (VALUES
    (N'resort_ticket_type',  N'RSTT', 4, N'hotel', 0, 1, N'-'),
    (N'resort_ticket_order', N'RSTO', 4, N'hotel', 0, 1, N'-'),
    (N'resort_ticket',       N'TCK',  6, N'hotel', 0, 1, N'-')
) AS source(doc_code, prefix, padding, scope_level, include_hotel_in_number, uses_global_zaaer_id, separator)
ON target.doc_code = source.doc_code
WHEN MATCHED THEN
    UPDATE SET
        prefix = source.prefix,
        padding = source.padding,
        scope_level = source.scope_level,
        include_hotel_in_number = source.include_hotel_in_number,
        uses_global_zaaer_id = source.uses_global_zaaer_id,
        separator = source.separator,
        is_active = 1,
        updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (doc_code, prefix, padding, scope_level, include_hotel_in_number, uses_global_zaaer_id, separator)
    VALUES (source.doc_code, source.prefix, source.padding, source.scope_level, source.include_hotel_in_number, source.uses_global_zaaer_id, source.separator);

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
    (N'resort_tickets.view', N'View resort tickets', N'عرض تذاكر المنتجع', N'resort', N'tickets', N'view', 900),
    (N'resort_tickets.issue', N'Issue resort tickets', N'إصدار تذاكر المنتجع', N'resort', N'tickets', N'issue', 901),
    (N'resort_tickets.cancel', N'Cancel resort tickets', N'إلغاء تذاكر المنتجع', N'resort', N'tickets', N'cancel', 902),
    (N'resort_tickets.print', N'Print resort tickets', N'طباعة تذاكر المنتجع', N'resort', N'tickets', N'print', 903),
    (N'resort_tickets.refund', N'Refund resort tickets', N'استرداد تذاكر المنتجع', N'resort', N'tickets', N'refund', 904),
    (N'resort_tickets.manage_types', N'Manage resort ticket types', N'إدارة أنواع تذاكر المنتجع', N'resort', N'tickets', N'manage_types', 905),
    (N'resort_tickets.validate', N'Validate resort tickets at gate', N'التحقق من التذاكر عند البوابة', N'resort', N'tickets', N'validate', 906);

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

PRINT N'Resort ticket numbering and permissions synced.';
