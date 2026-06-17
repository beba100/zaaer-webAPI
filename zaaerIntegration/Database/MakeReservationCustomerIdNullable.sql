/*
  Remove draft placeholder customers, purge orphan reservations without a guest,
  and enforce customer_id NOT NULL on every reservation row.
  Run on EACH tenant database (e.g. db54636_Dammam1) after deploying the application update.
*/

-- 1) Unlink reservations that still point at system placeholder rows
UPDATE r
SET customer_id = NULL
FROM dbo.reservations r
WHERE EXISTS (
    SELECT 1
    FROM dbo.customers c
    WHERE c.comments = N'pms:draft-placeholder'
      AND (
          r.customer_id = c.customer_id
          OR (c.zaaer_id IS NOT NULL AND r.customer_id = c.zaaer_id)
      )
);

-- 2) Delete placeholder customer rows (no longer referenced)
DELETE c
FROM dbo.customers c
WHERE c.comments = N'pms:draft-placeholder'
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.reservations r
      WHERE r.customer_id = c.customer_id
         OR (c.zaaer_id IS NOT NULL AND r.customer_id = c.zaaer_id)
  );

-- 3) Remove orphan reservations (no guest) and dependent rows
IF OBJECT_ID(N'dbo.reservation_extras', N'U') IS NOT NULL
BEGIN
    DELETE re
    FROM dbo.reservation_extras re
    WHERE EXISTS (
        SELECT 1
        FROM dbo.reservations r
        WHERE r.customer_id IS NULL
          AND (
              re.reservation_id = r.reservation_id
              OR (r.zaaer_id IS NOT NULL AND re.reservation_id = r.zaaer_id)
          )
    );
END

IF OBJECT_ID(N'dbo.reservation_companions', N'U') IS NOT NULL
BEGIN
    DELETE rc
    FROM dbo.reservation_companions rc
    WHERE EXISTS (
        SELECT 1
        FROM dbo.reservations r
        WHERE r.customer_id IS NULL
          AND (
              rc.reservation_id = r.reservation_id
              OR (r.zaaer_id IS NOT NULL AND rc.reservation_id = r.zaaer_id)
          )
    );
END

DELETE ru
FROM dbo.reservation_units ru
WHERE EXISTS (
    SELECT 1
    FROM dbo.reservations r
    WHERE r.customer_id IS NULL
      AND (
          ru.reservation_id = r.reservation_id
          OR (r.zaaer_id IS NOT NULL AND ru.reservation_id = r.zaaer_id)
      )
);

DELETE FROM dbo.reservations WHERE customer_id IS NULL;

-- 4) Drop legacy partial CHECK if present
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_reservations_customer_required_unless_draft'
      AND parent_object_id = OBJECT_ID(N'dbo.reservations')
)
BEGIN
    ALTER TABLE dbo.reservations DROP CONSTRAINT CK_reservations_customer_required_unless_draft;
END

-- 5) Require customer on every reservation (zero exceptions)
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.reservations')
      AND name = N'customer_id'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.reservations ALTER COLUMN customer_id INT NOT NULL;
END
