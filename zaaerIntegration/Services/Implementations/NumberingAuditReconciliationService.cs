using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class NumberingAuditReconciliationService : INumberingAuditReconciliationService
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ILogger<NumberingAuditReconciliationService> _logger;

        public NumberingAuditReconciliationService(
            MasterDbContext masterDbContext,
            ILogger<NumberingAuditReconciliationService> logger)
        {
            _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<NumberingAuditReconciliationResult> GetStaleReservedAsync(
            int staleMinutes,
            int maxRows,
            CancellationToken cancellationToken = default)
        {
            if (staleMinutes < 1)
            {
                staleMinutes = 15;
            }

            if (maxRows < 1)
            {
                maxRows = 500;
            }

            var rows = new List<StaleNumberGenerationAuditRow>();
            var connection = (SqlConnection)_masterDbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            const string sql = """
                SELECT COUNT(1)
                FROM dbo.NumberGenerationAudit WITH (NOLOCK)
                WHERE status = N'reserved'
                  AND created_at < DATEADD(MINUTE, -@StaleMinutes, SYSUTCDATETIME());

                SELECT TOP (@MaxRows)
                    audit_id,
                    tenant_id,
                    hotel_zaaer_id,
                    local_hotel_id,
                    doc_code,
                    document_no,
                    zaaer_id,
                    request_ref,
                    generated_by,
                    created_at,
                    status
                FROM dbo.NumberGenerationAudit WITH (NOLOCK)
                WHERE status = N'reserved'
                  AND created_at < DATEADD(MINUTE, -@StaleMinutes, SYSUTCDATETIME())
                ORDER BY created_at ASC;
                """;

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@StaleMinutes", SqlDbType.Int) { Value = staleMinutes });
            command.Parameters.Add(new SqlParameter("@MaxRows", SqlDbType.Int) { Value = maxRows });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var totalStale = 0;
            if (await reader.ReadAsync(cancellationToken))
            {
                totalStale = reader.GetInt32(0);
            }

            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(new StaleNumberGenerationAuditRow(
                        reader.GetInt64(0),
                        reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        reader.IsDBNull(2) ? null : reader.GetInt32(2),
                        reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5),
                        reader.IsDBNull(6) ? null : reader.GetInt64(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7),
                        reader.IsDBNull(8) ? null : reader.GetString(8),
                        reader.GetDateTime(9),
                        reader.GetString(10)));
                }
            }

            if (totalStale > 0)
            {
                _logger.LogWarning(
                    "Numbering audit reconciliation found {StaleCount} reserved rows older than {StaleMinutes} minutes (reporting {ReportedCount}).",
                    totalStale,
                    staleMinutes,
                    rows.Count);
            }

            return new NumberingAuditReconciliationResult(totalStale, rows.Count, staleMinutes, rows);
        }
    }
}
