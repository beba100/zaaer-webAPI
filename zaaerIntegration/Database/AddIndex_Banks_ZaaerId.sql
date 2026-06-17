-- =============================================
-- Create Index on banks.zaaer_id for Faster Lookups
-- إنشاء فهرس على zaaer_id في جدول banks لتسريع البحث
-- Purpose: Optimize queries that look up banks by zaaer_id
-- This query is executed in:
--   1. PaymentReceiptJournalEntryService: Check bank is_default when voucher_code = 'transfers_to_bank'
--   2. ZaaerBankService.UpsertBankByZaaerIdAsync: Find bank by zaaer_id for upsert operations
-- =============================================

-- Create NONCLUSTERED INDEX on banks.zaaer_id (Filtered Index for non-null values only)
-- Filtered indexes are smaller and faster when most queries filter out NULL values
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Banks_ZaaerId' AND object_id = OBJECT_ID('dbo.banks'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Banks_ZaaerId]
        ON [dbo].[banks] ([zaaer_id] ASC)
        WHERE [zaaer_id] IS NOT NULL; -- Filtered index: only index non-null zaaer_id values
    
    PRINT '✅ Index [IX_Banks_ZaaerId] created successfully on [banks] table.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_Banks_ZaaerId] already exists on [banks] table.';
END
GO

-- Performance Note:
-- This index significantly improves the performance of:
-- 1. PaymentReceiptJournalEntryService: WHERE zaaer_id = receipt.BankId (when BankId stores zaaer_id)
-- 2. ZaaerBankService.UpsertBankByZaaerIdAsync: WHERE zaaer_id = zaaerId
-- 3. Any other queries that filter banks by zaaer_id
-- 4. JOIN operations between banks and other tables using zaaer_id
--
-- Filtered Index Benefits:
-- - Smaller index size (only non-null values are indexed)
-- - Faster index maintenance (INSERT/UPDATE operations)
-- - Better query performance for non-null zaaer_id lookups
--
-- Query Performance Impact:
-- - Without index: Table Scan (O(n)) - scans all rows
-- - With index: Index Seek (O(log n)) - direct lookup
--
-- Expected Performance Improvement:
-- - Before: ~10-50ms for bank lookup (depending on table size)
-- - After: ~1-5ms for bank lookup (constant time regardless of table size)
-- =============================================

