-- ======================================================================
-- Add EnableVoMSync Field to Tenants Table
-- ======================================================================
-- Purpose: Allow enabling/disabling VoM synchronization per tenant
-- Usage: SET EnableVoMSync = 0 to disable VoM sync for a specific tenant
--        SET EnableVoMSync = 1 to enable VoM sync for a specific tenant
-- ======================================================================

USE [db32357_Master_DB];
GO

PRINT '========================================';
PRINT 'Adding EnableVoMSync to Tenants Table';
PRINT '========================================';
PRINT '';

-- Step 1: Add the column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID('[dbo].[Tenants]') 
               AND name = 'EnableVoMSync')
BEGIN
    ALTER TABLE [dbo].[Tenants]
    ADD [EnableVoMSync] BIT NOT NULL DEFAULT 1;
    
    PRINT '✅ Added EnableVoMSync column to Tenants table';
    PRINT '   Default value: 1 (enabled) for all existing tenants';
END
ELSE
BEGIN
    PRINT '⚠️ EnableVoMSync column already exists - skipping';
END
GO

-- Step 2: Verify the change
PRINT '';
PRINT '========================================';
PRINT 'Current Tenants and VoM Sync Status:';
PRINT '========================================';
PRINT '';

SELECT 
    Id,
    Code AS [Tenant Code],
    Name AS [Tenant Name],
    DatabaseName,
    EnableVoMSync AS [VoM Enabled],
    CASE 
        WHEN EnableVoMSync = 1 THEN 'Will sync to VoM ✅'
        ELSE 'VoM sync disabled ❌'
    END AS [Status]
FROM [dbo].[Tenants]
ORDER BY Code;
GO

PRINT '';
PRINT '========================================';
PRINT 'Summary:';
PRINT '========================================';

DECLARE @TotalTenants INT;
DECLARE @EnabledTenants INT;
DECLARE @DisabledTenants INT;

SELECT @TotalTenants = COUNT(*) FROM [dbo].[Tenants];
SELECT @EnabledTenants = COUNT(*) FROM [dbo].[Tenants] WHERE EnableVoMSync = 1;
SELECT @DisabledTenants = COUNT(*) FROM [dbo].[Tenants] WHERE EnableVoMSync = 0;

PRINT 'Total Tenants: ' + CAST(@TotalTenants AS VARCHAR(10));
PRINT 'VoM Enabled: ' + CAST(@EnabledTenants AS VARCHAR(10)) + ' ✅';
PRINT 'VoM Disabled: ' + CAST(@DisabledTenants AS VARCHAR(10)) + ' ❌';

PRINT '';
PRINT '========================================';
PRINT 'Usage Examples:';
PRINT '========================================';
PRINT '';
PRINT '-- Disable VoM sync for a specific tenant:';
PRINT '-- UPDATE [dbo].[Tenants] SET EnableVoMSync = 0 WHERE Code = ''Dammam1'';';
PRINT '';
PRINT '-- Enable VoM sync for a specific tenant:';
PRINT '-- UPDATE [dbo].[Tenants] SET EnableVoMSync = 1 WHERE Code = ''Dammam1'';';
PRINT '';
PRINT '-- Disable VoM sync for multiple tenants:';
PRINT '-- UPDATE [dbo].[Tenants] SET EnableVoMSync = 0 WHERE Code IN (''Dammam1'', ''Dammam2'', ''Riyadh1'');';
PRINT '';
PRINT '✅ Script completed successfully!';
GO

