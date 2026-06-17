#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/hotel-targets")]
    [Produces("application/json")]
    public sealed class PmsHotelTargetsController : ControllerBase
    {
        private readonly IPmsHotelTargetService _targetService;
        private readonly IPermissionService _permissionService;
        private readonly ICurrentUserContext _currentUser;
        private readonly ITenantService _tenantService;
        private readonly ApplicationDbContext _context;

        public PmsHotelTargetsController(
            IPmsHotelTargetService targetService,
            IPermissionService permissionService,
            ICurrentUserContext currentUser,
            ITenantService tenantService,
            ApplicationDbContext context)
        {
            _targetService = targetService;
            _permissionService = permissionService;
            _currentUser = currentUser;
            _tenantService = tenantService;
            _context = context;
        }

        [HttpGet("report")]
        [RequireLodgingReportPermission("targets")]
        public async Task<IActionResult> TargetReport(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _targetService.GetTargetReportAsync(fromDate, toDate, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("settings")]
        public async Task<IActionResult> ListSettings(CancellationToken cancellationToken)
        {
            if (!await HasManagePermissionAsync(cancellationToken))
            {
                return Forbid();
            }

            try
            {
                var data = await _targetService.ListTargetsForCurrentHotelAsync(cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("settings")]
        public async Task<IActionResult> CreateSetting(
            [FromBody] UpsertPmsHotelMonthlyTargetDto dto,
            CancellationToken cancellationToken)
        {
            if (!await HasManagePermissionAsync(cancellationToken))
            {
                return Forbid();
            }

            try
            {
                var data = await _targetService.CreateTargetAsync(dto, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("settings/{id:int}")]
        public async Task<IActionResult> UpdateSetting(
            int id,
            [FromBody] UpsertPmsHotelMonthlyTargetDto dto,
            CancellationToken cancellationToken)
        {
            if (!await HasManagePermissionAsync(cancellationToken))
            {
                return Forbid();
            }

            try
            {
                var data = await _targetService.UpdateTargetAsync(id, dto, cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        private async Task<bool> HasManagePermissionAsync(CancellationToken cancellationToken)
        {
            if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue || !_currentUser.TenantId.HasValue)
            {
                return false;
            }

            var propertyType = await ResolvePropertyTypeAsync(cancellationToken);
            if (PropertyTypes.IsHall(propertyType))
            {
                return false;
            }

            foreach (var code in PmsReportPermissions.LodgingTargetManage(propertyType))
            {
                if (await _permissionService.HasPermissionAsync(
                        _currentUser.UserId.Value,
                        _currentUser.TenantId.Value,
                        code,
                        _currentUser.AuthMode,
                        cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<string?> ResolvePropertyTypeAsync(CancellationToken cancellationToken)
        {
            var tenant = _tenantService.GetTenant();
            if (tenant == null || string.IsNullOrWhiteSpace(tenant.Code))
            {
                return null;
            }

            var code = tenant.Code.Trim();
            var propertyType = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelCode != null && h.HotelCode.ToLower() == code.ToLower())
                .Select(h => h.PropertyType)
                .FirstOrDefaultAsync(cancellationToken);

            return propertyType?.Trim().ToLowerInvariant();
        }
    }
}
