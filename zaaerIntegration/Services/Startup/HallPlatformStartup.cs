using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;

namespace zaaerIntegration.Services.Startup
{
    /// <summary>
    /// Applies hall platform SQL patches to Master + tenant databases on startup.
    /// </summary>
    public static class HallPlatformStartup
    {
        private static readonly string[] SqlSearchRoots =
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        public static async Task ApplyAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            await ApplyMasterPermissionsAsync(services, configuration, logger, cancellationToken);
            await ApplyTenantSchemaAsync(services, configuration, logger, cancellationToken);
            await ApplyCashLedgerSchemaAsync(services, configuration, logger, cancellationToken);
        }

        private static async Task ApplyMasterPermissionsAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var scriptPath = ResolveSqlPath("Database/AddHallEventMasterSetup.sql");
            if (scriptPath == null)
            {
                logger.LogWarning("[Startup] AddHallEventMasterSetup.sql not found; skipping hall RBAC seed.");
                return;
            }

            try
            {
                var masterContext = services.GetRequiredService<MasterDbContext>();
                var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
                var connectionString = masterContext.Database.GetConnectionString()
                    ?? configuration.GetConnectionString("MasterDb")
                    ?? configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    logger.LogWarning("[Startup] Master connection string missing; skipping hall RBAC seed.");
                    return;
                }

                await ExecuteBatchedSqlAsync(connectionString, script, cancellationToken);
                logger.LogInformation("[Startup] Hall platform permissions synced on Master DB.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Startup] Failed to sync hall permissions on Master DB: {Message}", ex.Message);
            }
        }

        private static async Task ApplyTenantSchemaAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var scriptPath = ResolveSqlPath("Database/AddHallEvents.sql");
            if (scriptPath == null)
            {
                logger.LogWarning("[Startup] AddHallEvents.sql not found; skipping hall tenant schema.");
                return;
            }

            var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            var masterContext = services.GetRequiredService<MasterDbContext>();
            var tenants = await masterContext.Tenants.AsNoTracking().ToListAsync(cancellationToken);

            var server = configuration["TenantDatabase:Server"]?.Trim();
            var userId = configuration["TenantDatabase:UserId"]?.Trim();
            var password = configuration["TenantDatabase:Password"]?.Trim();

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("[Startup] Tenant database settings missing; skipping hall tenant schema.");
                return;
            }

            foreach (var tenant in tenants)
            {
                try
                {
                    var connectionString =
                        $"Server={server}; Database={tenant.DatabaseName}; User Id={userId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                    await using var conn = new SqlConnection(connectionString);
                    await conn.OpenAsync(cancellationToken);

                    var needsPatch = await conn.ExecuteScalarAsync<int?>(
                        new CommandDefinition(
                            """
                            SELECT CASE
                                WHEN COL_LENGTH('dbo.room_types', 'hall_gender_type') IS NULL THEN 1
                                WHEN COL_LENGTH('dbo.room_types', 'hall_capacity') IS NULL THEN 1
                                WHEN COL_LENGTH('dbo.room_types', 'allow_split') IS NULL THEN 1
                                WHEN COL_LENGTH('dbo.room_types', 'minimum_booking_hours') IS NULL THEN 1
                                WHEN COL_LENGTH('dbo.room_types', 'venue_kind') IS NULL THEN 1
                                WHEN COL_LENGTH('dbo.apartments', 'hall_preparation_status') IS NULL THEN 1
                                WHEN COL_LENGTH('dbo.packages', 'price_type') IS NULL THEN 1
                                WHEN COL_LENGTH('dbo.packages', 'package_category') IS NULL THEN 1
                                WHEN OBJECT_ID('dbo.reservation_event_profiles', 'U') IS NULL THEN 1
                                WHEN OBJECT_ID('dbo.event_function_sheets', 'U') IS NULL THEN 1
                                WHEN OBJECT_ID('dbo.event_function_sheet_items', 'U') IS NULL THEN 1
                                WHEN OBJECT_ID('dbo.hall_event_alerts', 'U') IS NULL THEN 1
                                ELSE 0
                            END
                            """,
                            cancellationToken: cancellationToken));

                    if (needsPatch != 1)
                    {
                        logger.LogDebug("[Startup] Hall schema already present for tenant {Code}", tenant.Code);
                        continue;
                    }

                    await ExecuteBatchedSqlAsync(connectionString, script, cancellationToken);
                    logger.LogInformation("[Startup] Hall schema applied for tenant {Code}", tenant.Code);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Startup] Failed hall schema for tenant {Code}: {Message}", tenant.Code, ex.Message);
                }
            }
        }

        private static async Task ApplyCashLedgerSchemaAsync(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var scriptPath = ResolveSqlPath("Database/AddCashLedger.sql");
            if (scriptPath == null)
            {
                logger.LogWarning("[Startup] AddCashLedger.sql not found; skipping cash ledger schema.");
                return;
            }

            var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            var masterContext = services.GetRequiredService<MasterDbContext>();
            var tenants = await masterContext.Tenants.AsNoTracking().ToListAsync(cancellationToken);

            var server = configuration["TenantDatabase:Server"]?.Trim();
            var userId = configuration["TenantDatabase:UserId"]?.Trim();
            var password = configuration["TenantDatabase:Password"]?.Trim();

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("[Startup] Tenant database settings missing; skipping cash ledger schema.");
                return;
            }

            foreach (var tenant in tenants)
            {
                try
                {
                    var connectionString =
                        $"Server={server}; Database={tenant.DatabaseName}; User Id={userId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";

                    await ExecuteBatchedSqlAsync(connectionString, script, cancellationToken);
                    logger.LogInformation("[Startup] Cash ledger schema synced for tenant {Code}", tenant.Code);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Startup] Failed cash ledger schema for tenant {Code}: {Message}", tenant.Code, ex.Message);
                }
            }
        }

        private static async Task ExecuteBatchedSqlAsync(string connectionString, string script, CancellationToken cancellationToken)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            var batches = script.Split(new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var batch in batches)
            {
                var sql = PrepareSqlBatch(batch);
                if (string.IsNullOrWhiteSpace(sql))
                {
                    continue;
                }

                await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            }
        }

        /// <summary>
        /// Strips leading blank/comment lines so batches like "-- header\nALTER TABLE ..." are not skipped.
        /// </summary>
        private static string? PrepareSqlBatch(string batch)
        {
            var lines = batch.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .SkipWhile(line =>
                    string.IsNullOrWhiteSpace(line) ||
                    line.TrimStart().StartsWith("--", StringComparison.Ordinal))
                .ToList();

            if (lines.Count == 0)
            {
                return null;
            }

            return string.Join('\n', lines);
        }

        private static string? ResolveSqlPath(string relativePath)
        {
            foreach (var root in SqlSearchRoots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
            {
                var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
