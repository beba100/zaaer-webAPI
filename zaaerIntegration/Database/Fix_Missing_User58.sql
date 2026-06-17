USE db29328; -- Master DB

-- 1. Ensure Tenant Exists
DECLARE @TenantId INT;
SELECT @TenantId = Id FROM Tenants WHERE Code = 'Dammam1';

IF @TenantId IS NULL
BEGIN
    INSERT INTO Tenants (Code, Name, ConnectionString, DatabaseName, IsActive, CreatedDate)
    VALUES ('Dammam1', N'الدمام 1', 'Server=.;Database=db29328;Trusted_Connection=True;', 'db29328', 1, GETDATE());
    
    SET @TenantId = SCOPE_IDENTITY();
    PRINT 'Created default tenant Dammam1';
END

-- 2. Ensure Role 'Admin' Exists
DECLARE @RoleId INT;
SELECT @RoleId = Id FROM Roles WHERE Code = 'Admin';

IF @RoleId IS NULL
BEGIN
    INSERT INTO Roles (Name, Code)
    VALUES (N'مدير النظام', 'Admin');
    
    SET @RoleId = SCOPE_IDENTITY();
    PRINT 'Created Admin role';
END

-- 3. Upsert Master User '58'
DECLARE @UserId INT;
SELECT @UserId = Id FROM MasterUsers WHERE EmployeeNumber = '58';

IF @UserId IS NULL
BEGIN
    INSERT INTO MasterUsers (
        Username, 
        PasswordHash, 
        TenantId, 
        IsActive, 
        CreatedAt, 
        EmployeeNumber, 
        FullName
    )
    VALUES (
        '58',           -- Username = EmployeeNumber for simplicity
        '123',          -- PasswordHash (Plain text as per MasterUserService)
        @TenantId,
        1,              -- IsActive
        GETDATE(),
        '58',           -- EmployeeNumber
        N'موظف 58'      -- FullName
    );
    
    SET @UserId = SCOPE_IDENTITY();
    PRINT 'Created user 58';
END
ELSE
BEGIN
    -- Update existing user to ensure password is '123'
    UPDATE MasterUsers
    SET PasswordHash = '123', 
        IsActive = 1,
        TenantId = @TenantId
    WHERE Id = @UserId;
    
    PRINT 'Updated user 58 password to 123';
END

-- 4. Assign Role to User
IF NOT EXISTS (SELECT * FROM UserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
BEGIN
    INSERT INTO UserRoles (UserId, RoleId)
    VALUES (@UserId, @RoleId);
    PRINT 'Assigned Admin role to user 58';
END

-- 5. Ensure UserTenants has the tenant
IF NOT EXISTS (SELECT * FROM UserTenants WHERE UserId = @UserId AND TenantId = @TenantId)
BEGIN
    INSERT INTO UserTenants (UserId, TenantId, CreatedAt)
    VALUES (@UserId, @TenantId, GETDATE());
    PRINT 'Assigned Tenant to UserTenants';
END
