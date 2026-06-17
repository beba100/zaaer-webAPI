-- =============================================
-- Update Reservations from Customer Accounts
-- تحديث الحجوزات من حسابات العملاء
-- =============================================
-- Purpose: Update amount_paid and balance_amount in reservations table
--          from customer_accounts.total_credit for all existing reservations
-- الغرض: تحديث amount_paid و balance_amount في جدول reservations
--        من customer_accounts.total_credit لجميع الحجوزات الموجودة
--
-- Usage: Run this script on each tenant database
-- الاستخدام: قم بتشغيل هذا السكريبت على كل قاعدة بيانات tenant

PRINT '========================================';
PRINT 'Updating Reservations from Customer Accounts';
PRINT '========================================';
PRINT '';

-- Step 1: Update reservations that have customer_accounts
-- الخطوة 1: تحديث الحجوزات التي تحتوي على customer_accounts
UPDATE r
SET 
    r.amount_paid = ISNULL(ca.total_credit, 0.00),
    r.balance_amount = COALESCE(r.total_amount, r.subtotal, 0.00) - ISNULL(ca.total_credit, 0.00)
FROM 
    [dbo].[reservations] r
    INNER JOIN [dbo].[customer_accounts] ca 
        ON (ca.reservation_id = r.reservation_id OR ca.reservation_id = r.zaaer_id)
        AND ca.reservation_id IS NOT NULL
WHERE 
    -- Only update if values are different (avoid unnecessary writes)
    -- تحديث فقط إذا كانت القيم مختلفة (تجنب الكتابات غير الضرورية)
    (r.amount_paid IS NULL OR ABS(r.amount_paid - ISNULL(ca.total_credit, 0.00)) > 0.01)
    OR (r.balance_amount IS NULL OR ABS(r.balance_amount - (COALESCE(r.total_amount, r.subtotal, 0.00) - ISNULL(ca.total_credit, 0.00))) > 0.01);

DECLARE @UpdatedCount INT = @@ROWCOUNT;
PRINT '✓ Updated ' + CAST(@UpdatedCount AS VARCHAR(10)) + ' reservations from customer_accounts';

-- Step 2: Update reservations that don't have customer_accounts (set to 0)
-- الخطوة 2: تحديث الحجوزات التي لا تحتوي على customer_accounts (تعيينها إلى 0)
UPDATE r
SET 
    r.amount_paid = 0.00,
    r.balance_amount = COALESCE(r.total_amount, r.subtotal, 0.00)
FROM 
    [dbo].[reservations] r
WHERE 
    -- Reservations without customer_accounts
    -- الحجوزات التي لا تحتوي على customer_accounts
    NOT EXISTS (
        SELECT 1 
        FROM [dbo].[customer_accounts] ca 
        WHERE (ca.reservation_id = r.reservation_id OR ca.reservation_id = r.zaaer_id)
        AND ca.reservation_id IS NOT NULL
    )
    -- Only update if amount_paid is not already 0
    -- تحديث فقط إذا كان amount_paid ليس 0 بالفعل
    AND (r.amount_paid IS NULL OR r.amount_paid != 0.00);

DECLARE @ZeroCount INT = @@ROWCOUNT;
PRINT '✓ Updated ' + CAST(@ZeroCount AS VARCHAR(10)) + ' reservations without customer_accounts (set to 0)';

PRINT '';
PRINT '========================================';
PRINT 'Update completed successfully!';
PRINT 'Total reservations updated: ' + CAST((@UpdatedCount + @ZeroCount) AS VARCHAR(10));
PRINT '========================================';

