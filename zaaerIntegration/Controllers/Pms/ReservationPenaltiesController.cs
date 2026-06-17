#pragma warning disable CS1591

using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Security;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/reservation-penalties")]
    [Produces("application/json")]
    public sealed class ReservationPenaltiesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReservationPenaltiesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("catalog")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPenaltyCatalog([FromQuery] int? hotelId, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.Penalties.AsNoTracking().Where(p => p.IsActive);

                if (hotelId.HasValue)
                {
                    query = query.Where(p => p.HotelId == hotelId.Value);
                }

                var rows = await query
                    .OrderBy(p => p.PenaltyName)
                    .ThenBy(p => p.PenaltyId)
                    .ToListAsync(cancellationToken);

                var items = rows
                    .GroupBy(p => new
                    {
                        Type = (p.PenaltyType ?? string.Empty).Trim(),
                        Name = (p.PenaltyName ?? string.Empty).Trim(),
                        NameAr = (p.PenaltyNameAr ?? string.Empty).Trim()
                    })
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key.Name))
                    .Select(g => g.OrderByDescending(p => p.PenaltyId).First())
                    .OrderBy(p => p.PenaltyName)
                    .Select(ToDto)
                    .ToList();

                return Ok(new { success = true, data = items });
            }
            catch
            {
                return Ok(new { success = true, data = Array.Empty<ReservationPenaltyCatalogDto>() });
            }
        }

        [HttpPost("catalog")]
        [RequirePermission("reservations.penalty")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePenaltyCatalog(
            [FromBody] CreateReservationPenaltyCatalogDto request,
            CancellationToken cancellationToken)
        {
            var name = (request.PenaltyName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { success = false, message = "Penalty name is required." });
            }

            if (!request.ReservationId.HasValue)
            {
                return BadRequest(new { success = false, message = "ReservationId is required." });
            }

            var reservation = await _context.Reservations
                .AsNoTracking()
                .Where(r => r.ReservationId == request.ReservationId.Value || r.ZaaerId == request.ReservationId.Value)
                .OrderBy(r => r.ReservationId == request.ReservationId.Value ? 0 : 1)
                .FirstOrDefaultAsync(cancellationToken);

            if (reservation == null)
            {
                return BadRequest(new { success = false, message = "Reservation not found." });
            }

            if (request.HotelId.HasValue && reservation.HotelId != request.HotelId.Value)
            {
                return BadRequest(new { success = false, message = "Reservation does not belong to the selected hotel." });
            }

            var amount = Math.Max(0, request.BaseAmount);
            var penalty = new Penalty
            {
                HotelId = reservation.HotelId,
                ReservationId = reservation.ReservationId,
                PenaltyType = string.IsNullOrWhiteSpace(request.PenaltyType)
                    ? PenaltyTypes.Other
                    : request.PenaltyType.Trim(),
                PenaltyName = name,
                PenaltyNameAr = string.IsNullOrWhiteSpace(request.PenaltyNameAr) ? null : request.PenaltyNameAr.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                CalculationMethod = PenaltyCalculationMethods.FixedAmount,
                CalculationValue = amount,
                BaseAmount = amount,
                TotalAmount = amount,
                AppliedDate = KsaTime.Now,
                IsActive = true,
                CreatedAt = KsaTime.Now
            };

            _context.Penalties.Add(penalty);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, data = ToDto(penalty) });
        }

        private static ReservationPenaltyCatalogDto ToDto(Penalty penalty)
        {
            return new ReservationPenaltyCatalogDto
            {
                PenaltyId = penalty.PenaltyId,
                HotelId = penalty.HotelId,
                ReservationId = penalty.ReservationId,
                PenaltyType = penalty.PenaltyType,
                PenaltyName = penalty.PenaltyName,
                PenaltyNameAr = penalty.PenaltyNameAr,
                Description = penalty.Description,
                BaseAmount = penalty.BaseAmount,
                IsActive = penalty.IsActive
            };
        }
    }
}
