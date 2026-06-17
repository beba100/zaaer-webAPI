#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.Security;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/lookups")]
    [Produces("application/json")]
    public sealed class PmsLookupsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PmsLookupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("visit-purposes")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVisitPurposes(CancellationToken cancellationToken)
        {
            var items = await _context.VisitPurposes
                .AsNoTracking()
                .Where(v => v.IsActive)
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.VpId)
                .Select(v => new
                {
                    id = v.VpId,
                    name = v.VpName,
                    nameAr = v.VpNameAr
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        /// <summary>
        /// Active rows from tenant <c>sources</c> (sort_order: Reception / primary first). Falls back to built-in list if the table is missing or empty.
        /// </summary>
        [HttpGet("reservation-sources")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReservationSources(CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _context.ReservationSources
                    .AsNoTracking()
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SortOrder)
                    .ThenBy(s => s.Name)
                    .Select(s => new
                    {
                        code = s.Code,
                        name = s.Name,
                        nameAr = s.NameAr
                    })
                    .ToListAsync(cancellationToken);

                if (rows.Count > 0)
                {
                    return Ok(new { success = true, data = rows });
                }
            }
            catch
            {
                // Table may not exist yet on this tenant — use fallback below.
            }

            var fallback = new[]
            {
                new { code = "Reception", name = "Reception", nameAr = "الاستقبال" },
                new { code = "Website", name = "Website", nameAr = "الموقع" },
                new { code = "Phone", name = "Phone", nameAr = "الهاتف" },
                new { code = "TravelAgent", name = "Travel agent", nameAr = "وكيل سفر" },
                new { code = "WalkIn", name = "Walk-in", nameAr = "حضوري" }
            };

            return Ok(new { success = true, data = fallback });
        }

        [HttpGet("guest-types")]
        [RequirePermission("guests.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGuestTypes(CancellationToken cancellationToken)
        {
            var items = await _context.GuestTypes
                .AsNoTracking()
                .Where(g => g.GtypeActive)
                .OrderBy(g => g.GtypeName)
                .Select(g => new
                {
                    id = g.GtypeId,
                    name = g.GtypeName,
                    nameAr = g.GtypeNameAr
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        [HttpGet("guest-categories")]
        [RequirePermission("guests.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGuestCategories(CancellationToken cancellationToken)
        {
            var items = await _context.GuestCategories
                .AsNoTracking()
                .Where(gc => gc.GcActive)
                .OrderBy(gc => gc.GcName)
                .Select(gc => new
                {
                    id = gc.GcId,
                    name = gc.GcName,
                    nameAr = gc.GcNameAr
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        [HttpGet("id-types")]
        [RequirePermission("guests.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetIdTypes(CancellationToken cancellationToken)
        {
            var items = await _context.IdTypes
                .AsNoTracking()
                .Where(t => t.ItActive)
                .OrderBy(t => t.ItName)
                .Select(t => new
                {
                    id = t.ItId,
                    name = t.ItName,
                    nameAr = t.ItNameAr
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        [HttpGet("customer-relations")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCustomerRelations(CancellationToken cancellationToken)
        {
            var items = await _context.CustomerRelations
                .AsNoTracking()
                .OrderBy(r => r.CrId)
                .Select(r => new
                {
                    id = r.CrId,
                    name = r.CrName,
                    nameAr = r.CrNameAr
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        [HttpGet("payment-methods")]
        [RequirePermission("payments.create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPaymentMethods(CancellationToken cancellationToken)
        {
            var items = await _context.PaymentMethods
                .AsNoTracking()
                .Where(pm => pm.IsActive)
                .OrderBy(pm => pm.SortOrder)
                .ThenBy(pm => pm.MethodName)
                .Select(pm => new
                {
                    id = pm.PaymentMethodId,
                    name = pm.MethodName,
                    nameAr = pm.MethodNameAr,
                    code = pm.MethodCode,
                    category = pm.Category,
                    sortOrder = pm.SortOrder,
                    requiresTransactionNo = pm.RequiresTransactionNo
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        /// <summary>
        /// Active banks; <c>id</c> is <c>zaaer_id</c> for storage in <c>payment_receipts.bank_id</c>.
        /// </summary>
        [HttpGet("banks")]
        [RequirePermission("payments.create")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBanks(CancellationToken cancellationToken)
        {
            var items = await _context.Banks
                .AsNoTracking()
                .Where(b => b.IsActive && b.ZaaerId.HasValue && b.ZaaerId.Value > 0)
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.BankNameAr)
                .Select(b => new
                {
                    id = b.ZaaerId,
                    bankId = b.BankId,
                    zaaerId = b.ZaaerId,
                    name = b.BankNameEn,
                    nameAr = b.BankNameAr,
                    code = b.BankCode,
                    isDefault = b.IsDefault,
                    sortOrder = b.SortOrder
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        [HttpGet("nationalities")]
        [RequirePermission("guests.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNationalities(CancellationToken cancellationToken)
        {
            var items = await _context.Nationalities
                .AsNoTracking()
                .Where(n => n.IsActive)
                .OrderBy(n => n.NName)
                .Select(n => new
                {
                    id = n.NId,
                    name = n.NName,
                    nameAr = n.NNameAr,
                    codePrefix = n.CodePrefix
                })
                .ToListAsync(cancellationToken);

            return Ok(new { success = true, data = items });
        }

        /// <summary>Current Saudi Arabia date/time (<see cref="KsaTime.Now"/>).</summary>
        [HttpGet("ksa-now")]
        [RequirePermission("reservations.view")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetKsaNow()
        {
            var now = KsaTime.Now;
            return Ok(new
            {
                success = true,
                data = new
                {
                    year = now.Year,
                    month = now.Month,
                    day = now.Day,
                    hour = now.Hour,
                    minute = now.Minute,
                    second = now.Second
                }
            });
        }
    }
}
