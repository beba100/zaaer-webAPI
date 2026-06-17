-- =============================================
-- Alter Invoice Journal Entries Table - Single Database
-- تعديل جدول تتبع القيود المحاسبية للفواتير - قاعدة بيانات واحدة
-- Purpose: Add invoice_zaaer_id column and indexes to a single tenant database
-- Author: System
-- Date: 2025-12-17
-- =============================================
-- ⚠️ IMPORTANT: 
-- 1. Change the database name below to your target tenant database
-- 2. Run this script on EACH tenant database separately
-- 3. Safe to run multiple times (idempotent - uses IF NOT EXISTS checks)
-- =============================================

-- ⚠️ CHANGE THIS DATABASE NAME TO YOUR TARGET DATABASE
USE [YOUR_DATABASE_NAME_HERE];
GO

PRINT '========================================';
PRINT 'Starting update for invoice_journal_entries table...';
PRINT 'Database: ' + DB_NAME();
PRINT '========================================';
GO

BEGIN TRY
    -- Add invoice_zaaer_id column if it doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[invoice_journal_entries]') AND name = 'invoice_zaaer_id')
    BEGIN
        ALTER TABLE [dbo].[invoice_journal_entries]
        ADD [invoice_zaaer_id] INT NULL;
        PRINT '✅ Column [invoice_zaaer_id] added successfully';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Column [invoice_zaaer_id] already exists';
    END
    
    -- Update existing records: Set invoice_zaaer_id from invoices.zaaer_id
    DECLARE @UpdatedRows INT;
    UPDATE ije
    SET ije.invoice_zaaer_id = inv.zaaer_id
    FROM [dbo].[invoice_journal_entries] ije
    INNER JOIN [dbo].[invoices] inv ON ije.invoice_id = inv.invoice_id
    WHERE ije.invoice_zaaer_id IS NULL;
    
    SET @UpdatedRows = @@ROWCOUNT;
    PRINT '✅ Updated ' + CAST(@UpdatedRows AS NVARCHAR(10)) + ' existing records with invoice_zaaer_id values';
    
    -- Create index on invoice_zaaer_id for faster lookups
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceJournalEntries_InvoiceZaaerId' AND object_id = OBJECT_ID('invoice_journal_entries'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_InvoiceJournalEntries_InvoiceZaaerId]
            ON [dbo].[invoice_journal_entries] ([invoice_zaaer_id] ASC);
        PRINT '✅ Index [IX_InvoiceJournalEntries_InvoiceZaaerId] created successfully';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index [IX_InvoiceJournalEntries_InvoiceZaaerId] already exists';
    END
    
    -- Create NONCLUSTERED INDEX on invoices.zaaer_id (Filtered Index)
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Invoices_ZaaerId' AND object_id = OBJECT_ID('invoices'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Invoices_ZaaerId]
            ON [dbo].[invoices] ([zaaer_id] ASC)
            WHERE [zaaer_id] IS NOT NULL;
        PRINT '✅ Index [IX_Invoices_ZaaerId] created successfully';
    END
    ELSE
    BEGIN
        PRINT '⚠️ Index [IX_Invoices_ZaaerId] already exists';
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
-- 3. ✅ Safe to run multiple times (idempotent - uses IF NOT EXISTS checks)
-- 4. ✅ You can copy this script and change the database name for each tenant
--
-- What this script does:
-- - Adds invoice_zaaer_id column to invoice_journal_entries table
-- - Updates existing records with zaaer_id values from invoices table
-- - Creates index on invoice_journal_entries.invoice_zaaer_id
-- - Creates filtered index on invoices.zaaer_id
--
-- Performance Impact:
-- - Minimal downtime (only adds column and indexes)
-- - Safe to run during business hours (non-blocking operations)
-- - Index creation may take a few seconds depending on data size
-- =============================================
