-- =============================================
-- Backfill Script: Auto-link Existing Invoices and Receipts
-- =============================================
-- This script attempts to link existing unlinked invoices and receipts
-- based on matching amounts, reservation_id, and customer_id
-- =============================================
-- IMPORTANT: Review and test on a copy of production data first!
-- =============================================

PRINT 'Starting backfill: Auto-linking existing invoices and receipts...';
PRINT '';

-- Step 1: Link receipts to invoices where amount matches exactly
-- Strategy: Find receipts with unallocated_amount > 0 and invoices with amount_remaining > 0
-- Match by: reservation_id, customer_id, and exact amount match

PRINT 'Step 1: Linking receipts to invoices with exact amount match...';

-- Create temporary table to store matches
IF OBJECT_ID('tempdb..#ExactMatches') IS NOT NULL DROP TABLE #ExactMatches;

CREATE TABLE #ExactMatches (
    InvoiceId INT,
    ReceiptId INT,
    Amount DECIMAL(18,2),
    ReservationId INT,
    CustomerId INT,
    HotelId INT
);

-- Find exact matches: receipt amount = invoice remaining amount
INSERT INTO #ExactMatches (InvoiceId, ReceiptId, Amount, ReservationId, CustomerId, HotelId)
SELECT 
    i.invoice_id,
    r.receipt_id,
    r.unallocated_amount,
    i.reservation_id,
    i.customer_id,
    i.hotel_id
FROM invoices i
INNER JOIN payment_receipts r ON 
    i.hotel_id = r.hotel_id AND
    i.reservation_id = r.reservation_id AND
    i.customer_id = r.customer_id AND
    (i.payment_status = 'unpaid' OR i.payment_status = 'partially_paid') AND
    (r.unallocated_amount > 0 OR (r.unallocated_amount IS NULL AND r.amount_paid > 0)) AND
    r.receipt_status = 'active' AND
    r.customer_id > 0 AND
    -- Exact match: invoice remaining = receipt unallocated
    ABS((i.total_amount - ISNULL(i.amount_paid, 0)) - ISNULL(r.unallocated_amount, r.amount_paid)) < 0.01
WHERE 
    i.total_amount > 0 AND
    (i.total_amount - ISNULL(i.amount_paid, 0)) > 0
    -- Exclude receipts already linked via invoice_id
    AND (r.invoice_id IS NULL OR r.invoice_id = 0)
    -- Exclude receipts already linked via mappings
    AND NOT EXISTS (
        SELECT 1 FROM invoice_receipt_mappings irm 
        WHERE irm.receipt_id = r.receipt_id
    );

PRINT 'Found ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' exact matches';
PRINT '';

-- Step 2: Create mappings for exact matches
PRINT 'Step 2: Creating invoice_receipt_mappings for exact matches...';

INSERT INTO invoice_receipt_mappings (invoice_id, receipt_id, allocated_amount, mapping_date, created_by, created_at)
SELECT 
    InvoiceId,
    ReceiptId,
    Amount,
    GETDATE(),
    NULL, -- created_by will be NULL for backfill
    GETDATE()
FROM #ExactMatches;

PRINT 'Created ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' mappings';
PRINT '';

-- Step 3: Update payment_receipts allocation fields
PRINT 'Step 3: Updating payment_receipts allocation fields...';

UPDATE r
SET 
    r.allocated_amount = ISNULL(r.allocated_amount, 0) + m.Amount,
    r.unallocated_amount = ISNULL(r.unallocated_amount, r.amount_paid) - m.Amount,
    r.is_fully_allocated = CASE 
        WHEN (ISNULL(r.unallocated_amount, r.amount_paid) - m.Amount) <= 0 THEN 1 
        ELSE 0 
    END,
    -- For backward compatibility, set invoice_id if not already set
    r.invoice_id = CASE 
        WHEN r.invoice_id IS NULL THEN m.InvoiceId 
        ELSE r.invoice_id 
    END
FROM payment_receipts r
INNER JOIN #ExactMatches m ON r.receipt_id = m.ReceiptId;

PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' payment_receipts';
PRINT '';

-- Step 4: Update invoices payment status and amounts
PRINT 'Step 4: Updating invoices payment status and amounts...';

UPDATE i
SET 
    i.amount_paid = ISNULL(i.amount_paid, 0) + m.TotalAllocated,
    i.amount_remaining = i.total_amount - (ISNULL(i.amount_paid, 0) + m.TotalAllocated),
    i.payment_status = CASE 
        WHEN (i.total_amount - (ISNULL(i.amount_paid, 0) + m.TotalAllocated)) <= 0 THEN 'paid'
        WHEN (ISNULL(i.amount_paid, 0) + m.TotalAllocated) > 0 THEN 'partially_paid'
        ELSE i.payment_status
    END
FROM invoices i
INNER JOIN (
    SELECT InvoiceId, SUM(Amount) AS TotalAllocated
    FROM #ExactMatches
    GROUP BY InvoiceId
) m ON i.invoice_id = m.InvoiceId;

PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' invoices';
PRINT '';

-- Step 5: Report summary
PRINT 'Step 5: Generating summary report...';
PRINT '';

SELECT 
    'Summary' AS ReportType,
    COUNT(DISTINCT InvoiceId) AS InvoicesLinked,
    COUNT(DISTINCT ReceiptId) AS ReceiptsLinked,
    SUM(Amount) AS TotalAmountLinked
FROM #ExactMatches;

-- Show details of linked invoices
PRINT '';
PRINT 'Linked Invoices:';
SELECT 
    i.invoice_id,
    i.invoice_no,
    i.total_amount,
    i.amount_paid AS AmountPaidAfter,
    i.amount_remaining AS AmountRemainingAfter,
    i.payment_status AS PaymentStatusAfter,
    COUNT(m.receipt_id) AS LinkedReceiptsCount,
    SUM(m.allocated_amount) AS TotalAllocated
FROM invoices i
INNER JOIN invoice_receipt_mappings m ON i.invoice_id = m.invoice_id
WHERE m.created_at >= DATEADD(MINUTE, -5, GETDATE()) -- Only show recently created mappings
GROUP BY i.invoice_id, i.invoice_no, i.total_amount, i.amount_paid, i.amount_remaining, i.payment_status
ORDER BY i.invoice_id;

-- Show details of linked receipts
PRINT '';
PRINT 'Linked Receipts:';
SELECT 
    r.receipt_id,
    r.receipt_no,
    r.amount_paid,
    r.allocated_amount AS AllocatedAmountAfter,
    r.unallocated_amount AS UnallocatedAmountAfter,
    r.is_fully_allocated AS IsFullyAllocatedAfter,
    COUNT(m.invoice_id) AS LinkedInvoicesCount
FROM payment_receipts r
INNER JOIN invoice_receipt_mappings m ON r.receipt_id = m.receipt_id
WHERE m.created_at >= DATEADD(MINUTE, -5, GETDATE()) -- Only show recently created mappings
GROUP BY r.receipt_id, r.receipt_no, r.amount_paid, r.allocated_amount, r.unallocated_amount, r.is_fully_allocated
ORDER BY r.receipt_id;

-- Cleanup
DROP TABLE #ExactMatches;

PRINT '';
PRINT 'Backfill completed successfully!';
PRINT '';
PRINT 'NOTE: This script only handles exact matches.';
PRINT 'For partial matches or complex scenarios, use the application''s auto-linking feature.';
PRINT '';

GO
