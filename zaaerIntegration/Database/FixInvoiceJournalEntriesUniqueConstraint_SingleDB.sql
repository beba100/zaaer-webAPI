-- =============================================
-- Fix Invoice Journal Entries Unique Constraint - Single Database
-- إصلاح قيد التفرد في جدول invoice_journal_entries - قاعدة بيانات واحدة
-- Purpose: Change unique constraint from invoice_id to journal_entry_code for a single tenant database
-- Author: System
-- Date: 2025-12-17
-- =============================================
-- ⚠️ IMPORTANT: 
-- 1. Change the database name below to your target tenant database
-- 2. Run this script on EACH tenant database separately
-- 3. Safe to run multiple times (idempotent - uses IF EXISTS checks)
-- =============================================

-- ⚠️ CHANGE THIS DATABASE NAME TO YOUR TARGET DATABASE
USE [YOUR_DATABASE_NAME_HERE];
GO

PRINT '========================================';
PRINT 'Starting fix for invoice_journal_entries unique constraint...';
PRINT 'Database: ' + DB_NAME();
PRINT '========================================';
GO

BEGIN TRY
    -- Step 1: Drop the existing unique index on invoice_id (if exists)
    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_InvoiceId' AND object_id = OBJECT_ID('invoice_journal_entries'))
    BEGIN
        DECLARE @IsUnique BIT;
        SELECT @IsUnique = is_unique
        FROM sys.indexes
        WHERE name = 'IX_InvoiceJournalEntries_InvoiceId' 
          AND object_id = OBJECT_ID('invoice_journal_entries');
        
        IF @IsUnique = 1
        BEGIN
            DROP INDEX [IX_InvoiceJournalEntries_InvoiceId] ON [dbo].[invoice_journal_entries];
            PRINT '✅ Dropped unique index [IX_InvoiceJournalEntries_InvoiceId]';
        END
        ELSE
        BEGIN
            PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] exists but is not unique';
        END
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] does not exist';
    END
    
    -- Step 2: Create non-unique index on invoice_id (for performance)
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_InvoiceId' AND object_id = OBJECT_ID('invoice_journal_entries'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_InvoiceId]
            ON [dbo].[invoice_journal_entries] ([invoice_id] ASC);
        PRINT '✅ Created non-unique index [IX_InvoiceJournalEntries_InvoiceId]';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] already exists';
    END
    
    -- Step 3: Create unique index on journal_entry_code (if not exists)
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_JournalEntryCode' AND object_id = OBJECT_ID('invoice_journal_entries'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_JournalEntryCode]
            ON [dbo].[invoice_journal_entries] ([journal_entry_code] ASC);
        PRINT '✅ Created unique index [IX_InvoiceJournalEntries_JournalEntryCode]';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Unique index [IX_InvoiceJournalEntries_JournalEntryCode] already exists';
    END
    
    -- Step 4: Verify data integrity
    DECLARE @DuplicateCount INT;
    SELECT @DuplicateCount = COUNT(*)
    FROM (
        SELECT [journal_entry_code], COUNT(*) AS [Count]
        FROM [dbo].[invoice_journal_entries]
        GROUP BY [journal_entry_code]
        HAVING COUNT(*) > 1
    ) AS Duplicates;
    
    IF @DuplicateCount > 0
    BEGIN
        PRINT '⚠️ WARNING: Found ' + CAST(@DuplicateCount AS NVARCHAR(10)) + ' duplicate journal_entry_code values';
    END
    ELSE
    BEGIN
        PRINT '✅ All journal_entry_code values are unique';
    END
    
    PRINT '';
    PRINT '========================================';
    PRINT '✅ Successfully completed!';
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
-- 1. ⚠️ Change [YOUR_DATABASE_NAME_HERE] to your actual database name
-- 2. ✅ Run this script on EACH tenant database separately
-- 3. ✅ Safe to run multiple times (idempotent - uses IF EXISTS checks)
-- 4. ✅ You can copy this script and change the database name for each tenant
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
