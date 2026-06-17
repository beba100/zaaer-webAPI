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
    [Route("api/v1/pms/reservation-packages")]
    [Produces("application/json")]
    public sealed class ReservationPackagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReservationPackagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPackages([FromQuery] int? hotelId, CancellationToken cancellationToken)
        {
            try
            {
                var query = _context.ReservationPackages.AsNoTracking().Where(p => p.IsActive);

                if (hotelId.HasValue)
                {
                    query = query.Where(p => p.HotelId == null || p.HotelId == hotelId.Value);
                }

                var items = await query
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.Name)
                    .Select(p => new ReservationPackageDto
                    {
                        PackageId = p.PackageId,
                        HotelId = p.HotelId,
                        Name = p.Name,
                        NameAr = p.NameAr,
                        Description = p.Description,
                        UnitPrice = p.UnitPrice,
                        IsActive = p.IsActive,
                        SortOrder = p.SortOrder
                    })
                    .ToListAsync(cancellationToken);

                return Ok(new { success = true, data = items });
            }
            catch
            {
                // Some tenant databases may not have the new table yet.
                return Ok(new { success = true, data = Array.Empty<ReservationPackageDto>() });
            }
        }

        [HttpPost]
        [RequirePermission("reservations.package")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePackage(
            [FromBody] CreateReservationPackageDto request,
            CancellationToken cancellationToken)
        {
            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { success = false, message = "Package name is required." });
            }

            var package = new ReservationPackage
            {
                HotelId = request.HotelId,
                Name = name,
                NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                UnitPrice = Math.Max(0, request.UnitPrice),
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                CreatedAt = KsaTime.Now
            };

            _context.ReservationPackages.Add(package);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                data = new ReservationPackageDto
                {
                    PackageId = package.PackageId,
                    HotelId = package.HotelId,
                    Name = package.Name,
                    NameAr = package.NameAr,
                    Description = package.Description,
                    UnitPrice = package.UnitPrice,
                    IsActive = package.IsActive,
                    SortOrder = package.SortOrder
                }
            });
        }
    }
}
