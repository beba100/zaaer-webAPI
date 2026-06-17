SET NOCOUNT ON;

/*
  Grant hall-only permissions to the Hall Employee role on Master DB.
  Safe to re-run (idempotent).

  On db32357_MasterDB the role may not exist yet — set @CreateIfMissing = 1 (default).

  Optional overrides at top:
    @RoleId          — use exact role_id if you know it
    @RoleName         — display name (default Hall Employee)
    @RoleCode         — stable code (default hall_employee)
    @CreateIfMissing  — create role when missing
    @RevokeHotelResortReports — remove hotel/resort report grants from this role
*/

DECLARE @RoleId INT = NULL;              -- e.g. 5 — leave NULL to resolve by name/code
DECLARE @RoleName NVARCHAR(150) = N'Hall Employee';
DECLARE @RoleNameAr NVARCHAR(150) = N'موظف القاعات';
DECLARE @RoleCode NVARCHAR(100) = N'hall_employee';
DECLARE @CreateIfMissing BIT = 1;
DECLARE @RevokeHotelResortReports BIT = 1;

IF @RoleId IS NULL
BEGIN
    SELECT @RoleId = role_id
    FROM dbo.pms_roles
    WHERE is_active = 1
      AND (
            role_code = @RoleCode
            OR role_name = @RoleName
            OR role_name_en = @RoleName
            OR role_name_ar = @RoleNameAr
            OR role_name LIKE N'%Hall%Employee%'
            OR role_name_en LIKE N'%Hall%Employee%'
            OR role_name_ar LIKE N'%قاع%'
          );
END;

IF @RoleId IS NULL AND @CreateIfMissing = 1
BEGIN
    INSERT INTO dbo.pms_roles (
        role_name,
        role_name_en,
        role_name_ar,
        role_description,
        role_code,
        is_active,
        created_at
    )
    VALUES (
        @RoleName,
        @RoleName,
        @RoleNameAr,
        N'Hall operations and hall reports only',
        @RoleCode,
        1,
        SYSUTCDATETIME()
    );

    SET @RoleId = SCOPE_IDENTITY();
    PRINT CONCAT(N'Created role: ', @RoleName, N' (role_id=', @RoleId, N', role_code=', @RoleCode, N')');
END;

IF @RoleId IS NULL
BEGIN
    PRINT N'Available active roles on this Master DB:';
    SELECT role_id, role_name, role_name_en, role_name_ar, role_code
    FROM dbo.pms_roles
    WHERE is_active = 1
    ORDER BY role_name;

    RAISERROR(
        N'Hall Employee role not found. Set @RoleId, fix @RoleName/@RoleCode, or @CreateIfMissing = 1.',
        16,
        1
    );
    RETURN;
END;

DECLARE @ResolvedRoleName NVARCHAR(150);
SELECT @ResolvedRoleName = role_name
FROM dbo.pms_roles
WHERE role_id = @RoleId;

PRINT CONCAT(N'Target role_id=', @RoleId, N' name=', ISNULL(@ResolvedRoleName, N''));

DECLARE @grantCodes TABLE (permission_code NVARCHAR(150) NOT NULL PRIMARY KEY);
INSERT INTO @grantCodes (permission_code) VALUES
    (N'room_board.view'),
    (N'hall.events.view'),
    (N'hall.reports'),
    (N'nav.menu.board'),
    (N'nav.menu.hall.operations'),
    (N'nav.menu.hall.reports'),
    (N'nav.menu.hall.report.daily_journal'),
    (N'nav.menu.hall.report.cash_ledger'),
    (N'nav.menu.hall.report.network_cash'),
    (N'nav.menu.hall.report.bookings'),
    (N'nav.menu.hall.report.receipts'),
    (N'nav.menu.hall.report.disbursements'),
    (N'nav.menu.hall.report.deposits'),
    (N'nav.menu.hall.report.expenses'),
    (N'nav.menu.hall.report.invoices'),
    (N'nav.menu.hall.report.credit_notes');

;WITH hall_grants AS (
    SELECT @RoleId AS role_id, p.permission_id
    FROM dbo.pms_permissions p
    INNER JOIN @grantCodes g ON g.permission_code = p.permission_code
    WHERE p.is_active = 1
)
MERGE dbo.pms_role_permissions AS target
USING hall_grants AS source
ON target.role_id = source.role_id AND target.permission_id = source.permission_id
WHEN MATCHED AND target.granted = 0 THEN
    UPDATE SET granted = 1
WHEN NOT MATCHED BY TARGET THEN
    INSERT (role_id, permission_id, granted, created_at)
    VALUES (source.role_id, source.permission_id, 1, SYSUTCDATETIME());

PRINT CONCAT(N'Hall Employee grants applied: ', @@ROWCOUNT);

SELECT g.permission_code AS missing_in_catalog
FROM @grantCodes g
LEFT JOIN dbo.pms_permissions p ON p.permission_code = g.permission_code AND p.is_active = 1
WHERE p.permission_id IS NULL;

IF @RevokeHotelResortReports = 1
BEGIN
    UPDATE rp
    SET granted = 0
    FROM dbo.pms_role_permissions rp
    INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
    WHERE rp.role_id = @RoleId
      AND rp.granted = 1
      AND (
            p.permission_code = N'hotel.reports'
            OR p.permission_code LIKE N'hotel.reports.%'
            OR p.permission_code = N'resort.reports'
            OR p.permission_code LIKE N'resort.reports.%'
            OR p.permission_code LIKE N'nav.menu.hotel.report.%'
            OR p.permission_code LIKE N'nav.menu.resort.report.%'
            OR p.permission_code = N'nav.menu.hotel.reports'
            OR p.permission_code = N'nav.menu.resort.reports'
          );

    PRINT CONCAT(N'Hotel/resort report grants revoked: ', @@ROWCOUNT);
END;

PRINT N'--- Effective hall permissions ---';
SELECT p.permission_code, p.permission_name_ar, rp.granted
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions p ON p.permission_id = rp.permission_id
WHERE rp.role_id = @RoleId
  AND rp.granted = 1
  AND (
        p.permission_code LIKE N'hall.%'
        OR p.permission_code LIKE N'nav.menu.hall%'
        OR p.permission_code IN (N'nav.menu.board', N'room_board.view')
      )
ORDER BY p.permission_code;

PRINT N'Done.';
