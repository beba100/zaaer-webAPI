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

INSERT INTO @p VALUES
    (N'hall.events.view', N'View hall events', N'عرض فعاليات القاعات', N'hall', N'events', N'view', 1000),
    (N'hall.events.manage', N'Manage hall events', N'إدارة فعاليات القاعات', N'hall', N'events', N'manage', 1001),
    (N'hall.function_sheet.edit', N'Edit function sheets', N'تعديل ورقة التشغيل', N'hall', N'function_sheet', N'edit', 1002),
    (N'hall.finance.deposit', N'Record hall deposits', N'تسجيل عربون القاعة', N'hall', N'finance', N'deposit', 1003),
    (N'hall.reports', N'View hall reports', N'عرض تقارير القاعات', N'hall', N'reports', N'view', 1004);

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

-- Grant hall permissions to roles that can view the room board (same operators who run PMS daily).
DECLARE @anchorCode NVARCHAR(150) = N'room_board.view';

;WITH hall_role_grants AS (
    SELECT DISTINCT rp.role_id, hp.permission_id
    FROM dbo.pms_role_permissions rp
    INNER JOIN dbo.pms_permissions anchor ON anchor.permission_id = rp.permission_id
    INNER JOIN dbo.pms_permissions hp ON hp.permission_code IN (
        N'hall.events.view',
        N'hall.events.manage',
        N'hall.function_sheet.edit',
        N'hall.finance.deposit',
        N'hall.reports'
    )
    WHERE anchor.permission_code = @anchorCode
      AND rp.granted = 1
      AND hp.is_active = 1
)
MERGE dbo.pms_role_permissions AS target
USING hall_role_grants AS source
ON target.role_id = source.role_id AND target.permission_id = source.permission_id
WHEN MATCHED AND target.granted = 0 THEN
    UPDATE SET granted = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, granted, created_at)
    VALUES (source.role_id, source.permission_id, 1, SYSUTCDATETIME());

PRINT CONCAT(N'Hall permissions granted to room_board.view roles: ', @@ROWCOUNT);
