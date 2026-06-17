/*
  Verifies PMS critical performance indexes on the current tenant database.
  This script is read-only.
*/

PRINT '============================================================';
PRINT 'PMS critical performance indexes - verification';
PRINT 'Database: ' + DB_NAME();
PRINT '============================================================';

DECLARE @Expected TABLE
(
    TableName SYSNAME NOT NULL,
    IndexName SYSNAME NOT NULL
);

INSERT INTO @Expected (TableName, IndexName)
VALUES
    (N'reservation_units', N'IX_reservation_units_apartment_checkin'),
    (N'reservation_units', N'IX_reservation_units_reservation_checkin'),
    (N'maintenances', N'IX_maintenances_hotel_unit_dates'),
    (N'payment_receipts', N'IX_payment_receipts_hotel_receipt_date'),
    (N'payment_receipts', N'IX_payment_receipts_reservation_date'),
    (N'invoices', N'IX_invoices_hotel_invoice_date'),
    (N'invoices', N'IX_invoices_reservation_date'),
    (N'reservations', N'IX_reservations_hotel_reservation_date'),
    (N'reservations', N'IX_reservations_hotel_checkin'),
    (N'reservation_unit_day_rates', N'IX_reservation_unit_day_rates_reservation'),
    (N'reservation_unit_swaps', N'IX_reservation_unit_swaps_created_reservation'),
    (N'apartments', N'IX_apartments_hotel_zaaer'),
    (N'customers', N'IX_customers_zaaer_id');

SELECT
    e.TableName,
    e.IndexName,
    CASE
        WHEN OBJECT_ID(N'dbo.' + e.TableName, N'U') IS NULL THEN N'TABLE_MISSING'
        WHEN i.index_id IS NULL THEN N'MISSING'
        ELSE N'OK'
    END AS Status
FROM @Expected AS e
LEFT JOIN sys.indexes AS i
    ON i.object_id = OBJECT_ID(N'dbo.' + e.TableName)
   AND i.name = e.IndexName
ORDER BY e.TableName, e.IndexName;

PRINT '============================================================';
PRINT 'Verification completed';
PRINT '============================================================';
GO
