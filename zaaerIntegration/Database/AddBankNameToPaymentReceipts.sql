-- =============================================
-- Add bank_name column to payment_receipts table
-- إضافة عمود bank_name إلى جدول payment_receipts
-- Purpose: Store bank name directly from Zaaer integration
-- =============================================

-- Check if column already exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('payment_receipts') AND name = 'bank_name')
BEGIN
    -- Add bank_name column
    ALTER TABLE [dbo].[payment_receipts]
    ADD [bank_name] NVARCHAR(255) NULL;

    PRINT '✅ Column [bank_name] added successfully to [payment_receipts] table.';
END
ELSE
BEGIN
    PRINT '⚠️ Column [bank_name] already exists in [payment_receipts] table.';
END
GO

