using System.Data;
using FinanceLedgerAPI.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Centralized Master DB-backed number allocation service.
    /// </summary>
    public class NumberingService : INumberingService
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ITenantService _tenantService;
        private readonly ILogger<NumberingService> _logger;

        public NumberingService(
            MasterDbContext masterDbContext,
            ITenantService tenantService,
            ILogger<NumberingService> logger)
        {
            _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<GeneratedZaaerId> GetNextEntityZaaerIdAsync(
            string docCode,
            string? generatedBy = null,
            string? requestRef = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(docCode))
            {
                throw new ArgumentException("Document type (docCode) is required for entity Zaaer ID allocation.", nameof(docCode));
            }

            await using var command = CreateStoredProcedureCommand(
                "dbo.GetNextEntityZaaerId",
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = docCode.Trim() },
                new SqlParameter("@GeneratedBy", SqlDbType.NVarChar, 100) { Value = ToDbValue(generatedBy) },
                new SqlParameter("@RequestRef", SqlDbType.NVarChar, 150) { Value = ToDbValue(requestRef) });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Master DB did not return an entity Zaaer ID for '{docCode}'.");
            }

            return new GeneratedZaaerId(
                Convert.ToInt64(reader["ZaaerId"]),
                Convert.ToInt64(reader["AuditId"]));
        }

        public async Task<GeneratedDocumentNumber> GetNextDocumentNumberAsync(
            string docCode,
            int hotelId,
            string? generatedBy = null,
            string? requestRef = null,
            CancellationToken cancellationToken = default)
        {
            var scope = ResolveHotelScope(hotelId);

            await using var command = CreateStoredProcedureCommand(
                "dbo.GetNextDocumentNumber",
                new SqlParameter("@HotelZaaerId", SqlDbType.Int) { Value = scope.HotelZaaerId },
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = docCode },
                new SqlParameter("@TenantId", SqlDbType.Int) { Value = ToDbValue(scope.TenantId) },
                new SqlParameter("@LocalHotelId", SqlDbType.Int) { Value = ToDbValue(scope.LocalHotelId) },
                new SqlParameter("@GeneratedBy", SqlDbType.NVarChar, 100) { Value = ToDbValue(generatedBy) },
                new SqlParameter("@RequestRef", SqlDbType.NVarChar, 150) { Value = ToDbValue(requestRef) });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Master DB did not return a document number for '{docCode}'.");
            }

            return new GeneratedDocumentNumber(
                Convert.ToInt64(reader["NumericValue"]),
                Convert.ToString(reader["DocumentNo"]) ?? string.Empty,
                Convert.ToInt64(reader["AuditId"]));
        }

        public async Task<GeneratedBusinessIdentity> GetNextBusinessIdentityAsync(
            string docCode,
            int hotelId,
            string? generatedBy = null,
            string? requestRef = null,
            CancellationToken cancellationToken = default)
        {
            var scope = ResolveHotelScope(hotelId);

            await using var command = CreateStoredProcedureCommand(
                "dbo.GetNextBusinessIdentity",
                new SqlParameter("@HotelZaaerId", SqlDbType.Int) { Value = scope.HotelZaaerId },
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = docCode },
                new SqlParameter("@TenantId", SqlDbType.Int) { Value = ToDbValue(scope.TenantId) },
                new SqlParameter("@LocalHotelId", SqlDbType.Int) { Value = ToDbValue(scope.LocalHotelId) },
                new SqlParameter("@GeneratedBy", SqlDbType.NVarChar, 100) { Value = ToDbValue(generatedBy) },
                new SqlParameter("@RequestRef", SqlDbType.NVarChar, 150) { Value = ToDbValue(requestRef) });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException($"Master DB did not return a business identity for '{docCode}'.");
            }

            return new GeneratedBusinessIdentity(
                reader["ZaaerId"] == DBNull.Value ? null : Convert.ToInt64(reader["ZaaerId"]),
                Convert.ToInt64(reader["NumericValue"]),
                Convert.ToString(reader["DocumentNo"]) ?? string.Empty,
                Convert.ToInt64(reader["AuditId"]));
        }

        public async Task EnsureDocumentCounterAtLeastAsync(
            string docCode,
            int hotelId,
            long currentValue,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(docCode))
            {
                throw new ArgumentException("Document type (docCode) is required.", nameof(docCode));
            }

            if (currentValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentValue), "Current value cannot be negative.");
            }

            if (currentValue == 0)
            {
                return;
            }

            var scope = ResolveHotelScope(hotelId);

            await using var command = CreateStoredProcedureCommand(
                "dbo.SeedDocumentCounter",
                new SqlParameter("@HotelZaaerId", SqlDbType.Int) { Value = scope.HotelZaaerId },
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = docCode.Trim() },
                new SqlParameter("@CurrentValue", SqlDbType.BigInt) { Value = currentValue },
                new SqlParameter("@TenantId", SqlDbType.Int) { Value = ToDbValue(scope.TenantId) },
                new SqlParameter("@LocalHotelId", SqlDbType.Int) { Value = ToDbValue(scope.LocalHotelId) });

            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Ensured document counter for doc_code={DocCode}, hotel_zaaer_id={HotelZaaerId}, local_hotel_id={LocalHotelId}, at_least={CurrentValue}",
                docCode,
                scope.HotelZaaerId,
                scope.LocalHotelId,
                currentValue);
        }

        public async Task MarkCommittedAsync(long auditId, CancellationToken cancellationToken = default)
        {
            await ExecuteAuditStatusProcedureAsync("dbo.MarkNumberGenerationCommitted", auditId, null, cancellationToken);
        }

        public async Task MarkVoidedAsync(long auditId, string? reason = null, CancellationToken cancellationToken = default)
        {
            await ExecuteAuditStatusProcedureAsync("dbo.MarkNumberGenerationVoided", auditId, reason, cancellationToken);
        }

        private async Task ExecuteAuditStatusProcedureAsync(
            string procedureName,
            long auditId,
            string? reason,
            CancellationToken cancellationToken)
        {
            try
            {
                if (reason == null)
                {
                    await using var command = CreateStoredProcedureCommand(
                        procedureName,
                        new SqlParameter("@AuditId", SqlDbType.BigInt) { Value = auditId });
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    await using var command = CreateStoredProcedureCommand(
                        procedureName,
                        new SqlParameter("@AuditId", SqlDbType.BigInt) { Value = auditId },
                        new SqlParameter("@VoidReason", SqlDbType.NVarChar, 1000) { Value = reason });
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update number generation audit {AuditId} with procedure {ProcedureName}", auditId, procedureName);
            }
        }

        private SqlCommand CreateStoredProcedureCommand(string procedureName, params SqlParameter[] parameters)
        {
            var connection = (SqlConnection)_masterDbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddRange(parameters);

            return command;
        }

        private HotelScope ResolveHotelScope(int hotelId)
        {
            Tenant? tenant;
            try
            {
                tenant = _tenantService.GetTenant();
            }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException)
            {
                throw new InvalidOperationException(
                    "Cannot allocate document numbers without a resolved tenant. Provide a valid X-Hotel-Code header.",
                    ex);
            }

            if (tenant == null)
            {
                throw new InvalidOperationException(
                    "Cannot allocate document numbers: tenant was not resolved. Provide a valid X-Hotel-Code header.");
            }

            if (tenant.ZaaerId is not > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot allocate document numbers for tenant '{tenant.Code}': Tenants.ZaaerId is not configured in Master DB.");
            }

            if (hotelId <= 0)
            {
                throw new InvalidOperationException(
                    $"Cannot allocate document numbers: local hotel id must be positive (received {hotelId}).");
            }

            return new HotelScope(tenant.ZaaerId.Value, hotelId, tenant.Id);
        }

        private static object ToDbValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        }

        private static object ToDbValue(int? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private sealed record HotelScope(int HotelZaaerId, int LocalHotelId, int? TenantId);
    }
}
