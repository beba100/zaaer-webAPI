using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers.Jobs
{
    /// <summary>
    /// Reports Master DB number allocations stuck in reserved status (monitoring / ops).
    /// </summary>
    [Route("api/jobs/[controller]")]
    [ApiController]
    public class NumberingAuditReconciliationJobController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly INumberingAuditReconciliationService _reconciliationService;
        private readonly ILogger<NumberingAuditReconciliationJobController> _logger;

        public NumberingAuditReconciliationJobController(
            IConfiguration configuration,
            INumberingAuditReconciliationService reconciliationService,
            ILogger<NumberingAuditReconciliationJobController> logger)
        {
            _configuration = configuration;
            _reconciliationService = reconciliationService;
            _logger = logger;
        }

        [HttpGet("stale-reserved")]
        [HttpPost("stale-reserved")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ExecuteStaleReservedJob(
            [FromHeader(Name = "X-API-Key")] string? apiKeyHeader,
            [FromQuery] string? apiKey,
            [FromQuery] int? staleMinutes = null,
            [FromQuery] int? maxRows = null,
            CancellationToken cancellationToken = default)
        {
            if (!_configuration.GetValue<bool>("Jobs:NumberingAuditReconciliation:Enabled", true))
            {
                return Ok(new { success = true, message = "Numbering audit reconciliation job is disabled." });
            }

            var configuredApiKey = _configuration["Jobs:NumberingAuditReconciliation:ApiKey"];
            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                return StatusCode(500, new { error = "Jobs:NumberingAuditReconciliation:ApiKey not configured." });
            }

            var providedApiKey = apiKeyHeader ?? apiKey;
            if (!string.Equals(providedApiKey, configuredApiKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "Invalid API Key" });
            }

            var thresholdMinutes = staleMinutes
                ?? _configuration.GetValue<int>("Jobs:NumberingAuditReconciliation:StaleMinutes", 15);
            var rowLimit = maxRows
                ?? _configuration.GetValue<int>("Jobs:NumberingAuditReconciliation:MaxRows", 500);

            var startTime = KsaTime.Now;
            _logger.LogInformation("[Numbering Audit Job] Started at {Start}", startTime);

            var result = await _reconciliationService.GetStaleReservedAsync(
                thresholdMinutes,
                rowLimit,
                cancellationToken);

            return Ok(new
            {
                success = true,
                startedAt = startTime,
                staleMinutesThreshold = result.StaleMinutesThreshold,
                staleReservedCount = result.StaleReservedCount,
                reportedCount = result.ReportedCount,
                rows = result.Rows.Select(r => new
                {
                    r.AuditId,
                    r.TenantId,
                    r.HotelZaaerId,
                    r.LocalHotelId,
                    r.DocCode,
                    r.DocumentNo,
                    r.ZaaerId,
                    r.RequestRef,
                    r.GeneratedBy,
                    r.CreatedAtUtc,
                    r.Status
                })
            });
        }
    }
}
