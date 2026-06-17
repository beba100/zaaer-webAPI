#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Integrations;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Integrations;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/integrations")]
    [Produces("application/json")]
    public sealed class PmsIntegrationsController : ControllerBase
    {
        private readonly IPmsNtmpSettingsService _ntmpSettings;
        private readonly IPmsShomoosSettingsService _shomoosSettings;
        private readonly IPmsZatcaSettingsService _zatcaSettings;
        private readonly IPmsZatcaOnboardingService _zatcaOnboarding;
        private readonly IPmsZatcaComplianceService _zatcaCompliance;
        private readonly IPmsIntegrationResponsesService _responses;
        private readonly IPmsBaladyReportService _baladyReport;
        private readonly INtmpIntegrationOrchestrator _ntmpOrchestrator;
        private readonly IZatcaSubmissionOrchestrator _zatcaSubmission;

        public PmsIntegrationsController(
            IPmsNtmpSettingsService ntmpSettings,
            IPmsShomoosSettingsService shomoosSettings,
            IPmsZatcaSettingsService zatcaSettings,
            IPmsZatcaOnboardingService zatcaOnboarding,
            IPmsZatcaComplianceService zatcaCompliance,
            IPmsIntegrationResponsesService responses,
            IPmsBaladyReportService baladyReport,
            INtmpIntegrationOrchestrator ntmpOrchestrator,
            IZatcaSubmissionOrchestrator zatcaSubmission)
        {
            _ntmpSettings = ntmpSettings;
            _shomoosSettings = shomoosSettings;
            _zatcaSettings = zatcaSettings;
            _zatcaOnboarding = zatcaOnboarding;
            _zatcaCompliance = zatcaCompliance;
            _responses = responses;
            _baladyReport = baladyReport;
            _ntmpOrchestrator = ntmpOrchestrator;
            _zatcaSubmission = zatcaSubmission;
        }

        [HttpGet("ntmp")]
        [RequirePermission("integrations.view")]
        public async Task<IActionResult> GetNtmp(CancellationToken cancellationToken)
        {
            var data = await _ntmpSettings.GetCurrentAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPut("ntmp")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> PutNtmp(
            [FromBody] PmsUpsertNtmpSettingsDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var data = await _ntmpSettings.UpsertCurrentAsync(dto, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("ntmp/test-connection")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> TestNtmpConnection(CancellationToken cancellationToken)
        {
            var data = await _ntmpSettings.TestConnectionAsync(cancellationToken);
            return Ok(new { success = data.Success, data });
        }

        [HttpPost("ntmp/reservations/{reservationId:int}/retry")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> RetryNtmp(
            [FromRoute] int reservationId,
            CancellationToken cancellationToken)
        {
            await _ntmpOrchestrator.RetryReservationAsync(reservationId, cancellationToken);
            return Ok(new { success = true, message = "NTMP retry queued." });
        }

        [HttpGet("shomoos")]
        [RequirePermission("integrations.view")]
        public async Task<IActionResult> GetShomoos(CancellationToken cancellationToken)
        {
            var data = await _shomoosSettings.GetCurrentAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPut("shomoos")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> PutShomoos(
            [FromBody] PmsUpsertShomoosSettingsDto dto,
            CancellationToken cancellationToken)
        {
            var data = await _shomoosSettings.UpsertCurrentAsync(dto, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("zatca")]
        [RequirePermission("integrations.view")]
        public async Task<IActionResult> GetZatca(CancellationToken cancellationToken)
        {
            var data = await _zatcaSettings.GetCurrentAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPut("zatca")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> PutZatca(
            [FromBody] PmsUpsertZatcaSettingsDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var data = await _zatcaSettings.UpsertCurrentAsync(dto, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("zatca/device")]
        [RequirePermission("integrations.view")]
        public async Task<IActionResult> GetZatcaDevice(CancellationToken cancellationToken)
        {
            var data = await _zatcaOnboarding.GetDeviceStatusAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("zatca/onboard")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> OnboardZatcaDevice(
            [FromBody] PmsZatcaOnboardRequestDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var result = await _zatcaOnboarding.OnboardDeviceAsync(dto, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, data = result, message = result.Message });
            }

            return Ok(new { success = true, data = result, message = result.Message });
        }

        [HttpPost("zatca/production-csid")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> RequestZatcaProductionCsid(CancellationToken cancellationToken)
        {
            var result = await _zatcaOnboarding.RequestProductionCsidAsync(cancellationToken);
            if (!result.Success)
            {
                return BadRequest(new { success = false, data = result, message = result.Message });
            }

            return Ok(new { success = true, data = result, message = result.Message });
        }

        [HttpPost("zatca/run-compliance")]
        [RequirePermission("integrations.manage")]
        public async Task<IActionResult> RunZatcaCompliance(
            [FromBody] PmsZatcaComplianceRunRequestDto? dto,
            CancellationToken cancellationToken)
        {
            var run = await _zatcaCompliance.RunAllSixForCurrentHotelAsync(
                dto?.ApiEnvironment,
                cancellationToken);

            var data = new PmsZatcaComplianceRunResultDto
            {
                Success = run.AllPassed,
                AllPassed = run.AllPassed,
                Environment = run.Environment,
                Message = run.Message ?? string.Empty,
                Items = run.Items.Select(i => new PmsZatcaComplianceItemResultDto
                {
                    DocumentType = i.DocumentType.ToString(),
                    Success = i.Success,
                    ErrorMessage = i.ErrorMessage,
                    HttpStatusCode = i.HttpStatusCode
                }).ToList()
            };

            if (!run.AllPassed)
            {
                return BadRequest(new { success = false, data, message = data.Message });
            }

            return Ok(new { success = true, data, message = data.Message });
        }

        [HttpPost("zatca/send-document")]
        public async Task<IActionResult> SendZatcaDocument(
            [FromBody] PmsZatcaSendDocumentRequestDto dto,
            CancellationToken cancellationToken)
        {
            if (dto == null || dto.DocumentId <= 0 || string.IsNullOrWhiteSpace(dto.DocumentKind))
            {
                return BadRequest(new { success = false, message = "DocumentKind and DocumentId are required." });
            }

            var kind = dto.DocumentKind.Trim().ToLowerInvariant();
            string permission;
            ZatcaSingleDocumentResult result;

            switch (kind)
            {
                case "invoice":
                    permission = "finance.invoice.send_zatca";
                    if (!await HasPermissionAsync(permission, cancellationToken))
                    {
                        return Forbid();
                    }

                    result = await _zatcaSubmission.ProcessInvoiceByIdAsync(dto.DocumentId, cancellationToken);
                    break;
                case "credit_note":
                case "creditnote":
                    permission = "finance.credit_note.send_zatca";
                    if (!await HasPermissionAsync(permission, cancellationToken))
                    {
                        return Forbid();
                    }

                    result = await _zatcaSubmission.ProcessCreditNoteByIdAsync(dto.DocumentId, cancellationToken);
                    break;
                case "debit_note":
                case "debitnote":
                    permission = "finance.debit_note.send_zatca";
                    if (!await HasPermissionAsync(permission, cancellationToken))
                    {
                        return Forbid();
                    }

                    result = await _zatcaSubmission.ProcessDebitNoteByIdAsync(dto.DocumentId, cancellationToken);
                    break;
                default:
                    return BadRequest(new { success = false, message = "Unsupported DocumentKind." });
            }

            var payload = new PmsZatcaSendDocumentResultDto
            {
                Success = result.Success,
                ZatcaStatus = result.ZatcaStatus,
                Message = result.Message
            };

            if (!result.Success)
            {
                return BadRequest(new { success = false, data = payload, message = result.Message });
            }

            return Ok(new { success = true, data = payload, message = "Document submitted to ZATCA." });
        }

        [HttpGet("responses")]
        [RequirePermission("integrations.view")]
        public async Task<IActionResult> SearchResponses(
            [FromQuery] PmsIntegrationResponseQueryDto query,
            CancellationToken cancellationToken)
        {
            var data = await _responses.SearchAsync(query, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("responses/{responseId:int}")]
        [RequirePermission("integrations.view")]
        public async Task<IActionResult> GetResponse(
            [FromRoute] int responseId,
            CancellationToken cancellationToken)
        {
            var data = await _responses.GetByIdAsync(responseId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Response not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpGet("balady/report")]
        [RequirePermission("integrations.balady.view")]
        public async Task<IActionResult> GetBaladyReport(
            [FromQuery] BaladyReportQueryDto query,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid year or month." });
            }

            var data = await _baladyReport.GetReportAsync(query, cancellationToken);
            return Ok(new { success = true, data });
        }

        private async Task<bool> HasPermissionAsync(string permissionCode, CancellationToken cancellationToken)
        {
            var currentUser = HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();
            if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue)
            {
                return false;
            }

            var permissionService = HttpContext.RequestServices.GetRequiredService<IPermissionService>();
            return await permissionService.HasPermissionAsync(
                currentUser.UserId.Value,
                currentUser.TenantId.Value,
                permissionCode,
                currentUser.AuthMode,
                cancellationToken);
        }
    }
}
