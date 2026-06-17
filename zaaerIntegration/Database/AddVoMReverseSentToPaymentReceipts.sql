-- =============================================
-- Add VoM Reverse Sent Flag to payment_receipts
-- ---------------------------------------------
-- Purpose:
--   Track whether a reversing journal entry has been sent to VoM
--   for cancelled payment receipts.
--
--   - Only receipts with:
--       receipt_status = 'cancelled'
--       status_vom     = 'sent'
--       vom_reverse_sent = 0
--     should be considered for reverse entries.
--
-- Usage:
--   Run this script on EACH tenant database.
--
-- Safety:
--   - Script is idempotent: checks column existence before adding.
-- =============================================

IF COL_LENGTH('payment_receipts', 'vom_reverse_sent') IS NULL
BEGIN
    PRINT 'Adding column [vom_reverse_sent] to [payment_receipts]...';

    ALTER TABLE payment_receipts
    ADD vom_reverse_sent BIT NOT NULL CONSTRAINT DF_payment_receipts_vom_reverse_sent DEFAULT (0);

    PRINT 'Column [vom_reverse_sent] added successfully.';
END
ELSE
BEGIN
    PRINT 'Column [vom_reverse_sent] already exists on [payment_receipts]. Skipping.';
END

GO


