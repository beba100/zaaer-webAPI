using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.DTOs.Rbac;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Security;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Auth;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/rbac")]
    public class RbacAdminController : ControllerBase
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly IRbacUserService _rbacUserService;
        private readonly ICurrentUserContext _currentUser;
        private readonly IResortTicketGateLandingService _gateLandingService;
        private readonly ISessionService _sessionService;

        public RbacAdminController(
            MasterDbContext masterDbContext,
            IRbacUserService rbacUserService,
            ICurrentUserContext currentUser,
            IResortTicketGateLandingService gateLandingService,
            ISessionService sessionService)
        {
            _masterDbContext = masterDbContext;
            _rbacUserService = rbacUserService;
            _currentUser = currentUser;
            _gateLandingService = gateLandingService;
            _sessionService = sessionService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized();
            }

            var detail = await _rbacUserService.GetDetailAsync(_currentUser.UserId.Value);
            var fullName = detail != null
                ? $"{detail.FirstName} {detail.LastName}".Trim()
                : _currentUser.Username;

            return Ok(new
            {
                _currentUser.IsAuthenticated,
                _currentUser.UserId,
                _currentUser.Username,
                fullName,
                employeeNumber = detail?.EmployeeNumber,
                email = detail?.Email,
                phoneNumber = detail?.PhoneNumber,
                department = detail?.Department,
                _currentUser.TenantId,
                _currentUser.TenantCode,
                _currentUser.AuthMode,
                roles = _currentUser.Roles,
                permissions = _currentUser.Permissions,
                allowedHotelIds = _currentUser.AllowedHotelIds
            });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized();
            }

            var profile = await _rbacUserService.GetProfileAsync(_currentUser.UserId.Value);
            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] RbacProfileUpdateDto dto)
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized();
            }

            try
            {
                var profile = await _rbacUserService.UpdateProfileAsync(_currentUser.UserId.Value, dto);
                return Ok(profile);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPut("profile/password")]
        public async Task<IActionResult> ChangePassword([FromBody] RbacChangePasswordDto dto)
        {
            if (!_currentUser.UserId.HasValue)
            {
                return Unauthorized();
            }

            try
            {
                await _rbacUserService.ChangePasswordAsync(_currentUser.UserId.Value, dto);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("tenants")]
        public async Task<IActionResult> Tenants()
        {
            var query = TenantScope.FilterForUser(_masterDbContext.Tenants.AsNoTracking(), _currentUser);
            var rows = await query
                .Where(t => t.Code != null && t.Code.Trim() != "")
                .OrderBy(x => x.Name)
                .Select(x => new { tenantId = x.Id, code = x.Code, name = x.Name, nameEn = x.NameEn, databaseName = x.DatabaseName, zaaerId = x.ZaaerId })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("users")]
        [RequirePermission("rbac.users.manage")]
        public async Task<IActionResult> Users()
        {
            return Ok(await _rbacUserService.GetAllAsync());
        }

        [HttpGet("users/{id:int}")]
        [RequirePermission("rbac.users.manage")]
        public async Task<IActionResult> GetUser(int id)
        {
            var detail = await _rbacUserService.GetDetailAsync(id);
            return detail == null ? NotFound() : Ok(detail);
        }

        [HttpPost("users")]
        [RequirePermission("rbac.users.manage")]
        public async Task<IActionResult> CreateUser([FromBody] RbacUserSaveDto dto)
        {
            try
            {
                var user = await _rbacUserService.CreateAsync(dto);
                return Ok(await _rbacUserService.GetDetailAsync(user.UserId));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("users/{id:int}")]
        [RequirePermission("rbac.users.manage")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] RbacUserSaveDto dto)
        {
            try
            {
                await _rbacUserService.UpdateAsync(id, dto);
                return Ok(await _rbacUserService.GetDetailAsync(id));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("users/{id:int}/sessions")]
        [RequirePermission("security.sessions.manage")]
        public async Task<IActionResult> GetUserSessions(int id, CancellationToken cancellationToken)
        {
            var user = await _rbacUserService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            long? currentSessionId = null;
            var sidClaim = User.FindFirst("sid")?.Value;
            if (long.TryParse(sidClaim, out var parsed) && parsed > 0)
            {
                currentSessionId = parsed;
            }

            var sessions = await _sessionService.GetUserSessionsForAdminAsync(id, currentSessionId, cancellationToken);
            return Ok(new
            {
                userId = id,
                username = user.Username,
                sessionVersion = user.SessionVersion,
                isLocked = user.IsLocked,
                sessions,
                activeSessions = sessions.Where(s => s.IsActive).ToList()
            });
        }

        [HttpPost("users/{id:int}/force-logout")]
        [RequirePermission("security.sessions.manage")]
        public async Task<IActionResult> ForceLogoutUser(int id, CancellationToken cancellationToken)
        {
            var user = await _rbacUserService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            await _sessionService.RevokeAllUserSessionsAsync(
                id,
                "AdminForceLogout",
                _currentUser.UserId,
                cancellationToken);

            return Ok(new { success = true, userId = id, sessionVersion = user.SessionVersion + 1 });
        }

        [HttpPost("users/{id:int}/sessions/{sessionId:long}/revoke")]
        [RequirePermission("security.sessions.manage")]
        public async Task<IActionResult> RevokeUserSession(int id, long sessionId, CancellationToken cancellationToken)
        {
            var revoked = await _sessionService.RevokeSessionAsync(
                sessionId,
                id,
                "AdminSessionRevoke",
                _currentUser.UserId,
                cancellationToken);

            return revoked ? Ok(new { success = true }) : NotFound();
        }

        [HttpPost("users/{id:int}/lock")]
        [RequirePermission("security.sessions.manage")]
        public async Task<IActionResult> LockUser(int id, [FromBody] RbacUserLockDto? dto, CancellationToken cancellationToken)
        {
            var user = await _masterDbContext.RbacUsers.FirstOrDefaultAsync(u => u.UserId == id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            user.IsLocked = true;
            user.LockedAt = KsaTime.Now;
            user.LockedReason = dto?.Reason;
            user.UpdatedAt = KsaTime.Now;
            await _masterDbContext.SaveChangesAsync(cancellationToken);

            await _sessionService.RevokeAllUserSessionsAsync(id, "UserLocked", _currentUser.UserId, cancellationToken);
            _sessionService.InvalidateUserAuthCache(id);

            await _sessionService.LogSecurityEventAsync(
                SecurityAuditEventTypes.UserLocked,
                id,
                _currentUser.UserId,
                null,
                null,
                dto?.Reason,
                cancellationToken);

            return Ok(new { success = true, userId = id, isLocked = true });
        }

        [HttpPost("users/{id:int}/unlock")]
        [RequirePermission("security.sessions.manage")]
        public async Task<IActionResult> UnlockUser(int id, CancellationToken cancellationToken)
        {
            var user = await _masterDbContext.RbacUsers.FirstOrDefaultAsync(u => u.UserId == id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            user.IsLocked = false;
            user.LockedAt = null;
            user.LockedReason = null;
            user.UpdatedAt = KsaTime.Now;
            await _masterDbContext.SaveChangesAsync(cancellationToken);
            _sessionService.InvalidateUserAuthCache(id);

            await _sessionService.LogSecurityEventAsync(
                SecurityAuditEventTypes.UserUnlocked,
                id,
                _currentUser.UserId,
                null,
                null,
                null,
                cancellationToken);

            return Ok(new { success = true, userId = id, isLocked = false });
        }

        [HttpPut("users/{id:int}/hotels")]
        [RequirePermission("rbac.users.manage")]
        public async Task<IActionResult> SaveUserHotels(int id, [FromBody] List<int> tenantIds)
        {
            await _rbacUserService.AssignHotelsAsync(id, tenantIds);
            return Ok(new { success = true });
        }

        [HttpGet("roles")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> Roles()
        {
            return Ok(await _masterDbContext.RbacRoles
                .AsNoTracking()
                .OrderBy(x => x.RoleNameEn ?? x.RoleName)
                .ToListAsync());
        }

        [HttpPost("roles")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> CreateRole([FromBody] RbacRoleSaveDto input)
        {
            var row = new MasterRbacRole
            {
                RoleNameEn = input.RoleNameEn,
                RoleNameAr = input.RoleNameAr,
                RoleName = input.RoleNameEn,
                RoleCode = input.RoleCode ?? SlugRoleCode(input.RoleNameEn),
                RoleDescription = input.RoleDescription,
                IsActive = input.IsActive,
                CreatedAt = KsaTime.Now
            };
            _masterDbContext.RbacRoles.Add(row);
            await _masterDbContext.SaveChangesAsync();
            return Ok(row);
        }

        [HttpPut("roles/{id:int}")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] RbacRoleSaveDto input)
        {
            var row = await _masterDbContext.RbacRoles.FirstOrDefaultAsync(x => x.RoleId == id);
            if (row == null)
            {
                return NotFound();
            }

            row.RoleNameEn = input.RoleNameEn;
            row.RoleNameAr = input.RoleNameAr;
            row.RoleName = input.RoleNameEn;
            row.RoleCode = input.RoleCode ?? row.RoleCode;
            row.RoleDescription = input.RoleDescription;
            row.IsActive = input.IsActive;
            row.UpdatedAt = KsaTime.Now;
            await _masterDbContext.SaveChangesAsync();
            return Ok(row);
        }

        [HttpGet("roles/{id:int}/permissions")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> GetRolePermissions(int id)
        {
            var rows = await _masterDbContext.RbacRolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == id)
                .Select(x => new { x.RolePermissionId, x.RoleId, x.PermissionId, x.Granted })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("roles/{id:int}/permissions/matrix")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> GetRolePermissionMatrix(int id)
        {
            var role = await _masterDbContext.RbacRoles.AsNoTracking().FirstOrDefaultAsync(x => x.RoleId == id);
            if (role == null)
            {
                return NotFound();
            }

            var grantedMap = await _masterDbContext.RbacRolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == id)
                .ToDictionaryAsync(x => x.PermissionId, x => x.Granted);

            var modules = await BuildPermissionCatalogAsync(grantedMap);
            return Ok(new RbacRolePermissionMatrixDto
            {
                RoleId = role.RoleId,
                RoleNameAr = role.RoleNameAr ?? role.RoleName ?? "",
                RoleNameEn = role.RoleNameEn ?? role.RoleName ?? "",
                RoleCode = role.RoleCode,
                Modules = modules
            });
        }

        [HttpPut("roles/{id:int}/permissions")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> SaveRolePermissions(int id, [FromBody] List<RbacRolePermissionSaveDto> assignments)
        {
            if (!await _masterDbContext.RbacRoles.AnyAsync(x => x.RoleId == id))
            {
                return NotFound();
            }

            var existing = await _masterDbContext.RbacRolePermissions
                .Where(x => x.RoleId == id)
                .ToListAsync();

            var byPermission = existing.ToDictionary(x => x.PermissionId);
            var now = KsaTime.Now;

            foreach (var item in assignments ?? new List<RbacRolePermissionSaveDto>())
            {
                if (item.PermissionId <= 0)
                {
                    continue;
                }

                if (byPermission.TryGetValue(item.PermissionId, out var row))
                {
                    row.Granted = item.Granted;
                }
                else
                {
                    _masterDbContext.RbacRolePermissions.Add(new MasterRbacRolePermission
                    {
                        RoleId = id,
                        PermissionId = item.PermissionId,
                        Granted = item.Granted,
                        CreatedAt = now
                    });
                }
            }

            await NavMenuLegacyPermissionSync.ApplyAsync(_masterDbContext, id, HttpContext.RequestAborted);
            await _masterDbContext.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("roles/{id:int}/gate-stations")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> GetRoleGateStations(int id, CancellationToken cancellationToken)
        {
            if (!await _masterDbContext.RbacRoles.AnyAsync(x => x.RoleId == id, cancellationToken))
            {
                return NotFound(new { success = false, message = "Role not found." });
            }

            var codes = await _gateLandingService.GetRoleGateStationCodesAsync(id, cancellationToken);
            return Ok(new { success = true, stationCodes = codes });
        }

        [HttpGet("gate-station-catalog")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> GetGateStationCatalog(CancellationToken cancellationToken)
        {
            try
            {
                var data = await _gateLandingService.GetStationCatalogAsync(cancellationToken);
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                return Ok(new { success = true, data = Array.Empty<PmsResortTicketGateStationDto>(), warning = ex.Message });
            }
        }

        [HttpPut("roles/{id:int}/gate-stations")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> SaveRoleGateStations(
            int id,
            [FromBody] SaveRoleGateStationsDto dto,
            CancellationToken cancellationToken)
        {
            if (!await _masterDbContext.RbacRoles.AnyAsync(x => x.RoleId == id, cancellationToken))
            {
                return NotFound();
            }

            await _gateLandingService.SaveRoleGateStationCodesAsync(id, dto.StationCodes, cancellationToken);
            return Ok(new { success = true });
        }

        [HttpGet("permissions")]
        [RequirePermission("rbac.permissions.view")]
        public async Task<IActionResult> Permissions()
        {
            var rows = await _masterDbContext.RbacPermissions
                .AsNoTracking()
                .OrderBy(x => x.ModuleName)
                .ThenBy(x => x.SubmoduleName)
                .ThenBy(x => x.SortOrder)
                .Select(p => new
                {
                    p.PermissionId,
                    p.PermissionCode,
                    permissionNameEn = p.PermissionNameEn ?? p.PermissionName,
                    permissionNameAr = p.PermissionNameAr ?? p.PermissionName,
                    p.ModuleName,
                    p.SubmoduleName,
                    p.ActionName,
                    p.SortOrder,
                    p.IsActive
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("permissions/catalog")]
        [RequirePermission("rbac.roles.manage")]
        public async Task<IActionResult> PermissionCatalog()
        {
            return Ok(await BuildPermissionCatalogAsync(null));
        }

        private async Task<List<RbacPermissionCatalogModuleDto>> BuildPermissionCatalogAsync(
            Dictionary<int, bool>? grantedByPermissionId)
        {
            var rows = await _masterDbContext.RbacPermissions
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.ModuleName)
                .ThenBy(x => x.SubmoduleName)
                .ThenBy(x => x.SortOrder)
                .ToListAsync();

            return rows
                .GroupBy(x => x.ModuleName)
                .Select(g => new RbacPermissionCatalogModuleDto
                {
                    ModuleCode = g.Key,
                    ModuleNameEn = ModuleLabelEn(g.Key),
                    ModuleNameAr = ModuleLabelAr(g.Key),
                    Submodules = g.GroupBy(x => x.SubmoduleName ?? x.ModuleName)
                        .Select(sg => new RbacPermissionCatalogSubmoduleDto
                        {
                            SubmoduleCode = sg.Key,
                            SubmoduleNameEn = SubmoduleLabelEn(sg.Key),
                            SubmoduleNameAr = SubmoduleLabelAr(sg.Key),
                            Permissions = sg.Select(p => new RbacPermissionCatalogItemDto
                            {
                                PermissionId = p.PermissionId,
                                PermissionCode = p.PermissionCode,
                                NameEn = p.PermissionNameEn ?? p.PermissionName,
                                NameAr = p.PermissionNameAr ?? p.PermissionName,
                                Granted = grantedByPermissionId != null &&
                                          grantedByPermissionId.TryGetValue(p.PermissionId, out var granted) &&
                                          granted
                            }).ToList()
                        }).ToList()
                }).ToList();
        }

        private static string SlugRoleCode(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "role";
            }

            return new string(name.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray())
                .Trim('_');
        }

        private static string ModuleLabelEn(string code) => code switch
        {
            "admin" => "Admin",
            "rbac" => "RBAC",
            "room_board" => "Room board",
            "reservations" => "Reservations",
            "guests" => "Guests",
            "finance" => "Finance",
            "property" => "Property & units",
            "integrations" => "Platform integrations",
            "booking_engine" => "Website",
            "pos" => "Point of sale",
            _ => code
        };

        private static string ModuleLabelAr(string code) => code switch
        {
            "admin" => "الإدارة",
            "rbac" => "الصلاحيات والمستخدمون",
            "room_board" => "لوحة الغرف",
            "reservations" => "الحجوزات",
            "guests" => "النزلاء",
            "finance" => "المالية",
            "property" => "الوحدات والممتلكات",
            "integrations" => "تكامل المنصات",
            "booking_engine" => "الموقع الإلكتروني",
            "pos" => "نقاط البيع",
            _ => code
        };

        private static string SubmoduleLabelEn(string code) => code switch
        {
            "rbac" => "Users & roles",
            "numbering" => "Numbering",
            "room_board" => "Room board",
            "reservations" => "Reservations",
            "core" => "Core",
            "stay" => "Stay",
            "units" => "Units",
            "adjustments" => "Adjustments",
            "pricing" => "Pricing",
            "dates" => "Dates & extension",
            "company" => "Company",
            "tax" => "Tax",
            "financial" => "Financial",
            "guests" => "Guests",
            "receipt_voucher" => "Receipt vouchers",
            "refund_voucher" => "Refund vouchers",
            "invoice" => "Invoices",
            "credit_note" => "Credit notes",
            "debit_note" => "Debit notes",
            "expense" => "Expenses",
            "promissory_note" => "Promissory notes",
            "settings" => "Settings",
            "buildings" => "Blocks",
            "room_types" => "Unit types",
            "facilities" => "Facilities",
            "rates" => "Unit rates",
            "platforms" => "Platforms",
            "balady" => "Balady report",
            "terminal" => "POS terminal",
            "orders" => "Orders",
            _ => HumanizeCode(code)
        };

        private static string SubmoduleLabelAr(string code) => code switch
        {
            "rbac" => "المستخدمون والأدوار",
            "numbering" => "الترقيم",
            "room_board" => "لوحة الغرف",
            "reservations" => "الحجوزات",
            "core" => "أساسي",
            "stay" => "الإقامة",
            "units" => "الوحدات",
            "adjustments" => "التعديلات",
            "pricing" => "الأسعار",
            "dates" => "التواريخ والتمديد",
            "company" => "الشركات",
            "tax" => "الضريبة",
            "financial" => "المالية",
            "guests" => "النزلاء",
            "receipt_voucher" => "سندات القبض",
            "refund_voucher" => "سندات الاسترداد",
            "invoice" => "الفواتير",
            "credit_note" => "الإشعارات الدائنة",
            "debit_note" => "الإشعارات المدينة",
            "expense" => "المصروفات",
            "promissory_note" => "سندات لأمر",
            "settings" => "الإعدادات",
            "buildings" => "البلوكات",
            "room_types" => "أنواع الوحدات",
            "facilities" => "المرافق",
            "rates" => "أسعار الوحدات",
            "platforms" => "المنصات",
            "balady" => "تقرير بلدي",
            "terminal" => "واجهة البيع",
            "orders" => "الطلبات",
            _ => HumanizeCode(code)
        };

        private static string HumanizeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "";
            }

            return code.Replace('_', ' ');
        }
    }
}
