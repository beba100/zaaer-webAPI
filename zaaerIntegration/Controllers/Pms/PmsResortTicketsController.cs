#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/resort-tickets")]
    [Produces("application/json")]
    public sealed class PmsResortTicketsController : ControllerBase
    {
        private readonly IPmsResortTicketService _service;
        private readonly IResortTicketGateLandingService _gateLanding;
        private readonly ICurrentUserContext _currentUser;
        private readonly ILogger<PmsResortTicketsController> _logger;

        public PmsResortTicketsController(
            IPmsResortTicketService service,
            IResortTicketGateLandingService gateLanding,
            ICurrentUserContext currentUser,
            ILogger<PmsResortTicketsController> logger)
        {
            _service = service;
            _gateLanding = gateLanding;
            _currentUser = currentUser;
            _logger = logger;
        }

        [HttpGet("lookups")]
        [RequirePermission("resort_tickets.view")]
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

        [HttpGet("types")]
        [RequirePermission("resort_tickets.view")]
        public async Task<IActionResult> ListTypes([FromQuery] string? category, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.ListTicketTypesAsync(category, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("types/{id:int}")]
        [RequirePermission("resort_tickets.view")]
        public async Task<IActionResult> GetType(int id, CancellationToken cancellationToken)
        {
            var data = await _service.GetTicketTypeAsync(id, cancellationToken);
            return data == null
                ? NotFound(new { success = false, message = "Ticket type not found." })
                : Ok(new { success = true, data });
        }

        [HttpPost("types")]
        [RequirePermission("resort_tickets.manage_types")]
        public async Task<IActionResult> CreateType([FromBody] PmsUpsertResortTicketTypeDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var data = await _service.CreateTicketTypeAsync(dto, cancellationToken);
            return Created(string.Empty, new { success = true, data });
        }

        [HttpPut("types/{id:int}")]
        [RequirePermission("resort_tickets.manage_types")]
        public async Task<IActionResult> UpdateType(int id, [FromBody] PmsUpsertResortTicketTypeDto dto, CancellationToken cancellationToken)
        {
            var data = await _service.UpdateTicketTypeAsync(id, dto, cancellationToken);
            return data == null
                ? NotFound(new { success = false, message = "Ticket type not found." })
                : Ok(new { success = true, data });
        }

        [HttpPatch("types/{id:int}/active")]
        [RequirePermission("resort_tickets.manage_types")]
        public async Task<IActionResult> SetTypeActive(int id, [FromBody] PmsSetResortTicketTypeActiveDto dto, CancellationToken cancellationToken)
        {
            var data = await _service.SetTicketTypeActiveAsync(id, dto.IsActive, cancellationToken);
            return data == null
                ? NotFound(new { success = false, message = "Ticket type not found." })
                : Ok(new { success = true, data });
        }

        [HttpGet("orders")]
        [RequirePermission("resort_tickets.view")]
        public async Task<IActionResult> ListOrders(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int? reservationId,
            [FromQuery] string? paymentStatus,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListOrdersAsync(fromDate, toDate, reservationId, paymentStatus, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("orders/{id:int}")]
        [RequirePermission("resort_tickets.view")]
        public async Task<IActionResult> GetOrder(int id, CancellationToken cancellationToken)
        {
            var data = await _service.GetOrderAsync(id, cancellationToken);
            return data == null
                ? NotFound(new { success = false, message = "Ticket order not found." })
                : Ok(new { success = true, data });
        }

        [HttpPost("orders")]
        [RequirePermission("resort_tickets.issue")]
        public async Task<IActionResult> CreateOrder([FromBody] PmsCreateResortTicketOrderDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.CreateOrderAsync(dto, cancellationToken);
                return Created(string.Empty, new { success = true, data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resort ticket order create failed");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("orders/{id:int}/cancel")]
        [RequirePermission("resort_tickets.cancel")]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] PmsCancelResortTicketOrderDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.CancelOrderAsync(id, dto, cancellationToken);
                return data == null
                    ? NotFound(new { success = false, message = "Ticket order not found." })
                    : Ok(new { success = true, data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("orders/{id:int}/print")]
        [RequirePermission("resort_tickets.print")]
        [Produces("application/pdf", "application/json")]
        public async Task<IActionResult> PrintOrder(int id, [FromQuery] string paper, CancellationToken cancellationToken)
        {
            var result = await _service.PrintOrderAsync(id, paper, cancellationToken);
            return result == null
                ? NotFound(new { success = false, message = "Ticket order not found." })
                : File(result.Content, result.MimeType, result.FileName);
        }

        [HttpGet("{id:int}/print")]
        [RequirePermission("resort_tickets.print")]
        [Produces("application/pdf", "application/json")]
        public async Task<IActionResult> PrintTicket(int id, [FromQuery] string paper, CancellationToken cancellationToken)
        {
            var result = await _service.PrintTicketAsync(id, paper, cancellationToken);
            return result == null
                ? NotFound(new { success = false, message = "Ticket not found." })
                : File(result.Content, result.MimeType, result.FileName);
        }

        [HttpGet("by-qr")]
        [RequirePermission("resort_tickets.validate")]
        public async Task<IActionResult> LookupByQr(
            [FromQuery] string qr,
            [FromQuery] string? station,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.LookupTicketByQrAsync(qr ?? string.Empty, station, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("redeem")]
        [RequirePermission("resort_tickets.validate")]
        public async Task<IActionResult> RedeemTicket([FromBody] PmsRedeemResortTicketDto dto, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.RedeemTicketByQrAsync(dto.QrCode, dto.StationCode, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("config/business-hours")]
        [RequireAnyPermission("resort_tickets.manage_settings", "resort_tickets.manage_types")]
        public async Task<IActionResult> GetBusinessConfig(CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.GetBusinessConfigAsync(cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("config/business-hours")]
        [RequireAnyPermission("resort_tickets.manage_settings", "resort_tickets.manage_types")]
        public async Task<IActionResult> UpdateBusinessConfig(
            [FromBody] PmsUpsertResortTicketBusinessConfigDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            try
            {
                var data = await _service.UpdateBusinessConfigAsync(dto, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("finance/receipts")]
        [RequirePermission("resort_tickets.finance")]
        public async Task<IActionResult> ListTicketReceipts(
            [FromQuery] string? receiptKind,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListTicketReceiptsAsync(receiptKind, fromDate, toDate, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("finance/reconciliation")]
        [RequirePermission("resort_tickets.finance")]
        public async Task<IActionResult> GetFinanceReconciliation(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken)
        {
            var data = await _service.GetFinanceReconciliationAsync(fromDate, toDate, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("finance/pending-invoices")]
        [RequirePermission("resort_tickets.finance")]
        public async Task<IActionResult> ListPendingInvoiceOrders(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListPendingInvoiceOrdersAsync(fromDate, toDate, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("finance/invoices")]
        [RequirePermission("resort_tickets.finance")]
        public async Task<IActionResult> ListTicketInvoices(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken)
        {
            var data = await _service.ListTicketInvoicesAsync(fromDate, toDate, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("finance/invoices")]
        [RequirePermission("resort_tickets.finance")]
        public async Task<IActionResult> CreateTicketInvoices(
            [FromBody] PmsCreateResortTicketInvoicesDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.CreateInvoicesForOrdersAsync(dto, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("gate/station-catalog")]
        [RequireAnyPermission("rbac.roles.manage", "resort_tickets.manage_settings")]
        public async Task<IActionResult> GetGateStationCatalog(CancellationToken cancellationToken)
        {
            var data = await _gateLanding.GetStationCatalogAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("gate/my-stations")]
        [RequirePermission("resort_tickets.validate")]
        public async Task<IActionResult> GetMyGateStations(CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? 0;
            if (userId <= 0)
            {
                return Unauthorized(new { success = false, message = "Authentication required." });
            }

            var permissions = User.FindFirst("permissions")?.Value?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();
            var tenantIdClaim = User.FindFirst("tenantId")?.Value;
            if (!int.TryParse(tenantIdClaim, out var tenantId) || tenantId <= 0)
            {
                return BadRequest(new { success = false, message = "Tenant not resolved." });
            }

            var data = await _gateLanding.GetUserGateStationsAsync(userId, tenantId, permissions, cancellationToken);
            var landingUrl = _gateLanding.ResolvePreferredLandingUrl(data);
            return Ok(new { success = true, data, landingUrl });
        }

        [HttpGet("gate/manifest")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGateManifest(
            [FromQuery] string station,
            [FromQuery] string? hotelCode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(station))
            {
                return BadRequest(new { success = false, message = "station is required." });
            }

            var tenantId = await ResolveTenantIdForGateAsync(hotelCode, cancellationToken);
            var catalog = await _gateLanding.LoadCatalogMapAsync(tenantId, cancellationToken);
            var normalized = station.Trim().ToLowerInvariant().Replace(' ', '_');
            if (!catalog.ContainsKey(normalized))
            {
                return NotFound(new { success = false, message = "Unknown station." });
            }

            var json = _gateLanding.BuildManifestJson(normalized, catalog);
            return Content(json, "application/manifest+json; charset=utf-8");
        }

        [HttpGet("gate/icon")]
        [AllowAnonymous]
        public IActionResult GetGateIcon([FromQuery] string station, [FromQuery] int size = 192)
        {
            if (string.IsNullOrWhiteSpace(station))
            {
                return BadRequest(new { success = false, message = "station is required." });
            }

            var bytes = _gateLanding.RenderStationIcon(station, size);
            return File(bytes, "image/png");
        }

        private async Task<int?> ResolveTenantIdForGateAsync(string? hotelCode, CancellationToken cancellationToken)
        {
            var code = (hotelCode ?? Request.Headers["X-Hotel-Code"].FirstOrDefault())?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var tenant = await HttpContext.RequestServices
                .GetRequiredService<MasterDbContext>()
                .Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
            return tenant?.Id;
        }
    }
}
