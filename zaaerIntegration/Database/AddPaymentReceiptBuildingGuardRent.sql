/*
  Add building guard rent flag to payment_receipts (tenant DB).
  Default false — applies only to rent receipt vouchers.
*/

IF COL_LENGTH(N'dbo.payment_receipts', N'is_building_guard_rent') IS NULL
BEGIN
    ALTER TABLE dbo.payment_receipts
        ADD is_building_guard_rent BIT NOT NULL
            CONSTRAINT DF_payment_receipts_is_building_guard_rent DEFAULT (0);
END
GO
