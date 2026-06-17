-- Deposit receipt images (bank transfers / الإيداعات)
-- deposit_images.receipt_id stores payment_receipts.zaaer_id (integration id), NOT receipt_id PK.

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
END
ELSE
BEGIN
    PRINT 'Unique index UQ_payment_receipts_zaaer_id_notnull already exists.';
END;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'deposit_images') AND type IN (N'U'))
BEGIN
    CREATE TABLE deposit_images (
        deposit_image_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        receipt_id INT NOT NULL,
        image_path NVARCHAR(500) NOT NULL,
        original_filename NVARCHAR(255) NULL,
        file_size BIGINT NULL,
        content_type NVARCHAR(100) NULL,
        display_order INT NOT NULL DEFAULT 0,
        created_at DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_DepositImages_PaymentReceipts
            FOREIGN KEY (receipt_id) REFERENCES payment_receipts(zaaer_id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_DepositImages_ReceiptId ON deposit_images(receipt_id);
    CREATE INDEX IX_DepositImages_DisplayOrder ON deposit_images(receipt_id, display_order);

    PRINT 'Table deposit_images created successfully.';
END
ELSE
BEGIN
    PRINT 'Table deposit_images already exists.';
END;
GO
