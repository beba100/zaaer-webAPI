using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Services.Integrations
{
    /// <summary>
    /// Applies idempotent NTMP integration schema patches on tenant databases (same as Database/NtmpIntegration_Phase1.sql).
    /// </summary>
    public interface INtmpIntegrationSchemaEnsurer
    {
        Task EnsureAsync(CancellationToken cancellationToken = default);
    }

    public sealed class NtmpIntegrationSchemaEnsurer : INtmpIntegrationSchemaEnsurer
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly HashSet<string> EnsuredConnectionKeys = new(StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _db;

        public NtmpIntegrationSchemaEnsurer(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task EnsureAsync(CancellationToken cancellationToken = default)
        {
            var key = _db.Database.GetConnectionString() ?? _db.Database.GetDbConnection().ConnectionString;
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "default";
            }

            if (EnsuredConnectionKeys.Contains(key))
            {
                return;
            }

            await Gate.WaitAsync(cancellationToken);
            try
            {
                if (EnsuredConnectionKeys.Contains(key))
                {
                    return;
                }

                const string sql = @"
IF COL_LENGTH('dbo.ntmp_details', 'api_environment') IS NULL
BEGIN
    ALTER TABLE dbo.ntmp_details ADD api_environment NVARCHAR(20) NOT NULL
        CONSTRAINT DF_ntmp_details_api_environment DEFAULT (N'production');
END;

IF COL_LENGTH('dbo.ntmp_details', 'password_encrypted') IS NULL
BEGIN
    ALTER TABLE dbo.ntmp_details ADD password_encrypted VARBINARY(MAX) NULL;
END;

IF COL_LENGTH('dbo.ntmp_details', 'channel_name') IS NULL
BEGIN
    ALTER TABLE dbo.ntmp_details ADD channel_name NVARCHAR(256) NULL;
END;

IF COL_LENGTH('dbo.reservations', 'ntmp_transaction_id') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD ntmp_transaction_id NVARCHAR(64) NULL;
END;

IF COL_LENGTH('dbo.reservations', 'ntmp_last_sync_at') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD ntmp_last_sync_at DATETIME2 NULL;
END;

IF COL_LENGTH('dbo.reservations', 'ntmp_last_event_type') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD ntmp_last_event_type NVARCHAR(100) NULL;
END;

IF COL_LENGTH('dbo.reservations', 'ntmp_last_status') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD ntmp_last_status NVARCHAR(20) NULL;
END;

IF COL_LENGTH('dbo.reservations', 'ntmp_synced_stages') IS NULL
BEGIN
    ALTER TABLE dbo.reservations ADD ntmp_synced_stages INT NOT NULL
        CONSTRAINT DF_reservations_ntmp_synced_stages DEFAULT (0);
END;

UPDATE dbo.reservations
SET ntmp_synced_stages = 1
WHERE ntmp_transaction_id IS NOT NULL
  AND ISNULL(ntmp_synced_stages, 0) = 0;

IF COL_LENGTH('dbo.integration_responses', 'request_payload') IS NULL
BEGIN
    ALTER TABLE dbo.integration_responses ADD request_payload NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH('dbo.integration_responses', 'response_payload') IS NULL
BEGIN
    ALTER TABLE dbo.integration_responses ADD response_payload NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH('dbo.integration_responses', 'http_status_code') IS NULL
BEGIN
    ALTER TABLE dbo.integration_responses ADD http_status_code INT NULL;
END;

IF COL_LENGTH('dbo.integration_responses', 'correlation_id') IS NULL
BEGIN
    ALTER TABLE dbo.integration_responses ADD correlation_id NVARCHAR(64) NULL;
END;

IF OBJECT_ID(N'dbo.integration_responses', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_IntegrationResponses_Hotel_Created_Service'
          AND object_id = OBJECT_ID(N'dbo.integration_responses'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_IntegrationResponses_Hotel_Created_Service
        ON dbo.integration_responses (hotel_id, created_at DESC, service);
END;";

                await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                EnsuredConnectionKeys.Add(key);
            }
            finally
            {
                Gate.Release();
            }
        }
    }
}
