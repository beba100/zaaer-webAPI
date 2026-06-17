/*
Hybrid PMS RBAC - Apply tenant patch to ALL tenant databases on this server.

Prerequisite: HybridRbac_TenantDB_Verify.sql showed NEEDS_PATCH / safe_to_run_patch = 1.

What it does (same as HybridRbac_TenantDB.sql, per database):
  - Adds sync columns on users / roles / permissions
  - Creates role_permissions only if missing
  - Creates Hybrid RBAC indexes if missing

Set @DryRun = 1 first to list targets without changing anything.
Set @DryRun = 0 to apply.

After run: execute HybridRbac_TenantDB_Verify.sql again -> expect READY for all patched DBs.
*/

SET NOCOUNT ON;

DECLARE @DryRun BIT = 0;  -- <<< change to 1 for preview only

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

IF OBJECT_ID('tempdb..#RbacApplyLog') IS NOT NULL
    DROP TABLE #RbacApplyLog;

CREATE TABLE #RbacApplyLog
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
        INSERT INTO #RbacApplyLog (database_name, apply_status, message)
        VALUES (@db, N'DRY_RUN', N'Would apply HybridRbac_TenantDB patch.');
        FETCH NEXT FROM db_cursor INTO @db;
        CONTINUE;
    END

    SET @sql = N'
USE ' + QUOTENAME(@db) + N';

DECLARE @msg NVARCHAR(MAX) = N'''';

IF OBJECT_ID(N''dbo.users'', N''U'') IS NULL
   OR OBJECT_ID(N''dbo.roles'', N''U'') IS NULL
   OR OBJECT_ID(N''dbo.permissions'', N''U'') IS NULL
BEGIN
    RAISERROR(N''Missing core RBAC tables (users/roles/permissions). Skipped.'', 16, 1);
END

BEGIN TRY
    IF OBJECT_ID(N''dbo.role_permissions'', N''U'') IS NULL
    BEGIN
        CREATE TABLE dbo.role_permissions (
            role_permission_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_role_permissions PRIMARY KEY,
            role_id INT NOT NULL,
            permission_id INT NOT NULL,
            granted BIT NOT NULL CONSTRAINT DF_role_permissions_granted DEFAULT (1),
            created_at DATETIME2 NOT NULL CONSTRAINT DF_role_permissions_created_at DEFAULT (SYSUTCDATETIME()),
            created_by INT NULL,
            zaaer_id INT NULL
        );
        SET @msg += N''Created role_permissions;'';
    END

    IF COL_LENGTH(N''dbo.users'', N''master_user_id'') IS NULL
        ALTER TABLE dbo.users ADD master_user_id INT NULL;
    IF COL_LENGTH(N''dbo.users'', N''sync_source'') IS NULL
        ALTER TABLE dbo.users ADD sync_source NVARCHAR(40) NULL;
    IF COL_LENGTH(N''dbo.users'', N''sync_version'') IS NULL
        ALTER TABLE dbo.users ADD sync_version INT NULL;
    IF COL_LENGTH(N''dbo.users'', N''last_synced_at'') IS NULL
        ALTER TABLE dbo.users ADD last_synced_at DATETIME2 NULL;
    IF COL_LENGTH(N''dbo.users'', N''is_local_override'') IS NULL
        ALTER TABLE dbo.users ADD is_local_override BIT NOT NULL CONSTRAINT DF_users_is_local_override DEFAULT (0);

    IF COL_LENGTH(N''dbo.roles'', N''master_role_id'') IS NULL
        ALTER TABLE dbo.roles ADD master_role_id INT NULL;
    IF COL_LENGTH(N''dbo.roles'', N''sync_source'') IS NULL
        ALTER TABLE dbo.roles ADD sync_source NVARCHAR(40) NULL;
    IF COL_LENGTH(N''dbo.roles'', N''sync_version'') IS NULL
        ALTER TABLE dbo.roles ADD sync_version INT NULL;
    IF COL_LENGTH(N''dbo.roles'', N''last_synced_at'') IS NULL
        ALTER TABLE dbo.roles ADD last_synced_at DATETIME2 NULL;
    IF COL_LENGTH(N''dbo.roles'', N''is_local_override'') IS NULL
        ALTER TABLE dbo.roles ADD is_local_override BIT NOT NULL CONSTRAINT DF_roles_is_local_override DEFAULT (0);

    IF COL_LENGTH(N''dbo.permissions'', N''master_permission_id'') IS NULL
        ALTER TABLE dbo.permissions ADD master_permission_id INT NULL;
    IF COL_LENGTH(N''dbo.permissions'', N''sync_source'') IS NULL
        ALTER TABLE dbo.permissions ADD sync_source NVARCHAR(40) NULL;
    IF COL_LENGTH(N''dbo.permissions'', N''sync_version'') IS NULL
        ALTER TABLE dbo.permissions ADD sync_version INT NULL;
    IF COL_LENGTH(N''dbo.permissions'', N''last_synced_at'') IS NULL
        ALTER TABLE dbo.permissions ADD last_synced_at DATETIME2 NULL;
    IF COL_LENGTH(N''dbo.permissions'', N''is_local_override'') IS NULL
        ALTER TABLE dbo.permissions ADD is_local_override BIT NOT NULL CONSTRAINT DF_permissions_is_local_override DEFAULT (0);

    SET @msg += N''Sync columns OK;'';

    IF OBJECT_ID(N''dbo.role_permissions'', N''U'') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE name = N''UX_role_permissions_role_permission''
              AND object_id = OBJECT_ID(N''dbo.role_permissions'')
        )
    BEGIN
        BEGIN TRY
            CREATE UNIQUE INDEX UX_role_permissions_role_permission
                ON dbo.role_permissions(role_id, permission_id);
            SET @msg += N''Index UX_role_permissions_role_permission;'';
        END TRY
        BEGIN CATCH
            SET @msg += N''WARN UX_role_permissions: '' + ERROR_MESSAGE() + N'';'';
        END CATCH
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N''IX_users_master_user_id'' AND object_id = OBJECT_ID(N''dbo.users'')
    )
    BEGIN
        CREATE INDEX IX_users_master_user_id ON dbo.users(master_user_id);
        SET @msg += N''Index IX_users_master_user_id;'';
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N''IX_roles_master_role_id'' AND object_id = OBJECT_ID(N''dbo.roles'')
    )
    BEGIN
        CREATE INDEX IX_roles_master_role_id ON dbo.roles(master_role_id);
        SET @msg += N''Index IX_roles_master_role_id;'';
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N''IX_permissions_master_permission_id'' AND object_id = OBJECT_ID(N''dbo.permissions'')
    )
    BEGIN
        CREATE INDEX IX_permissions_master_permission_id ON dbo.permissions(master_permission_id);
        SET @msg += N''Index IX_permissions_master_permission_id;'';
    END

    INSERT INTO #RbacApplyLog (database_name, apply_status, message)
    VALUES (DB_NAME(), N''SUCCESS'', @msg);
END TRY
BEGIN CATCH
    INSERT INTO #RbacApplyLog (database_name, apply_status, message)
    VALUES (DB_NAME(), N''FAILED'', ERROR_MESSAGE());
    THROW;
END CATCH
';

    BEGIN TRY
        EXEC sys.sp_executesql @sql;
    END TRY
    BEGIN CATCH
        IF NOT EXISTS (SELECT 1 FROM #RbacApplyLog WHERE database_name = @db)
        BEGIN
            INSERT INTO #RbacApplyLog (database_name, apply_status, message)
            VALUES (@db, N'FAILED', ERROR_MESSAGE());
        END
    END CATCH;

    FETCH NEXT FROM db_cursor INTO @db;
END

CLOSE db_cursor;
DEALLOCATE db_cursor;

SELECT
    database_name,
    apply_status,
    message,
    applied_at
FROM #RbacApplyLog
ORDER BY
    CASE apply_status
        WHEN N'FAILED' THEN 1
        WHEN N'SUCCESS' THEN 2
        WHEN N'DRY_RUN' THEN 3
        ELSE 4
    END,
    database_name;

SELECT
    apply_status,
    COUNT(*) AS database_count
FROM #RbacApplyLog
GROUP BY apply_status;

DECLARE @failed INT = (SELECT COUNT(*) FROM #RbacApplyLog WHERE apply_status = N'FAILED');
DECLARE @ok INT = (SELECT COUNT(*) FROM #RbacApplyLog WHERE apply_status = N'SUCCESS');

PRINT N'';
IF @DryRun = 1
    PRINT N'=== DRY RUN complete (no changes). Set @DryRun = 0 and run again to apply. ===';
ELSE
    PRINT N'=== Apply complete. SUCCESS=' + CAST(@ok AS NVARCHAR(10)) + N', FAILED=' + CAST(@failed AS NVARCHAR(10)) + N' ===';

IF @failed = 0 AND @DryRun = 0
    PRINT N'Next: run HybridRbac_TenantDB_Verify.sql — all tenants should show READY.';

DROP TABLE #RbacApplyLog;
