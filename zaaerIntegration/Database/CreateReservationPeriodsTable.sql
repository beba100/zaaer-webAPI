IF OBJECT_ID('dbo.reservation_periods', 'U') IS NULL
BEGIN
    -- reservation_id / unit_id = integration storage keys (zaaer ids), same as reservation_unit_day_rates.
    -- No FK to reservations.reservation_id — zaaer_id is not the PK.
    CREATE TABLE dbo.reservation_periods
    (
        period_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_reservation_periods PRIMARY KEY,
        reservation_id int NOT NULL,
        unit_id int NULL,
        rental_type nvarchar(30) NOT NULL,
        from_date date NOT NULL,
        to_date date NOT NULL,
        gross_rate decimal(12,2) NOT NULL,
        tax_included bit NOT NULL CONSTRAINT DF_reservation_periods_tax_included DEFAULT (1),
        status nvarchar(30) NOT NULL CONSTRAINT DF_reservation_periods_status DEFAULT ('Active'),
        created_at datetime2 NOT NULL CONSTRAINT DF_reservation_periods_created_at DEFAULT (SYSDATETIME()),
        updated_at datetime2 NULL,
        CONSTRAINT CK_reservation_periods_valid_dates CHECK (to_date >= from_date),
        CONSTRAINT CK_reservation_periods_gross_rate CHECK (gross_rate >= 0)
    );

    CREATE INDEX IX_ReservationPeriods_Range
        ON dbo.reservation_periods (reservation_id, unit_id, from_date, to_date);
END;
GO
