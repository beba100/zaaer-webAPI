-- =============================================
-- Add AccountId Column to CostCenters Table
-- إضافة عمود AccountId إلى جدول CostCenters
-- Purpose: Link each CostCenter (hotel) to its cash box account in ChartOfAccounts
-- Author: System
-- Date: 2025-12-XX
-- =============================================
-- ⚠️ IMPORTANT: Run this on MASTER DATABASE ONLY (NOT tenant databases)
-- =============================================

-- USE [db32357_MasterDB]; -- Replace with your Master DB name
-- DO NOT run on tenant databases!
GO

PRINT '========================================';
PRINT 'Adding AccountId column to CostCenters table...';
PRINT 'Database: ' + DB_NAME();
PRINT '========================================';
GO

BEGIN TRY
    -- Add AccountId column if it doesn't exist
    IF NOT EXISTS (
        SELECT * FROM sys.columns 
        WHERE object_id = OBJECT_ID('dbo.CostCenters') 
        AND name = 'AccountId'
    )
    BEGIN
        ALTER TABLE [dbo].[CostCenters]
        ADD [AccountId] INT NULL;
        
        PRINT '✅ Column [AccountId] added successfully to CostCenters table';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Column [AccountId] already exists in CostCenters table';
    END
    
    -- Create index on AccountId for faster lookups
    IF NOT EXISTS (
        SELECT * FROM sys.indexes 
        WHERE name = 'IX_CostCenters_AccountId' 
        AND object_id = OBJECT_ID('CostCenters')
    )
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_CostCenters_AccountId]
            ON [dbo].[CostCenters] ([AccountId] ASC)
            WHERE [AccountId] IS NOT NULL;
        PRINT '✅ Index [IX_CostCenters_AccountId] created successfully';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index [IX_CostCenters_AccountId] already exists';
    END
    
    -- Display current CostCenters with their AccountId status
    PRINT '';
    PRINT 'Current CostCenters and AccountId status:';
    SELECT 
        cc.[id] AS CostCenterId,
        cc.[name_ar] AS CostCenterName,
        cc.[hotel_id] AS HotelId,
        cc.[AccountId] AS AccountId,
        CASE 
            WHEN cc.[AccountId] IS NULL THEN '❌ Not Set'
            ELSE '✅ Set'
        END AS Status
    FROM [dbo].[CostCenters] cc
    WHERE cc.[is_active] = 1
    ORDER BY cc.[hotel_id];
    
    PRINT '';
    PRINT '========================================';
    PRINT '✅ Successfully completed!';
    PRINT '========================================';
    PRINT '';
    PRINT '⚠️ NEXT STEPS:';
    PRINT '1. Update AccountId for each CostCenter (hotel) with the cash box account ID from ChartOfAccounts';
    PRINT '2. Example SQL to update:';
    PRINT '   UPDATE [dbo].[CostCenters] SET [AccountId] = [YOUR_CASH_BOX_ACCOUNT_ID] WHERE [hotel_id] = [HOTEL_ID];';
    PRINT '3. Verify cash box accounts exist in ChartOfAccounts table';
    PRINT '========================================';
    
END TRY
BEGIN CATCH
    PRINT '';
    PRINT '========================================';
    PRINT '❌ ERROR occurred:';
    PRINT '   Error Message: ' + ERROR_MESSAGE();
    PRINT '   Error Number: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10));
    PRINT '   Error Line: ' + CAST(ERROR_LINE() AS NVARCHAR(10));
    PRINT '========================================';
END CATCH
GO

-- =============================================
-- USAGE NOTES:
-- =============================================
-- 1. ⚠️ Run this script on MASTER DATABASE ONLY
-- 2. ⚠️ DO NOT run on tenant databases!
-- 3. After adding the column, you need to UPDATE AccountId for each CostCenter
-- 4. AccountId should reference the cash box account ID from ChartOfAccounts table
-- 5. To find cash box accounts in ChartOfAccounts, run:
--    SELECT * FROM [dbo].[ChartOfAccounts] 
--    WHERE (NameAr LIKE '%صندوق%' OR NameEn LIKE '%cash%') 
--    AND UsedInPayment = 1 AND IsActive = 1;
-- =============================================

