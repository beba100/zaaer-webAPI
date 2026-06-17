-- Links resort ticket order cancellation refund disbursement (payment_refund).
IF COL_LENGTH('dbo.resort_ticket_orders', 'refund_receipt_id') IS NULL
BEGIN
    ALTER TABLE dbo.resort_ticket_orders ADD refund_receipt_id INT NULL;
    PRINT 'Added resort_ticket_orders.refund_receipt_id';
END
GO
