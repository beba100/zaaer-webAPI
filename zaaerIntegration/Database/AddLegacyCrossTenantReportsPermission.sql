SET NOCOUNT ON;

/*
  Permission for legacy cross-tenant ReportsController endpoints (find-tenant, etc.).
  Run on Master DB.
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
    (N'admin.legacy_reports.view', N'View legacy cross-tenant reports', N'عرض التقارير القديمة متعددة الفنادق', N'admin', N'legacy_reports', N'view', 36);

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

PRINT CONCAT(N'Legacy cross-tenant reports permission ensured: ', @@ROWCOUNT);
