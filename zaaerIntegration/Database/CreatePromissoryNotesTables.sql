-- Promissory notes (كمبيالات / سند لأمر) — tenant DB
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.promissory_notes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.promissory_notes
    (
        promissory_note_id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_promissory_notes PRIMARY KEY,
        promissory_no NVARCHAR(50) NOT NULL,
        zaaer_id INT NULL,
        hotel_id INT NOT NULL,
        reservation_id INT NULL,
        customer_id INT NULL,
        corporate_id INT NULL,
        payable_to NVARCHAR(200) NULL,
        reason NVARCHAR(500) NULL,
        place_of_maturity NVARCHAR(200) NULL,
        maturity_date DATE NOT NULL,
        amount DECIMAL(12,2) NOT NULL,
        amount_collected DECIMAL(12,2) NOT NULL
            CONSTRAINT DF_promissory_notes_amount_collected DEFAULT (0),
        status NVARCHAR(30) NOT NULL
            CONSTRAINT DF_promissory_notes_status DEFAULT (N'open'),
        payment_link_sent BIT NOT NULL
            CONSTRAINT DF_promissory_notes_payment_link_sent DEFAULT (0),
        notes NVARCHAR(1000) NULL,
        collection_receipt_id INT NULL,
        created_by INT NULL,
        created_at DATETIME2(3) NOT NULL
            CONSTRAINT DF_promissory_notes_created_at DEFAULT (SYSUTCDATETIME()),
        updated_at DATETIME2(3) NULL,
        CONSTRAINT CK_promissory_notes_amount CHECK (amount > 0),
        CONSTRAINT CK_promissory_notes_amount_collected CHECK (amount_collected >= 0),
        CONSTRAINT CK_promissory_notes_status CHECK (
            status IN (N'draft', N'open', N'partial', N'collected', N'cancelled'))
    );

    CREATE INDEX IX_promissory_notes_hotel_reservation
        ON dbo.promissory_notes (hotel_id, reservation_id)
        INCLUDE (status, amount, amount_collected, maturity_date);

    CREATE INDEX IX_promissory_notes_zaaer_id
        ON dbo.promissory_notes (zaaer_id)
        WHERE zaaer_id IS NOT NULL;

    PRINT 'Table [promissory_notes] created.';
END
ELSE
BEGIN
    PRINT 'Table [promissory_notes] already exists.';
END
GO
