/*
Hybrid PMS RBAC - Tenant DB verification (all tenant databases on the server).

Run on the SQL Server instance (any database context; uses master for catalog).
Does NOT modify data. Reports per-database readiness for HybridRbac_TenantDB.sql.

Excluded (not scanned):
  master, model, msdb, tempdb, Monitoring,
  db32463, db32464, db32357_MasterDB, db32465_centralDB

Interpretation:
  READY        - All RBAC tables + sync columns + indexes already present.
  NEEDS_PATCH  - Safe to run HybridRbac_TenantDB.sql (will add missing columns/indexes/table).
  PARTIAL      - Core tables exist; role_permissions missing (script will CREATE it).
  REVIEW       - Missing users/roles/permissions; do not run blindly (not a standard PMS tenant).
  SKIP         - Database offline / not accessible.
*/

SET NOCOUNT ON;

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

IF OBJECT_ID('tempdb..#RbacVerify') IS NOT NULL
    DROP TABLE #RbacVerify;

CREATE TABLE #RbacVerify
(
    database_name           SYSNAME       NOT NULL,
    scan_status             VARCHAR(20)   NOT NULL,
    has_users               BIT           NOT NULL,
    has_roles               BIT           NOT NULL,
    has_permissions         BIT           NOT NULL,
    has_role_permissions    BIT           NOT NULL,
    users_rows              BIGINT        NULL,
    roles_rows              BIGINT        NULL,
    permissions_rows        BIGINT        NULL,
    role_permissions_rows   BIGINT        NULL,
    missing_sync_columns    NVARCHAR(MAX) NULL,
    missing_indexes         NVARCHAR(MAX) NULL,
    safe_to_run_patch       BIT           NOT NULL,
    recommendation          NVARCHAR(500) NOT NULL
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
    SET @sql = N'
USE ' + QUOTENAME(@db) + N';

DECLARE
    @has_users            BIT = 0,
    @has_roles            BIT = 0,
    @has_permissions      BIT = 0,
    @has_role_permissions BIT = 0,
    @users_rows           BIGINT = NULL,
    @roles_rows           BIGINT = NULL,
    @permissions_rows     BIGINT = NULL,
    @rp_rows              BIGINT = NULL,
    @missing_cols         NVARCHAR(MAX) = N'''',
    @missing_idx          NVARCHAR(MAX) = N'''',
    @scan_status          VARCHAR(20) = N''REVIEW'',
    @safe                 BIT = 0,
    @recommendation       NVARCHAR(500) = N'''';

IF OBJECT_ID(N''dbo.users'', N''U'') IS NOT NULL
BEGIN
    SET @has_users = 1;
    SELECT @users_rows = COUNT_BIG(*) FROM dbo.users;
END

IF OBJECT_ID(N''dbo.roles'', N''U'') IS NOT NULL
BEGIN
    SET @has_roles = 1;
    SELECT @roles_rows = COUNT_BIG(*) FROM dbo.roles;
END

IF OBJECT_ID(N''dbo.permissions'', N''U'') IS NOT NULL
BEGIN
    SET @has_permissions = 1;
    SELECT @permissions_rows = COUNT_BIG(*) FROM dbo.permissions;
END

IF OBJECT_ID(N''dbo.role_permissions'', N''U'') IS NOT NULL
BEGIN
    SET @has_role_permissions = 1;
    SELECT @rp_rows = COUNT_BIG(*) FROM dbo.role_permissions;
END

IF @has_users = 1
BEGIN
    IF COL_LENGTH(N''dbo.users'', N''master_user_id'') IS NULL SET @missing_cols += N''users.master_user_id;'';
    IF COL_LENGTH(N''dbo.users'', N''sync_source'') IS NULL SET @missing_cols += N''users.sync_source;'';
    IF COL_LENGTH(N''dbo.users'', N''sync_version'') IS NULL SET @missing_cols += N''users.sync_version;'';
    IF COL_LENGTH(N''dbo.users'', N''last_synced_at'') IS NULL SET @missing_cols += N''users.last_synced_at;'';
    IF COL_LENGTH(N''dbo.users'', N''is_local_override'') IS NULL SET @missing_cols += N''users.is_local_override;'';
END

IF @has_roles = 1
BEGIN
    IF COL_LENGTH(N''dbo.roles'', N''master_role_id'') IS NULL SET @missing_cols += N''roles.master_role_id;'';
    IF COL_LENGTH(N''dbo.roles'', N''sync_source'') IS NULL SET @missing_cols += N''roles.sync_source;'';
    IF COL_LENGTH(N''dbo.roles'', N''sync_version'') IS NULL SET @missing_cols += N''roles.sync_version;'';
    IF COL_LENGTH(N''dbo.roles'', N''last_synced_at'') IS NULL SET @missing_cols += N''roles.last_synced_at;'';
    IF COL_LENGTH(N''dbo.roles'', N''is_local_override'') IS NULL SET @missing_cols += N''roles.is_local_override;'';
END

IF @has_permissions = 1
BEGIN
    IF COL_LENGTH(N''dbo.permissions'', N''master_permission_id'') IS NULL SET @missing_cols += N''permissions.master_permission_id;'';
    IF COL_LENGTH(N''dbo.permissions'', N''sync_source'') IS NULL SET @missing_cols += N''permissions.sync_source;'';
    IF COL_LENGTH(N''dbo.permissions'', N''sync_version'') IS NULL SET @missing_cols += N''permissions.sync_version;'';
    IF COL_LENGTH(N''dbo.permissions'', N''last_synced_at'') IS NULL SET @missing_cols += N''permissions.last_synced_at;'';
    IF COL_LENGTH(N''dbo.permissions'', N''is_local_override'') IS NULL SET @missing_cols += N''permissions.is_local_override;'';
END

IF @has_role_permissions = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''UX_role_permissions_role_permission'' AND object_id = OBJECT_ID(N''dbo.role_permissions''))
        SET @missing_idx += N''UX_role_permissions_role_permission;'';
END
ELSE
    SET @missing_idx += N''(table role_permissions missing - will be created);'';

IF @has_users = 1 AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_users_master_user_id'' AND object_id = OBJECT_ID(N''dbo.users''))
    SET @missing_idx += N''IX_users_master_user_id;'';

IF @has_roles = 1 AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_roles_master_role_id'' AND object_id = OBJECT_ID(N''dbo.roles''))
    SET @missing_idx += N''IX_roles_master_role_id;'';

IF @has_permissions = 1 AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N''IX_permissions_master_permission_id'' AND object_id = OBJECT_ID(N''dbo.permissions''))
    SET @missing_idx += N''IX_permissions_master_permission_id;'';

IF @has_users = 1 AND @has_roles = 1 AND @has_permissions = 1
BEGIN
    SET @safe = 1;

    IF @missing_cols = N'''' AND @has_role_permissions = 1
       AND @missing_idx = N''''
    BEGIN
        SET @scan_status = N''READY'';
        SET @recommendation = N''Already patched. No need to run HybridRbac_TenantDB.sql.'';
    END
    ELSE IF @has_role_permissions = 0
    BEGIN
        SET @scan_status = N''PARTIAL'';
        SET @recommendation = N''Safe: run HybridRbac_TenantDB.sql (will create role_permissions + sync columns/indexes).'';
    END
    ELSE
    BEGIN
        SET @scan_status = N''NEEDS_PATCH'';
        SET @recommendation = N''Safe: run HybridRbac_TenantDB.sql (additive ALTER INDEX only).'';
    END
END
ELSE
BEGIN
    SET @safe = 0;
    SET @scan_status = N''REVIEW'';
    SET @recommendation = N''Missing core RBAC tables (users/roles/permissions). Review manually before running patch.'';
END

INSERT INTO #RbacVerify
(
    database_name, scan_status, has_users, has_roles, has_permissions, has_role_permissions,
    users_rows, roles_rows, permissions_rows, role_permissions_rows,
    missing_sync_columns, missing_indexes, safe_to_run_patch, recommendation
)
VALUES
(
    DB_NAME(), @scan_status, @has_users, @has_roles, @has_permissions, @has_role_permissions,
    @users_rows, @roles_rows, @permissions_rows, @rp_rows,
    NULLIF(@missing_cols, N''''), NULLIF(@missing_idx, N''''),
    @safe, @recommendation
);
';

    BEGIN TRY
        EXEC sys.sp_executesql @sql;
    END TRY
    BEGIN CATCH
        INSERT INTO #RbacVerify
        (
            database_name, scan_status, has_users, has_roles, has_permissions, has_role_permissions,
            users_rows, roles_rows, permissions_rows, role_permissions_rows,
            missing_sync_columns, missing_indexes, safe_to_run_patch, recommendation
        )
        VALUES
        (
            @db, N'SKIP', 0, 0, 0, 0,
            NULL, NULL, NULL, NULL,
            NULL, NULL, 0,
            CONCAT(N'Error: ', ERROR_MESSAGE())
        );
    END CATCH;

    FETCH NEXT FROM db_cursor INTO @db;
END

CLOSE db_cursor;
DEALLOCATE db_cursor;

/* -------------------------------------------------------------------------
   Result set 1: per-database detail
   ------------------------------------------------------------------------- */
SELECT
    database_name,
    scan_status,
    safe_to_run_patch,
    has_users,
    has_roles,
    has_permissions,
    has_role_permissions,
    users_rows,
    roles_rows,
    permissions_rows,
    role_permissions_rows,
    missing_sync_columns,
    missing_indexes,
    recommendation
FROM #RbacVerify
ORDER BY
    CASE scan_status
        WHEN N'REVIEW' THEN 1
        WHEN N'SKIP' THEN 2
        WHEN N'PARTIAL' THEN 3
        WHEN N'NEEDS_PATCH' THEN 4
        WHEN N'READY' THEN 5
        ELSE 6
    END,
    database_name;

/* -------------------------------------------------------------------------
   Result set 2: summary counts
   ------------------------------------------------------------------------- */
SELECT
    scan_status,
    COUNT(*) AS database_count
FROM #RbacVerify
GROUP BY scan_status
ORDER BY
    CASE scan_status
        WHEN N'REVIEW' THEN 1
        WHEN N'SKIP' THEN 2
        WHEN N'PARTIAL' THEN 3
        WHEN N'NEEDS_PATCH' THEN 4
        WHEN N'READY' THEN 5
        ELSE 6
    END;

/* -------------------------------------------------------------------------
   Result set 3: databases safe to patch now (run HybridRbac_TenantDB.sql)
   ------------------------------------------------------------------------- */
SELECT database_name, scan_status, recommendation
FROM #RbacVerify
WHERE safe_to_run_patch = 1
  AND scan_status IN (N'NEEDS_PATCH', N'PARTIAL')
ORDER BY database_name;

PRINT N'';
PRINT N'=== Hybrid RBAC Tenant Verify complete ===';
PRINT N'NEEDS_PATCH / PARTIAL => safe to execute HybridRbac_TenantDB.sql on that database.';
PRINT N'READY => already fully patched.';
PRINT N'REVIEW => inspect manually (not a standard tenant RBAC schema).';

DROP TABLE #RbacVerify;
