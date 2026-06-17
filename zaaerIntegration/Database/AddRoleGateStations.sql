-- Master DB: link RBAC roles to resort ticket gate stations (dynamic per game).
IF OBJECT_ID('dbo.pms_role_gate_stations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.pms_role_gate_stations (
        role_gate_station_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        role_id INT NOT NULL,
        station_code NVARCHAR(100) NOT NULL,
        sort_order INT NOT NULL CONSTRAINT DF_pms_role_gate_stations_sort DEFAULT(0),
        created_at DATETIME2 NOT NULL CONSTRAINT DF_pms_role_gate_stations_created DEFAULT(SYSDATETIME()),
        CONSTRAINT UQ_pms_role_gate_stations_role_code UNIQUE (role_id, station_code)
    );

    CREATE INDEX IX_pms_role_gate_stations_role ON dbo.pms_role_gate_stations(role_id);
END;
GO
