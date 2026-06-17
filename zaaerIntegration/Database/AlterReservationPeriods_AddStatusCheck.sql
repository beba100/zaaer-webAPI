IF OBJECT_ID('dbo.reservation_periods', 'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.check_constraints
       WHERE name = 'CK_reservation_periods_status'
         AND parent_object_id = OBJECT_ID('dbo.reservation_periods'))
BEGIN
    ALTER TABLE dbo.reservation_periods
        ADD CONSTRAINT CK_reservation_periods_status
            CHECK (status IN (N'Active', N'Closed', N'Cancelled'));
END;
GO
