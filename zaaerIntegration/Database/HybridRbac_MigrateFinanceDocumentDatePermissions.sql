/*
  Finance document_date permissions:
  - finance.document_date → receipt + refund_voucher (legacy split)
  - finance.disbursement_voucher.document_date → finance.refund_voucher.document_date (same card: سندات الاسترداد)

  Run on Master DB after HybridRbac_SeedPermissions.sql
*/

SET NOCOUNT ON;

DECLARE @legacyGlobal NVARCHAR(150) = N'finance.document_date';
DECLARE @legacyDisbursement NVARCHAR(150) = N'finance.disbursement_voucher.document_date';
DECLARE @receiptCode NVARCHAR(150) = N'finance.receipt_voucher.document_date';
DECLARE @refundCode NVARCHAR(150) = N'finance.refund_voucher.document_date';

DECLARE @receiptPermissionId INT = (
    SELECT permission_id FROM dbo.pms_permissions WHERE permission_code = @receiptCode
);
DECLARE @refundPermissionId INT = (
    SELECT permission_id FROM dbo.pms_permissions WHERE permission_code = @refundCode
);

IF @receiptPermissionId IS NULL OR @refundPermissionId IS NULL
BEGIN
    RAISERROR(N'Missing receipt/refund document_date permissions — run HybridRbac_SeedPermissions.sql first.', 16, 1);
    RETURN;
END

DECLARE @sources TABLE (permission_code NVARCHAR(150) NOT NULL PRIMARY KEY);
INSERT INTO @sources (permission_code) VALUES (@legacyGlobal), (@legacyDisbursement);

INSERT INTO dbo.pms_role_permissions (role_id, permission_id, granted, created_at)
SELECT DISTINCT rp.role_id, @receiptPermissionId, 1, SYSUTCDATETIME()
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
INNER JOIN @sources s ON s.permission_code = p.permission_code
WHERE rp.granted = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.pms_role_permissions x
      WHERE x.role_id = rp.role_id AND x.permission_id = @receiptPermissionId AND x.granted = 1
  );

INSERT INTO dbo.pms_role_permissions (role_id, permission_id, granted, created_at)
SELECT DISTINCT rp.role_id, @refundPermissionId, 1, SYSUTCDATETIME()
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
INNER JOIN @sources s ON s.permission_code = p.permission_code
WHERE rp.granted = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.pms_role_permissions x
      WHERE x.role_id = rp.role_id AND x.permission_id = @refundPermissionId AND x.granted = 1
  );

UPDATE dbo.pms_permissions
SET is_active = 0
WHERE permission_code IN (@legacyGlobal, @legacyDisbursement);

PRINT N'Finance document_date permissions migrated; obsolete codes deactivated.';
