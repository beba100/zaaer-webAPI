SET NOCOUNT ON;

/*
  Enterprise session management — Master DB only.
  Adds session_version / lock columns on pms_users,
  pms_user_sessions, pms_security_audit, and security.sessions.manage permission.
*/

-- ---------------------------------------------------------------------------
-- pms_users: session version + account lock
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.pms_users', N'session_version') IS NULL
    ALTER TABLE dbo.pms_users ADD session_version INT NOT NULL
        CONSTRAINT DF_pms_users_session_version DEFAULT (0);

IF COL_LENGTH(N'dbo.pms_users', N'is_locked') IS NULL
    ALTER TABLE dbo.pms_users ADD is_locked BIT NOT NULL
        CONSTRAINT DF_pms_users_is_locked DEFAULT (0);

IF COL_LENGTH(N'dbo.pms_users', N'locked_at') IS NULL
    ALTER TABLE dbo.pms_users ADD locked_at DATETIME2 NULL;

IF COL_LENGTH(N'dbo.pms_users', N'locked_reason') IS NULL
    ALTER TABLE dbo.pms_users ADD locked_reason NVARCHAR(500) NULL;

-- ---------------------------------------------------------------------------
-- pms_user_sessions
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.pms_user_sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_user_sessions (
        session_id         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_user_sessions PRIMARY KEY,
        user_id            INT NOT NULL,
        refresh_token_hash NVARCHAR(128) NOT NULL,
        device_id          NVARCHAR(100) NULL,
        device_name        NVARCHAR(200) NULL,
        ip_address         NVARCHAR(64) NULL,
        user_agent         NVARCHAR(500) NULL,
        created_at         DATETIME2 NOT NULL CONSTRAINT DF_pms_user_sessions_created_at DEFAULT (SYSUTCDATETIME()),
        expires_at         DATETIME2 NOT NULL,
        last_activity_at   DATETIME2 NOT NULL CONSTRAINT DF_pms_user_sessions_last_activity DEFAULT (SYSUTCDATETIME()),
        revoked_at         DATETIME2 NULL,
        revoked_by         INT NULL,
        revoke_reason      NVARCHAR(200) NULL,
        CONSTRAINT FK_pms_user_sessions_user FOREIGN KEY (user_id)
            REFERENCES dbo.pms_users(user_id)
    );

    CREATE INDEX IX_pms_user_sessions_user_active
        ON dbo.pms_user_sessions(user_id, revoked_at, expires_at);

    CREATE UNIQUE INDEX UX_pms_user_sessions_refresh_hash
        ON dbo.pms_user_sessions(refresh_token_hash);
END;

-- ---------------------------------------------------------------------------
-- pms_security_audit
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.pms_security_audit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_security_audit (
        audit_id       BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_pms_security_audit PRIMARY KEY,
        user_id        INT NULL,
        actor_user_id  INT NULL,
        event_type     NVARCHAR(50) NOT NULL,
        session_id     BIGINT NULL,
        ip_address     NVARCHAR(64) NULL,
        details        NVARCHAR(MAX) NULL,
        created_at     DATETIME2 NOT NULL CONSTRAINT DF_pms_security_audit_created_at DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_pms_security_audit_user_created
        ON dbo.pms_security_audit(user_id, created_at DESC);
END;

-- ---------------------------------------------------------------------------
-- Permission: security.sessions.manage
-- ---------------------------------------------------------------------------
MERGE dbo.pms_permissions AS target
USING (
    SELECT
        N'security.sessions.manage' AS permission_code,
        N'Manage user sessions (force logout, revoke devices)' AS permission_name_en,
        N'إدارة جلسات المستخدمين (تسجيل خروج إجباري)' AS permission_name_ar,
        N'security' AS module_name,
        N'sessions' AS submodule_name,
        N'manage' AS action_name,
        10 AS sort_order
) AS source
ON target.permission_code = source.permission_code
WHEN MATCHED THEN
    UPDATE SET
        permission_name = source.permission_name_en,
        permission_name_en = source.permission_name_en,
        permission_name_ar = source.permission_name_ar,
        module_name = source.module_name,
        submodule_name = source.submodule_name,
        action_name = source.action_name,
        sort_order = source.sort_order,
        is_active = 1
WHEN NOT MATCHED THEN
    INSERT (permission_code, permission_name, permission_name_en, permission_name_ar,
            module_name, submodule_name, action_name, sort_order, is_active, created_at)
    VALUES (source.permission_code, source.permission_name_en, source.permission_name_en, source.permission_name_ar,
            source.module_name, source.submodule_name, source.action_name, source.sort_order, 1, SYSUTCDATETIME());

PRINT N'Enterprise session management schema applied.';

-- Grant security.sessions.manage to roles that already manage RBAC users.
INSERT INTO dbo.pms_role_permissions (role_id, permission_id, created_at)
SELECT rp.role_id, p.permission_id, SYSUTCDATETIME()
FROM dbo.pms_role_permissions rp
INNER JOIN dbo.pms_permissions src ON src.permission_id = rp.permission_id AND src.permission_code = N'rbac.users.manage'
INNER JOIN dbo.pms_permissions p ON p.permission_code = N'security.sessions.manage'
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.pms_role_permissions x
    WHERE x.role_id = rp.role_id AND x.permission_id = p.permission_id
);

PRINT CONCAT(N'Granted security.sessions.manage to ', @@ROWCOUNT, N' role(s).');
