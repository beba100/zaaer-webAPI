-- Nonclustered index to speed lookups by hotel + Zaaer external id (integration updates).
-- Safe to deploy when duplicates may exist; the app resolves ties by highest reservation_id.
-- Table/column names follow dbo.reservations convention used in this project.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_reservations_hotel_id_zaaer_id'
      AND object_id = OBJECT_ID(N'dbo.reservations')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_reservations_hotel_id_zaaer_id
    ON dbo.reservations (hotel_id, zaaer_id)
    INCLUDE (reservation_id);
END
GO
