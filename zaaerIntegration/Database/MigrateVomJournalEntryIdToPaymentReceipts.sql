-- =============================================
-- Migrate vom_journal_entry_id from payment_receipt_journal_entries to payment_receipts
-- نقل vom_journal_entry_id من payment_receipt_journal_entries إلى payment_receipts
-- Purpose: Consolidate VoM Journal Entry ID in payment_receipts for easier lookup
-- Author: System
-- Date: 2025-12-31
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [YOUR_DATABASE_NAME_HERE]; -- Replace with actual TENANT database name (e.g., db32421_Dammam9, etc.)
-- DO NOT run on Master DB!
GO

PRINT '🔄 Starting migration of vom_journal_entry_id from payment_receipt_journal_entries to payment_receipts...';
GO

-- Step 1: Update payment_receipts with vom_journal_entry_id from payment_receipt_journal_entries
-- Only update records where:
-- - payment_receipts.vom_journal_entry_id IS NULL (not already set)
-- - payment_receipt_journal_entries.status = 'Sent' (successfully sent)
-- - payment_receipt_journal_entries.vom_journal_entry_id IS NOT NULL (has valid ID)
-- Use the most recent entry (ORDER BY created_at DESC)
UPDATE pr
SET pr.vom_journal_entry_id = subquery.vom_journal_entry_id
FROM [dbo].[payment_receipts] pr
INNER JOIN (
    SELECT 
        prje.receipt_id,
        prje.vom_journal_entry_id,
        ROW_NUMBER() OVER (PARTITION BY prje.receipt_id ORDER BY prje.created_at DESC) AS rn
    FROM [dbo].[payment_receipt_journal_entries] prje
    WHERE prje.status = 'Sent'
        AND prje.vom_journal_entry_id IS NOT NULL
) subquery ON pr.receipt_id = subquery.receipt_id
WHERE pr.vom_journal_entry_id IS NULL
    AND subquery.rn = 1; -- Only take the most recent entry per receipt

DECLARE @UpdatedCount INT = @@ROWCOUNT;
PRINT '✅ Updated ' + CAST(@UpdatedCount AS VARCHAR(10)) + ' payment_receipts records with vom_journal_entry_id.';
GO

-- Step 2: Verify migration results
SELECT 
    'Migration Summary' AS Summary,
    COUNT(*) AS TotalReceipts,
    SUM(CASE WHEN vom_journal_entry_id IS NOT NULL THEN 1 ELSE 0 END) AS ReceiptsWithVoMId,
    SUM(CASE WHEN status_vom = 'sent' AND vom_journal_entry_id IS NOT NULL THEN 1 ELSE 0 END) AS SentWithVoMId,
    SUM(CASE WHEN status_vom = 'sent' AND vom_journal_entry_id IS NULL THEN 1 ELSE 0 END) AS SentWithoutVoMId
FROM [dbo].[payment_receipts]
WHERE status_vom = 'sent';
GO

-- Step 3: Show any discrepancies (receipts marked as 'sent' but without vom_journal_entry_id)
SELECT 
    pr.receipt_id,
    pr.receipt_no,
    pr.zaaer_id,
    pr.status_vom,
    pr.vom_journal_entry_id,
    pr.vom_sent_at,
    '⚠️ Receipt marked as sent but vom_journal_entry_id is NULL' AS Note
FROM [dbo].[payment_receipts] pr
WHERE pr.status_vom = 'sent'
    AND pr.vom_journal_entry_id IS NULL;
GO

PRINT '✅ Migration completed successfully.';
PRINT '📊 Check the results above to verify migration.';
GO

