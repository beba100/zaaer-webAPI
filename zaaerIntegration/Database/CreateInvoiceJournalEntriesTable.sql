-- =============================================
-- Create Invoice Journal Entries Table
-- جدول تتبع القيود المحاسبية للفواتير
-- Purpose: Track journal entries sent to VoM system for invoices
-- Author: System
-- Date: 2025-12-16
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [db30471]; -- Replace with actual TENANT database name (e.g., db30471, db30472, etc.)
-- DO NOT run on Master DB (db29328)!
GO

-- Check if table exists, if not create it
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[invoice_journal_entries]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[invoice_journal_entries]
    (
        [id] INT IDENTITY(1,1) NOT NULL,
        [invoice_id] INT NOT NULL,
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
        
        CONSTRAINT [PK_invoice_journal_entries] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [FK_InvoiceJournalEntries_Invoices] FOREIGN KEY ([invoice_id])
            REFERENCES [dbo].[invoices] ([invoice_id])
            ON DELETE NO ACTION
    );

    PRINT '✅ Table [invoice_journal_entries] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Table [invoice_journal_entries] already exists.';
END
GO

-- Create indexes for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_InvoiceId' AND object_id = OBJECT_ID('invoice_journal_entries'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_InvoiceId]
        ON [dbo].[invoice_journal_entries] ([invoice_id] ASC);
    PRINT '✅ Index [IX_InvoiceJournalEntries_InvoiceId] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceId] already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_Status' AND object_id = OBJECT_ID('invoice_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_Status]
        ON [dbo].[invoice_journal_entries] ([status] ASC);
    PRINT '✅ Index [IX_InvoiceJournalEntries_Status] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_InvoiceJournalEntries_Status] already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_JournalDate' AND object_id = OBJECT_ID('invoice_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_JournalDate]
        ON [dbo].[invoice_journal_entries] ([journal_date] ASC);
    PRINT '✅ Index [IX_InvoiceJournalEntries_JournalDate] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_InvoiceJournalEntries_JournalDate] already exists.';
END
GO

-- Verify table creation
SELECT 
    'invoice_journal_entries' AS TableName,
    COUNT(*) AS RecordCount
FROM [dbo].[invoice_journal_entries];
GO

PRINT '✅ Invoice Journal Entries table setup completed successfully.';
GO

-- =============================================
-- USAGE NOTES:
-- =============================================
-- 1. ⚠️ Run this script on EACH TENANT DATABASE (e.g., db30471, db30472, etc.)
-- 2. ⚠️ DO NOT run on Master DB (db29328)!
-- 3. Uncomment the USE statement above and replace with your tenant DB name
-- 4. This table tracks journal entries sent to VoM for invoices
-- 5. Status values: 'Pending', 'Sent', 'Failed', 'Cancelled'
-- 6. One invoice can have only one journal entry (enforced by unique index)
-- 7. Foreign Key: invoice_id → invoices.invoice_id (in same tenant DB)
-- =============================================

-- Example commands for multiple tenants:
-- sqlcmd -S localhost -d db30471 -i CreateInvoiceJournalEntriesTable.sql
-- sqlcmd -S localhost -d db30472 -i CreateInvoiceJournalEntriesTable.sql
-- sqlcmd -S localhost -d db30473 -i CreateInvoiceJournalEntriesTable.sql
-- =============================================
