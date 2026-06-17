#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.BookingEngine;
using zaaerIntegration.Security;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/booking-engine")]
    [Produces("application/json")]
    public sealed class BookingEngineSettingsController : ControllerBase
    {
        private readonly IBookingEngineService _bookingEngineService;
        private readonly ApplicationDbContext _context;

        public BookingEngineSettingsController(IBookingEngineService bookingEngineService, ApplicationDbContext context)
        {
            _bookingEngineService = bookingEngineService;
            _context = context;
        }

        [HttpGet("settings")]
        [RequirePermission("booking_engine.settings.view")]
        public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
        {
            var hotelId = await ResolveHotelIdAsync(cancellationToken);
            var data = await _bookingEngineService.GetAdminSettingsAsync(hotelId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Hotel settings not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPut("settings")]
        [RequirePermission("booking_engine.settings.manage")]
        public async Task<IActionResult> SaveSettings([FromBody] BookingEngineSettingsDto dto, CancellationToken cancellationToken)
        {
            var hotelId = await ResolveHotelIdAsync(cancellationToken);
            dto.HotelId = hotelId;
            var data = await _bookingEngineService.SaveAdminSettingsAsync(dto, cancellationToken);
            return Ok(new { success = true, message = "Settings saved.", data });
        }

        [HttpPost("upload-image")]
        [RequirePermission("booking_engine.settings.manage")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(BookingEngineImageStorage.MaxFileBytes)]
        public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest(new { success = false, message = "No image file provided." });
            }

            try
            {
                var hotelId = await ResolveHotelIdAsync(cancellationToken);
                var saved = await BookingEngineImageStorage.SaveAsync(file, hotelId, cancellationToken);
                return Ok(new { success = true, data = new { imageUrl = saved.RelativePath } });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("media")]
        [RequirePermission("booking_engine.settings.manage")]
        public async Task<IActionResult> AddMedia([FromBody] BookingEngineMediaDto dto, CancellationToken cancellationToken)
        {
            var hotelId = await ResolveHotelIdAsync(cancellationToken);
            var data = await _bookingEngineService.AddMediaAsync(
                hotelId,
                dto.RoomTypeId,
                dto.ImageUrl,
                dto.Caption,
                dto.IsPrimary,
                cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpDelete("media/{mediaId:int}")]
        [RequirePermission("booking_engine.settings.manage")]
        public async Task<IActionResult> DeleteMedia(int mediaId, CancellationToken cancellationToken)
        {
            var hotelId = await ResolveHotelIdAsync(cancellationToken);
            var ok = await _bookingEngineService.DeleteMediaAsync(hotelId, mediaId, cancellationToken);
            if (!ok)
            {
                return NotFound(new { success = false });
            }

            return Ok(new { success = true });
        }

        [HttpGet("coupons")]
        [RequirePermission("booking_engine.settings.view")]
        public async Task<IActionResult> ListCoupons(CancellationToken cancellationToken)
        {
            var hotelId = await ResolveHotelIdAsync(cancellationToken);
            var data = await _bookingEngineService.ListCouponsAsync(hotelId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("coupons")]
        [RequirePermission("booking_engine.settings.manage")]
        public async Task<IActionResult> CreateCoupon(
            [FromBody] BookingEngineCouponUpsertDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var hotelId = await ResolveHotelIdAsync(cancellationToken);
                var data = await _bookingEngineService.CreateCouponAsync(hotelId, dto, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("coupons/{couponId:int}")]
        [RequirePermission("booking_engine.settings.manage")]
        public async Task<IActionResult> UpdateCoupon(
            int couponId,
            [FromBody] BookingEngineCouponUpsertDto dto,
            CancellationToken cancellationToken)
        {
            try
            {
                var hotelId = await ResolveHotelIdAsync(cancellationToken);
                var data = await _bookingEngineService.UpdateCouponAsync(hotelId, couponId, dto, cancellationToken);
                if (data == null)
                {
                    return NotFound(new { success = false });
                }

                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("coupons/{couponId:int}")]
        [RequirePermission("booking_engine.settings.manage")]
        public async Task<IActionResult> DeleteCoupon(int couponId, CancellationToken cancellationToken)
        {
            var hotelId = await ResolveHotelIdAsync(cancellationToken);
            var ok = await _bookingEngineService.DeleteCouponAsync(hotelId, couponId, cancellationToken);
            if (!ok)
            {
                return NotFound(new { success = false });
            }

            return Ok(new { success = true });
        }

        [HttpGet("room-types")]
        [RequirePermission("booking_engine.settings.view")]
        public async Task<IActionResult> ListRoomTypes(CancellationToken cancellationToken)
        {
            var hotel = await _context.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (hotel == null)
            {
                return Ok(new { success = true, data = Array.Empty<object>() });
            }

            var scopeHotelId = hotel.ZaaerId is > 0 ? hotel.ZaaerId.Value : hotel.HotelId;
            var data = await _context.RoomTypes.AsNoTracking()
                .Where(rt => rt.HotelId == scopeHotelId || rt.HotelId == hotel.HotelId)
                .OrderBy(rt => rt.RoomTypeName)
                .Select(rt => new
                {
                    id = rt.ZaaerId ?? rt.RoomTypeId,
                    name = rt.RoomTypeName ?? rt.RoomTypeNameEn ?? $"Room {rt.RoomTypeId}"
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data });
        }

        [HttpGet("availability-overrides")]
        [RequirePermission("booking_engine.settings.view")]
        public async Task<IActionResult> ListAvailabilityOverrides(
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            CancellationToken cancellationToken)
        {
            var hotelId = await ResolveHotelIdAsync(cancellationToken);
            var data = await _bookingEngineService.ListAvailabilityOverridesAsync(
                hotelId,
                fromDate,
                toDate,
                cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPut("availability-overrides")]
        [RequirePermission("booking_engine.availability.manage")]
        public async Task<IActionResult> SaveAvailabilityOverrides(
            [FromBody] BookingEngineAvailabilityOverrideBatchDto batch,
            CancellationToken cancellationToken)
        {
            try
            {
                var hotelId = await ResolveHotelIdAsync(cancellationToken);
                var data = await _bookingEngineService.SaveAvailabilityOverridesAsync(
                    hotelId,
                    batch,
                    cancellationToken);
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

        /// <summary>
        /// Integration hotel id (<c>hotel_settings.zaaer_id</c>) used on booking_engine_* tables.
        /// </summary>
        private async Task<int> ResolveHotelIdAsync(CancellationToken cancellationToken)
        {
            var hotel = await _context.HotelSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (hotel == null)
            {
                throw new InvalidOperationException("Hotel settings not found for current tenant.");
            }

            return hotel.ZaaerId is > 0 ? hotel.ZaaerId.Value : hotel.HotelId;
        }
    }
}
