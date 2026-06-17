#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/pos/orders")]
    [Produces("application/json")]
    public sealed class PmsPosOrdersController : ControllerBase
    {
        private readonly IPmsPosOrderService _orders;
        private readonly ILogger<PmsPosOrdersController> _logger;

        public PmsPosOrdersController(IPmsPosOrderService orders, ILogger<PmsPosOrdersController> logger)
        {
            _orders = orders;
            _logger = logger;
        }

        [HttpGet]
        [RequirePermission("pos.view")]
        public async Task<IActionResult> ListOrders([FromQuery] int? outletId, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
        {
            var data = await _orders.ListOrdersAsync(outletId, take, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("recent")]
        [RequirePermission("pos.view")]
        public async Task<IActionResult> ListRecent([FromQuery] int? outletId, [FromQuery] int take = 30, CancellationToken cancellationToken = default)
        {
            var data = await _orders.ListRecentOrdersAsync(outletId, take, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("{orderId:int}")]
        [RequirePermission("pos.view")]
        public async Task<IActionResult> GetOrder(int orderId, CancellationToken cancellationToken)
        {
            var data = await _orders.GetOrderAsync(orderId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Order not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpGet("in-house-reservations")]
        [RequirePermission("pos.view")]
        public async Task<IActionResult> ListInHouseReservations(CancellationToken cancellationToken)
        {
            var data = await _orders.ListInHouseReservationsAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost]
        [RequirePermission("pos.orders.create")]
        public async Task<IActionResult> CreateOrder([FromBody] PmsCreatePosOrderDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            try
            {
                var data = await _orders.CreateOrderAsync(dto, cancellationToken);
                return Ok(new { success = true, message = "Order created.", data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS POS create order failed");
                return StatusCode(500, new { success = false, message = "Could not create order." });
            }
        }

        [HttpPatch("{orderId:int}/receipt")]
        [RequirePermission("pos.orders.receipt_edit")]
        public async Task<IActionResult> UpdateReceipt(int orderId, [FromBody] PmsUpdatePosOrderReceiptDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            try
            {
                var data = await _orders.UpdateOrderReceiptAsync(orderId, dto, cancellationToken);
                return Ok(new { success = true, message = "Receipt updated.", data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS POS update receipt failed for order {OrderId}", orderId);
                return StatusCode(500, new { success = false, message = "Could not update receipt." });
            }
        }

        [HttpPut("{orderId:int}/transferred")]
        [RequirePermission("pos.orders.create")]
        public async Task<IActionResult> UpdateTransferredOrder(
            int orderId,
            [FromBody] PmsUpdateTransferredPosOrderDto dto,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            try
            {
                var data = await _orders.UpdateTransferredOrderAsync(orderId, dto, cancellationToken);
                return Ok(new { success = true, message = "Order updated.", data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS POS update transferred order failed for order {OrderId}", orderId);
                return StatusCode(500, new { success = false, message = "Could not update order." });
            }
        }

        [HttpPost("{orderId:int}/cancel")]
        [RequirePermission("pos.orders.cancel")]
        public async Task<IActionResult> CancelOrder(int orderId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _orders.CancelOrderAsync(orderId, cancellationToken);
                return Ok(new { success = true, message = "Order cancelled.", data });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PMS POS cancel order failed for order {OrderId}", orderId);
                return StatusCode(500, new { success = false, message = "Could not cancel order." });
            }
        }
    }
}
