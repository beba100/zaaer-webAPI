using System.Text.RegularExpressions;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Rbac;
using zaaerIntegration.Services.Auth;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public class RbacUserService : IRbacUserService
    {
        private readonly MasterDbContext _db;
        private readonly IPasswordHashingService _passwordHashing;
        private readonly ICurrentUserContext _currentUser;
        private readonly ISessionService _sessionService;
        private readonly ILogger<RbacUserService> _logger;

        public RbacUserService(
            MasterDbContext db,
            IPasswordHashingService passwordHashing,
            ICurrentUserContext currentUser,
            ISessionService sessionService,
            ILogger<RbacUserService> logger)
        {
            _db = db;
            _passwordHashing = passwordHashing;
            _currentUser = currentUser;
            _sessionService = sessionService;
            _logger = logger;
        }

        public async Task<MasterRbacUser?> ValidateLoginAsync(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var key = login.Trim();
            var user = await _db.RbacUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.IsActive &&
                    u.Status &&
                    (
                        (u.EmployeeNumber != null && u.EmployeeNumber.ToLower() == key.ToLower()) ||
                        (u.Username.ToLower() == key.ToLower()) ||
                        (u.Email.ToLower() == key.ToLower()) ||
                        (u.PhoneNumber != null && u.PhoneNumber.Replace(" ", "") == key.Replace(" ", ""))
                    ));

            if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return null;
            }

            if (user.IsLocked)
            {
                return null;
            }

            if (!_passwordHashing.VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }

            if (_passwordHashing.NeedsRehash(user.PasswordHash))
            {
                var tracked = await _db.RbacUsers.FirstAsync(u => u.UserId == user.UserId);
                tracked.PasswordHash = _passwordHashing.HashPassword(password);
                tracked.UpdatedAt = KsaTime.Now;
                await _db.SaveChangesAsync();
            }

            await _db.RbacUsers
                .Where(u => u.UserId == user.UserId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LastLogin, KsaTime.Now)
                    .SetProperty(u => u.UpdatedAt, KsaTime.Now));

            return user;
        }

        public async Task<RbacUserDetailDto?> GetDetailAsync(int userId)
        {
            var user = await _db.RbacUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return null;
            }

            var roleIds = await _db.RbacUserRoles
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive)
                .Select(x => x.RoleId)
                .ToListAsync();

            var tenantIds = await _db.PmsUserHotels
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.IsActive)
                .Select(x => x.TenantId)
                .ToListAsync();

            return new RbacUserDetailDto
            {
                UserId = user.UserId,
                Username = user.Username,
                EmployeeNumber = user.EmployeeNumber,
                UserType = user.UserType,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Title = user.Title,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Department = user.Department,
                IsActive = user.IsActive,
                RoleIds = roleIds,
                TenantIds = tenantIds
            };
        }

        public Task<MasterRbacUser?> GetByIdAsync(int userId)
        {
            return _db.RbacUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task<IReadOnlyList<RbacUserListItemDto>> GetAllAsync()
        {
            var users = await _db.RbacUsers.AsNoTracking().OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();
            var tenants = await _db.Tenants.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Code);
            var roleMap = await _db.RbacUserRoles
                .AsNoTracking()
                .Where(ur => ur.IsActive)
                .Join(_db.RbacRoles.AsNoTracking(), ur => ur.RoleId, r => r.RoleId, (ur, r) => new { ur.UserId, r.RoleNameEn, r.RoleNameAr, r.RoleName, r.RoleCode })
                .ToListAsync();
            var hotelMap = await _db.PmsUserHotels
                .AsNoTracking()
                .Where(h => h.IsActive)
                .Select(h => new { h.UserId, h.TenantId })
                .ToListAsync();

            return users.Select(u => new RbacUserListItemDto
            {
                UserId = u.UserId,
                Username = u.Username,
                EmployeeNumber = u.EmployeeNumber,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Department = u.Department,
                UserType = u.UserType,
                IsActive = u.IsActive,
                RoleSummary = string.Join(", ", roleMap.Where(r => r.UserId == u.UserId)
                    .Select(r => r.RoleCode ?? r.RoleNameEn ?? r.RoleNameAr ?? r.RoleName).Distinct()),
                HotelsSummary = string.Join(", ", hotelMap.Where(h => h.UserId == u.UserId && tenants.ContainsKey(h.TenantId))
                    .Select(h => tenants[h.TenantId]).Distinct())
            }).ToList();
        }

        public async Task<MasterRbacUser> CreateAsync(RbacUserSaveDto dto)
        {
            ValidateUserDto(dto, requirePassword: true, requireHotels: true);

            var user = new MasterRbacUser
            {
                Username = dto.Username.Trim(),
                EmployeeNumber = dto.EmployeeNumber?.Trim(),
                PasswordHash = _passwordHashing.HashPassword(dto.Password!),
                UserType = dto.UserType,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Title = dto.Title,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Department = dto.Department,
                IsActive = dto.IsActive,
                Status = dto.IsActive,
                CreatedAt = KsaTime.Now
            };

            _db.RbacUsers.Add(user);
            await _db.SaveChangesAsync();

            if (dto.RoleId.HasValue)
            {
                await AssignRoleAsync(user.UserId, dto.RoleId.Value);
            }

            EnsureAssignableTenants(dto.TenantIds);
            await AssignHotelsAsync(user.UserId, dto.TenantIds);
            return user;
        }

        public async Task<MasterRbacUser> UpdateAsync(int userId, RbacUserSaveDto dto)
        {
            ValidateUserDto(dto, requirePassword: false, requireHotels: false);

            var user = await _db.RbacUsers.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new KeyNotFoundException($"User {userId} not found");

            var wasActive = user.IsActive;
            user.Username = dto.Username.Trim();
            user.EmployeeNumber = dto.EmployeeNumber?.Trim();
            user.UserType = dto.UserType;
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Title = dto.Title;
            user.Email = dto.Email;
            user.PhoneNumber = dto.PhoneNumber;
            user.Department = dto.Department;
            user.IsActive = dto.IsActive;
            user.Status = dto.IsActive;
            user.UpdatedAt = KsaTime.Now;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = _passwordHashing.HashPassword(dto.Password);
            }

            await _db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                await _sessionService.RevokeAllUserSessionsAsync(
                    userId,
                    SecurityAuditEventTypes.PasswordChanged,
                    _currentUser.UserId);
            }

            if (wasActive && !user.IsActive)
            {
                await _sessionService.RevokeAllUserSessionsAsync(
                    userId,
                    "UserDeactivated",
                    _currentUser.UserId);
            }

            if (dto.RoleId.HasValue)
            {
                await AssignRoleAsync(userId, dto.RoleId.Value);
            }

            if (dto.TenantIds != null && dto.TenantIds.Count > 0)
            {
                EnsureAssignableTenants(dto.TenantIds);
                await AssignHotelsAsync(userId, dto.TenantIds);
            }

            return user;
        }

        public async Task<IReadOnlyList<string>> GetUserRoleCodesAsync(int userId)
        {
            var roleIds = await _db.RbacUserRoles
                .AsNoTracking()
                .Where(ur => ur.IsActive && ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            if (roleIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            return await _db.RbacRoles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.RoleId) && r.IsActive)
                .Select(r => r.RoleCode ?? r.RoleNameEn ?? r.RoleName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToListAsync();
        }

        public async Task AssignRoleAsync(int userId, int roleId)
        {
            if (!await _db.RbacRoles.AnyAsync(r => r.RoleId == roleId))
            {
                throw new KeyNotFoundException($"Role {roleId} not found");
            }

            var existing = await _db.RbacUserRoles.Where(ur => ur.UserId == userId).ToListAsync();
            _db.RbacUserRoles.RemoveRange(existing);
            _db.RbacUserRoles.Add(new MasterRbacUserRole
            {
                UserId = userId,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = KsaTime.Now
            });
            await _db.SaveChangesAsync();
        }

        public async Task AssignHotelsAsync(int userId, IEnumerable<int> tenantIds)
        {
            var ids = (tenantIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                throw new ArgumentException("At least one hotel is required");
            }

            EnsureAssignableTenants(ids);

            foreach (var tenantId in ids)
            {
                if (!await _db.Tenants.AnyAsync(t => t.Id == tenantId))
                {
                    throw new KeyNotFoundException($"Tenant {tenantId} not found");
                }
            }

            var existing = await _db.PmsUserHotels.Where(x => x.UserId == userId).ToListAsync();
            _db.PmsUserHotels.RemoveRange(existing);

            foreach (var tenantId in ids)
            {
                _db.PmsUserHotels.Add(new PmsUserHotel
                {
                    UserId = userId,
                    TenantId = tenantId,
                    IsActive = true,
                    CreatedAt = KsaTime.Now
                });
            }

            await _db.SaveChangesAsync();

            await _sessionService.RevokeAllUserSessionsAsync(
                userId,
                "Hotel access assignment changed",
                _currentUser.IsAuthenticated ? _currentUser.UserId : null);

            _logger.LogInformation(
                "[SECURITY] Revoked active sessions for user {UserId} after hotel assignment change",
                userId);
        }

        private void EnsureAssignableTenants(IEnumerable<int> tenantIds)
        {
            if (!_currentUser.IsAuthenticated || _currentUser.AllowedHotelIds.Count == 0)
            {
                return;
            }

            var invalid = tenantIds
                .Where(id => id > 0 && !_currentUser.AllowedHotelIds.Contains(id))
                .Distinct()
                .ToList();

            if (invalid.Count > 0)
            {
                throw new UnauthorizedAccessException(
                    $"You cannot assign hotel(s) you do not have access to: {string.Join(", ", invalid)}");
            }
        }

        private static void ValidateUserDto(RbacUserSaveDto dto, bool requirePassword, bool requireHotels = true)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
            {
                throw new ArgumentException("Username is required");
            }

            if (requirePassword && string.IsNullOrWhiteSpace(dto.Password))
            {
                throw new ArgumentException("Password is required");
            }

            if (requireHotels && (dto.TenantIds == null || dto.TenantIds.Count == 0))
            {
                throw new ArgumentException("At least one hotel is required");
            }
        }

        public Task<MasterRbacUser?> GetByEmployeeNumberAsync(string employeeNumber)
        {
            if (string.IsNullOrWhiteSpace(employeeNumber))
            {
                return Task.FromResult<MasterRbacUser?>(null);
            }

            var key = employeeNumber.Trim();
            return _db.RbacUsers.AsNoTracking().FirstOrDefaultAsync(u =>
                u.IsActive && u.EmployeeNumber != null && u.EmployeeNumber.ToLower() == key.ToLower());
        }

        public Task<MasterRbacUser?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Task.FromResult<MasterRbacUser?>(null);
            }

            var key = email.Trim();
            return _db.RbacUsers.AsNoTracking().FirstOrDefaultAsync(u =>
                u.IsActive && u.Email.ToLower() == key.ToLower());
        }

        public async Task<int> GetRecentResetRequestsAsync(int userId, TimeSpan window)
        {
            var since = KsaTime.Now.Subtract(window);
            return await _db.ResetPasswordTokens
                .AsNoTracking()
                .CountAsync(t => t.UserId == userId && t.CreatedAt >= since);
        }

        public async Task<string> CreatePasswordResetTokenAsync(int userId, string? ipAddress = null)
        {
            var user = await _db.RbacUsers.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null || !user.IsActive)
            {
                throw new KeyNotFoundException($"User {userId} not found or inactive");
            }

            var existingTokens = await _db.ResetPasswordTokens
                .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > KsaTime.Now)
                .ToListAsync();

            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
                existingToken.UsedAt = KsaTime.Now;
            }

            var tokenBytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }

            var resetTokenString = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            var resetToken = new ResetPasswordToken
            {
                UserId = userId,
                Token = resetTokenString,
                ExpiresAt = KsaTime.Now.AddMinutes(30),
                IsUsed = false,
                RequestIpAddress = ipAddress,
                CreatedAt = KsaTime.Now
            };

            await _db.ResetPasswordTokens.AddAsync(resetToken);
            await _db.SaveChangesAsync();
            return resetTokenString;
        }

        public async Task<int?> ValidateResetTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var resetToken = await _db.ResetPasswordTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Token == token);

            if (resetToken == null || resetToken.IsUsed || resetToken.ExpiresAt < KsaTime.Now)
            {
                return null;
            }

            var user = await _db.RbacUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == resetToken.UserId);
            if (user == null || !user.IsActive)
            {
                return null;
            }

            return resetToken.UserId;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var userId = await ValidateResetTokenAsync(token);
            if (!userId.HasValue)
            {
                return false;
            }

            var user = await _db.RbacUsers.FirstOrDefaultAsync(u => u.UserId == userId.Value);
            if (user == null || !user.IsActive)
            {
                return false;
            }

            user.PasswordHash = _passwordHashing.HashPassword(newPassword);
            user.UpdatedAt = KsaTime.Now;

            var resetToken = await _db.ResetPasswordTokens.FirstOrDefaultAsync(t => t.Token == token);
            if (resetToken != null)
            {
                resetToken.IsUsed = true;
                resetToken.UsedAt = KsaTime.Now;
            }

            await _db.SaveChangesAsync();

            await _sessionService.RevokeAllUserSessionsAsync(
                user.UserId,
                SecurityAuditEventTypes.PasswordChanged,
                user.UserId);

            return true;
        }

        public async Task<RbacProfileDto?> GetProfileAsync(int userId)
        {
            var user = await _db.RbacUsers.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return null;
            }

            var (countryCode, phoneLocal) = SplitPhoneNumber(user.PhoneNumber);
            var fullName = $"{user.FirstName} {user.LastName}".Trim();

            return new RbacProfileDto
            {
                UserId = user.UserId,
                Username = user.Username,
                EmployeeNumber = user.EmployeeNumber,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneCountryCode = countryCode,
                PhoneLocal = phoneLocal,
                PhoneNumber = user.PhoneNumber,
                Department = user.Department,
                FullName = string.IsNullOrWhiteSpace(fullName) ? user.Username : fullName,
                Initials = BuildInitials(user.FirstName, user.LastName, user.Username)
            };
        }

        public async Task<RbacProfileDto> UpdateProfileAsync(int userId, RbacProfileUpdateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName))
            {
                throw new ArgumentException("First name is required");
            }

            if (string.IsNullOrWhiteSpace(dto.LastName))
            {
                throw new ArgumentException("Last name is required");
            }

            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                throw new ArgumentException("Email is required");
            }

            var user = await _db.RbacUsers.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new KeyNotFoundException($"User {userId} not found");

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            user.Email = dto.Email.Trim();
            user.PhoneNumber = CombinePhoneNumber(dto.PhoneCountryCode, dto.PhoneLocal);
            user.UpdatedAt = KsaTime.Now;

            await _db.SaveChangesAsync();
            return (await GetProfileAsync(userId))!;
        }

        public async Task ChangePasswordAsync(int userId, RbacChangePasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
            {
                throw new ArgumentException("Current password is required");
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                throw new ArgumentException("New password is required");
            }

            if (dto.NewPassword != dto.ConfirmPassword)
            {
                throw new ArgumentException("Password confirmation does not match");
            }

            if (dto.NewPassword.Length < 6)
            {
                throw new ArgumentException("Password must be at least 6 characters");
            }

            var user = await _db.RbacUsers.FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new KeyNotFoundException($"User {userId} not found");

            if (!_passwordHashing.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Current password is incorrect");
            }

            user.PasswordHash = _passwordHashing.HashPassword(dto.NewPassword);
            user.ChangePassword = false;
            user.UpdatedAt = KsaTime.Now;
            await _db.SaveChangesAsync();

            await _sessionService.RevokeAllUserSessionsAsync(
                userId,
                SecurityAuditEventTypes.PasswordChanged,
                userId);
            await _sessionService.LogSecurityEventAsync(
                SecurityAuditEventTypes.PasswordChanged,
                userId,
                userId,
                null,
                null,
                null);
        }

        public string HashPassword(string password) => _passwordHashing.HashPassword(password);

        private static string BuildInitials(string firstName, string lastName, string username)
        {
            var f = (firstName ?? "").Trim();
            var l = (lastName ?? "").Trim();
            if (f.Length > 0 && l.Length > 0)
            {
                return $"{f[0]}{l[0]}".ToUpperInvariant();
            }

            if (f.Length > 0)
            {
                return f.Length >= 2 ? f[..2].ToUpperInvariant() : f[0].ToString().ToUpperInvariant();
            }

            var u = (username ?? "U").Trim();
            return u.Length >= 2 ? u[..2].ToUpperInvariant() : u.ToUpperInvariant();
        }

        private static (string CountryCode, string? Local) SplitPhoneNumber(string? phoneNumber)
        {
            var digits = Regex.Replace(phoneNumber ?? "", @"\D", "");
            if (string.IsNullOrWhiteSpace(digits))
            {
                return ("+966", null);
            }

            if (digits.StartsWith("966", StringComparison.Ordinal))
            {
                return ("+966", digits[3..]);
            }

            if (digits.StartsWith("05", StringComparison.Ordinal) && digits.Length >= 10)
            {
                return ("+966", digits[1..]);
            }

            if (digits.StartsWith("5", StringComparison.Ordinal) && digits.Length >= 9)
            {
                return ("+966", digits);
            }

            return ("+966", digits);
        }

        private static string? CombinePhoneNumber(string? countryCode, string? local)
        {
            var localDigits = Regex.Replace(local ?? "", @"\D", "");
            if (string.IsNullOrWhiteSpace(localDigits))
            {
                return null;
            }

            if (localDigits.StartsWith("0", StringComparison.Ordinal))
            {
                localDigits = localDigits[1..];
            }

            var ccDigits = Regex.Replace(countryCode ?? "+966", @"\D", "");
            if (string.IsNullOrWhiteSpace(ccDigits))
            {
                ccDigits = "966";
            }

            return ccDigits + localDigits;
        }
    }
}
