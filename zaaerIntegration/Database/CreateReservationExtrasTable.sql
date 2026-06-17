-- Tenant DB: reservation extra / add-on lines (packages, posting, amounts).
-- reservation_id: Zaaer booking id when present on the reservation, else internal reservations.reservation_id (no FK to reservations).
-- unit_id: optional FK to reservation_units.unit_id (room line).

IF OBJECT_ID(N'dbo.reservation_extras', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[reservation_extras] (
        [extra_id]        INT IDENTITY(1, 1) NOT NULL,
        [reservation_id]  INT NOT NULL,
        [unit_id]         INT NULL,
        [package_id]      INT NULL,
        [item_name]       NVARCHAR(400) NULL,
        [posting_rule]    NVARCHAR(80) NOT NULL CONSTRAINT [DF_reservation_extras_posting] DEFAULT (N'OnCheckIn'),
        [service_date]    DATE NULL,
        [guest_count]     INT NULL,
        [night_count]     INT NULL,
        [unit_price]      DECIMAL(12, 2) NOT NULL CONSTRAINT [DF_reservation_extras_unit_price] DEFAULT (0),
        [subtotal]        DECIMAL(12, 2) NOT NULL CONSTRAINT [DF_reservation_extras_subtotal] DEFAULT (0),
        [tax_amount]      DECIMAL(12, 2) NOT NULL CONSTRAINT [DF_reservation_extras_tax] DEFAULT (0),
        [total_amount]    DECIMAL(12, 2) NOT NULL CONSTRAINT [DF_reservation_extras_total] DEFAULT (0),
        [created_by]      INT NULL,
        [created_at]      DATETIME2(0) NOT NULL CONSTRAINT [DF_reservation_extras_created] DEFAULT (SYSUTCDATETIME()),
        [updated_at]      DATETIME2(0) NULL,
        CONSTRAINT [PK_reservation_extras] PRIMARY KEY CLUSTERED ([extra_id] ASC),
        CONSTRAINT [FK_reservation_extras_units]
            FOREIGN KEY ([unit_id]) REFERENCES [dbo].[reservation_units] ([unit_id]) ON DELETE SET NULL
    );

    CREATE NONCLUSTERED INDEX [IX_reservation_extras_reservation_id]
        ON [dbo].[reservation_extras] ([reservation_id] ASC);
END
GO
