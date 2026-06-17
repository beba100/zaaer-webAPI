/*
  Master DB — add finance.expense.document_date (تاريخ المصروف) under finance / expense group.
  Grants to roles that already have finance.expense.create or finance.expense.update.
  Safe to re-run.
*/
SET NOCOUNT ON;

DECLARE @code NVARCHAR(150) = N'finance.expense.document_date';

MERGE dbo.pms_permissions AS target
USING (
    SELECT
        @code AS permission_code,
        N'Show expense date' AS permission_name_en,
        N'تاريخ المصروف' AS permission_name_ar,
        N'finance' AS module_name,
        N'expense' AS submodule_name,
        N'document_date' AS action_name,
        674 AS sort_order
) AS source
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

DECLARE @permissionId INT = (
    SELECT permission_id FROM dbo.pms_permissions WHERE permission_code = @code
);

IF @permissionId IS NULL
BEGIN
    RAISERROR(N'Failed to upsert finance.expense.document_date.', 16, 1);
    RETURN;
END

INSERT INTO dbo.pms_role_permissions (role_id, permission_id, granted, created_at)
SELECT DISTINCT rp.role_id, @permissionId, 1, SYSUTCDATETIME()
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
WHERE rp.granted = 1
  AND p.permission_code IN (N'finance.expense.create', N'finance.expense.update')
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.pms_role_permissions x
      WHERE x.role_id = rp.role_id AND x.permission_id = @permissionId AND x.granted = 1
  );

PRINT CONCAT(N'finance.expense.document_date synced; role grants added: ', @@ROWCOUNT);
GO
