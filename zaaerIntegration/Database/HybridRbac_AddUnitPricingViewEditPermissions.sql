/*
  Unit night pricing — view-only vs edit/apply permissions (reservation detail popup).
  Run on Master DB after HybridRbac_SeedPermissions.sql

  reservations.pricing_view — open unit night pricing grid (read-only; no apply)
  reservations.pricing_edit  — edit night rates in grid and apply to reservation

  Grant manually per role in RBAC admin (no auto-grant).
*/

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

INSERT INTO @p (permission_code, permission_name_en, permission_name_ar, module_name, submodule_name, action_name, sort_order)
VALUES
    (N'reservations.pricing_view', N'View unit night prices (read-only)', N'عرض أسعار الليلة للوحدات (قراءة فقط — بدون تعديل)', N'reservations', N'pricing', N'pricing_view', 348),
    (N'reservations.pricing_edit', N'Edit and apply unit night prices', N'تعديل وتطبيق أسعار الليلة للوحدات', N'reservations', N'pricing', N'pricing_edit', 352);

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

PRINT CONCAT(N'Unit pricing view/edit permissions synced: ', @@ROWCOUNT);
