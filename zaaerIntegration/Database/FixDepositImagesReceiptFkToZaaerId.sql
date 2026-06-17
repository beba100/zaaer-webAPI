-- Fix deposit_images FK: receipt_id -> payment_receipts.zaaer_id (not receipt_id PK)
-- Run on tenant DBs that already created deposit_images with the old FK.

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UQ_payment_receipts_zaaer_id_notnull'
      AND object_id = OBJECT_ID(N'dbo.payment_receipts')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_payment_receipts_zaaer_id_notnull]
        ON [dbo].[payment_receipts] ([zaaer_id])
        WHERE [zaaer_id] IS NOT NULL;

    PRINT 'Unique index UQ_payment_receipts_zaaer_id_notnull created.';
END;
GO

IF OBJECT_ID(N'dbo.deposit_images', N'U') IS NULL
BEGIN
    PRINT 'deposit_images table does not exist — run AddDepositImagesTable.sql instead.';
END
ELSE IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_DepositImages_PaymentReceipts'
      AND parent_object_id = OBJECT_ID(N'dbo.deposit_images')
)
BEGIN
    DECLARE @refCol SYSNAME;
    SELECT @refCol = COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id)
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.name = N'FK_DepositImages_PaymentReceipts'
      AND fk.parent_object_id = OBJECT_ID(N'dbo.deposit_images');

    IF @refCol = N'receipt_id'
    BEGIN
        ALTER TABLE dbo.deposit_images DROP CONSTRAINT FK_DepositImages_PaymentReceipts;

        UPDATE di
        SET di.receipt_id = pr.zaaer_id
        FROM dbo.deposit_images AS di
        INNER JOIN dbo.payment_receipts AS pr ON pr.receipt_id = di.receipt_id
        WHERE pr.zaaer_id IS NOT NULL;

        DELETE FROM dbo.deposit_images
        WHERE receipt_id NOT IN (SELECT zaaer_id FROM dbo.payment_receipts WHERE zaaer_id IS NOT NULL);

        ALTER TABLE dbo.deposit_images
            ADD CONSTRAINT FK_DepositImages_PaymentReceipts
            FOREIGN KEY (receipt_id) REFERENCES dbo.payment_receipts(zaaer_id)
            ON DELETE CASCADE;

        PRINT 'deposit_images FK updated to payment_receipts.zaaer_id.';
    END
    ELSE
    BEGIN
        PRINT 'deposit_images FK already references payment_receipts.zaaer_id — no change.';
    END
END
ELSE
BEGIN
    ALTER TABLE dbo.deposit_images
        ADD CONSTRAINT FK_DepositImages_PaymentReceipts
        FOREIGN KEY (receipt_id) REFERENCES dbo.payment_receipts(zaaer_id)
        ON DELETE CASCADE;

    PRINT 'deposit_images FK added on payment_receipts.zaaer_id.';
END;
GO
