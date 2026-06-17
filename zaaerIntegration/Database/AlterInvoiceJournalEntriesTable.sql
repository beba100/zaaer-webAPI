-- =============================================
-- Alter Invoice Journal Entries Table
-- تعديل جدول تتبع القيود المحاسبية للفواتير
-- Purpose: Add invoice_zaaer_id column to track by zaaer_id instead of invoice_id
-- Author: System
-- Date: 2025-12-17
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [db30471]; -- Replace with actual TENANT database name
-- DO NOT run on Master DB (db29328)!
GO

-- Add invoice_zaaer_id column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[invoice_journal_entries]') AND name = 'invoice_zaaer_id')
BEGIN
    ALTER TABLE [dbo].[invoice_journal_entries]
    ADD [invoice_zaaer_id] INT NULL;
    
    PRINT '✅ Column [invoice_zaaer_id] added successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Column [invoice_zaaer_id] already exists.';
END
GO

-- Update existing records: Set invoice_zaaer_id from invoices.zaaer_id
UPDATE ije
SET ije.invoice_zaaer_id = inv.zaaer_id
FROM [dbo].[invoice_journal_entries] ije
INNER JOIN [dbo].[invoices] inv ON ije.invoice_id = inv.invoice_id
WHERE ije.invoice_zaaer_id IS NULL;

PRINT '✅ Updated existing records with invoice_zaaer_id values.';
GO

-- Create index on invoice_zaaer_id for faster lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_InvoiceZaaerId' AND object_id = OBJECT_ID('invoice_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_InvoiceZaaerId]
        ON [dbo].[invoice_journal_entries] ([invoice_zaaer_id] ASC);
    PRINT '✅ Index [IX_InvoiceJournalEntries_InvoiceZaaerId] created successfully.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceZaaerId] already exists.';
END
GO

-- Note: We keep invoice_id column and FK constraint for referential integrity
-- invoice_zaaer_id is used for tracking/lookup purposes (matching credit_notes.invoice_id to invoices.zaaer_id)
-- =============================================

-- =============================================
-- Create Index on invoices.zaaer_id for Faster Lookups
-- إنشاء فهرس على zaaer_id في جدول invoices لتسريع البحث
-- Purpose: Optimize the query: SELECT * FROM invoices WHERE zaaer_id = @creditNoteInvoiceId
-- This query is executed in ZaaerCreditNoteCreateHandler to validate invoice existence before sending reverse journal entry
-- =============================================

-- Create NONCLUSTERED INDEX on invoices.zaaer_id (Filtered Index for non-null values only)
-- Filtered indexes are smaller and faster when most queries filter out NULL values
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Invoices_ZaaerId' AND object_id = OBJECT_ID('invoices'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoices_ZaaerId]
        ON [dbo].[invoices] ([zaaer_id] ASC)
        WHERE [zaaer_id] IS NOT NULL; -- Filtered index: only index non-null zaaer_id values
    
    PRINT '✅ Index [IX_Invoices_ZaaerId] created successfully on [invoices] table.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_Invoices_ZaaerId] already exists on [invoices] table.';
END
GO

-- Performance Note:
-- This index significantly improves the performance of:
-- 1. Credit Note reverse journal entry validation: WHERE zaaer_id = creditNote.InvoiceId
-- 2. Any other queries that filter invoices by zaaer_id
-- 3. JOIN operations between invoices and other tables using zaaer_id
--
-- Filtered Index Benefits:
-- - Smaller index size (only non-null values are indexed)
-- - Faster index maintenance (INSERT/UPDATE operations)
-- - Better query performance for non-null zaaer_id lookups
--
-- Query Performance Impact:
-- - Without index: Table Scan (O(n)) - scans all rows
-- - With index: Index Seek (O(log n)) - direct lookup
-- =============================================
