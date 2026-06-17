#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/hotel-reports")]
    [Produces("application/json")]
    public sealed class PmsHotelReportsController : ControllerBase
    {
        private readonly IPmsHotelReportService _reportService;

        public PmsHotelReportsController(IPmsHotelReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("reports/bookings")]
        [RequireLodgingReportPermission("bookings")]
        public async Task<IActionResult> BookingsReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetBookingsReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/receipts")]
        [RequireLodgingReportPermission("receipts")]
        public async Task<IActionResult> ReceiptsReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetReceiptsReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/disbursements")]
        [RequireLodgingReportPermission("disbursements")]
        public async Task<IActionResult> DisbursementsReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetDisbursementsReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/invoices")]
        [RequireLodgingReportPermission("invoices")]
        public async Task<IActionResult> InvoicesReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetInvoicesReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/credit-notes")]
        [RequireLodgingReportPermission("credit_notes")]
        public async Task<IActionResult> CreditNotesReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetCreditNotesReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/daily-journal")]
        [RequireLodgingReportPermission("daily_journal")]
        public async Task<IActionResult> DailyJournalReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetDailyJournalReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/network-cash-payments")]
        [RequireLodgingReportPermission("network_cash")]
        public async Task<IActionResult> NetworkCashPaymentsReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetNetworkCashPaymentsReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/departures")]
        [RequireLodgingReportPermission("departures")]
        public async Task<IActionResult> DeparturesReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetDeparturesReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/online-bookings")]
        [RequireLodgingReportPermission("online_bookings")]
        public async Task<IActionResult> OnlineBookingsReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetOnlineBookingsReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/unit-transfers")]
        [RequireLodgingReportPermission("unit_transfers")]
        public async Task<IActionResult> UnitTransfersReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetUnitTransfersReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/month-end-closing")]
        [RequireLodgingReportPermission("month_end_closing")]
        public async Task<IActionResult> MonthEndClosingReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetMonthEndClosingReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
