#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/cash-ledger")]
    [Produces("application/json")]
    public sealed class PmsCashLedgerController : ControllerBase
    {
        private readonly IPmsCashLedgerService _service;

        public PmsCashLedgerController(IPmsCashLedgerService service)
        {
            _service = service;
        }

        [HttpGet("report")]
        [RequireAnyPermission(
            "hall.reports.cash_ledger",
            "hall.reports",
            "hotel.reports.cash_ledger",
            "hotel.reports",
            "resort.reports.cash_ledger",
            "resort.reports")]
        public async Task<IActionResult> GetReport(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken)
        {
            var to = (toDate ?? DateTime.Today).Date;
            var from = (fromDate ?? new DateTime(to.Year, to.Month, 1)).Date;
            var data = await _service.GetReportAsync(from, to, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("backfill")]
        [RequirePermission("hall.reports")]
        public async Task<IActionResult> Backfill(CancellationToken cancellationToken)
        {
            await _service.BackfillAsync(cancellationToken);
            return Ok(new { success = true });
        }
    }
}
