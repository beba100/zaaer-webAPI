-- Add reason and rent period columns to payment_receipts (PMS receipt vouchers)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('payment_receipts') AND name = 'reason')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [reason] NVARCHAR(500) NULL;
    PRINT 'Column [reason] added to [payment_receipts].';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('payment_receipts') AND name = 'receipt_from')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [receipt_from] DATE NULL;
    PRINT 'Column [receipt_from] added to [payment_receipts].';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('payment_receipts') AND name = 'receipt_to')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [receipt_to] DATE NULL;
    PRINT 'Column [receipt_to] added to [payment_receipts].';
END
GO
