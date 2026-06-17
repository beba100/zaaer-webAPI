-- =============================================
-- Add vom_journal_entry_id Column to payment_receipts Table
-- إضافة عمود vom_journal_entry_id إلى جدول payment_receipts
-- Purpose: Store VoM Journal Entry ID directly in payment_receipts for easier lookup
-- Author: System
-- Date: 2025-12-31
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [YOUR_DATABASE_NAME_HERE]; -- Replace with actual TENANT database name (e.g., db32421_Dammam9, etc.)
-- DO NOT run on Master DB!
GO

-- Check if column exists, if not add it
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[payment_receipts]') 
    AND name = 'vom_journal_entry_id'
)
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [vom_journal_entry_id] INT NULL;
    
    PRINT '✅ Column [vom_journal_entry_id] added successfully to [payment_receipts] table.';
    
    -- Add index for better performance when searching by vom_journal_entry_id
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceipts_VomJournalEntryId' AND object_id = OBJECT_ID('payment_receipts'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_PaymentReceipts_VomJournalEntryId]
            ON [dbo].[payment_receipts] ([vom_journal_entry_id] ASC)
            WHERE [vom_journal_entry_id] IS NOT NULL;
        
        PRINT '✅ Index [IX_PaymentReceipts_VomJournalEntryId] created successfully.';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index [IX_PaymentReceipts_VomJournalEntryId] already exists.';
    END
END
ELSE
BEGIN
    PRINT '⚠️ Column [vom_journal_entry_id] already exists in [payment_receipts] table.';
END
GO

-- Optional: Migrate existing data from payment_receipt_journal_entries to payment_receipts
-- (Only if you want to populate the new column with existing data)
/*
UPDATE pr
SET pr.vom_journal_entry_id = prje.vom_journal_entry_id
FROM [dbo].[payment_receipts] pr
INNER JOIN [dbo].[payment_receipt_journal_entries] prje
    ON pr.receipt_id = prje.receipt_id
WHERE prje.status = 'Sent'
    AND prje.vom_journal_entry_id IS NOT NULL
    AND pr.vom_journal_entry_id IS NULL;

PRINT '✅ Migrated existing vom_journal_entry_id values from payment_receipt_journal_entries to payment_receipts.';
*/
GO

