-- =============================================
-- Performance Indexes for Payment Receipt Journal Entries
-- فهارس الأداء لجدول تتبع القيود المحاسبية لسندات القبض
-- =============================================
-- Purpose: Optimize queries for finding existing VoM journal entries
--          especially when updating receipts (DELETE then CREATE pattern)
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [YOUR_DATABASE_NAME_HERE]; -- Replace with actual TENANT database name (e.g., db30471, db30472, etc.)
-- DO NOT run on Master DB!
GO

-- =============================================
-- Composite Index for Update Query Optimization
-- =============================================
-- This index optimizes the query:
--   WHERE receipt_zaaer_id = @value 
--     AND status = 'Sent' 
--     AND vom_journal_entry_id IS NOT NULL
--   ORDER BY created_at DESC
-- 
-- Query frequency: High (on every receipt update)
-- Expected records: Millions in the future
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE name = 'IX_PaymentReceiptJournalEntries_ZaaerId_Status_VomId_CreatedAt' 
               AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceiptJournalEntries_ZaaerId_Status_VomId_CreatedAt]
        ON [dbo].[payment_receipt_journal_entries] 
        (
            [receipt_zaaer_id] ASC,
            [status] ASC,
            [vom_journal_entry_id] ASC,
            [created_at] DESC
        )
        WHERE [receipt_zaaer_id] IS NOT NULL 
          AND [status] = 'Sent' 
          AND [vom_journal_entry_id] IS NOT NULL;
    
    PRINT '✅ Index [IX_PaymentReceiptJournalEntries_ZaaerId_Status_VomId_CreatedAt] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_PaymentReceiptJournalEntries_ZaaerId_Status_VomId_CreatedAt] already exists.';
END
GO

-- =============================================
-- Additional Index for ReceiptId Lookups (if needed)
-- =============================================
-- This index is already created in CreatePaymentReceiptJournalEntriesTable.sql
-- But we verify it exists for completeness
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE name = 'IX_PaymentReceiptJournalEntries_ReceiptId' 
               AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceiptJournalEntries_ReceiptId]
        ON [dbo].[payment_receipt_journal_entries] ([receipt_id] ASC);
    PRINT '✅ Index [IX_PaymentReceiptJournalEntries_ReceiptId] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_PaymentReceiptJournalEntries_ReceiptId] already exists.';
END
GO

-- =============================================
-- Index for Status Filtering (if not exists)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE name = 'IX_PaymentReceiptJournalEntries_Status' 
               AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceiptJournalEntries_Status]
        ON [dbo].[payment_receipt_journal_entries] ([status] ASC);
    PRINT '✅ Index [IX_PaymentReceiptJournalEntries_Status] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_PaymentReceiptJournalEntries_Status] already exists.';
END
GO

-- =============================================
-- Index for VoM Journal Entry ID Lookups
-- =============================================
-- Useful for reverse lookups (finding receipt by VoM ID)
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE name = 'IX_PaymentReceiptJournalEntries_VomJournalEntryId' 
               AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceiptJournalEntries_VomJournalEntryId]
        ON [dbo].[payment_receipt_journal_entries] ([vom_journal_entry_id] ASC)
        WHERE [vom_journal_entry_id] IS NOT NULL;
    PRINT '✅ Index [IX_PaymentReceiptJournalEntries_VomJournalEntryId] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_PaymentReceiptJournalEntries_VomJournalEntryId] already exists.';
END
GO

-- =============================================
-- Verify Indexes
-- =============================================
PRINT '';
PRINT '📊 Current Indexes on [payment_receipt_journal_entries]:';
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_unique AS IsUnique,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS IndexColumns,
    i.filter_definition AS FilterDefinition
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('payment_receipt_journal_entries')
  AND i.type > 0 -- Exclude heap
GROUP BY i.name, i.type_desc, i.is_unique, i.filter_definition
ORDER BY i.name;
GO

PRINT '';
PRINT '✅ Performance indexes setup completed successfully.';
PRINT '';
PRINT '📝 NOTES:';
PRINT '   - Main composite index optimizes: receipt_zaaer_id + status + vom_journal_entry_id + created_at';
PRINT '   - Filtered index only includes rows where status = ''Sent'' and vom_journal_entry_id IS NOT NULL';
PRINT '   - This significantly improves query performance for millions of records';
PRINT '   - Index maintenance overhead is minimal due to filtered index';
GO

-- =============================================
-- USAGE NOTES:
-- =============================================
-- 1. ⚠️ Run this script on EACH TENANT DATABASE (e.g., db30471, db30472, etc.)
-- 2. ⚠️ DO NOT run on Master DB!
-- 3. Uncomment the USE statement above and replace with your tenant DB name
-- 4. This script adds optimized indexes for high-performance queries
-- 5. The main composite index uses a filtered index to reduce index size
-- 6. Expected performance improvement: 10-100x faster on large tables
-- =============================================

