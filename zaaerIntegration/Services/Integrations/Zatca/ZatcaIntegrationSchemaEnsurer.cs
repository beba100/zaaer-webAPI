using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Services.Integrations.Zatca
{
    public interface IZatcaIntegrationSchemaEnsurer
    {
        Task EnsureAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Idempotent schema patches — mirrors Database/ZatcaIntegration_Phase1.sql.
    /// </summary>
    public sealed class ZatcaIntegrationSchemaEnsurer : IZatcaIntegrationSchemaEnsurer
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly HashSet<string> EnsuredConnectionKeys = new(StringComparer.OrdinalIgnoreCase);

        private readonly ApplicationDbContext _db;

        public ZatcaIntegrationSchemaEnsurer(ApplicationDbContext db)
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

            // Idempotent fixes for DBs that already ran Phase1 before newer columns.
            await ApplyWidenDeviceTokensAsync(cancellationToken);
            await ApplyDeviceCommonNameColumnAsync(cancellationToken);
            await ApplyAlignZatcaEnvironmentColumnsAsync(cancellationToken);
            await ApplyZatcaIsActiveColumnAsync(cancellationToken);

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

                var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", "ZatcaIntegration_Phase1.sql");
                if (File.Exists(scriptPath))
                {
                    var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
                    await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                }
                else
                {
                    await _db.Database.ExecuteSqlRawAsync(MinimalPatchSql, cancellationToken);
                }

                EnsuredConnectionKeys.Add(key);
            }
            finally
            {
                Gate.Release();
            }
        }

        private const string MinimalPatchSql = @"
IF COL_LENGTH('dbo.invoices', 'zatca_status') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_status NVARCHAR(30) NOT NULL CONSTRAINT DF_invoices_zatca_status DEFAULT (N'pending');
IF OBJECT_ID(N'dbo.debit_notes', N'U') IS NULL
    SELECT 1;
";

        private async Task ApplyWidenDeviceTokensAsync(CancellationToken cancellationToken)
        {
            var widenPath = Path.Combine(AppContext.BaseDirectory, "Database", "ZatcaIntegration_Phase1b_WidenDeviceTokens.sql");
            if (File.Exists(widenPath))
            {
                var sql = await File.ReadAllTextAsync(widenPath, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                return;
            }

            await _db.Database.ExecuteSqlRawAsync(WidenDeviceTokensSql, cancellationToken);
        }

        private async Task ApplyDeviceCommonNameColumnAsync(CancellationToken cancellationToken)
        {
            await _db.Database.ExecuteSqlRawAsync(DeviceCommonNameColumnSql, cancellationToken);
        }

        private async Task ApplyAlignZatcaEnvironmentColumnsAsync(CancellationToken cancellationToken)
        {
            await _db.Database.ExecuteSqlRawAsync(AlignZatcaEnvironmentColumnsSql, cancellationToken);
        }

        /// <summary>One-time alignment: api_environment wins when both are set.</summary>
        private const string AlignZatcaEnvironmentColumnsSql = @"
IF OBJECT_ID(N'dbo.zatca_details', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.zatca_details
    SET environment = LOWER(LTRIM(RTRIM(api_environment)))
    WHERE api_environment IS NOT NULL
      AND LTRIM(RTRIM(api_environment)) <> N''
      AND (
          environment IS NULL
          OR LOWER(LTRIM(RTRIM(environment))) <> LOWER(LTRIM(RTRIM(api_environment)))
      );

    UPDATE dbo.zatca_details
    SET api_environment = LOWER(LTRIM(RTRIM(environment)))
    WHERE (api_environment IS NULL OR LTRIM(RTRIM(api_environment)) = N'')
      AND environment IS NOT NULL
      AND LTRIM(RTRIM(environment)) <> N'';
END;
";

        private async Task ApplyZatcaIsActiveColumnAsync(CancellationToken cancellationToken)
        {
            await _db.Database.ExecuteSqlRawAsync(ZatcaIsActiveColumnSql, cancellationToken);
        }

        private const string ZatcaIsActiveColumnSql = @"
IF OBJECT_ID(N'dbo.zatca_details', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.zatca_details', 'is_active') IS NULL
    ALTER TABLE dbo.zatca_details ADD is_active BIT NOT NULL
        CONSTRAINT DF_zatca_details_is_active DEFAULT (1);
";

        private const string DeviceCommonNameColumnSql = @"
IF OBJECT_ID(N'dbo.zatca_details', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.zatca_details', 'device_common_name') IS NULL
    ALTER TABLE dbo.zatca_details ADD device_common_name NVARCHAR(200) NULL;
";

        private const string WidenDeviceTokensSql = @"
IF OBJECT_ID(N'dbo.zatca_devices', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.zatca_devices', 'compliance_csid') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN compliance_csid NVARCHAR(MAX) NULL;
    IF COL_LENGTH('dbo.zatca_devices', 'production_csid') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN production_csid NVARCHAR(MAX) NULL;
    IF COL_LENGTH('dbo.zatca_devices', 'compliance_secret') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN compliance_secret NVARCHAR(1000) NULL;
    IF COL_LENGTH('dbo.zatca_devices', 'production_secret') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN production_secret NVARCHAR(1000) NULL;
END;
";
    }
}
