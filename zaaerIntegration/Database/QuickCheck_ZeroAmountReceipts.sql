-- =============================================
-- Quick Check: Zero Amount Receipt REC0002
-- =============================================
-- Quick investigation query for immediate results
-- Run this first to get a quick overview
-- =============================================

-- Quick overview of REC0002
SELECT 
    'Receipt Details' AS Section,
    receipt_id,
    receipt_no,
    amount_paid,
    receipt_type,
    receipt_status,
    payment_method,
    hotel_id,
    reservation_id,
    invoice_id,
    order_id,
    customer_id,
    receipt_date,
    created_at,
    zaaer_id,
    status_vom,
    vom_error,
    allocated_amount,
    unallocated_amount
FROM [dbo].[payment_receipts]
WHERE receipt_no = 'REC0002';

-- Check if it has invoice mappings
SELECT 
    'Invoice Mappings' AS Section,
    irm.mapping_id,
    irm.invoice_id,
    irm.allocated_amount,
    i.invoice_no,
    i.total_amount AS invoice_total
FROM [dbo].[invoice_receipt_mappings] irm
INNER JOIN [dbo].[payment_receipts] pr ON irm.receipt_id = pr.receipt_id
LEFT JOIN [dbo].[invoices] i ON irm.invoice_id = i.invoice_id
WHERE pr.receipt_no = 'REC0002';

-- Check VoM sync attempts
SELECT 
    'VoM Sync History' AS Section,
    prje.id,
    prje.status,
    prje.error_message,
    prje.retry_count,
    prje.created_at
FROM [dbo].[payment_receipt_journal_entries] prje
INNER JOIN [dbo].[payment_receipts] pr ON prje.receipt_id = pr.receipt_id
WHERE pr.receipt_no = 'REC0002'
ORDER BY prje.created_at DESC;

-- Find all zero amount receipts
SELECT 
    'All Zero Amount Receipts' AS Section,
    receipt_no,
    amount_paid,
    receipt_status,
    status_vom,
    created_at,
    zaaer_id,
    CASE 
        WHEN receipt_status = 'cancelled' THEN 'Cancelled - OK'
        WHEN receipt_status = 'active' THEN '⚠️ Active with Zero - PROBLEM'
        ELSE 'Check'
    END AS Issue
FROM [dbo].[payment_receipts]
WHERE amount_paid = 0
ORDER BY created_at DESC;

