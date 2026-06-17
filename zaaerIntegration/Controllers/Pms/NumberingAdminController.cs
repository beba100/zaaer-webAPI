#pragma warning disable CS1591

using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Admin;
using zaaerIntegration.Security;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/numbering-admin")]
    [Produces("application/json")]
    public sealed class NumberingAdminController : ControllerBase
    {
        private const string Permission = "admin.numbering.manage";

        private readonly MasterDbContext _masterDb;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NumberingAdminController> _logger;

        public NumberingAdminController(MasterDbContext masterDb, IConfiguration configuration, ILogger<NumberingAdminController> logger)
        {
            _masterDb = masterDb;
            _configuration = configuration;
            _logger = logger;
        }

        private bool IsEnabled() => _configuration.GetValue<bool>("Features:NumberingAdmin");

        private IActionResult Disabled() => NotFound(new { success = false, message = "Not found." });

        [HttpGet("status")]
        [RequirePermission(Permission)]
        public async Task<IActionResult> Status([FromQuery] int? hotelZaaerId, CancellationToken cancellationToken)
        {
            if (!IsEnabled())
                return Disabled();

            var types = await QueryRowsAsync(
                "SELECT doc_code, prefix, padding, scope_level, include_hotel_in_number, uses_global_zaaer_id, separator, zaaer_entity_code, is_active FROM dbo.DocumentTypes ORDER BY doc_code",
                cancellationToken);

            var entity = await QueryRowsAsync(
                "SELECT entity_code, current_value, updated_at FROM dbo.EntityZaaerCounters ORDER BY entity_code",
                cancellationToken);

            object? counters = null;
            if (hotelZaaerId is > 0)
            {
                counters = await QueryRowsAsync(
                    "SELECT tenant_id, hotel_zaaer_id, local_hotel_id, doc_code, current_value, updated_at FROM dbo.DocumentCounters WHERE hotel_zaaer_id = @HotelZaaerId ORDER BY doc_code",
                    cancellationToken,
                    new SqlParameter("@HotelZaaerId", SqlDbType.Int) { Value = hotelZaaerId.Value });
            }

            var audit = await QueryRowsAsync(
                "SELECT TOP (50) audit_id, tenant_id, hotel_zaaer_id, local_hotel_id, doc_code, numeric_value, document_no, zaaer_id, status, request_ref, created_at, committed_at, voided_at FROM dbo.NumberGenerationAudit ORDER BY audit_id DESC",
                cancellationToken);

            return Ok(new { success = true, data = new { documentTypes = types, entityZaaerCounters = entity, documentCounters = counters, audit } });
        }

        [HttpPost("seed/all")]
        [RequirePermission(Permission)]
        public async Task<IActionResult> SeedAll(CancellationToken cancellationToken)
        {
            if (!IsEnabled())
                return Disabled();

            var tenants = await _masterDb.Tenants.AsNoTracking()
                .Where(t => t.DatabaseName != null && t.DatabaseName.Trim() != string.Empty)
                .OrderBy(t => t.Id)
                .Select(t => new { t.Id, t.Code, t.DatabaseName })
                .ToListAsync(cancellationToken);

            var processed = 0;
            var skipped = 0;
            var failed = 0;
            var failures = new List<object>();

            foreach (var t in tenants)
            {
                if (string.IsNullOrWhiteSpace(t.DatabaseName))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    await ExecSeedTenantAsync(t.Id, t.DatabaseName.Trim(), cancellationToken);
                    processed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    failures.Add(new { tenantId = t.Id, tenantCode = t.Code, databaseName = t.DatabaseName, error = ex.Message });
                    _logger.LogError(ex, "[NumberingAdmin] SeedAll failed for tenant {TenantId} ({TenantCode})", t.Id, t.Code);
                }
            }

            var entity = await _masterDb.Set<dynamic>()
                .FromSqlRaw("SELECT entity_code, current_value, updated_at FROM dbo.EntityZaaerCounters ORDER BY entity_code")
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new
                {
                    tenantsProcessed = processed,
                    tenantsSkipped = skipped,
                    tenantsFailed = failed,
                    failures,
                    entityZaaerCounters = entity
                }
            });
        }

        [HttpPost("seed/tenant")]
        [RequirePermission(Permission)]
        public async Task<IActionResult> SeedTenant([FromBody] SeedTenantRequestDto dto, CancellationToken cancellationToken)
        {
            if (!IsEnabled())
                return Disabled();

            var tenant = await ResolveTenantAsync(dto.TenantId, dto.TenantCode, cancellationToken);
            if (tenant == null)
                return NotFound(new { success = false, message = "Tenant not found." });

            if (string.IsNullOrWhiteSpace(tenant.DatabaseName))
                return BadRequest(new { success = false, message = "Tenant.DatabaseName is missing." });

            await ExecSeedTenantAsync(tenant.Id, tenant.DatabaseName.Trim(), cancellationToken);

            return Ok(new { success = true, message = "Seed completed.", data = new { tenant.Id, tenant.Code, tenant.DatabaseName } });
        }

        [HttpPost("sync-entity-from-audit")]
        [RequirePermission(Permission)]
        public async Task<IActionResult> SyncEntityFromAudit([FromQuery] string docCode, CancellationToken cancellationToken)
        {
            if (!IsEnabled())
                return Disabled();

            if (string.IsNullOrWhiteSpace(docCode))
                return BadRequest(new { success = false, message = "docCode is required." });

            var code = docCode.Trim();

            if (string.Equals(code, "all", StringComparison.OrdinalIgnoreCase))
            {
                var all = await QueryStringListAsync(
                    "SELECT doc_code FROM dbo.DocumentTypes WHERE is_active = 1 AND uses_global_zaaer_id = 1 ORDER BY doc_code",
                    cancellationToken);

                var results = new List<object>();
                foreach (var row in all)
                {
                    var r = await SyncSingleEntityFromAuditAsync(row, cancellationToken);
                    results.Add(r);
                }

                return Ok(new { success = true, data = new { docCode = "all", results } });
            }

            var single = await SyncSingleEntityFromAuditAsync(code, cancellationToken);
            return Ok(new { success = true, data = single });
        }

        [HttpPost("sync-document-counters-from-audit")]
        [RequirePermission(Permission)]
        public async Task<IActionResult> SyncDocumentCountersFromAudit(
            [FromQuery] int hotelZaaerId,
            [FromQuery] string docCode,
            CancellationToken cancellationToken)
        {
            if (!IsEnabled())
                return Disabled();

            if (hotelZaaerId <= 0)
                return BadRequest(new { success = false, message = "hotelZaaerId is required." });

            if (string.IsNullOrWhiteSpace(docCode))
                return BadRequest(new { success = false, message = "docCode is required." });

            var code = docCode.Trim();
            if (string.Equals(code, "all", StringComparison.OrdinalIgnoreCase))
            {
                var all = await QueryStringListAsync(
                    "SELECT doc_code FROM dbo.DocumentTypes WHERE is_active = 1 ORDER BY doc_code",
                    cancellationToken);

                var results = new List<object>();
                foreach (var row in all)
                {
                    var r = await SyncSingleDocCounterFromAuditAsync(hotelZaaerId, row, cancellationToken);
                    results.Add(r);
                }

                return Ok(new { success = true, data = new { hotelZaaerId, docCode = "all", results } });
            }

            var single = await SyncSingleDocCounterFromAuditAsync(hotelZaaerId, code, cancellationToken);
            return Ok(new { success = true, data = single });
        }

        [HttpPost("ensure-document-counters")]
        [RequirePermission(Permission)]
        public async Task<IActionResult> EnsureDocumentCounters([FromBody] EnsureDocumentCountersRequestDto dto, CancellationToken cancellationToken)
        {
            if (!IsEnabled())
                return Disabled();

            await ExecStoredProcedureAsync(
                "dbo.SeedDocumentCounter",
                cancellationToken,
                new SqlParameter("@HotelZaaerId", SqlDbType.Int) { Value = dto.HotelZaaerId },
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = "customer" },
                new SqlParameter("@CurrentValue", SqlDbType.BigInt) { Value = 0 },
                new SqlParameter("@TenantId", SqlDbType.Int) { Value = dto.TenantId },
                new SqlParameter("@LocalHotelId", SqlDbType.Int) { Value = dto.LocalHotelId }
            );

            // Note: the EnsureDocumentCountersForHotel.sql uses SeedDocumentCounter for each doc type.
            // Here we do a simple full ensure by inserting missing rows with current_value=0 via SQL.
            await _masterDb.Database.ExecuteSqlRawAsync(@"
INSERT INTO dbo.DocumentCounters(tenant_id, hotel_zaaer_id, local_hotel_id, doc_code, current_value)
SELECT {0}, {1}, {2}, dt.doc_code, 0
FROM dbo.DocumentTypes dt
WHERE dt.is_active = 1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.DocumentCounters c
      WHERE c.hotel_zaaer_id = {1} AND c.doc_code = dt.doc_code
  );", new object[] { dto.TenantId, dto.HotelZaaerId, dto.LocalHotelId }, cancellationToken);

            return Ok(new { success = true, message = "Ensure completed." });
        }

        [HttpPost("tenants")]
        [RequirePermission(Permission)]
        public async Task<IActionResult> CreateOrUpdateTenant([FromBody] CreateOrUpdateTenantRequestDto dto, CancellationToken cancellationToken)
        {
            if (!IsEnabled())
                return Disabled();

            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.DatabaseName) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { success = false, message = "Code, Name, and DatabaseName are required." });

            var code = dto.Code.Trim();
            var dbName = dto.DatabaseName.Trim();

            var tenant = dto.Id.HasValue
                ? await _masterDb.Tenants.FirstOrDefaultAsync(t => t.Id == dto.Id.Value, cancellationToken)
                : await _masterDb.Tenants.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);

            if (tenant == null)
            {
                tenant = new FinanceLedgerAPI.Models.Tenant();
                _masterDb.Tenants.Add(tenant);
            }

            tenant.Code = code;
            tenant.Name = dto.Name.Trim();
            tenant.NameEn = string.IsNullOrWhiteSpace(dto.NameEn) ? null : dto.NameEn.Trim();
            tenant.DatabaseName = dbName;
            tenant.ZaaerId = dto.ZaaerId;

            await _masterDb.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, data = new { tenant.Id, tenant.Code, tenant.Name, tenant.DatabaseName, tenant.ZaaerId } });
        }

        private async Task<FinanceLedgerAPI.Models.Tenant?> ResolveTenantAsync(int? tenantId, string? tenantCode, CancellationToken cancellationToken)
        {
            if (tenantId is > 0)
                return await _masterDb.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                var code = tenantCode.Trim();
                return await _masterDb.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
            }

            return null;
        }

        private async Task ExecSeedTenantAsync(int tenantId, string tenantDatabase, CancellationToken cancellationToken)
        {
            await ExecStoredProcedureAsync(
                "dbo.SeedCentralNumberingForTenant",
                cancellationToken,
                new SqlParameter("@TenantId", SqlDbType.Int) { Value = tenantId },
                new SqlParameter("@TenantDatabase", SqlDbType.NVarChar, 128) { Value = tenantDatabase });
        }

        private async Task<object> SyncSingleEntityFromAuditAsync(string docCode, CancellationToken cancellationToken)
        {
            var code = docCode.Trim();
            var entityCode = await ResolveEntityCodeAsync(code, cancellationToken);

            var maxZaaer = await ExecuteScalarLongAsync(@"
SELECT ISNULL(MAX(TRY_CAST(zaaer_id AS BIGINT)), 0)
FROM dbo.NumberGenerationAudit
WHERE doc_code = @DocCode
  AND status IN (N'reserved', N'committed');",
                cancellationToken,
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = code });

            await ExecStoredProcedureAsync(
                "dbo.SeedEntityZaaerCounter",
                cancellationToken,
                new SqlParameter("@EntityCode", SqlDbType.NVarChar, 50) { Value = entityCode },
                new SqlParameter("@CurrentValue", SqlDbType.BigInt) { Value = maxZaaer });

            return new { docCode = code, entityCode, maxZaaerId = maxZaaer };
        }

        private async Task<object> SyncSingleDocCounterFromAuditAsync(int hotelZaaerId, string docCode, CancellationToken cancellationToken)
        {
            var code = docCode.Trim();

            var maxNumeric = await ExecuteScalarLongAsync(@"
SELECT ISNULL(MAX(TRY_CAST(numeric_value AS BIGINT)), 0)
FROM dbo.NumberGenerationAudit
WHERE doc_code = @DocCode
  AND hotel_zaaer_id = @HotelZaaerId
  AND status IN (N'reserved', N'committed');",
                cancellationToken,
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = code },
                new SqlParameter("@HotelZaaerId", SqlDbType.Int) { Value = hotelZaaerId });

            await ExecStoredProcedureAsync(
                "dbo.SeedDocumentCounter",
                cancellationToken,
                new SqlParameter("@HotelZaaerId", SqlDbType.Int) { Value = hotelZaaerId },
                new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = code },
                new SqlParameter("@CurrentValue", SqlDbType.BigInt) { Value = maxNumeric });

            return new { hotelZaaerId, docCode = code, maxNumericValue = maxNumeric };
        }

        private async Task<string> ResolveEntityCodeAsync(string docCode, CancellationToken cancellationToken)
        {
            var code = docCode.Trim();
            var conn = (SqlConnection)_masterDb.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = @"
SELECT TOP (1) COALESCE(zaaer_entity_code, doc_code)
FROM dbo.DocumentTypes
WHERE doc_code = @DocCode;";
            cmd.Parameters.Add(new SqlParameter("@DocCode", SqlDbType.NVarChar, 50) { Value = code });
            var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
            var resolved = scalar == null || scalar == DBNull.Value ? null : Convert.ToString(scalar);
            return string.IsNullOrWhiteSpace(resolved) ? code : resolved.Trim();
        }

        private async Task<long> ExecuteScalarLongAsync(string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
        {
            var conn = (SqlConnection)_masterDb.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            cmd.Parameters.AddRange(parameters);
            var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
            return scalar == null || scalar == DBNull.Value ? 0L : Convert.ToInt64(scalar);
        }

        private async Task<List<Dictionary<string, object?>>> QueryRowsAsync(string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
        {
            var conn = (SqlConnection)_masterDb.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            if (parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var value = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                    row[name] = value;
                }
                rows.Add(row);
            }

            return rows;
        }

        private async Task<List<string>> QueryStringListAsync(string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
        {
            var conn = (SqlConnection)_masterDb.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = sql;
            if (parameters.Length > 0)
            {
                cmd.Parameters.AddRange(parameters);
            }

            var rows = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var value = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                if (!string.IsNullOrWhiteSpace(value))
                {
                    rows.Add(value.Trim());
                }
            }

            return rows;
        }

        private async Task ExecStoredProcedureAsync(string name, CancellationToken cancellationToken, params SqlParameter[] parameters)
        {
            var connection = (SqlConnection)_masterDb.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = name;
            cmd.Parameters.AddRange(parameters);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

