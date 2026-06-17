-- =============================================
-- Fix Credit Note Invoice Constraint - Apply to ALL Tenants
-- إصلاح قيد العلاقة بين Credit Notes و Invoices - تطبيق على جميع الفنادق
-- Purpose: Remove incorrect FK constraint and add proper validation to ALL tenant databases
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
        -- Build dynamic SQL to execute on tenant database
        SET @Sql = N'
        USE [' + QUOTENAME(@DatabaseName) + N'];
        
        -- Step 1: Drop the incorrect Foreign Key constraint (if exists)
        IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = ''FK_CreditNotes_Invoices'')
        BEGIN
            ALTER TABLE [dbo].[credit_notes]
            DROP CONSTRAINT [FK_CreditNotes_Invoices];
            PRINT ''✅ Dropped incorrect FK constraint [FK_CreditNotes_Invoices] in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''⚠️ FK constraint [FK_CreditNotes_Invoices] does not exist in ' + @TenantCode + N'.'';
        END
        
        -- Step 2: Create validation function (drop if exists first)
        IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N''[dbo].[fn_ValidateCreditNoteInvoice]'') AND type = N''FN'')
        BEGIN
            DROP FUNCTION [dbo].[fn_ValidateCreditNoteInvoice];
        END
        
        -- Create function
        CREATE FUNCTION [dbo].[fn_ValidateCreditNoteInvoice]
        (
            @InvoiceId INT
        )
        RETURNS BIT
        AS
        BEGIN
            DECLARE @Exists BIT = 0;
            IF EXISTS (SELECT 1 FROM [dbo].[invoices] WHERE [zaaer_id] = @InvoiceId)
                SET @Exists = 1;
            RETURN @Exists;
        END
        
        PRINT ''✅ Created validation function [fn_ValidateCreditNoteInvoice] in ' + @TenantCode + N'.'';
        
        -- Step 3: Add Check Constraint (drop if exists first)
        IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = ''CK_CreditNotes_InvoiceId_Valid'')
        BEGIN
            ALTER TABLE [dbo].[credit_notes]
            DROP CONSTRAINT [CK_CreditNotes_InvoiceId_Valid];
        END
        
        ALTER TABLE [dbo].[credit_notes]
        ADD CONSTRAINT [CK_CreditNotes_InvoiceId_Valid]
        CHECK ([dbo].[fn_ValidateCreditNoteInvoice]([invoice_id]) = 1);
        
        PRINT ''✅ Added Check Constraint [CK_CreditNotes_InvoiceId_Valid] in ' + @TenantCode + N'.'';
        
        -- Step 4: Create Index for performance
        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = ''IX_CreditNotes_InvoiceId'' AND object_id = OBJECT_ID(''credit_notes''))
        BEGIN
            CREATE NONCLUSTERED INDEX [IX_CreditNotes_InvoiceId]
                ON [dbo].[credit_notes] ([invoice_id] ASC);
            PRINT ''✅ Created Index [IX_CreditNotes_InvoiceId] in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''⚠️ Index [IX_CreditNotes_InvoiceId] already exists in ' + @TenantCode + N'.'';
        END
        
        -- Step 5: Verify data integrity
        DECLARE @InvalidCount INT;
        SELECT @InvalidCount = COUNT(*)
        FROM [dbo].[credit_notes] cn
        LEFT JOIN [dbo].[invoices] inv ON cn.[invoice_id] = inv.[zaaer_id]
        WHERE inv.[invoice_id] IS NULL;
        
        IF @InvalidCount > 0
        BEGIN
            PRINT ''⚠️ WARNING: Found '' + CAST(@InvalidCount AS NVARCHAR(10)) + '' invalid credit notes in ' + @TenantCode + N'.'';
        END
        ELSE
        BEGIN
            PRINT ''✅ All credit notes have valid invoice_id references in ' + @TenantCode + N'.'';
        END
        ';
        
        -- Execute dynamic SQL using EXEC (supports USE statement)
        EXEC(@Sql);
        
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
-- - Removes incorrect FK constraint (FK_CreditNotes_Invoices)
-- - Creates validation function (fn_ValidateCreditNoteInvoice)
-- - Adds Check Constraint (CK_CreditNotes_InvoiceId_Valid)
-- - Creates Index (IX_CreditNotes_InvoiceId)
-- - Verifies data integrity
--
-- The constraint ensures: credit_notes.invoice_id = invoices.zaaer_id
-- =============================================
