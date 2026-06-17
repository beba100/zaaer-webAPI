-- =============================================
-- Migration Script: Invoice-Receipt Many-to-Many Relationship
-- =============================================
-- This script creates the InvoiceReceiptMapping table and adds tracking fields
-- to payment_receipts table to support Many-to-Many relationship between
-- invoices and payment receipts.
--
-- IMPORTANT: This migration maintains backward compatibility with Zaaer integration
-- by keeping the invoice_id column in payment_receipts (nullable).
-- =============================================



-- =============================================
-- Step 1: Add tracking columns to payment_receipts table
-- =============================================
PRINT 'Step 1: Adding tracking columns to payment_receipts table...';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.payment_receipts') AND name = 'allocated_amount')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [allocated_amount] DECIMAL(12,2) DEFAULT 0 NOT NULL;
    PRINT 'Column [allocated_amount] added to [dbo].[payment_receipts]';
END
ELSE
BEGIN
    PRINT 'Column [allocated_amount] already exists in [dbo].[payment_receipts]';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.payment_receipts') AND name = 'unallocated_amount')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [unallocated_amount] DECIMAL(12,2) NULL;
    PRINT 'Column [unallocated_amount] added to [dbo].[payment_receipts]';
END
ELSE
BEGIN
    PRINT 'Column [unallocated_amount] already exists in [dbo].[payment_receipts]';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.payment_receipts') AND name = 'is_fully_allocated')
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ADD [is_fully_allocated] BIT DEFAULT 0 NOT NULL;
    PRINT 'Column [is_fully_allocated] added to [dbo].[payment_receipts]';
END
ELSE
BEGIN
    PRINT 'Column [is_fully_allocated] already exists in [dbo].[payment_receipts]';
END
GO

-- =============================================
-- Step 2: Make invoice_id nullable (if not already)
-- =============================================
PRINT 'Step 2: Making invoice_id nullable for backward compatibility...';

IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID('dbo.payment_receipts') 
           AND name = 'invoice_id' 
           AND is_nullable = 0)
BEGIN
    ALTER TABLE [dbo].[payment_receipts]
    ALTER COLUMN [invoice_id] INT NULL;
    PRINT 'Column [invoice_id] is now nullable in [dbo].[payment_receipts]';
END
ELSE
BEGIN
    PRINT 'Column [invoice_id] is already nullable in [dbo].[payment_receipts]';
END
GO

-- =============================================
-- Step 3: Create InvoiceReceiptMapping table
-- =============================================
PRINT 'Step 3: Creating InvoiceReceiptMapping table...';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[invoice_receipt_mappings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[invoice_receipt_mappings](
        [mapping_id] INT IDENTITY(1,1) NOT NULL,
        [invoice_id] INT NOT NULL,
        [receipt_id] INT NOT NULL,
        [allocated_amount] DECIMAL(12,2) NOT NULL,
        [mapping_date] DATETIME NOT NULL DEFAULT GETDATE(),
        [created_by] INT NULL,
        [created_at] DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [PK_InvoiceReceiptMappings] PRIMARY KEY CLUSTERED ([mapping_id] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];
    
    PRINT 'Table [dbo].[invoice_receipt_mappings] created successfully';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[invoice_receipt_mappings] already exists';
END
GO

-- =============================================
-- Step 4: Add Foreign Key constraints
-- =============================================
PRINT 'Step 4: Adding Foreign Key constraints...';

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_InvoiceReceiptMappings_Invoices')
BEGIN
    ALTER TABLE [dbo].[invoice_receipt_mappings]
    ADD CONSTRAINT [FK_InvoiceReceiptMappings_Invoices]
    FOREIGN KEY([invoice_id])
    REFERENCES [dbo].[invoices] ([invoice_id])
    ON DELETE NO ACTION
    ON UPDATE NO ACTION;
    
    PRINT 'Foreign Key [FK_InvoiceReceiptMappings_Invoices] added';
END
ELSE
BEGIN
    PRINT 'Foreign Key [FK_InvoiceReceiptMappings_Invoices] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_InvoiceReceiptMappings_PaymentReceipts')
BEGIN
    ALTER TABLE [dbo].[invoice_receipt_mappings]
    ADD CONSTRAINT [FK_InvoiceReceiptMappings_PaymentReceipts]
    FOREIGN KEY([receipt_id])
    REFERENCES [dbo].[payment_receipts] ([receipt_id])
    ON DELETE NO ACTION
    ON UPDATE NO ACTION;
    
    PRINT 'Foreign Key [FK_InvoiceReceiptMappings_PaymentReceipts] added';
END
ELSE
BEGIN
    PRINT 'Foreign Key [FK_InvoiceReceiptMappings_PaymentReceipts] already exists';
END
GO

-- =============================================
-- Step 5: Create Indexes for performance
-- =============================================
PRINT 'Step 5: Creating indexes...';

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceReceiptMappings_InvoiceId' AND object_id = OBJECT_ID('dbo.invoice_receipt_mappings'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceReceiptMappings_InvoiceId]
    ON [dbo].[invoice_receipt_mappings] ([invoice_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_InvoiceReceiptMappings_InvoiceId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_InvoiceReceiptMappings_InvoiceId] already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_InvoiceReceiptMappings_ReceiptId' AND object_id = OBJECT_ID('dbo.invoice_receipt_mappings'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_InvoiceReceiptMappings_ReceiptId]
    ON [dbo].[invoice_receipt_mappings] ([receipt_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Index [IX_InvoiceReceiptMappings_ReceiptId] created';
END
ELSE
BEGIN
    PRINT 'Index [IX_InvoiceReceiptMappings_ReceiptId] already exists';
END
GO

-- Unique constraint to prevent duplicate mappings
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_InvoiceReceiptMappings_InvoiceId_ReceiptId' AND object_id = OBJECT_ID('dbo.invoice_receipt_mappings'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceReceiptMappings_InvoiceId_ReceiptId]
    ON [dbo].[invoice_receipt_mappings] ([invoice_id] ASC, [receipt_id] ASC)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY];
    
    PRINT 'Unique Index [UQ_InvoiceReceiptMappings_InvoiceId_ReceiptId] created';
END
ELSE
BEGIN
    PRINT 'Unique Index [UQ_InvoiceReceiptMappings_InvoiceId_ReceiptId] already exists';
END
GO

-- =============================================
-- Step 6: Migrate existing data
-- =============================================
PRINT 'Step 6: Migrating existing data...';

-- For receipts WITH invoice_id (already linked)
PRINT 'Updating receipts with invoice_id...';
UPDATE [dbo].[payment_receipts]
SET 
    [allocated_amount] = [amount_paid],
    [unallocated_amount] = 0,
    [is_fully_allocated] = 1
WHERE [invoice_id] IS NOT NULL;

PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' receipts with invoice_id';

-- Create mapping records for existing relationships
PRINT 'Creating mapping records for existing relationships...';
INSERT INTO [dbo].[invoice_receipt_mappings] ([invoice_id], [receipt_id], [allocated_amount], [mapping_date], [created_at])
SELECT 
    [invoice_id], 
    [receipt_id], 
    [amount_paid], 
    [created_at],
    [created_at]
FROM [dbo].[payment_receipts]
WHERE [invoice_id] IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM [dbo].[invoice_receipt_mappings] 
    WHERE [invoice_receipt_mappings].[invoice_id] = [payment_receipts].[invoice_id]
    AND [invoice_receipt_mappings].[receipt_id] = [payment_receipts].[receipt_id]
);

PRINT 'Created ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' mapping records';

-- For receipts WITHOUT invoice_id (unlinked)
PRINT 'Updating receipts without invoice_id...';
UPDATE [dbo].[payment_receipts]
SET 
    [allocated_amount] = 0,
    [unallocated_amount] = [amount_paid],
    [is_fully_allocated] = 0
WHERE [invoice_id] IS NULL;

PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' receipts without invoice_id';

-- =============================================
-- Step 7: Data validation
-- =============================================
PRINT 'Step 7: Validating data integrity...';

-- Check for any data inconsistencies
DECLARE @InconsistentReceipts INT;
SELECT @InconsistentReceipts = COUNT(*)
FROM [dbo].[payment_receipts]
WHERE ([allocated_amount] + ISNULL([unallocated_amount], 0)) != [amount_paid]
AND [unallocated_amount] IS NOT NULL;

IF @InconsistentReceipts > 0
BEGIN
    PRINT 'WARNING: Found ' + CAST(@InconsistentReceipts AS VARCHAR(10)) + ' receipts with inconsistent allocation amounts!';
    PRINT 'Please review these records manually.';
END
ELSE
BEGIN
    PRINT 'Data validation passed - all receipts have consistent allocation amounts.';
END
GO

-- =============================================
-- Migration Complete
-- =============================================
PRINT '';
PRINT '=============================================';
PRINT 'Migration completed successfully!';
PRINT '=============================================';
PRINT '';
PRINT 'Summary:';
PRINT '- Added tracking columns to payment_receipts';
PRINT '- Created InvoiceReceiptMapping table';
PRINT '- Created indexes for performance';
PRINT '- Migrated existing data';
PRINT '- Validated data integrity';
PRINT '';
PRINT 'IMPORTANT NOTES:';
PRINT '1. The invoice_id column in payment_receipts is kept for backward compatibility with Zaaer';
PRINT '2. New allocations should use InvoiceReceiptMapping table';
PRINT '3. Existing Zaaer integration will continue to work unchanged';
PRINT '';
