/*
    Master DB — add cancel permissions for promissory notes and invoices (credit note).
    Safe to re-run (MERGE by permission_code).
*/
SET NOCOUNT ON;

DECLARE @p TABLE (
    permission_code NVARCHAR(150) NOT NULL PRIMARY KEY,
    permission_name_en NVARCHAR(200) NOT NULL,
    permission_name_ar NVARCHAR(200) NOT NULL,
    module_name NVARCHAR(80) NOT NULL,
    submodule_name NVARCHAR(80) NOT NULL,
    action_name NVARCHAR(80) NOT NULL,
    sort_order INT NOT NULL
);

INSERT INTO @p (permission_code, permission_name_en, permission_name_ar, module_name, submodule_name, action_name, sort_order)
VALUES
    (N'finance.invoice.cancel', N'Cancel invoice (credit note)', N'إلغاء الفاتورة (إشعار دائن)', N'finance', N'invoice', N'cancel', 655),
    (N'finance.promissory_note.cancel', N'Cancel promissory note', N'إلغاء سند لأمر', N'finance', N'promissory_note', N'cancel', 668);

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

PRINT CONCAT(N'Finance cancel permissions synced: ', @@ROWCOUNT);
GO
