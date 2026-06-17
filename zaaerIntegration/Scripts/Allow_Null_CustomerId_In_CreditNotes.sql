-- =============================================
-- Allow NULL for customer_id in credit_notes table
-- السماح بـ NULL في عمود customer_id في جدول credit_notes
-- Purpose: Allow credit notes to be created without customer_id
-- Author: System
-- Date: 2025-12-30
-- =============================================
-- ⚠️ IMPORTANT: Run this on EACH TENANT DATABASE (NOT Master DB)
-- =============================================

-- USE [YourTenantDatabase]; -- Replace with actual TENANT database name
-- DO NOT run on Master DB!
GO

PRINT '========================================';
PRINT 'Allowing NULL for customer_id in credit_notes...';
PRINT '========================================';
GO

-- Check if column exists and is NOT NULL
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'dbo' 
    AND TABLE_NAME = 'credit_notes' 
    AND COLUMN_NAME = 'customer_id'
    AND IS_NULLABLE = 'NO'
)
BEGIN
    -- Alter column to allow NULL
    ALTER TABLE [dbo].[credit_notes]
    ALTER COLUMN [customer_id] INT NULL;
    
    PRINT '✅ Successfully altered customer_id column to allow NULL in credit_notes table.';
END
ELSE IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = 'dbo' 
    AND TABLE_NAME = 'credit_notes' 
    AND COLUMN_NAME = 'customer_id'
    AND IS_NULLABLE = 'YES'
)
BEGIN
    PRINT 'ℹ️ Column customer_id already allows NULL. No changes needed.';
END
ELSE
BEGIN
    PRINT '⚠️ Column customer_id does not exist in credit_notes table.';
END
GO

PRINT '========================================';
PRINT 'Script completed successfully.';
PRINT '========================================';
GO

