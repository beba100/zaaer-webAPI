/*
  Apply reservation_periods schema to ALL tenant databases on this server.

  Prerequisites:
    - CreateReservationPeriodsTable.sql
    - AlterReservationPeriods_AddStatusCheck.sql

  Targets: every online user database except system / master / central DBs.
  Skips databases without dbo.reservations.

  Set @DryRun = 1 to preview targets only.
*/

SET NOCOUNT ON;

DECLARE @DryRun BIT = 0;

DECLARE @Excluded TABLE (name SYSNAME PRIMARY KEY);
INSERT INTO @Excluded (name) VALUES
    (N'master'),
    (N'model'),
    (N'msdb'),
    (N'tempdb'),
    (N'Monitoring'),
    (N'db32463'),
    (N'db32464'),
    (N'db32357_MasterDB'),
    (N'db32465_centralDB');

IF OBJECT_ID('tempdb..#ReservationPeriodsApplyLog') IS NOT NULL
    DROP TABLE #ReservationPeriodsApplyLog;

CREATE TABLE #ReservationPeriodsApplyLog
(
    database_name SYSNAME       NOT NULL,
    apply_status  VARCHAR(20)   NOT NULL,
    message       NVARCHAR(MAX) NULL,
    applied_at    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);

DECLARE @db SYSNAME;
DECLARE @sql NVARCHAR(MAX);

DECLARE db_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT d.name
    FROM sys.databases d
    WHERE d.state = 0
      AND NOT EXISTS (SELECT 1 FROM @Excluded e WHERE e.name = d.name)
    ORDER BY d.name;

OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @db;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF @DryRun = 1
    BEGIN
        INSERT INTO #ReservationPeriodsApplyLog (database_name, apply_status, message)
        VALUES (@db, N'DRY_RUN', N'Would apply reservation_periods schema.');
        FETCH NEXT FROM db_cursor INTO @db;
        CONTINUE;
    END

    SET @sql = N'
USE ' + QUOTENAME(@db) + N';

IF OBJECT_ID(N''dbo.reservations'', N''U'') IS NULL
BEGIN
    INSERT INTO #ReservationPeriodsApplyLog (database_name, apply_status, message)
    VALUES (N''' + REPLACE(@db, '''', '''''') + N''', N''SKIPPED'', N''No dbo.reservations table.'');
END
ELSE
BEGIN
    BEGIN TRY
        IF OBJECT_ID(''dbo.reservation_periods'', ''U'') IS NULL
        BEGIN
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
                status nvarchar(30) NOT NULL CONSTRAINT DF_reservation_periods_status DEFAULT (''Active''),
                created_at datetime2 NOT NULL CONSTRAINT DF_reservation_periods_created_at DEFAULT (SYSDATETIME()),
                updated_at datetime2 NULL,
                CONSTRAINT CK_reservation_periods_valid_dates CHECK (to_date >= from_date),
                CONSTRAINT CK_reservation_periods_gross_rate CHECK (gross_rate >= 0)
            );

            CREATE INDEX IX_ReservationPeriods_Range
                ON dbo.reservation_periods (reservation_id, unit_id, from_date, to_date);
        END

        IF OBJECT_ID(''dbo.reservation_periods'', ''U'') IS NOT NULL
           AND NOT EXISTS (
               SELECT 1
               FROM sys.check_constraints
               WHERE name = ''CK_reservation_periods_status''
                 AND parent_object_id = OBJECT_ID(''dbo.reservation_periods''))
        BEGIN
            ALTER TABLE dbo.reservation_periods
                ADD CONSTRAINT CK_reservation_periods_status
                    CHECK (status IN (N''Active'', N''Closed'', N''Cancelled''));
        END

        INSERT INTO #ReservationPeriodsApplyLog (database_name, apply_status, message)
        VALUES (N''' + REPLACE(@db, '''', '''''') + N''', N''OK'', N''reservation_periods ready.'');
    END TRY
    BEGIN CATCH
        INSERT INTO #ReservationPeriodsApplyLog (database_name, apply_status, message)
        VALUES (N''' + REPLACE(@db, '''', '''''') + N''', N''ERROR'', ERROR_MESSAGE());
    END CATCH
END
';

    BEGIN TRY
        EXEC sys.sp_executesql @sql;
    END TRY
    BEGIN CATCH
        INSERT INTO #ReservationPeriodsApplyLog (database_name, apply_status, message)
        VALUES (@db, N'ERROR', ERROR_MESSAGE());
    END CATCH

    FETCH NEXT FROM db_cursor INTO @db;
END

CLOSE db_cursor;
DEALLOCATE db_cursor;

SELECT * FROM #ReservationPeriodsApplyLog ORDER BY database_name;
