-- Tenant DB: reservation companions (normalized). Replaces reservations.companions_json.
-- Run on each hotel database after deploy.
--
-- reservation_id = reservations.zaaer_id when set, else reservations.reservation_id (drafts).
-- customer_id = customers.zaaer_id when set, else customers.customer_id.
-- unit_id = apartments.zaaer_id when set, else apartments.apartment_id (not reservation_units.unit_id).
-- No FK on reservation_id / customer_id / unit_id to those tables (integration ids).

IF OBJECT_ID(N'dbo.reservation_companions', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[reservation_companions] (
        [companion_id]      INT IDENTITY(1, 1) NOT NULL,
        [reservation_id]    INT NOT NULL,
        [customer_id]       INT NOT NULL,
        [unit_id]           INT NULL,
        [apartment_id]      INT NULL,
        [relation_id]       INT NULL,
        [sort_order]        INT NOT NULL CONSTRAINT [DF_reservation_companions_sort] DEFAULT (0),
        [created_at]        DATETIME2(0) NOT NULL CONSTRAINT [DF_reservation_companions_created] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_reservation_companions] PRIMARY KEY CLUSTERED ([companion_id] ASC),
        CONSTRAINT [FK_reservation_companions_apartments]
            FOREIGN KEY ([apartment_id]) REFERENCES [dbo].[apartments] ([apartment_id]),
        CONSTRAINT [FK_reservation_companions_relations]
            FOREIGN KEY ([relation_id]) REFERENCES [dbo].[customer_relations] ([cr_id])
    );

    CREATE NONCLUSTERED INDEX [IX_reservation_companions_reservation_id]
        ON [dbo].[reservation_companions] ([reservation_id] ASC);
END
GO

-- Drop legacy JSON column if present (no migration data required per team).
IF EXISTS (
    SELECT 1
    FROM sys.columns c
    INNER JOIN sys.objects t ON c.object_id = t.object_id
    WHERE t.schema_id = SCHEMA_ID('dbo')
      AND t.name = 'reservations'
      AND c.name = 'companions_json'
)
BEGIN
    ALTER TABLE [dbo].[reservations] DROP COLUMN [companions_json];
END
GO
