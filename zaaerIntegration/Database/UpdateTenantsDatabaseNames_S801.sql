-- =============================================================================
-- S801 dedicated MSSQL — align Master.Tenants.DatabaseName with actual DB names
-- Run on: db54638_Master (SSMS → s801.public.eu.machineasp.net or db54638 host)
-- =============================================================================
-- App builds tenant connections as:
--   Server = appsettings TenantDatabase:Server  (s801.public.eu.machineasp.net)
--   Database = Tenants.DatabaseName             (must match Object Explorer)
--   User Id / Password = TenantDatabase:UserId / Password (admin on S801)
-- =============================================================================

USE [db54638_Master];
GO

-- Preview current rows
SELECT Id, Code, Name, DatabaseName
FROM dbo.Tenants
ORDER BY Code;
GO

-- Dammam1 / Dammam2 on S801 (names as shown in SSMS Object Explorer)
UPDATE dbo.Tenants
SET DatabaseName = N'db54638_Dammam1'
WHERE Code = N'Dammam1'
  AND DatabaseName <> N'db54638_Dammam1';

UPDATE dbo.Tenants
SET DatabaseName = N'db54638_Dammam2'
WHERE Code = N'Dammam2'
  AND DatabaseName <> N'db54638_Dammam2';

-- Dammam3: only run when that database exists on S801 (rename if your DB name differs)
-- UPDATE dbo.Tenants
-- SET DatabaseName = N'db54637_Dammam3'
-- WHERE Code = N'Dammam3';

-- If Dammam3 is not on S801 yet, delete or fix its DatabaseName when the DB is created.

GO

SELECT Id, Code, Name, DatabaseName
FROM dbo.Tenants
ORDER BY Code;
GO
