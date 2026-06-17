-- =============================================
-- Performance Indexes for Payment Allocation
-- =============================================
-- This script adds indexes to improve performance of auto-linking queries
-- =============================================

-- Index for GetUnallocatedReceiptsAsync query optimization
-- Covers: HotelId, ReservationId, UnallocatedAmount, ReceiptStatus
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceipts_UnallocatedLookup' AND object_id = OBJECT_ID('dbo.payment_receipts'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceipts_UnallocatedLookup]
    ON [dbo].[payment_receipts] ([hotel_id], [reservation_id], [receipt_status], [unallocated_amount])
    INCLUDE ([receipt_id], [customer_id], [amount_paid], [allocated_amount], [receipt_date])
    WHERE [receipt_status] = 'active' AND ([unallocated_amount] > 0 OR [unallocated_amount] IS NULL);
    
    PRINT 'Index [IX_PaymentReceipts_UnallocatedLookup] created successfully';
END
ELSE
BEGIN
    PRINT 'Index [IX_PaymentReceipts_UnallocatedLookup] already exists';
END
GO

-- Index for AutoLinkReceiptToInvoiceAsync query optimization
-- Covers: HotelId, CustomerId, ReservationId, PaymentStatus, TotalAmount
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Invoices_UnpaidLookup' AND object_id = OBJECT_ID('dbo.invoices'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoices_UnpaidLookup]
    ON [dbo].[invoices] ([hotel_id], [customer_id], [reservation_id], [payment_status], [total_amount])
    INCLUDE ([invoice_id], [amount_paid], [invoice_date])
    WHERE [payment_status] IN ('unpaid', 'partially_paid') AND [total_amount] > 0;
    
    PRINT 'Index [IX_Invoices_UnpaidLookup] created successfully';
END
ELSE
BEGIN
    PRINT 'Index [IX_Invoices_UnpaidLookup] already exists';
END
GO

-- Index for customer_id filtering in GetUnallocatedReceiptsAsync
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_PaymentReceipts_CustomerId_Unallocated' AND object_id = OBJECT_ID('dbo.payment_receipts'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PaymentReceipts_CustomerId_Unallocated]
    ON [dbo].[payment_receipts] ([customer_id], [hotel_id], [unallocated_amount])
    INCLUDE ([receipt_id], [receipt_date], [amount_paid], [allocated_amount])
    WHERE [receipt_status] = 'active' AND ([unallocated_amount] > 0 OR [unallocated_amount] IS NULL);
    
    PRINT 'Index [IX_PaymentReceipts_CustomerId_Unallocated] created successfully';
END
ELSE
BEGIN
    PRINT 'Index [IX_PaymentReceipts_CustomerId_Unallocated] already exists';
END
GO

PRINT 'All performance indexes created successfully!';
GO
