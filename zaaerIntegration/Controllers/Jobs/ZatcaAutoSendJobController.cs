using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Integrations;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers.Jobs
{
    /// <summary>
    /// Background job: submit pending invoices, credit notes, and debit notes to ZATCA.
    /// </summary>
    [Route("api/jobs/[controller]")]
    [ApiController]
    public class ZatcaAutoSendJobController : ControllerBase
    {
        private readonly ILogger<ZatcaAutoSendJobController> _logger;
        private readonly MasterDbContext _masterDb;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public ZatcaAutoSendJobController(
            ILogger<ZatcaAutoSendJobController> logger,
            MasterDbContext masterDb,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _masterDb = masterDb;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        [HttpGet("zatca-auto-send")]
        [HttpPost("zatca-auto-send")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ExecuteAutoSendJob(
            [FromHeader(Name = "X-API-Key")] string? apiKeyHeader,
            [FromQuery] string? apiKey,
            [FromQuery] int? maxRetries = null,
            [FromQuery] int? batchSize = null,
            CancellationToken cancellationToken = default)
        {
            if (!_configuration.GetValue<bool>("Jobs:ZatcaAutoSend:Enabled", true))
            {
                return Ok(new { success = true, message = "ZATCA auto-send job is disabled." });
            }

            var configuredApiKey = _configuration["Jobs:ZatcaAutoSend:ApiKey"];
            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                return StatusCode(500, new { error = "Jobs:ZatcaAutoSend:ApiKey not configured." });
            }

            var providedApiKey = apiKeyHeader ?? apiKey;
            if (providedApiKey != configuredApiKey)
            {
                return Unauthorized(new { error = "Invalid API Key" });
            }

            var jobMaxRetries = maxRetries ?? _configuration.GetValue<int>("Jobs:ZatcaAutoSend:MaxRetries", 3);
            var jobBatchSize = batchSize ?? _configuration.GetValue<int>("Jobs:ZatcaAutoSend:BatchSize", 10);
            var startTime = KsaTime.Now;

            _logger.LogInformation("[ZATCA Job] Started at {Start}", startTime);

            var tenants = await _masterDb.Tenants.AsNoTracking().ToListAsync(cancellationToken);
            var totals = new ZatcaBatchProcessResult();
            var tenantErrors = 0;

            foreach (var tenant in tenants)
            {
                if (string.IsNullOrWhiteSpace(tenant.DatabaseName))
                {
                    continue;
                }

                try
                {
                    var connectionString = BuildConnectionString(tenant);
                    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseSqlServer(connectionString)
                        .Options;

                    await using var db = new ApplicationDbContext(options);
                    using var scope = _serviceProvider.CreateScope();
                    var orchestrator = new ZatcaSubmissionOrchestrator(
                        db,
                        new ZatcaIntegrationSchemaEnsurer(db),
                        new ZatcaProfileResolver(db),
                        scope.ServiceProvider.GetRequiredService<IZatcaUblBuilder>(),
                        scope.ServiceProvider.GetRequiredService<IZatcaGatewayClient>(),
                        scope.ServiceProvider.GetRequiredService<IZatcaAcceptLanguageResolver>(),
                        scope.ServiceProvider.GetRequiredService<IIntegrationSecretProtector>(),
                        scope.ServiceProvider.GetRequiredService<ILogger<ZatcaSubmissionOrchestrator>>());

                    var stats = await orchestrator.ProcessPendingBatchAsync(
                        jobMaxRetries,
                        jobBatchSize,
                        cancellationToken);

                    totals.Add(stats);

                    if (stats.InvoicesProcessed + stats.CreditNotesProcessed + stats.DebitNotesProcessed > 0)
                    {
                        _logger.LogInformation(
                            "[ZATCA Job] Tenant {Code}: inv {Ok}/{Total}, cn {CnOk}/{CnTotal}, dn {DnOk}/{DnTotal}",
                            tenant.Code,
                            stats.InvoicesSucceeded,
                            stats.InvoicesProcessed,
                            stats.CreditNotesSucceeded,
                            stats.CreditNotesProcessed,
                            stats.DebitNotesSucceeded,
                            stats.DebitNotesProcessed);
                    }
                }
                catch (Exception ex)
                {
                    tenantErrors++;
                    _logger.LogError(ex, "[ZATCA Job] Tenant {Code} failed", tenant.Code);
                }
            }

            var endTime = KsaTime.Now;
            return Ok(new
            {
                success = true,
                message = "ZATCA auto-send job completed",
                startTime,
                endTime,
                duration = (endTime - startTime).ToString(@"hh\:mm\:ss"),
                tenants = new { processed = tenants.Count, errors = tenantErrors },
                invoices = new
                {
                    processed = totals.InvoicesProcessed,
                    succeeded = totals.InvoicesSucceeded,
                    failed = totals.InvoicesFailed
                },
                creditNotes = new
                {
                    processed = totals.CreditNotesProcessed,
                    succeeded = totals.CreditNotesSucceeded,
                    failed = totals.CreditNotesFailed
                },
                debitNotes = new
                {
                    processed = totals.DebitNotesProcessed,
                    succeeded = totals.DebitNotesSucceeded,
                    failed = totals.DebitNotesFailed
                }
            });
        }

        private string BuildConnectionString(Tenant tenant)
        {
            var server = _configuration["TenantDatabase:Server"]
                ?? throw new InvalidOperationException("TenantDatabase:Server is not configured.");
            var userId = _configuration["TenantDatabase:UserId"]
                ?? throw new InvalidOperationException("TenantDatabase:UserId is not configured.");
            var password = _configuration["TenantDatabase:Password"]
                ?? throw new InvalidOperationException("TenantDatabase:Password is not configured.");

            return $"Server={server}; Database={tenant.DatabaseName}; User Id={userId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
        }
    }
}
