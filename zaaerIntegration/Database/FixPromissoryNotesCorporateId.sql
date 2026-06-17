/*
    Backfill promissory_notes.corporate_id from reservations when it was stored as NULL
    (legacy create only matched internal corporate_id, not zaaer_id on reservation).
*/
SET NOCOUNT ON;

UPDATE pn
SET pn.corporate_id = r.corporate_id,
    pn.updated_at = SYSUTCDATETIME()
FROM dbo.promissory_notes AS pn
INNER JOIN dbo.reservations AS r
    ON pn.reservation_id = r.zaaer_id
    OR pn.reservation_id = r.reservation_id
WHERE pn.corporate_id IS NULL
  AND r.corporate_id IS NOT NULL
  AND r.corporate_id > 0;

PRINT CONCAT(N'Promissory notes corporate_id backfilled: ', @@ROWCOUNT);
