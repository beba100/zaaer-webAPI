/*
Add login fields to MasterDB.dbo.pms_users (Hybrid RBAC).
Run once on Master database.
*/

IF COL_LENGTH(N'dbo.pms_users', N'username') IS NULL
BEGIN
    ALTER TABLE dbo.pms_users ADD username NVARCHAR(100) NOT NULL CONSTRAINT DF_pms_users_username DEFAULT (N'');
END;

IF COL_LENGTH(N'dbo.pms_users', N'employee_number') IS NULL
BEGIN
    ALTER TABLE dbo.pms_users ADD employee_number NVARCHAR(50) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pms_users_username' AND object_id = OBJECT_ID(N'dbo.pms_users'))
    CREATE INDEX IX_pms_users_username ON dbo.pms_users(username);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pms_users_employee_number' AND object_id = OBJECT_ID(N'dbo.pms_users'))
    CREATE INDEX IX_pms_users_employee_number ON dbo.pms_users(employee_number);

PRINT N'pms_users login columns ready.';
