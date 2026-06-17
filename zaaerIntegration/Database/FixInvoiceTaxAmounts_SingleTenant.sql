-- =============================================
-- Fix Invoice Tax Amounts - SINGLE TENANT (Safe Test)
-- =============================================

DECLARE @TenantDbName SYSNAME = N'db32415_Dammam1'; -- TODO: change if needed
DECLARE @DoUpdate BIT = 0; -- 0 = preview only, 1 = execute update

IF DB_ID(@TenantDbName) IS NULL
BEGIN
    RAISERROR('Database not found: %s', 16, 1, @TenantDbName);
    RETURN;
END;

DECLARE @Sql NVARCHAR(MAX) = N'
USE ' + QUOTENAME(@TenantDbName) + N';

IF OBJECT_ID(N''[dbo].[invoices]'') IS NULL
BEGIN
    RAISERROR(''Table [dbo].[invoices] not found'', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N''[dbo].[taxes]'') IS NULL
BEGIN
    RAISERROR(''Table [dbo].[taxes] not found'', 16, 1);
    RETURN;
END;

IF OBJECT_ID(''tempdb..#FixValues'') IS NOT NULL DROP TABLE #FixValues;

;WITH LodgingTaxStatus AS
(
    SELECT
        t.hotel_id,
        MAX(CASE WHEN ISNULL(t.enabled, 0) = 1 THEN 1 ELSE 0 END) AS has_enabled,
        MAX(CASE WHEN ISNULL(t.enabled, 0) = 0 THEN 1 ELSE 0 END) AS has_disabled
    FROM dbo.taxes t
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
    FROM dbo.taxes t
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
    FROM dbo.taxes t
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
        i.subtotal AS old_subtotal,
        i.vat_amount AS old_vat_amount,
        i.lodging_tax_amount AS old_lodging_tax_amount,
        CAST(ISNULL(NULLIF(i.vat_rate, 0), ISNULL(vr.vat_rate, 0)) AS DECIMAL(18,6)) AS effective_vat_rate,
        CAST(
            CASE
                WHEN lts.has_enabled = 1 THEN ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
                WHEN lts.has_enabled = 0 AND lts.has_disabled = 1 THEN 0
                ELSE ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
            END
            AS DECIMAL(18,6)
        ) AS effective_lodging_rate,
        CASE WHEN lts.has_enabled = 0 AND lts.has_disabled = 1 THEN 1 ELSE 0 END AS lodging_forced_zero
    FROM dbo.invoices i
    LEFT JOIN LodgingTaxStatus lts ON lts.hotel_id = i.hotel_id
    LEFT JOIN VatRateByHotel vr ON vr.hotel_id = i.hotel_id
    LEFT JOIN LodgingRateByHotel lr ON lr.hotel_id = i.hotel_id
    WHERE i.total_amount IS NOT NULL
),
FinalCalc AS
(
    SELECT
        c.invoice_id,
        c.hotel_id,
        c.total_amount,
        c.old_subtotal,
        c.old_vat_amount,
        c.old_lodging_tax_amount,
        CAST(ROUND(c.total_amount / NULLIF(1 + (c.effective_vat_rate / 100.0) + (c.effective_lodging_rate / 100.0), 0), 2) AS DECIMAL(12,2)) AS calc_subtotal,
        CAST(ROUND(ROUND(c.total_amount / NULLIF(1 + (c.effective_vat_rate / 100.0) + (c.effective_lodging_rate / 100.0), 0), 2) * (c.effective_vat_rate / 100.0), 2) AS DECIMAL(12,2)) AS calc_vat_amount,
        CAST(
            CASE
                WHEN c.lodging_forced_zero = 1 THEN 0
                ELSE ROUND(ROUND(c.total_amount / NULLIF(1 + (c.effective_vat_rate / 100.0) + (c.effective_lodging_rate / 100.0), 0), 2) * (c.effective_lodging_rate / 100.0), 2)
            END
            AS DECIMAL(12,2)
        ) AS calc_lodging_tax_amount
    FROM Calc c
)
SELECT
    f.invoice_id,
    f.hotel_id,
    f.total_amount,
    f.old_subtotal,
    CAST(ROUND(f.total_amount - f.calc_vat_amount - f.calc_lodging_tax_amount, 2) AS DECIMAL(12,2)) AS new_subtotal,
    f.old_vat_amount,
    f.calc_vat_amount AS new_vat_amount,
    f.old_lodging_tax_amount,
    f.calc_lodging_tax_amount AS new_lodging_tax_amount
INTO #FixValues
FROM FinalCalc f
WHERE
    ABS(ISNULL(f.old_subtotal, 0) - ROUND(f.total_amount - f.calc_vat_amount - f.calc_lodging_tax_amount, 2)) > 0.01
    OR ABS(ISNULL(f.old_vat_amount, 0) - f.calc_vat_amount) > 0.01
    OR ABS(ISNULL(f.old_lodging_tax_amount, 0) - f.calc_lodging_tax_amount) > 0.01;

SELECT TOP 300
    invoice_id, hotel_id, total_amount,
    old_subtotal, new_subtotal,
    old_vat_amount, new_vat_amount,
    old_lodging_tax_amount, new_lodging_tax_amount
FROM #FixValues
ORDER BY invoice_id DESC;

SELECT COUNT(1) AS RowsToUpdate FROM #FixValues;
';

EXEC sp_executesql @Sql;

IF @DoUpdate = 1
BEGIN
    BEGIN TRY
        BEGIN TRAN;

        SET @Sql = N'
        USE ' + QUOTENAME(@TenantDbName) + N';

        IF OBJECT_ID(''tempdb..#FixValues'') IS NOT NULL DROP TABLE #FixValues;

        ;WITH LodgingTaxStatus AS
        (
            SELECT
                t.hotel_id,
                MAX(CASE WHEN ISNULL(t.enabled, 0) = 1 THEN 1 ELSE 0 END) AS has_enabled,
                MAX(CASE WHEN ISNULL(t.enabled, 0) = 0 THEN 1 ELSE 0 END) AS has_disabled
            FROM dbo.taxes t
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
            FROM dbo.taxes t
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
            FROM dbo.taxes t
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
                i.total_amount,
                i.subtotal AS old_subtotal,
                i.vat_amount AS old_vat_amount,
                i.lodging_tax_amount AS old_lodging_tax_amount,
                CAST(ISNULL(NULLIF(i.vat_rate, 0), ISNULL(vr.vat_rate, 0)) AS DECIMAL(18,6)) AS effective_vat_rate,
                CAST(
                    CASE
                        WHEN lts.has_enabled = 1 THEN ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
                        WHEN lts.has_enabled = 0 AND lts.has_disabled = 1 THEN 0
                        ELSE ISNULL(NULLIF(i.lodging_tax_rate, 0), ISNULL(lr.lodging_rate, 0))
                    END
                    AS DECIMAL(18,6)
                ) AS effective_lodging_rate,
                CASE WHEN lts.has_enabled = 0 AND lts.has_disabled = 1 THEN 1 ELSE 0 END AS lodging_forced_zero
            FROM dbo.invoices i
            LEFT JOIN LodgingTaxStatus lts ON lts.hotel_id = i.hotel_id
            LEFT JOIN VatRateByHotel vr ON vr.hotel_id = i.hotel_id
            LEFT JOIN LodgingRateByHotel lr ON lr.hotel_id = i.hotel_id
            WHERE i.total_amount IS NOT NULL
        ),
        FinalCalc AS
        (
            SELECT
                c.invoice_id,
                c.total_amount,
                c.old_subtotal,
                c.old_vat_amount,
                c.old_lodging_tax_amount,
                CAST(ROUND(ROUND(c.total_amount / NULLIF(1 + (c.effective_vat_rate / 100.0) + (c.effective_lodging_rate / 100.0), 0), 2) * (c.effective_vat_rate / 100.0), 2) AS DECIMAL(12,2)) AS calc_vat_amount,
                CAST(
                    CASE
                        WHEN c.lodging_forced_zero = 1 THEN 0
                        ELSE ROUND(ROUND(c.total_amount / NULLIF(1 + (c.effective_vat_rate / 100.0) + (c.effective_lodging_rate / 100.0), 0), 2) * (c.effective_lodging_rate / 100.0), 2)
                    END
                    AS DECIMAL(12,2)
                ) AS calc_lodging_tax_amount
            FROM Calc c
        )
        SELECT
            f.invoice_id,
            CAST(ROUND(f.total_amount - f.calc_vat_amount - f.calc_lodging_tax_amount, 2) AS DECIMAL(12,2)) AS new_subtotal,
            f.calc_vat_amount AS new_vat_amount,
            f.calc_lodging_tax_amount AS new_lodging_tax_amount
        INTO #FixValues
        FROM FinalCalc f
        WHERE
            ABS(ISNULL(f.old_subtotal, 0) - ROUND(f.total_amount - f.calc_vat_amount - f.calc_lodging_tax_amount, 2)) > 0.01
            OR ABS(ISNULL(f.old_vat_amount, 0) - f.calc_vat_amount) > 0.01
            OR ABS(ISNULL(f.old_lodging_tax_amount, 0) - f.calc_lodging_tax_amount) > 0.01;

        UPDATE i
        SET
            i.subtotal = f.new_subtotal,
            i.vat_amount = f.new_vat_amount,
            i.lodging_tax_amount = f.new_lodging_tax_amount
        FROM dbo.invoices i
        INNER JOIN #FixValues f ON f.invoice_id = i.invoice_id;

        SELECT @@ROWCOUNT AS UpdatedRows;
        ';

        EXEC sp_executesql @Sql;
        COMMIT TRAN;
        PRINT 'Update committed successfully.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        THROW;
    END CATCH
END
ELSE
BEGIN
    PRINT 'Preview only. No updates were made. Set @DoUpdate = 1 to apply.';
END;




