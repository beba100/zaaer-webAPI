/*
  Grant payments.refund_voucher.cancel to roles that could cancel disbursements via payments.refund (legacy).
  Run after HybridRbac_SeedPermissions.sql
*/

SET NOCOUNT ON;

DECLARE @newCode NVARCHAR(150) = N'payments.refund_voucher.cancel';
DECLARE @legacyRefund NVARCHAR(150) = N'payments.refund';

DECLARE @newPermissionId INT = (
    SELECT permission_id FROM dbo.pms_permissions WHERE permission_code = @newCode AND is_active = 1
);

IF @newPermissionId IS NULL
BEGIN
    RAISERROR(N'Missing %s — run HybridRbac_SeedPermissions.sql first.', 16, 1, @newCode);
    RETURN;
END

INSERT INTO dbo.pms_role_permissions (role_id, permission_id, granted, created_at)
SELECT DISTINCT rp.role_id, @newPermissionId, 1, SYSUTCDATETIME()
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
WHERE p.permission_code = @legacyRefund
  AND rp.granted = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.pms_role_permissions x
      WHERE x.role_id = rp.role_id AND x.permission_id = @newPermissionId AND x.granted = 1
  );

PRINT CONCAT(N'Granted ', @newCode, N' to roles that had ', @legacyRefund, N': ', @@ROWCOUNT);
