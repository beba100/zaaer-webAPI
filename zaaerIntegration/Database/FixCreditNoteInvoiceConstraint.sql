-- =============================================
-- Fix Credit Note Invoice Constraint
-- إصلاح قيد العلاقة بين Credit Notes و Invoices
-- Purpose: Remove incorrect FK constraint and add proper validation
-- Author: System
-- Date: 2025-12-17
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [db30471]; -- Replace with actual TENANT database name
-- DO NOT run on Master DB (db29328)!
GO

PRINT '========================================';
PRINT 'Fixing Credit Note Invoice Constraint...';
PRINT '========================================';
GO

-- Step 1: Drop the incorrect Foreign Key constraint (if exists)
-- The current FK links credit_notes.invoice_id to invoices.invoice_id (Primary Key)
-- But credit_notes.invoice_id actually contains invoices.zaaer_id (not invoice_id)
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_CreditNotes_Invoices')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    DROP CONSTRAINT [FK_CreditNotes_Invoices];
    
    PRINT '✅ Dropped incorrect FK constraint [FK_CreditNotes_Invoices].';
END
ELSE
BEGIN
    PRINT '⚠️ FK constraint [FK_CreditNotes_Invoices] does not exist.';
END
GO

-- Step 2: Create a Check Constraint using a Function
-- Note: SQL Server Check Constraints cannot use subqueries directly
-- So we'll create a function-based check constraint

-- First, create a function to validate the relationship
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[fn_ValidateCreditNoteInvoice]') AND type = N'FN')
BEGIN
    DROP FUNCTION [dbo].[fn_ValidateCreditNoteInvoice];
    PRINT '✅ Dropped existing function [fn_ValidateCreditNoteInvoice].';
END
GO

CREATE FUNCTION [dbo].[fn_ValidateCreditNoteInvoice]
(
    @InvoiceId INT  -- This is actually zaaer_id from invoices table
)
RETURNS BIT
AS
BEGIN
    DECLARE @Exists BIT = 0;
    
    -- Check if there's an invoice with zaaer_id = @InvoiceId
    IF EXISTS (SELECT 1 FROM [dbo].[invoices] WHERE [zaaer_id] = @InvoiceId)
        SET @Exists = 1;
    
    RETURN @Exists;
END
GO

PRINT '✅ Created validation function [fn_ValidateCreditNoteInvoice].';
GO

-- Step 3: Add Check Constraint using the function
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_CreditNotes_InvoiceId_Valid')
BEGIN
    ALTER TABLE [dbo].[credit_notes]
    ADD CONSTRAINT [CK_CreditNotes_InvoiceId_Valid]
    CHECK ([dbo].[fn_ValidateCreditNoteInvoice]([invoice_id]) = 1);
    
    PRINT '✅ Added Check Constraint [CK_CreditNotes_InvoiceId_Valid].';
END
ELSE
BEGIN
    PRINT '⚠️ Check Constraint [CK_CreditNotes_InvoiceId_Valid] already exists.';
END
GO

-- Step 4: Create Index for performance (credit_notes.invoice_id to invoices.zaaer_id lookup)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_CreditNotes_InvoiceId' AND object_id = OBJECT_ID('credit_notes'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CreditNotes_InvoiceId]
        ON [dbo].[credit_notes] ([invoice_id] ASC);
    
    PRINT '✅ Created Index [IX_CreditNotes_InvoiceId] on [credit_notes] table.';
END
ELSE
BEGIN
    PRINT '⚠️ Index [IX_CreditNotes_InvoiceId] already exists on [credit_notes] table.';
END
GO

-- Step 5: Verify existing data integrity
PRINT '';
PRINT '========================================';
PRINT 'Verifying data integrity...';
PRINT '========================================';

DECLARE @InvalidCount INT;
SELECT @InvalidCount = COUNT(*)
FROM [dbo].[credit_notes] cn
LEFT JOIN [dbo].[invoices] inv ON cn.[invoice_id] = inv.[zaaer_id]
WHERE inv.[invoice_id] IS NULL;

IF @InvalidCount > 0
BEGIN
    PRINT '⚠️ WARNING: Found ' + CAST(@InvalidCount AS NVARCHAR(10)) + ' credit notes with invalid invoice_id (no matching zaaer_id in invoices table).';
    PRINT '   These records violate the constraint and may cause issues.';
    PRINT '   Please review and fix these records manually.';
    
    -- Show invalid records
    SELECT 
        cn.[credit_note_id],
        cn.[credit_note_no],
        cn.[invoice_id] AS [credit_note_invoice_id],
        'No matching invoice with zaaer_id = ' + CAST(cn.[invoice_id] AS NVARCHAR(10)) AS [error]
    FROM [dbo].[credit_notes] cn
    LEFT JOIN [dbo].[invoices] inv ON cn.[invoice_id] = inv.[zaaer_id]
    WHERE inv.[invoice_id] IS NULL;
END
ELSE
BEGIN
    PRINT '✅ All credit notes have valid invoice_id references (matching invoices.zaaer_id).';
END
GO

PRINT '';
PRINT '========================================';
PRINT 'Constraint Fix Summary:';
PRINT '========================================';
PRINT '1. ✅ Removed incorrect FK constraint (FK_CreditNotes_Invoices)';
PRINT '2. ✅ Created validation function (fn_ValidateCreditNoteInvoice)';
PRINT '3. ✅ Added Check Constraint (CK_CreditNotes_InvoiceId_Valid)';
PRINT '4. ✅ Created Index (IX_CreditNotes_InvoiceId)';
PRINT '========================================';
GO

-- =============================================
-- NOTES:
-- =============================================
-- 1. The Check Constraint ensures: credit_notes.invoice_id = invoices.zaaer_id
-- 2. This constraint will prevent inserting/updating credit notes with invalid invoice_id
-- 3. The Index improves lookup performance when joining credit_notes with invoices
-- 4. If you have existing invalid data, fix it before enabling the constraint
-- =============================================
