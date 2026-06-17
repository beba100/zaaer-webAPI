-- =============================================
-- Fix Invoice Tax Amounts - Apply to ALL Tenants
-- إصلاح مبالغ ضرائب الفواتير - تطبيق على جميع الفنادق
-- Purpose:
--   Recalculate subtotal, vat_amount, lodging_tax_amount from total_amount
--   and force lodging_tax_amount = 0 when lodging tax is disabled in dbo.taxes
-- Author: System
-- Date: 2026-03-24
-- =============================================
-- IMPORTANT:
-- - Run from master DB (default: db29328)
-- - Processes all active tenants in dbo.Tenants
-- - Safe to run multiple times (idempotent logic)
-- =============================================

USE [db32357_MasterDB];
GO

PRINT '========================================';
PRINT 'Starting invoice tax fix for ALL tenants...';
PRINT '========================================';
GO

DECLARE @DatabaseName NVARCHAR(255);
DECLARE @TenantCode NVARCHAR(100);
DECLARE @Sql NVARCHAR(MAX);

DECLARE @TenantCount INT = 0;
DECLARE @SuccessCount INT = 0;
DECLARE @ErrorCount INT = 0;
DECLARE @TotalUpdated INT = 0;

DECLARE tenant_cursor CURSOR FOR
SELECT [DatabaseName], [Code]
FROM [dbo].[Tenants]
WHERE [IsActive] = 1
  AND [DatabaseName] IS NOT NULL
  AND LTRIM(RTRIM([DatabaseName])) <> ''
ORDER BY [Code];

OPEN tenant_cursor;
FETCH NEXT FROM tenant_cursor INTO @DatabaseName, @TenantCode;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @TenantCount += 1;

    PRINT '';
    PRINT '========================================';
    PRINT 'Processing Tenant: ' + ISNULL(@TenantCode, '(no-code)') + ' | DB: ' + @DatabaseName;
    PRINT '========================================';

    BEGIN TRY
        SET @Sql = N'
IF OBJECT_ID(''' + QUOTENAME(@DatabaseName) + N'.[dbo].[invoices]'') IS NULL
BEGIN
    PRINT ''⚠️ Skipped: invoices table not found in ' + ISNULL(@TenantCode, N'(no-code)') + N'.'';
    SELECT 0 AS UpdatedRows;
    RETURN;
END;

IF OBJECT_ID(''' + QUOTENAME(@DatabaseName) + N'.[dbo].[taxes]'') IS NULL
BEGIN
    PRINT ''⚠️ Skipped: taxes table not found in ' + ISNULL(@TenantCode, N'(no-code)') + N'.'';
    SELECT 0 AS UpdatedRows;
    RETURN;
END;

;WITH LodgingTaxStatus AS
(
    SELECT
        t.hotel_id,
        MAX(CASE WHEN ISNULL(t.enabled, 0) = 1 THEN 1 ELSE 0 END) AS has_enabled,
        MAX(CASE WHEN ISNULL(t.enabled, 0) = 0 THEN 1 ELSE 0 END) AS has_disabled
    FROM ' + QUOTENAME(@DatabaseName) + N'.[dbo].[taxes] t
    WHERE
        LOWER(ISNULL(t.tax_type, '''')) IN (''ewa'', ''lodging'', ''lodging_tax'', ''lodgingtax'')
        OR LOWER(ISNULL(t.tax_name, '''')) LIKE ''%ewa%''
        OR LOWER(ISNULL(t.tax_name, '''')) LIKE ''%lodging%''
    GROUP BY t.hotel_id
),
VatRateByHotel AS
(
    SELECT
        t.hotel_id,
        MAX(CASE WHEN ISNULL(t.enabled, 0) = 1 THEN t.tax_rate END) AS vat_rate
    FROM ' + QUOTENAME(@DatabaseName) + N'.[dbo].[taxes] t
    WHERE
        LOWER(ISNULL(t.tax_type, '''')) = ''vat''
        OR LOWER(ISNULL(t.tax_name, '''')) LIKE ''%vat%''
    GROUP BY t.hotel_id
),
LodgingRateByHotel AS
(
    SELECT
        t.hotel_id,
        MAX(CASE WHEN ISNULL(t.enabled, 0) = 1 THEN t.tax_rate END) AS lodging_rate
    FROM ' + QUOTENAME(@DatabaseName) + N'.[dbo].[taxes] t
    WHERE
        LOWER(ISNULL(t.tax_type, '''')) IN (''ewa'', ''lodging'', ''lodging_tax'', ''lodgingtax'')
        OR LOWER(ISNULL(t.tax_name, '''')) LIKE ''%ewa%''
        OR LOWER(ISNULL(t.tax_name, '''')) LIKE ''%lodging%''
    GROUP BY t.hotel_id
),
Calc AS
(
    SELECT
        i.invoice_id,
        i.hotel_id,
        i.total_amount,
        i.subtotal AS current_subtotal,
        i.vat_amount AS current_vat_amount,
        i.lodging_tax_amount AS current_lodging_tax_amount,

        CAST(
            ISNULL(NULLIF(i.vat_rate, 0), ISNULL(vr.vat_rate, 0))
            AS DECIMAL(18,6)
        ) AS effective_vat_rate,

        CAST(
            CASE
                WHEN lts.has_enabled = 1 THEN ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
                WHEN lts.has_enabled = 0 AND lts.has_disabled = 1 THEN 0
                ELSE ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
            END
            AS DECIMAL(18,6)
        ) AS effective_lodging_rate,

        CASE
            WHEN ISNULL(i.total_amount, 0) = 0 THEN CAST(0 AS DECIMAL(18,6))
            ELSE
                CAST(i.total_amount AS DECIMAL(18,6))
                /
                NULLIF(
                    1
                    + (ISNULL(NULLIF(i.vat_rate, 0), ISNULL(vr.vat_rate, 0)) / 100.0)
                    + (
                        CASE
                            WHEN lts.has_enabled = 1 THEN ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
                            WHEN lts.has_enabled = 0 AND lts.has_disabled = 1 THEN 0
                            ELSE ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
                        END / 100.0
                    ),
                    0
                )
        END AS base_subtotal_raw,

        CASE
            WHEN lts.has_enabled = 0 AND lts.has_disabled = 1 THEN 1
            ELSE 0
        END AS lodging_forced_zero
    FROM ' + QUOTENAME(@DatabaseName) + N'.[dbo].[invoices] i
    LEFT JOIN LodgingTaxStatus lts ON lts.hotel_id = i.hotel_id
    LEFT JOIN VatRateByHotel vr ON vr.hotel_id = i.hotel_id
    LEFT JOIN LodgingRateByHotel lr ON lr.hotel_id = i.hotel_id
    WHERE i.total_amount IS NOT NULL
),
FinalCalc AS
(
    SELECT
        c.invoice_id,
        ROUND(c.base_subtotal_raw, 2) AS rounded_subtotal,
        ROUND(ROUND(c.base_subtotal_raw, 2) * (c.effective_vat_rate / 100.0), 2) AS new_vat_amount,
        CASE
            WHEN c.lodging_forced_zero = 1 THEN CAST(0 AS DECIMAL(18,2))
            ELSE ROUND(ROUND(c.base_subtotal_raw, 2) * (c.effective_lodging_rate / 100.0), 2)
        END AS new_lodging_tax_amount
    FROM Calc c
),
FixValues AS
(
    SELECT
        i.invoice_id,
        CAST(ROUND(i.total_amount - f.new_vat_amount - f.new_lodging_tax_amount, 2) AS DECIMAL(12,2)) AS new_subtotal,
        CAST(f.new_vat_amount AS DECIMAL(12,2)) AS new_vat_amount,
        CAST(f.new_lodging_tax_amount AS DECIMAL(12,2)) AS new_lodging_tax_amount
    FROM ' + QUOTENAME(@DatabaseName) + N'.[dbo].[invoices] i
    INNER JOIN FinalCalc f ON f.invoice_id = i.invoice_id
    WHERE
        ABS(ISNULL(i.subtotal, 0) - ROUND(i.total_amount - f.new_vat_amount - f.new_lodging_tax_amount, 2)) > 0.01
        OR ABS(ISNULL(i.vat_amount, 0) - f.new_vat_amount) > 0.01
        OR ABS(ISNULL(i.lodging_tax_amount, 0) - f.new_lodging_tax_amount) > 0.01
        OR (
            ISNULL(i.lodging_tax_amount, 0) <> 0
            AND EXISTS
            (
                SELECT 1
                FROM LodgingTaxStatus x
                WHERE x.hotel_id = i.hotel_id
                  AND x.has_enabled = 0
                  AND x.has_disabled = 1
            )
        )
)
UPDATE i
SET
    i.subtotal = f.new_subtotal,
    i.vat_amount = f.new_vat_amount,
    i.lodging_tax_amount = f.new_lodging_tax_amount
FROM ' + QUOTENAME(@DatabaseName) + N'.[dbo].[invoices] i
INNER JOIN FixValues f ON f.invoice_id = i.invoice_id;

SELECT @@ROWCOUNT AS UpdatedRows;
';

        DECLARE @Result TABLE (UpdatedRows INT);
        INSERT INTO @Result (UpdatedRows)
        EXEC sp_executesql @Sql;

        DECLARE @UpdatedRows INT = ISNULL((SELECT TOP 1 UpdatedRows FROM @Result), 0);
        SET @TotalUpdated += @UpdatedRows;
        SET @SuccessCount += 1;

        PRINT '✅ Tenant processed successfully. Updated rows: ' + CAST(@UpdatedRows AS NVARCHAR(20));
    END TRY
    BEGIN CATCH
        SET @ErrorCount += 1;
        PRINT '❌ ERROR processing tenant: ' + ISNULL(@TenantCode, '(no-code)');
        PRINT '   DB: ' + @DatabaseName;
        PRINT '   Error: ' + ERROR_MESSAGE();
    END CATCH;

    FETCH NEXT FROM tenant_cursor INTO @DatabaseName, @TenantCode;
END;

CLOSE tenant_cursor;
DEALLOCATE tenant_cursor;

PRINT '';
PRINT '========================================';
PRINT 'Invoice Tax Fix Summary';
PRINT '========================================';
PRINT 'Total Tenants Processed: ' + CAST(@TenantCount AS NVARCHAR(20));
PRINT 'Successfully Processed: ' + CAST(@SuccessCount AS NVARCHAR(20));
PRINT 'Errors: ' + CAST(@ErrorCount AS NVARCHAR(20));
PRINT 'Total Updated Invoice Rows: ' + CAST(@TotalUpdated AS NVARCHAR(20));
PRINT '========================================';
GO

-- Optional verification query (run inside a tenant DB):
-- SELECT TOP 200 invoice_id, hotel_id, subtotal, vat_rate, vat_amount, lodging_tax_rate, lodging_tax_amount, total_amount
-- FROM dbo.invoices
-- ORDER BY invoice_id DESC;
