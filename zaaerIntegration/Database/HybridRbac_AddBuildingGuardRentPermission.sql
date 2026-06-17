/*
  Building guard rent checkbox on rent payment receipts.
  Run on Master DB after HybridRbac_SeedPermissions.sql

  payments.building_guard_rent — toggle «إيجار حارس العمارة» on rent receipt vouchers

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
    (N'payments.building_guard_rent', N'Building guard rent on rent receipts', N'إيجار حارس العمارة في سندات قبض الإيجار', N'finance', N'receipt_voucher', N'building_guard_rent', 628);

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

PRINT CONCAT(N'Building guard rent permission synced: ', @@ROWCOUNT);
