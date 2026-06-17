/*
Optional English display names for PMS chrome (hotel badge + user menu).
Safe to re-run. Run on Master DB (Tenants, pms_users) and each tenant DB (hotel_settings).
*/

SET NOCOUNT ON;

-- Master: Tenants
IF OBJECT_ID(N'dbo.Tenants', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Tenants', N'name_en') IS NULL
BEGIN
    ALTER TABLE dbo.Tenants ADD name_en NVARCHAR(200) NULL;
    PRINT N'Tenants.name_en added.';
END

-- Master: pms_users
IF OBJECT_ID(N'dbo.pms_users', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pms_users', N'first_name_en') IS NULL
BEGIN
    ALTER TABLE dbo.pms_users ADD first_name_en NVARCHAR(100) NULL;
    PRINT N'pms_users.first_name_en added.';
END

IF OBJECT_ID(N'dbo.pms_users', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pms_users', N'last_name_en') IS NULL
BEGIN
    ALTER TABLE dbo.pms_users ADD last_name_en NVARCHAR(100) NULL;
    PRINT N'pms_users.last_name_en added.';
END

-- Tenant DB: hotel_settings (run per tenant database)
IF OBJECT_ID(N'dbo.hotel_settings', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.hotel_settings', N'hotel_name_en') IS NULL
BEGIN
    ALTER TABLE dbo.hotel_settings ADD hotel_name_en NVARCHAR(100) NULL;
    PRINT N'hotel_settings.hotel_name_en added.';
END

PRINT N'AddLocalizedDisplayNameColumns.sql complete.';
