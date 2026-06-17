-- =============================================
-- Investigation Script: Zero Amount Payment Receipts
-- =============================================
-- Purpose: Investigate payment receipts with zero or invalid amounts
-- This script helps identify why REC0002 (or any receipt) has AmountPaid = 0
-- =============================================
-- ⚠️ IMPORTANT: Run this on the TENANT DATABASE where the issue occurs
-- =============================================

-- USE [YOUR_DATABASE_NAME_HERE]; -- Replace with actual TENANT database name
-- GO

PRINT '========================================';
PRINT 'Investigation: Zero Amount Payment Receipts';
PRINT '========================================';
PRINT '';

-- =============================================
-- Step 1: Find the specific receipt REC0002
-- =============================================
PRINT 'Step 1: Details of Receipt REC0002';
PRINT '----------------------------------------';

SELECT 
    receipt_id,
    receipt_no,
    hotel_id,
    reservation_id,
    invoice_id,
    order_id,
    customer_id,
    receipt_date,
    receipt_type,
    voucher_code,
    amount_paid,
    payment_method_id,
    payment_method,
    bank_id,
    bank_name,
    transaction_no,
    notes,
    receipt_status,
    created_by,
    created_at,
    zaaer_id,
    status_vom,
    vom_sent_at,
    vom_error,
    vom_retry_count,
    allocated_amount,
    unallocated_amount,
    is_fully_allocated,
    revenue_category
FROM [dbo].[payment_receipts]
WHERE receipt_no = 'REC0002';

PRINT '';
PRINT '';

-- =============================================
-- Step 2: Check if receipt was created from Zaaer integration
-- =============================================
PRINT 'Step 2: Check Zaaer Integration Data';
PRINT '----------------------------------------';

SELECT 
    pr.receipt_id,
    pr.receipt_no,
    pr.zaaer_id,
    pr.amount_paid,
    pr.created_at,
    pr.status_vom,
    pr.vom_error,
    CASE 
        WHEN pr.zaaer_id IS NOT NULL THEN 'Yes - Created from Zaaer'
        ELSE 'No - Created Manually'
    END AS Source
FROM [dbo].[payment_receipts] pr
WHERE pr.receipt_no = 'REC0002';

PRINT '';
PRINT '';

-- =============================================
-- Step 3: Check related invoice mappings
-- =============================================
PRINT 'Step 3: Check Invoice-Receipt Mappings';
PRINT '----------------------------------------';

SELECT 
    irm.mapping_id,
    irm.receipt_id,
    irm.invoice_id,
    irm.allocated_amount,
    irm.mapping_date,
    pr.receipt_no,
    pr.amount_paid AS receipt_amount_paid,
    i.invoice_no,
    i.total_amount AS invoice_total_amount,
    i.amount_paid AS invoice_amount_paid
FROM [dbo].[invoice_receipt_mappings] irm
INNER JOIN [dbo].[payment_receipts] pr ON irm.receipt_id = pr.receipt_id
LEFT JOIN [dbo].[invoices] i ON irm.invoice_id = i.invoice_id
WHERE pr.receipt_no = 'REC0002';

PRINT '';
PRINT '';

-- =============================================
-- Step 4: Check related invoice (if exists)
-- =============================================
PRINT 'Step 4: Check Related Invoice';
PRINT '----------------------------------------';

SELECT 
    i.invoice_id,
    i.invoice_no,
    i.hotel_id,
    i.total_amount,
    i.amount_paid,
    i.subtotal,
    i.vat_amount,
    i.lodging_tax_amount,
    i.invoice_date,
    i.invoice_type,
    i.status,
    i.status_vom
FROM [dbo].[invoices] i
WHERE i.invoice_id IN (
    SELECT invoice_id 
    FROM [dbo].[payment_receipts] 
    WHERE receipt_no = 'REC0002' AND invoice_id IS NOT NULL
);

PRINT '';
PRINT '';

-- =============================================
-- Step 5: Check related reservation (if exists)
-- =============================================
PRINT 'Step 5: Check Related Reservation';
PRINT '----------------------------------------';

SELECT 
    r.reservation_id,
    r.reservation_no,
    r.hotel_id,
    r.customer_id,
    r.unit_id,
    r.check_in_date,
    r.check_out_date,
    r.total_amount,
    r.amount_paid,
    r.reservation_status
FROM [dbo].[reservations] r
WHERE r.reservation_id IN (
    SELECT reservation_id 
    FROM [dbo].[payment_receipts] 
    WHERE receipt_no = 'REC0002' AND reservation_id IS NOT NULL
);

PRINT '';
PRINT '';

-- =============================================
-- Step 6: Check related order (if exists)
-- =============================================
PRINT 'Step 6: Check Related Order';
PRINT '----------------------------------------';

SELECT 
    o.order_id,
    o.order_no,
    o.hotel_id,
    o.customer_id,
    o.total_amount,
    o.amount_paid,
    o.order_date,
    o.order_status
FROM [dbo].[orders] o
WHERE o.order_id IN (
    SELECT order_id 
    FROM [dbo].[payment_receipts] 
    WHERE receipt_no = 'REC0002' AND order_id IS NOT NULL
);

PRINT '';
PRINT '';

-- =============================================
-- Step 7: Check VoM sync status and errors
-- =============================================
PRINT 'Step 7: Check VoM Sync Status';
PRINT '----------------------------------------';

SELECT 
    receipt_no,
    status_vom,
    vom_sent_at,
    vom_error,
    vom_retry_count,
    vom_journal_entry_id,
    vom_reverse_sent,
    CASE 
        WHEN status_vom = 'pending' AND amount_paid = 0 THEN '⚠️ Pending with Zero Amount'
        WHEN status_vom = 'failed' AND amount_paid = 0 THEN '❌ Failed with Zero Amount'
        WHEN status_vom = 'sent' AND amount_paid = 0 THEN '⚠️ Sent with Zero Amount (Should not happen)'
        ELSE 'OK'
    END AS StatusAnalysis
FROM [dbo].[payment_receipts]
WHERE receipt_no = 'REC0002';

PRINT '';
PRINT '';

-- =============================================
-- Step 8: Check payment receipt journal entries
-- =============================================
PRINT 'Step 8: Check Journal Entry History';
PRINT '----------------------------------------';

SELECT 
    prje.id,
    prje.receipt_id,
    prje.journal_entry_code,
    prje.journal_date,
    prje.total_debit,
    prje.total_credit,
    prje.status,
    prje.error_message,
    prje.retry_count,
    prje.last_retry_at,
    prje.created_at
FROM [dbo].[payment_receipt_journal_entries] prje
INNER JOIN [dbo].[payment_receipts] pr ON prje.receipt_id = pr.receipt_id
WHERE pr.receipt_no = 'REC0002'
ORDER BY prje.created_at DESC;

PRINT '';
PRINT '';

-- =============================================
-- Step 9: Find ALL receipts with zero amount (potential data quality issues)
-- =============================================
PRINT 'Step 9: Find ALL Receipts with Zero Amount';
PRINT '----------------------------------------';

SELECT 
    receipt_id,
    receipt_no,
    hotel_id,
    receipt_type,
    amount_paid,
    payment_method,
    receipt_status,
    status_vom,
    created_at,
    zaaer_id,
    CASE 
        WHEN zaaer_id IS NOT NULL THEN 'From Zaaer'
        ELSE 'Manual'
    END AS Source,
    CASE 
        WHEN receipt_status = 'cancelled' THEN 'Cancelled - OK'
        WHEN receipt_status = 'active' AND amount_paid = 0 THEN '⚠️ Active with Zero Amount - PROBLEM'
        ELSE 'Check'
    END AS IssueType
FROM [dbo].[payment_receipts]
WHERE amount_paid = 0 OR amount_paid IS NULL
ORDER BY created_at DESC;

PRINT '';
PRINT '';

-- =============================================
-- Step 10: Check for data inconsistencies
-- =============================================
PRINT 'Step 10: Data Consistency Check';
PRINT '----------------------------------------';

SELECT 
    pr.receipt_no,
    pr.amount_paid,
    pr.allocated_amount,
    pr.unallocated_amount,
    pr.is_fully_allocated,
    ISNULL(SUM(irm.allocated_amount), 0) AS total_mapped_amount,
    CASE 
        WHEN pr.amount_paid = 0 AND pr.allocated_amount > 0 THEN '⚠️ Zero amount but has allocations'
        WHEN pr.amount_paid = 0 AND ISNULL(SUM(irm.allocated_amount), 0) > 0 THEN '⚠️ Zero amount but has mappings'
        WHEN pr.amount_paid != ISNULL(SUM(irm.allocated_amount), 0) + ISNULL(pr.unallocated_amount, 0) THEN '⚠️ Amount mismatch'
        ELSE 'OK'
    END AS ConsistencyCheck
FROM [dbo].[payment_receipts] pr
LEFT JOIN [dbo].[invoice_receipt_mappings] irm ON pr.receipt_id = irm.receipt_id
WHERE pr.receipt_no = 'REC0002'
GROUP BY 
    pr.receipt_no,
    pr.amount_paid,
    pr.allocated_amount,
    pr.unallocated_amount,
    pr.is_fully_allocated;

PRINT '';
PRINT '';

-- =============================================
-- Step 11: Check creation history and audit trail
-- =============================================
PRINT 'Step 11: Creation History';
PRINT '----------------------------------------';

SELECT 
    receipt_no,
    amount_paid,
    created_at,
    created_by,
    receipt_date,
    DATEDIFF(day, created_at, GETDATE()) AS days_since_creation,
    CASE 
        WHEN created_at = receipt_date THEN 'Same day'
        ELSE 'Different dates'
    END AS DateComparison
FROM [dbo].[payment_receipts]
WHERE receipt_no = 'REC0002';

PRINT '';
PRINT '';

-- =============================================
-- Step 12: Summary and Recommendations
-- =============================================
PRINT '========================================';
PRINT 'Summary and Recommendations';
PRINT '========================================';
PRINT '';

DECLARE @ReceiptExists BIT = 0;
DECLARE @AmountPaid DECIMAL(12,2);
DECLARE @ReceiptStatus NVARCHAR(50);
DECLARE @StatusVoM NVARCHAR(20);
DECLARE @HasZaaerId BIT = 0;
DECLARE @HasMappings BIT = 0;

SELECT 
    @ReceiptExists = 1,
    @AmountPaid = amount_paid,
    @ReceiptStatus = receipt_status,
    @StatusVoM = status_vom,
    @HasZaaerId = CASE WHEN zaaer_id IS NOT NULL THEN 1 ELSE 0 END
FROM [dbo].[payment_receipts]
WHERE receipt_no = 'REC0002';

SELECT @HasMappings = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
FROM [dbo].[invoice_receipt_mappings] irm
INNER JOIN [dbo].[payment_receipts] pr ON irm.receipt_id = pr.receipt_id
WHERE pr.receipt_no = 'REC0002';

IF @ReceiptExists = 0
BEGIN
    PRINT '❌ Receipt REC0002 does not exist in the database.';
    PRINT '   Check if the receipt number is correct or if it was deleted.';
END
ELSE
BEGIN
    PRINT '✅ Receipt REC0002 found.';
    PRINT '';
    PRINT 'Analysis:';
    PRINT '--------';
    PRINT 'Amount Paid: ' + CAST(@AmountPaid AS NVARCHAR(20));
    PRINT 'Receipt Status: ' + ISNULL(@ReceiptStatus, 'NULL');
    PRINT 'VoM Status: ' + ISNULL(@StatusVoM, 'NULL');
    PRINT 'Source: ' + CASE WHEN @HasZaaerId = 1 THEN 'Zaaer Integration' ELSE 'Manual Entry' END;
    PRINT 'Has Invoice Mappings: ' + CASE WHEN @HasMappings = 1 THEN 'Yes' ELSE 'No' END;
    PRINT '';
    PRINT 'Possible Causes:';
    PRINT '---------------';
    
    IF @AmountPaid = 0
    BEGIN
        PRINT '1. ⚠️ Receipt was created with zero amount (data entry error)';
        PRINT '2. ⚠️ Receipt amount was updated to zero (manual update or bug)';
        
        IF @HasZaaerId = 1
        BEGIN
            PRINT '3. ⚠️ Zaaer integration sent zero amount (check Zaaer system)';
        END
        
        IF @HasMappings = 1
        BEGIN
            PRINT '4. ⚠️ Receipt has invoice mappings but zero amount (data inconsistency)';
        END
        
        IF @ReceiptStatus = 'cancelled'
        BEGIN
            PRINT '5. ✅ Receipt is cancelled - zero amount might be intentional';
        END
        ELSE
        BEGIN
            PRINT '5. ❌ Receipt is active with zero amount - THIS IS A PROBLEM';
        END
    END
    
    PRINT '';
    PRINT 'Recommendations:';
    PRINT '---------------';
    
    IF @AmountPaid = 0 AND @ReceiptStatus = 'active'
    BEGIN
        PRINT '1. ❌ FIX REQUIRED: Active receipt with zero amount should be:';
        PRINT '   - Updated with correct amount, OR';
        PRINT '   - Cancelled if it was created by mistake';
        PRINT '';
        PRINT '2. Check the source system (Zaaer or manual entry) to find the correct amount';
        PRINT '';
        PRINT '3. If from Zaaer, verify the data in Zaaer system matches your database';
        PRINT '';
        PRINT '4. Review the creation logs to understand how this receipt was created';
    END
    ELSE IF @AmountPaid = 0 AND @ReceiptStatus = 'cancelled'
    BEGIN
        PRINT '1. ✅ Receipt is cancelled - zero amount is acceptable';
        PRINT '2. No action needed unless you want to investigate why it was created';
    END
END

PRINT '';
PRINT '========================================';
PRINT 'Investigation Complete';
PRINT '========================================';

