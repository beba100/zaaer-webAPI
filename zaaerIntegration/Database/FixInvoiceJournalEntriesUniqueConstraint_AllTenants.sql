-- =============================================
-- Fix Invoice Journal Entries Unique Constraint - Apply to ALL Tenants
-- إصلاح قيد التفرد في جدول invoice_journal_entries - تطبيق على جميع الفنادق
-- Purpose: Change unique constraint from invoice_id to journal_entry_code for ALL tenant databases
-- Author: System
-- Date: 2025-12-17
-- =============================================
-- ⚠️ IMPORTANT: Run this script ONCE from Master DB (db29328)
-- It will automatically apply changes to ALL tenant databases
-- =============================================

USE [db29328]; -- Master Database
GO

PRINT '========================================';
PRINT 'Starting batch fix for ALL tenant databases...';
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
        -- Use sp_executesql with database context switching
        SET @Sql = N'
        -- Step 1: Drop the existing unique index on invoice_id (if exists)
        IF EXISTS (SELECT * FROM [' + QUOTENAME(@DatabaseName) + N'].[sys].[indexes] WHERE name = ''IX_InvoiceJournalEntries_InvoiceId'' AND object_id = OBJECT_ID(''[' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]''))
        BEGIN
            DECLARE @IsUnique' + CAST(@TenantCount AS NVARCHAR(10)) + N' BIT;
            SELECT @IsUnique' + CAST(@TenantCount AS NVARCHAR(10)) + N' = is_unique
            FROM [' + QUOTENAME(@DatabaseName) + N'].[sys].[indexes]
            WHERE name = ''IX_InvoiceJournalEntries_InvoiceId'' 
              AND object_id = OBJECT_ID(''[' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]'');
            
            IF @IsUnique' + CAST(@TenantCount AS NVARCHAR(10)) + N' = 1
            BEGIN
                DROP INDEX [IX_InvoiceJournalEntries_InvoiceId] ON [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries];
                PRINT ''✅ Dropped unique index [IX_InvoiceJournalEntries_InvoiceId] in ' + @TenantCode + N'.'';
            END
            ELSE
            BEGIN
                PRINT ''⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] exists but is not unique in ' + @TenantCode + N'.'';
            END
        END
        ELSE
        BEGIN
            PRINT ''⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] does not exist in ' + @TenantCode + N'.'';
        END
        
        -- Step 2: Create non-unique index on invoice_id (for performance)
        IF NOT EXISTS (SELECT * FROM [' + QUOTENAME(@DatabaseName) + N'].[sys].[indexes] WHERE name = ''IX_InvoiceJournalEntries_InvoiceId'' AND object_id = OBJECT_ID(''[' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]''))
        BEGIN
            CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_InvoiceId]
                ON [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries] ([invoice_id] ASC);
            PRINT ''✅ Created non-unique index [IX_InvoiceJournalEntries_InvoiceId] in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] already exists in ' + @TenantCode + N'.'';
        END
        
        -- Step 3: Create unique index on journal_entry_code (if not exists)
        IF NOT EXISTS (SELECT * FROM [' + QUOTENAME(@DatabaseName) + N'].[sys].[indexes] WHERE name = ''IX_InvoiceJournalEntries_JournalEntryCode'' AND object_id = OBJECT_ID(''[' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]''))
        BEGIN
            CREATE UNIQUE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_JournalEntryCode]
                ON [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries] ([journal_entry_code] ASC);
            PRINT ''✅ Created unique index [IX_InvoiceJournalEntries_JournalEntryCode] in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''⚠️ Unique index [IX_InvoiceJournalEntries_JournalEntryCode] already exists in ' + @TenantCode + N'.'';
        END
        
        -- Step 4: Verify data integrity
        DECLARE @DuplicateCount' + CAST(@TenantCount AS NVARCHAR(10)) + N' INT;
        SELECT @DuplicateCount' + CAST(@TenantCount AS NVARCHAR(10)) + N' = COUNT(*)
        FROM (
            SELECT [journal_entry_code], COUNT(*) AS [Count]
            FROM [' + QUOTENAME(@DatabaseName) + N'].[dbo].[invoice_journal_entries]
            GROUP BY [journal_entry_code]
            HAVING COUNT(*) > 1
        ) AS Duplicates;
        
        IF @DuplicateCount' + CAST(@TenantCount AS NVARCHAR(10)) + N' > 0
        BEGIN
            PRINT ''⚠️ WARNING: Found duplicate journal_entry_code values in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''✅ All journal_entry_code values are unique in ' + @TenantCode + N'.'';
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
PRINT 'Batch Fix Summary:';
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
-- 3. ✅ Safe to run multiple times (idempotent - uses IF EXISTS checks)
-- 4. ✅ Each tenant database is processed independently
-- 5. ✅ Errors in one tenant don't stop processing of others
-- 6. ✅ Summary report shows success/error count at the end
--
-- What this script does:
-- - Removes unique constraint on invoice_id (allows multiple entries per invoice)
-- - Creates non-unique index on invoice_id (for performance)
-- - Creates unique index on journal_entry_code (for idempotency)
--
-- This allows:
-- - One journal entry per Invoice (journal_entry_code = InvoiceNo)
-- - One reverse journal entry per Credit Note (journal_entry_code = CreditNoteNo)
-- - Both can share the same invoice_id if credit note reverses an invoice
-- =============================================
