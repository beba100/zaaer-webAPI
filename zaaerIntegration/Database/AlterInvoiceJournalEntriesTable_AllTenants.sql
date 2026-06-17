-- =============================================
-- Alter Invoice Journal Entries Table - Apply to ALL Tenants
-- تعديل جدول تتبع القيود المحاسبية للفواتير - تطبيق على جميع الفنادق
-- Purpose: Add invoice_zaaer_id column and indexes to ALL tenant databases automatically
-- Author: System
-- Date: 2025-12-17
-- =============================================
-- ⚠️ IMPORTANT: Run this script ONCE from Master DB (db29328)
-- It will automatically apply changes to ALL tenant databases
--
-- ✅ SAFE TO RUN:
-- - All operations use IF NOT EXISTS checks (idempotent)
-- - Can be run multiple times without issues
-- - Each tenant database is processed independently
-- - Errors in one tenant don't affect others
-- - Works on same SQL Server instance (all databases accessible)
--
-- ⚠️ REQUIREMENTS:
-- - All tenant databases must be on the same SQL Server instance
-- - Master DB (db29328) must have Tenants table with DatabaseName column
-- - User must have ALTER permissions on all tenant databases
-- =============================================

USE [db29328]; -- Master Database
GO

PRINT '========================================';
PRINT 'Starting batch update for ALL tenant databases...';
PRINT '========================================';
GO

-- Declare variables
DECLARE @DatabaseName NVARCHAR(255);
DECLARE @TenantCode NVARCHAR(50);
DECLARE @Sql NVARCHAR(MAX);
DECLARE @TenantCount INT = 0;
DECLARE @SuccessCount INT = 0;
DECLARE @ErrorCount INT = 0;

-- Cursor to iterate through all active tenants
DECLARE tenant_cursor CURSOR FOR
SELECT 
    [DatabaseName],
    [Code]
FROM [dbo].[Tenants]
WHERE [IsActive] = 1 
    AND [DatabaseName] IS NOT NULL 
    AND [DatabaseName] != ''
ORDER BY [Code];

OPEN tenant_cursor;
FETCH NEXT FROM tenant_cursor INTO @DatabaseName, @TenantCode;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @TenantCount = @TenantCount + 1;
    
    PRINT '';
    PRINT '========================================';
    PRINT 'Processing Tenant: ' + @TenantCode + ' (Database: ' + @DatabaseName + ')';
    PRINT '========================================';
    
    BEGIN TRY
        -- Build dynamic SQL using fully qualified names (no USE statement to avoid identifier length limit)
        SET @Sql = N'
        -- Add invoice_zaaer_id column if it doesn''t exist
        IF NOT EXISTS (SELECT * FROM [' + QUOTENAME(@DatabaseName) + N'].[sys].[columns] WHERE object_id = OBJECT_ID(N''[' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]'') AND name = ''invoice_zaaer_id'')
        BEGIN
            ALTER TABLE [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]
            ADD [invoice_zaaer_id] INT NULL;
            PRINT ''✅ Column [invoice_zaaer_id] added successfully in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''⚠️ Column [invoice_zaaer_id] already exists in ' + @TenantCode + N'.'';
        END
        
        -- Update existing records: Set invoice_zaaer_id from invoices.zaaer_id
        UPDATE ije
        SET ije.invoice_zaaer_id = inv.zaaer_id
        FROM [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries] ije
        INNER JOIN [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoices] inv ON ije.invoice_id = inv.invoice_id
        WHERE ije.invoice_zaaer_id IS NULL;
        
        PRINT ''✅ Updated existing records with invoice_zaaer_id values in ' + @TenantCode + N'.'';
        
        -- Create index on invoice_zaaer_id for faster lookups
        IF NOT EXISTS (SELECT * FROM [' + QUOTENAME(@DatabaseName) + N'].[sys].[indexes] WHERE name = ''IX_InvoiceJournalEntries_InvoiceZaaerId'' AND object_id = OBJECT_ID(''[' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]''))
        BEGIN
            CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_InvoiceZaaerId]
                ON [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries] ([invoice_zaaer_id] ASC);
            PRINT ''✅ Index [IX_InvoiceJournalEntries_InvoiceZaaerId] created successfully in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''⚠️ Index [IX_InvoiceJournalEntries_InvoiceZaaerId] already exists in ' + @TenantCode + N'.'';
        END
        
        -- Create NONCLUSTERED INDEX on invoices.zaaer_id (Filtered Index)
        IF NOT EXISTS (SELECT * FROM [' + QUOTENAME(@DatabaseName) + N'].[sys].[indexes] WHERE name = ''IX_Invoices_ZaaerId'' AND object_id = OBJECT_ID(''[' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoices]''))
        BEGIN
            CREATE NONCLUSTERED INDEX [IX_Invoices_ZaaerId]
                ON [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoices] ([zaaer_id] ASC)
                WHERE [zaaer_id] IS NOT NULL;
            PRINT ''✅ Index [IX_Invoices_ZaaerId] created successfully in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''⚠️ Index [IX_Invoices_ZaaerId] already exists in ' + @TenantCode + N'.'';
        END
        ';
        
        -- Execute dynamic SQL using sp_executesql (no USE statement needed)
        EXEC sp_executesql @Sql;
        
        SET @SuccessCount = @SuccessCount + 1;
        PRINT '✅ Successfully processed tenant: ' + @TenantCode;
        
    END TRY
    BEGIN CATCH
        SET @ErrorCount = @ErrorCount + 1;
        PRINT '❌ ERROR processing tenant: ' + @TenantCode;
        PRINT '   Error Message: ' + ERROR_MESSAGE();
        PRINT '   Error Number: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10));
        PRINT '   Error Line: ' + CAST(ERROR_LINE() AS NVARCHAR(10));
    END CATCH
    
    FETCH NEXT FROM tenant_cursor INTO @DatabaseName, @TenantCode;
END

CLOSE tenant_cursor;
DEALLOCATE tenant_cursor;

-- Summary
PRINT '';
PRINT '========================================';
PRINT 'Batch Update Summary:';
PRINT '========================================';
PRINT 'Total Tenants Processed: ' + CAST(@TenantCount AS NVARCHAR(10));
PRINT 'Successfully Updated: ' + CAST(@SuccessCount AS NVARCHAR(10));
PRINT 'Errors: ' + CAST(@ErrorCount AS NVARCHAR(10));
PRINT '========================================';
GO

-- =============================================
-- USAGE NOTES:
-- =============================================
-- 1. ✅ Run this script ONCE from Master DB (db29328)
-- 2. ✅ It automatically processes ALL active tenant databases
-- 3. ✅ Safe to run multiple times (idempotent - uses IF NOT EXISTS checks)
-- 4. ✅ Each tenant database is processed independently
-- 5. ✅ Errors in one tenant don't stop processing of others
-- 6. ✅ Summary report shows success/error count at the end
--
-- What this script does:
-- - Adds invoice_zaaer_id column to invoice_journal_entries table
-- - Updates existing records with zaaer_id values
-- - Creates index on invoice_journal_entries.invoice_zaaer_id
-- - Creates filtered index on invoices.zaaer_id
--
-- Performance Impact:
-- - Minimal downtime (only adds column and indexes)
-- - Safe to run during business hours (non-blocking operations)
-- - Index creation may take a few seconds per tenant depending on data size
-- =============================================
