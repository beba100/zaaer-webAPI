#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/hall-events")]
    [Produces("application/json")]
    public sealed class PmsHallEventsController : ControllerBase
    {
        private readonly IPmsHallEventService _service;
        private readonly IPmsHallReportService _reportService;

        public PmsHallEventsController(
            IPmsHallEventService service,
            IPmsHallReportService reportService)
        {
            _service = service;
            _reportService = reportService;
        }

        [HttpGet("lookups")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> GetLookups(CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetLookupsAsync(cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> List(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? eventStatus,
            [FromQuery] int? hallId,
            [FromQuery] string? fromDateHijri,
            [FromQuery] string? toDateHijri,
            [FromQuery] string? eventDateHijri,
            [FromQuery] int? hijriYear,
            [FromQuery] int? hijriMonth,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.ListEventsAsync(
                    fromDate,
                    toDate,
                    eventStatus,
                    hallId,
                    fromDateHijri,
                    toDateHijri,
                    eventDateHijri,
                    hijriYear,
                    hijriMonth,
                    cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("unpaid-balances")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> UnpaidBalances(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            [FromQuery] bool countOnly = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var data = await _service.GetUnpaidBalancesAsync(skip, take, countOnly, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{reservationId:int}/settlement")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> Settlement(int reservationId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetSettlementAsync(reservationId, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{reservationId:int}")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> Get(int reservationId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetEventAsync(reservationId, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{reservationId:int}")]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> Update(
            int reservationId,
            [FromBody] PmsUpdateHallEventDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var data = await _service.UpdateEventAsync(reservationId, dto, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{reservationId:int}/schedule")]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> UpdateSchedule(
            int reservationId,
            [FromBody] PmsUpdateHallEventScheduleDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var data = await _service.UpdateEventScheduleAsync(reservationId, dto, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> Create([FromBody] PmsCreateHallEventDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                var data = await _service.CreateEventAsync(dto, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{reservationId:int}/transition")]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> Transition(int reservationId, [FromBody] PmsTransitionHallEventStatusDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.TransitionStatusAsync(reservationId, dto, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{reservationId:int}/check-in")]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> CheckIn(int reservationId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.CheckInEventAsync(reservationId, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpPost("{reservationId:int}/deposit")]
        [RequirePermission("hall.finance.deposit")]
        public async Task<IActionResult> RecordDeposit(int reservationId, [FromBody] PmsRecordHallDepositDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.RecordDepositAsync(reservationId, dto, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{reservationId:int}/complete")]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> Complete(int reservationId, [FromBody] PmsCompleteHallEventDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.CompleteEventAsync(reservationId, dto, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Event not found." })
                    : Ok(new { success = true, data });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("scheduler")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> Scheduler([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetSchedulerItemsAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("dashboard")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetDashboardAsync(cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("occupancy")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> Occupancy(CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetOccupancyAsync(cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("sync-statuses")]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> SyncStatuses(CancellationToken cancellationToken)
        {
            try
            {
                await _service.SyncOperationalStatusesAsync(cancellationToken);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{reservationId:int}/function-sheet")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> GetFunctionSheet(int reservationId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetFunctionSheetAsync(reservationId, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Function sheet not found." })
                    : Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{reservationId:int}/function-sheet")]
        [RequirePermission("hall.function_sheet.edit")]
        public async Task<IActionResult> UpsertFunctionSheet(int reservationId, [FromBody] PmsFunctionSheetDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.UpsertFunctionSheetAsync(reservationId, dto, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("{reservationId:int}/function-sheet/approve")]
        [RequirePermission("hall.function_sheet.edit")]
        public async Task<IActionResult> ApproveFunctionSheet(int reservationId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.ApproveFunctionSheetAsync(reservationId, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Function sheet not found." })
                    : Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{reservationId:int}/function-sheet/print")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> PrintFunctionSheet(int reservationId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.PrintFunctionSheetAsync(reservationId, cancellationToken);
                if (result == null)
                {
                    return NotFound(new { success = false, message = "Function sheet not found." });
                }

                return File(result.Content, result.MimeType, result.FileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{reservationId:int}/contract/print")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> PrintContract(int reservationId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.PrintContractAsync(reservationId, cancellationToken);
                if (result == null)
                {
                    return NotFound(new { success = false, message = "Event not found." });
                }

                return File(result.Content, result.MimeType, result.FileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("alerts")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> Alerts([FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var data = await _service.ListAlertsAsync(unreadOnly, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("alerts/{alertId:int}/read")]
        [RequirePermission("hall.events.view")]
        public async Task<IActionResult> MarkAlertRead(int alertId, CancellationToken cancellationToken)
        {
            try
            {
                await _service.MarkAlertReadAsync(alertId, cancellationToken);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/daily")]
        [RequirePermission("hall.reports")]
        public async Task<IActionResult> DailyReport([FromQuery] DateTime date, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetDailyEventsReportAsync(date, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/utilization")]
        [RequirePermission("hall.reports")]
        public async Task<IActionResult> UtilizationReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetUtilizationReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/bookings")]
        [RequireHallReportPermission("bookings")]
        public async Task<IActionResult> BookingsReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string? eventStatus,
            [FromQuery] int? hallId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _reportService.GetBookingsReportAsync(
                    fromDate,
                    toDate,
                    eventStatus,
                    hallId,
                    cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("reports/receipts")]
        [RequireHallReportPermission("receipts")]
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
        [RequireHallReportPermission("disbursements")]
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
        [RequireHallReportPermission("invoices")]
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
        [RequireHallReportPermission("credit_notes")]
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
        [RequireHallReportPermission("daily_journal")]
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
        [RequireHallReportPermission("network_cash")]
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

        [HttpPatch("halls/{hallId:int}/preparation")]
        [RequirePermission("hall.events.manage")]
        public async Task<IActionResult> UpdatePreparation(int hallId, [FromBody] PmsUpdateHallPreparationDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _service.UpdateHallPreparationAsync(hallId, dto, cancellationToken);
                return ok
                    ? Ok(new { success = true })
                    : NotFound(new { success = false, message = "Hall not found." });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}
