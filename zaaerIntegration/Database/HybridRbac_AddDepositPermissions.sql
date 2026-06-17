-- Add finance.deposit.* permissions for existing deployments (idempotent).
IF NOT EXISTS (SELECT 1 FROM permissions WHERE permission_code = N'finance.deposit.view')
BEGIN
    INSERT INTO permissions (permission_code, name_en, name_ar, module_name, submodule_name, action_name, sort_order)
    VALUES
        (N'finance.deposit.view', N'View bank deposits', N'عرض الإيداعات', N'finance', N'deposit', N'view', 675),
        (N'finance.deposit.create', N'Create bank deposit', N'إنشاء إيداع', N'finance', N'deposit', N'create', 676),
        (N'finance.deposit.update', N'Update bank deposit', N'تعديل إيداع', N'finance', N'deposit', N'update', 677),
        (N'finance.deposit.document_date', N'Show deposit date', N'تاريخ الإيداع', N'finance', N'deposit', N'document_date', 678);
END;
GO
