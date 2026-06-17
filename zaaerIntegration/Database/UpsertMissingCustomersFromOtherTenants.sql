/*
  Upsert missing customers (and their identifications) into each tenant DB.
  Logic (same as C# ZaaerCustomerService): reservations.customer_id = customers.zaaer_id.
  If a reservation references a customer_id (zaaer_id) that doesn't exist in that DB's customers,
  copy that customer (and customer_identifications) from any other tenant that has it.

  Run from any DB on the same instance (e.g. master or a utility DB).
  Excluded DBs: master, model, msdb, tempdb, Monitoring, db32463, db32464, db32357_MasterDB, db32465_centralDB.
*/

SET NOCOUNT ON;

-- DBs to skip (same as your existing script)
DECLARE @Excluded TABLE (name SYSNAME);
INSERT INTO @Excluded (name) VALUES
  ('master'),('model'),('msdb'),('tempdb'),
  ('Monitoring'),('db32463'),('db32464'),('db32357_MasterDB'),('db32465_centralDB');

-- All tenant DBs
DECLARE @TenantDBs TABLE (dbname SYSNAME);
INSERT INTO @TenantDBs (dbname)
SELECT name FROM sys.databases
WHERE state_desc = 'ONLINE'
  AND name NOT IN (SELECT name FROM @Excluded);

-- (TargetDB, ZaaerId) = missing customers per tenant
DECLARE @Missing TABLE (TargetDB SYSNAME, ZaaerId INT, TargetHotelId INT);
DECLARE @TargetDB SYSNAME;
DECLARE @SQL NVARCHAR(MAX);

-- Collect missing customers per tenant (and one hotel_id from reservations for that tenant)
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT dbname FROM @TenantDBs;
OPEN c;
FETCH NEXT FROM c INTO @TargetDB;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @SQL = N'
    INSERT INTO @Missing (TargetDB, ZaaerId, TargetHotelId)
    SELECT @db, r.customer_id, (SELECT TOP 1 r2.hotel_id FROM [' + @TargetDB + N'].dbo.reservations r2 WHERE r2.customer_id = r.customer_id)
    FROM [' + @TargetDB + N'].dbo.reservations r
    WHERE NOT EXISTS (SELECT 1 FROM [' + @TargetDB + N'].dbo.customers c WHERE c.zaaer_id = r.customer_id)
    GROUP BY r.customer_id';
  -- Run in context of current DB; we need to insert into @Missing, so we pass @TargetDB and use a temp table instead
  -- Actually @Missing is a table variable in this batch, so we can't populate it from dynamic SQL that runs in same batch.
  -- So we need to use a #temp table that dynamic SQL can insert into.
  FETCH NEXT FROM c INTO @TargetDB;
END;
CLOSE c;
DEALLOCATE c;

-- Use a temp table so dynamic SQL can write to it
IF OBJECT_ID('tempdb..#Missing') IS NOT NULL DROP TABLE #Missing;
CREATE TABLE #Missing (TargetDB SYSNAME, ZaaerId INT, TargetHotelId INT);

DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT dbname FROM @TenantDBs;
OPEN c;
FETCH NEXT FROM c INTO @TargetDB;
WHILE @@FETCH_STATUS = 0
BEGIN
  SET @SQL = N'
    INSERT INTO #Missing (TargetDB, ZaaerId, TargetHotelId)
    SELECT N''' + REPLACE(@TargetDB, '''', '''''') + N''', r.customer_id,
      (SELECT TOP 1 r2.hotel_id FROM [' + @TargetDB + N'].dbo.reservations r2 WHERE r2.customer_id = r.customer_id)
    FROM [' + @TargetDB + N'].dbo.reservations r
    WHERE NOT EXISTS (SELECT 1 FROM [' + @TargetDB + N'].dbo.customers c WHERE c.zaaer_id = r.customer_id)
    GROUP BY r.customer_id';
  EXEC sp_executesql @SQL;
  FETCH NEXT FROM c INTO @TargetDB;
END;
CLOSE c;
DEALLOCATE c;

-- Optional: show what we will fix
SELECT TargetDB, ZaaerId, TargetHotelId FROM #Missing ORDER BY TargetDB, ZaaerId;

-- For each (TargetDB, ZaaerId), find a source DB and copy customer + identifications
DECLARE @ZaaerId INT, @TargetHotelId INT;
DECLARE @SourceDB SYSNAME;
DECLARE @FoundSource INT;

DECLARE c2 CURSOR LOCAL FAST_FORWARD FOR SELECT TargetDB, ZaaerId, TargetHotelId FROM #Missing;
OPEN c2;
FETCH NEXT FROM c2 INTO @TargetDB, @ZaaerId, @TargetHotelId;

WHILE @@FETCH_STATUS = 0
BEGIN
  -- Find any other tenant that has this customer (zaaer_id)
  SET @SourceDB = NULL;
  SELECT TOP 1 @SourceDB = d.dbname
  FROM @TenantDBs d
  WHERE d.dbname <> @TargetDB
    AND EXISTS (SELECT 1 FROM sys.databases sd WHERE sd.name = d.dbname AND sd.state_desc = 'ONLINE')
  ORDER BY d.dbname;

  -- Check which source DB actually has this zaaer_id
  DECLARE @TryDB SYSNAME;
  DECLARE cSrc CURSOR LOCAL FAST_FORWARD FOR
    SELECT dbname FROM @TenantDBs WHERE dbname <> @TargetDB;
  OPEN cSrc;
  FETCH NEXT FROM cSrc INTO @TryDB;
  WHILE @@FETCH_STATUS = 0
  BEGIN
    SET @SQL = N'IF EXISTS (SELECT 1 FROM [' + @TryDB + N'].dbo.customers WHERE zaaer_id = @zid) SELECT @out = 1 ELSE SELECT @out = 0';
    EXEC sp_executesql @SQL, N'@zid INT, @out INT OUTPUT', @zid = @ZaaerId, @out = @FoundSource OUTPUT;
    IF @FoundSource = 1
    BEGIN
      SET @SourceDB = @TryDB;
      BREAK;
    END
    FETCH NEXT FROM cSrc INTO @TryDB;
  END;
  CLOSE cSrc;
  DEALLOCATE cSrc;

  IF @SourceDB IS NOT NULL AND @TargetHotelId IS NOT NULL
  BEGIN
    -- Idempotent: only insert if still missing in target
    -- INSERT customer into target (from source), using target hotel_id
    SET @SQL = N'
      INSERT INTO [' + @TargetDB + N'].dbo.customers (
        customer_no, customer_name, gtype_id, n_id, guest_category_id, visa_no, mobile_no, email, address, comments,
        entered_by, entered_at, gender, birthday, birthdate_hijri, birthdate_gregorian, hotel_id, zaaer_id, is_active, created_at, updated_at
      )
      SELECT
        src.customer_no, src.customer_name, src.gtype_id, src.n_id, src.guest_category_id, src.visa_no, src.mobile_no, src.email, src.address, src.comments,
        src.entered_by, src.entered_at, src.gender, src.birthday, src.birthdate_hijri, src.birthdate_gregorian,
        @hid, src.zaaer_id, src.is_active, src.created_at, src.updated_at
      FROM [' + @SourceDB + N'].dbo.customers src
      WHERE src.zaaer_id = @zid
        AND NOT EXISTS (SELECT 1 FROM [' + @TargetDB + N'].dbo.customers t WHERE t.zaaer_id = @zid)';
    EXEC sp_executesql @SQL, N'@zid INT, @hid INT', @zid = @ZaaerId, @hid = @TargetHotelId;

    -- INSERT customer_identifications into target (customer_id = zaaer_id, same as app). Idempotent: only if we have none for this customer in target
    SET @SQL = N'
      INSERT INTO [' + @TargetDB + N'].dbo.customer_identifications (
        customer_id, zaaer_id, id_type_id, id_number, version_number, issue_place, issue_place_ar, issue_date, expiry_date, notes, is_primary, is_active, created_at, updated_at
      )
      SELECT @zid, src.zaaer_id, src.id_type_id, src.id_number, src.version_number, src.issue_place, src.issue_place_ar, src.issue_date, src.expiry_date, src.notes, src.is_primary, src.is_active, src.created_at, src.updated_at
      FROM [' + @SourceDB + N'].dbo.customer_identifications src
      WHERE src.customer_id = @zid
        AND NOT EXISTS (SELECT 1 FROM [' + @TargetDB + N'].dbo.customer_identifications t WHERE t.customer_id = @zid)';
    EXEC sp_executesql @SQL, N'@zid INT', @zid = @ZaaerId;
  END

  FETCH NEXT FROM c2 INTO @TargetDB, @ZaaerId, @TargetHotelId;
END;
CLOSE c2;
DEALLOCATE c2;

-- Summary
SELECT 'Done. Upserted missing customers (and identifications) per tenant.' AS Result;
DROP TABLE #Missing;
