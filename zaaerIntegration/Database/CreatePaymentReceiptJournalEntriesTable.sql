-- =============================================
-- Create Payment Receipt Journal Entries Table
-- جدول تتبع القيود المحاسبية لسندات القبض
-- Purpose: Track journal entries sent to VoM system for payment receipts
-- Author: System
-- Date: 2025-12-XX
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [YOUR_DATABASE_NAME_HERE]; -- Replace with actual TENANT database name (e.g., db30471, db30472, etc.)
-- DO NOT run on Master DB!
GO

-- Check if table exists, if not create it
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[payment_receipt_journal_entries]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[payment_receipt_journal_entries]
    (
        [id] INT IDENTITY(1,1) NOT NULL,
        [receipt_id] INT NOT NULL,
        [receipt_zaaer_id] INT NULL,
        [vom_journal_entry_id] INT NULL,
        [journal_entry_code] NVARCHAR(50) NOT NULL,
        [journal_date] DATETIME NOT NULL,
        [total_debit] DECIMAL(12,2) NOT NULL DEFAULT 0,
        [total_credit] DECIMAL(12,2) NOT NULL DEFAULT 0,
        [status] NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        [vom_response] NVARCHAR(MAX) NULL,
        [error_message] NVARCHAR(1000) NULL,
        [retry_count] INT NOT NULL DEFAULT 0,
        [last_retry_at] DATETIME NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        [updated_at] DATETIME NULL,
        
        CONSTRAINT [PK_payment_receipt_journal_entries] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [FK_PaymentReceiptJournalEntries_PaymentReceipts] FOREIGN KEY ([receipt_id])
            REFERENCES [dbo].[payment_receipts] ([receipt_id])
            ON DELETE NO ACTION
    );

    PRINT '✅ Table [payment_receipt_journal_entries] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Table [payment_receipt_journal_entries] already exists.';
END
GO

-- Create indexes for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceiptJournalEntries_ReceiptId' AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
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

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceiptJournalEntries_ReceiptZaaerId' AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceiptJournalEntries_ReceiptZaaerId]
        ON [dbo].[payment_receipt_journal_entries] ([receipt_zaaer_id] ASC)
        WHERE [receipt_zaaer_id] IS NOT NULL;
    PRINT '✅ Index [IX_PaymentReceiptJournalEntries_ReceiptZaaerId] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_PaymentReceiptJournalEntries_ReceiptZaaerId] already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceiptJournalEntries_Status' AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
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

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceiptJournalEntries_JournalDate' AND object_id = OBJECT_ID('payment_receipt_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceiptJournalEntries_JournalDate]
        ON [dbo].[payment_receipt_journal_entries] ([journal_date] ASC);
    PRINT '✅ Index [IX_PaymentReceiptJournalEntries_JournalDate] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_PaymentReceiptJournalEntries_JournalDate] already exists.';
END
GO

-- Verify table creation
SELECT 
    'payment_receipt_journal_entries' AS TableName,
    COUNT(*) AS RecordCount
FROM [dbo].[payment_receipt_journal_entries];
GO

PRINT '✅ Payment Receipt Journal Entries table setup completed successfully.';
GO

-- =============================================
-- USAGE NOTES:
-- =============================================
-- 1. ⚠️ Run this script on EACH TENANT DATABASE (e.g., db30471, db30472, etc.)
-- 2. ⚠️ DO NOT run on Master DB!
-- 3. Uncomment the USE statement above and replace with your tenant DB name
-- 4. This table tracks journal entries sent to VoM for payment receipts
-- 5. Status values: 'Pending', 'Sent', 'Failed', 'Cancelled'
-- 6. Foreign Key: receipt_id → payment_receipts.receipt_id (in same tenant DB)
-- =============================================

-- Example commands for multiple tenants:
-- sqlcmd -S localhost -d db30471 -i CreatePaymentReceiptJournalEntriesTable.sql
-- sqlcmd -S localhost -d db30472 -i CreatePaymentReceiptJournalEntriesTable.sql
-- sqlcmd -S localhost -d db30473 -i CreatePaymentReceiptJournalEntriesTable.sql
-- =============================================

