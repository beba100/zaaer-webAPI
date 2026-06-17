-- =============================================
-- Fix Invoice Journal Entries Unique Constraint
-- إصلاح قيد التفرد في جدول invoice_journal_entries
-- Purpose: Change unique constraint from invoice_id to journal_entry_code
-- Author: System
-- Date: 2025-12-17
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [db30471]; -- Replace with actual TENANT database name
-- DO NOT run on Master DB (db29328)!
GO

PRINT '========================================';
PRINT 'Fixing Invoice Journal Entries Unique Constraint...';
PRINT '========================================';
GO

-- Step 1: Drop the existing unique index on invoice_id (if exists)
-- This index prevents multiple journal entries for the same invoice_id
-- But we need to allow multiple entries (e.g., invoice + credit note reverse entry)
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_InvoiceId' AND object_id = OBJECT_ID('invoice_journal_entries'))
BEGIN
    -- Check if it's a unique index
    DECLARE @IsUnique BIT;
    SELECT @IsUnique = is_unique
    FROM sys.indexes
    WHERE name = 'IX_InvoiceJournalEntries_InvoiceId' 
      AND object_id = OBJECT_ID('invoice_journal_entries');
    
    IF @IsUnique = 1
    BEGIN
        DROP INDEX [IX_InvoiceJournalEntries_InvoiceId] ON [dbo].[invoice_journal_entries];
        PRINT '✅ Dropped unique index [IX_InvoiceJournalEntries_InvoiceId].';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] exists but is not unique.';
    END
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] does not exist.';
END
GO

-- Step 2: Create non-unique index on invoice_id (for performance, not uniqueness)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_InvoiceId' AND object_id = OBJECT_ID('invoice_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_InvoiceId]
        ON [dbo].[invoice_journal_entries] ([invoice_id] ASC);
    
    PRINT '✅ Created non-unique index [IX_InvoiceJournalEntries_InvoiceId] on [invoice_journal_entries] table.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] already exists (non-unique).';
END
GO

-- Step 3: Create unique index on journal_entry_code (if not exists)
-- This ensures each InvoiceNo or CreditNoteNo has only one journal entry record
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_JournalEntryCode' AND object_id = OBJECT_ID('invoice_journal_entries'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_JournalEntryCode]
        ON [dbo].[invoice_journal_entries] ([journal_entry_code] ASC);
    
    PRINT '✅ Created unique index [IX_InvoiceJournalEntries_JournalEntryCode] on [invoice_journal_entries] table.';
END
ELSE
BEGIN
    PRINT '⚠️ Unique index [IX_InvoiceJournalEntries_JournalEntryCode] already exists.';
END
GO

-- Step 4: Verify data integrity - check for duplicate journal_entry_code
PRINT '';
PRINT '========================================';
PRINT 'Verifying data integrity...';
PRINT '========================================';

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
    PRINT '⚠️ WARNING: Found duplicate journal_entry_code values in invoice_journal_entries table.';
    PRINT '   These records will prevent the unique constraint from being created.';
    PRINT '   Please review and fix these records manually.';
    
    -- Show duplicate records
    SELECT 
        [journal_entry_code],
        COUNT(*) AS [DuplicateCount],
        STRING_AGG(CAST([id] AS NVARCHAR(10)), ', ') AS [RecordIds]
    FROM [dbo].[invoice_journal_entries]
    GROUP BY [journal_entry_code]
    HAVING COUNT(*) > 1;
END
ELSE
BEGIN
    PRINT '✅ All journal_entry_code values are unique.';
END
GO

PRINT '';
PRINT '========================================';
PRINT 'Constraint Fix Summary:';
PRINT '========================================';
PRINT '1. ✅ Removed unique constraint on invoice_id';
PRINT '2. ✅ Created non-unique index on invoice_id (for performance)';
PRINT '3. ✅ Created unique index on journal_entry_code (for idempotency)';
PRINT '========================================';
GO

-- =============================================
-- NOTES:
-- =============================================
-- 1. journal_entry_code is now UNIQUE (prevents duplicate entries for same InvoiceNo/CreditNoteNo)
-- 2. invoice_id is NOT unique (allows multiple entries per invoice, e.g., invoice + credit note)
-- 3. This allows:
--    - One journal entry per Invoice (journal_entry_code = InvoiceNo)
--    - One reverse journal entry per Credit Note (journal_entry_code = CreditNoteNo)
--    - Both can share the same invoice_id if credit note reverses an invoice
-- =============================================
