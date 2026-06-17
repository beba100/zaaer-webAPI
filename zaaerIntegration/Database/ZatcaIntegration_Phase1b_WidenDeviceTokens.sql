/*
  ZATCA — widen zatca_devices token columns (binarySecurityToken exceeds 500 chars).
  Run on tenant DB if onboarding failed with "String or binary data would be truncated".
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.zatca_devices', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.zatca_devices', 'compliance_csid') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN compliance_csid NVARCHAR(MAX) NULL;

    IF COL_LENGTH('dbo.zatca_devices', 'production_csid') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN production_csid NVARCHAR(MAX) NULL;

    IF COL_LENGTH('dbo.zatca_devices', 'compliance_secret') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN compliance_secret NVARCHAR(1000) NULL;

    IF COL_LENGTH('dbo.zatca_devices', 'production_secret') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN production_secret NVARCHAR(1000) NULL;
END;

PRINT N'ZatcaIntegration_Phase1b_WidenDeviceTokens.sql completed.';
