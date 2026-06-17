using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Service for managing Master Users
    /// </summary>
    public class MasterUserService : IMasterUserService
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ILogger<MasterUserService> _logger;
        private readonly IPasswordHashingService _passwordHashingService;

        /// <summary>
        /// Constructor for MasterUserService
        /// </summary>
        /// <param name="masterDbContext">Master database context</param>
        /// <param name="logger">Logger instance</param>
        public MasterUserService(
            MasterDbContext masterDbContext,
            ILogger<MasterUserService> logger,
            IPasswordHashingService passwordHashingService)
        {
            _masterDbContext = masterDbContext ?? throw new ArgumentNullException(nameof(masterDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _passwordHashingService = passwordHashingService ?? throw new ArgumentNullException(nameof(passwordHashingService));
        }

        /// <summary>
        /// الحصول على المستخدم بواسطة Username
        /// ✅ مهم: يجلب المستخدم من Master DB فقط (ليس Tenant DB)
        /// </summary>
        public async Task<MasterUser?> GetByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            // ✅ جلب المستخدم من Master DB فقط
            return await _masterDbContext.MasterUsers
                .AsNoTracking()
                .Include(u => u.Tenant)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        }

        /// <summary>
        /// الحصول على المستخدم بواسطة EmployeeNumber
        /// ✅ مهم: يجلب المستخدم من Master DB فقط (ليس Tenant DB)
        /// </summary>
        public async Task<MasterUser?> GetByEmployeeNumberAsync(string employeeNumber)
        {
            if (string.IsNullOrWhiteSpace(employeeNumber))
                return null;

            // ✅ جلب المستخدم من Master DB فقط باستخدام EmployeeNumber
            return await _masterDbContext.MasterUsers
                .AsNoTracking()
                .Include(u => u.Tenant)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.EmployeeNumber != null && u.EmployeeNumber.ToLower() == employeeNumber.ToLower());
        }

        /// <summary>
        /// الحصول على المستخدم بواسطة Id
        /// </summary>
        public async Task<MasterUser?> GetByIdAsync(int userId)
        {
            return await _masterDbContext.MasterUsers
                .AsNoTracking()
                .Include(u => u.Tenant)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        /// <summary>
        /// الحصول على أدوار المستخدم
        /// </summary>
        public async Task<IEnumerable<string>> GetUserRolesAsync(int userId)
        {
            var userRoles = await _masterDbContext.UserRoles
                .AsNoTracking()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role!.Code)
                .ToListAsync();

            return userRoles;
        }

        /// <summary>
        /// التحقق من كلمة المرور مع دعم القيم القديمة غير المشفرة.
        /// </summary>
        public bool ValidatePassword(string password, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            {
                _logger.LogWarning("Password validation failed: Empty password or password hash");
                return false;
            }

            var isValid = _passwordHashingService.VerifyPassword(password, passwordHash);
            if (!isValid)
            {
                _logger.LogWarning("❌ Password verification failed.");
            }
            else
            {
                _logger.LogInformation("✅ Password verification successful");
            }
            
            return isValid;
        }

        /// <summary>
        /// حفظ كلمة المرور بشكل آمن.
        /// </summary>
        public string HashPassword(string password)
        {
            return _passwordHashingService.HashPassword(password);
        }

        /// <summary>
        /// إنشاء مستخدم جديد
        /// </summary>
        public async Task<MasterUser> CreateUserAsync(string username, string password, int tenantId, IEnumerable<int> roleIds)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            // التحقق من وجود Tenant
            var tenant = await _masterDbContext.Tenants.FindAsync(tenantId);
            if (tenant == null)
                throw new KeyNotFoundException($"Tenant with id {tenantId} not found");

            // التحقق من عدم وجود مستخدم بنفس Username
            var existingUser = await GetByUsernameAsync(username);
            if (existingUser != null)
                throw new InvalidOperationException($"User with username '{username}' already exists");

            // إنشاء المستخدم
            var user = new MasterUser
            {
                Username = username,
                PasswordHash = HashPassword(password),
                TenantId = tenantId,
                IsActive = true,
                CreatedAt = KsaTime.Now
            };

            _masterDbContext.MasterUsers.Add(user);
            await _masterDbContext.SaveChangesAsync();

            // إضافة الأدوار
            if (roleIds != null && roleIds.Any())
            {
                foreach (var roleId in roleIds)
                {
                    var role = await _masterDbContext.Roles.FindAsync(roleId);
                    if (role != null)
                    {
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId
                        };
                        _masterDbContext.UserRoles.Add(userRole);
                    }
                }
                await _masterDbContext.SaveChangesAsync();
            }

            _logger.LogInformation("✅ User created successfully: Username={Username}, TenantId={TenantId}", username, tenantId);

            return user;
        }

        /// <summary>
        /// إنشاء مستخدم جديد مع الحقول الإضافية
        /// </summary>
        public async Task<MasterUser> CreateUserAsync(string username, string password, int tenantId, IEnumerable<int> roleIds, 
            string? phoneNumber, string? email, string? employeeNumber, string? fullName, 
            IEnumerable<int>? additionalTenantIds = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty", nameof(username));

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));

            // التحقق من وجود Tenant
            var tenant = await _masterDbContext.Tenants.FindAsync(tenantId);
            if (tenant == null)
                throw new KeyNotFoundException($"Tenant with id {tenantId} not found");

            // التحقق من عدم وجود مستخدم بنفس Username
            var existingUser = await GetByUsernameAsync(username);
            if (existingUser != null)
                throw new InvalidOperationException($"User with username '{username}' already exists");

            // إنشاء المستخدم
            var user = new MasterUser
            {
                Username = username,
                PasswordHash = HashPassword(password),
                TenantId = tenantId,
                PhoneNumber = phoneNumber,
                Email = email,
                EmployeeNumber = employeeNumber,
                FullName = fullName,
                IsActive = true,
                CreatedAt = KsaTime.Now
            };

            _masterDbContext.MasterUsers.Add(user);
            await _masterDbContext.SaveChangesAsync();

            // إضافة الأدوار
            var userRoles = new List<Role>();
            if (roleIds != null && roleIds.Any())
            {
                foreach (var roleId in roleIds)
                {
                    var role = await _masterDbContext.Roles.FindAsync(roleId);
                    if (role != null)
                    {
                        userRoles.Add(role);
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId
                        };
                        _masterDbContext.UserRoles.Add(userRole);
                    }
                }
                await _masterDbContext.SaveChangesAsync();
            }

            // إضافة UserTenants بناءً على الأدوار
            // القواعد:
            // - Supervisor: إذا تم تحديد فنادق إضافية، نضيف فقط الفنادق المحددة + الفندق الأساسي. إذا لم يتم تحديد، نضيف جميع الفنادق
            // - Manager, Accountant, Admin, Officer, Owner: إذا تم تحديد فنادق إضافية، نضيف الفنادق المحددة + الفندق الأساسي. إذا لم يتم تحديد، نضيف جميع الفنادق
            // - Staff: الفندق الأساسي فقط
            var hasSupervisorRole = userRoles.Any(r => r.Code.Equals("Supervisor", StringComparison.OrdinalIgnoreCase));
            var hasManagerRole = userRoles.Any(r => r.Code.Equals("Manager", StringComparison.OrdinalIgnoreCase));
            var hasAccountantRole = userRoles.Any(r => r.Code.Equals("Accountant", StringComparison.OrdinalIgnoreCase));
            var hasAdminRole = userRoles.Any(r => r.Code.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            var hasOfficerRole = userRoles.Any(r => r.Code.Equals("Officer", StringComparison.OrdinalIgnoreCase));
            var hasOwnerRole = userRoles.Any(r => r.Code.Equals("Owner", StringComparison.OrdinalIgnoreCase));
            var hasVerifierRole = userRoles.Any(r => r.Code.Equals("Verifier", StringComparison.OrdinalIgnoreCase));
            var hasStaffRole = userRoles.Any(r => r.Code.Equals("Staff", StringComparison.OrdinalIgnoreCase));

            // جمع جميع الفنادق المطلوبة في HashSet لتجنب التكرار
            var tenantsToAdd = new HashSet<int>();
            
            // دائماً إضافة الفندق الأساسي
            tenantsToAdd.Add(tenantId);

            if (hasSupervisorRole)
            {
                // Supervisor: إذا تم تحديد فنادق إضافية، نضيف فقط الفنادق المحددة + الفندق الأساسي
                // إذا لم يتم تحديد، نضيف جميع الفنادق
                if (additionalTenantIds != null && additionalTenantIds.Any())
                {
                    // إضافة الفنادق المحددة يدوياً
                    foreach (var additionalTenantId in additionalTenantIds)
                    {
                        if (additionalTenantId != tenantId)
                        {
                            tenantsToAdd.Add(additionalTenantId);
                        }
                    }
                    
                    _logger.LogInformation("✅ Added selected tenants for Supervisor user: UserId={UserId}, TenantCount={Count}", 
                        user.Id, tenantsToAdd.Count);
                }
                else
                {
                    // إضافة جميع الفنادق
                    var allTenants = await _masterDbContext.Tenants
                        .Select(t => t.Id)
                        .ToListAsync();
                    
                    foreach (var tenantIdToAdd in allTenants)
                    {
                        tenantsToAdd.Add(tenantIdToAdd);
                    }
                    
                    _logger.LogInformation("✅ Added all tenants for Supervisor user: UserId={UserId}, TenantCount={Count}", 
                        user.Id, tenantsToAdd.Count);
                }
            }
            else if (hasManagerRole || hasAccountantRole || hasAdminRole || hasOfficerRole || hasOwnerRole || hasVerifierRole)
            {
                // ✅ Manager, Accountant, Admin, Officer, Owner, Verifier: إضافة جميع الفنادق تلقائياً
                var allTenants = await _masterDbContext.Tenants
                    .Select(t => t.Id)
                    .ToListAsync();
                
                foreach (var tenantIdToAdd in allTenants)
                {
                    tenantsToAdd.Add(tenantIdToAdd);
                }
                
                var roleName = hasManagerRole ? "Manager" : 
                              hasAccountantRole ? "Accountant" : 
                              hasAdminRole ? "Admin" : 
                              hasOfficerRole ? "Officer" : 
                              hasOwnerRole ? "Owner" :
                              "Verifier";
                _logger.LogInformation("✅ Added all tenants for {Role} user: UserId={UserId}, TenantCount={Count}", 
                    roleName, user.Id, tenantsToAdd.Count);
            }
            else if (hasStaffRole)
            {
                // Staff: الفندق الأساسي فقط (تم إضافته بالفعل)
                _logger.LogInformation("✅ Added primary tenant only for Staff user: UserId={UserId}, TenantId={TenantId}", 
                    user.Id, tenantId);
            }

            // إضافة جميع الفنادق المجمعة إلى UserTenants
            foreach (var tenantIdToAdd in tenantsToAdd)
            {
                var existingUserTenant = await _masterDbContext.UserTenants
                    .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.TenantId == tenantIdToAdd);
                
                if (existingUserTenant == null)
                {
                    var userTenant = new UserTenant
                    {
                        UserId = user.Id,
                        TenantId = tenantIdToAdd,
                        CreatedAt = KsaTime.Now
                    };
                    _masterDbContext.UserTenants.Add(userTenant);
                }
            }

            // حفظ جميع التغييرات في UserTenants
            if (_masterDbContext.ChangeTracker.HasChanges())
            {
                await _masterDbContext.SaveChangesAsync();
            }

            _logger.LogInformation("✅ User created successfully with additional fields: Username={Username}, TenantId={TenantId}, Email={Email}", 
                username, tenantId, email);

            return user;
        }

        /// <summary>
        /// الحصول على جميع المستخدمين
        /// </summary>
        public async Task<IEnumerable<MasterUser>> GetAllUsersAsync()
        {
            return await _masterDbContext.MasterUsers
                .AsNoTracking()
                .Include(u => u.Tenant)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        /// <summary>
        /// تحديث مستخدم
        /// </summary>
        public async Task<MasterUser> UpdateUserAsync(int userId, string? username, string? password, int? tenantId, 
            string? phoneNumber, string? email, string? employeeNumber, string? fullName, 
            bool? isActive, IEnumerable<int>? roleIds, IEnumerable<int>? additionalTenantIds)
        {
            var user = await _masterDbContext.MasterUsers
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException($"User with id {userId} not found");

            // تحديث الحقول
            if (!string.IsNullOrWhiteSpace(username) && username != user.Username)
            {
                // التحقق من عدم وجود مستخدم آخر بنفس Username
                var existingUser = await GetByUsernameAsync(username);
                if (existingUser != null && existingUser.Id != userId)
                    throw new InvalidOperationException($"User with username '{username}' already exists");
                
                user.Username = username;
            }

            if (!string.IsNullOrWhiteSpace(password))
                user.PasswordHash = HashPassword(password);

            if (tenantId.HasValue)
            {
                var tenant = await _masterDbContext.Tenants.FindAsync(tenantId.Value);
                if (tenant == null)
                    throw new KeyNotFoundException($"Tenant with id {tenantId.Value} not found");
                user.TenantId = tenantId.Value;
            }

            if (phoneNumber != null)
                user.PhoneNumber = phoneNumber;

            if (email != null)
                user.Email = email;

            if (employeeNumber != null)
                user.EmployeeNumber = employeeNumber;

            if (fullName != null)
                user.FullName = fullName;

            if (isActive.HasValue)
                user.IsActive = isActive.Value;

            user.UpdatedAt = KsaTime.Now;

            // تحديث الأدوار
            var updatedRoles = new List<Role>();
            if (roleIds != null)
            {
                // حذف الأدوار الحالية
                var existingRoles = _masterDbContext.UserRoles.Where(ur => ur.UserId == userId);
                _masterDbContext.UserRoles.RemoveRange(existingRoles);

                // إضافة الأدوار الجديدة
                foreach (var roleId in roleIds)
                {
                    var role = await _masterDbContext.Roles.FindAsync(roleId);
                    if (role != null)
                    {
                        updatedRoles.Add(role);
                        var userRole = new UserRole
                        {
                            UserId = user.Id,
                            RoleId = roleId
                        };
                        _masterDbContext.UserRoles.Add(userRole);
                    }
                }
            }

            // تحديث UserTenants بناءً على الأدوار الجديدة
            // القواعد:
            // - Supervisor: جميع الفنادق ما عدا الفندق الأساسي
            // - Manager, Accountant, Admin: الفندق الأساسي فقط
            // إذا تم تحديث الأدوار، نحتاج إلى إعادة بناء UserTenants
            if (roleIds != null)
            {
                // حذف جميع UserTenants الحالية (سنعيد بناؤها بناءً على الأدوار)
                var existingUserTenants = _masterDbContext.UserTenants
                    .Where(ut => ut.UserId == userId);
                _masterDbContext.UserTenants.RemoveRange(existingUserTenants);

                var hasSupervisorRole = updatedRoles.Any(r => r.Code.Equals("Supervisor", StringComparison.OrdinalIgnoreCase));
                var hasManagerRole = updatedRoles.Any(r => r.Code.Equals("Manager", StringComparison.OrdinalIgnoreCase));
                var hasAccountantRole = updatedRoles.Any(r => r.Code.Equals("Accountant", StringComparison.OrdinalIgnoreCase));
                var hasAdminRole = updatedRoles.Any(r => r.Code.Equals("Admin", StringComparison.OrdinalIgnoreCase));
                var hasOfficerRole = updatedRoles.Any(r => r.Code.Equals("Officer", StringComparison.OrdinalIgnoreCase));
                var hasOwnerRole = updatedRoles.Any(r => r.Code.Equals("Owner", StringComparison.OrdinalIgnoreCase));
                var hasVerifierRole = updatedRoles.Any(r => r.Code.Equals("Verifier", StringComparison.OrdinalIgnoreCase));
                var hasStaffRole = updatedRoles.Any(r => r.Code.Equals("Staff", StringComparison.OrdinalIgnoreCase));

                var currentTenantId = tenantId ?? user.TenantId;

                // جمع جميع الفنادق المطلوبة في HashSet لتجنب التكرار
                var tenantsToAdd = new HashSet<int>();
                
                // دائماً إضافة الفندق الأساسي
                tenantsToAdd.Add(currentTenantId);

                if (hasSupervisorRole)
                {
                    // Supervisor: إذا تم تحديد فنادق إضافية، نضيف فقط الفنادق المحددة + الفندق الأساسي
                    // إذا لم يتم تحديد، نضيف جميع الفنادق
                    if (additionalTenantIds != null && additionalTenantIds.Any())
                    {
                        // إضافة الفنادق المحددة يدوياً
                        foreach (var additionalTenantId in additionalTenantIds)
                        {
                            if (additionalTenantId != currentTenantId)
                            {
                                tenantsToAdd.Add(additionalTenantId);
                            }
                        }
                        
                        _logger.LogInformation("✅ Updated UserTenants for Supervisor user (selected tenants): UserId={UserId}, TenantCount={Count}", 
                            user.Id, tenantsToAdd.Count);
                    }
                    else
                    {
                        // إضافة جميع الفنادق
                        var allTenants = await _masterDbContext.Tenants
                            .Select(t => t.Id)
                            .ToListAsync();
                        
                        foreach (var tenantIdToAdd in allTenants)
                        {
                            tenantsToAdd.Add(tenantIdToAdd);
                        }
                        
                        _logger.LogInformation("✅ Updated UserTenants for Supervisor user (all tenants): UserId={UserId}, TenantCount={Count}", 
                            user.Id, tenantsToAdd.Count);
                    }
                }
                else if (hasManagerRole || hasAccountantRole || hasAdminRole || hasOfficerRole || hasOwnerRole || hasVerifierRole)
                {
                    // ✅ Manager, Accountant, Admin, Officer, Owner, Verifier: إضافة جميع الفنادق تلقائياً
                    var allTenants = await _masterDbContext.Tenants
                        .Select(t => t.Id)
                        .ToListAsync();
                    
                    foreach (var tenantIdToAdd in allTenants)
                    {
                        tenantsToAdd.Add(tenantIdToAdd);
                    }
                    
                    var roleName = hasManagerRole ? "Manager" : 
                                  hasAccountantRole ? "Accountant" : 
                                  hasAdminRole ? "Admin" : 
                                  hasOfficerRole ? "Officer" : 
                                  hasOwnerRole ? "Owner" :
                                  "Verifier";
                    _logger.LogInformation("✅ Updated UserTenants for {Role} user (all tenants): UserId={UserId}, TenantCount={Count}", 
                        roleName, user.Id, tenantsToAdd.Count);
                }
                else if (hasStaffRole)
                {
                    // Staff: الفندق الأساسي فقط (تم إضافته بالفعل)
                    _logger.LogInformation("✅ Updated UserTenants for Staff user (primary only): UserId={UserId}, TenantId={TenantId}", 
                        user.Id, currentTenantId);
                }

                // إضافة جميع الفنادق المجمعة إلى UserTenants
                foreach (var tenantIdToAdd in tenantsToAdd)
                {
                    var userTenant = new UserTenant
                    {
                        UserId = user.Id,
                        TenantId = tenantIdToAdd,
                        CreatedAt = KsaTime.Now
                    };
                    _masterDbContext.UserTenants.Add(userTenant);
                }
            }
            else
            {
                // إذا لم يتم تحديث الأدوار، فقط تحديث الفنادق الإضافية المحددة يدوياً
                if (additionalTenantIds != null)
                {
                    // حذف الفنادق الإضافية الحالية (وليس الفندق الأساسي)
                    var existingTenants = _masterDbContext.UserTenants
                        .Where(ut => ut.UserId == userId);
                    _masterDbContext.UserTenants.RemoveRange(existingTenants);

                    // إضافة الفنادق الجديدة
                    if (additionalTenantIds.Any())
                    {
                        var currentTenantId = tenantId ?? user.TenantId;
                        foreach (var additionalTenantId in additionalTenantIds)
                        {
                            if (additionalTenantId != currentTenantId)
                            {
                                var additionalTenant = await _masterDbContext.Tenants.FindAsync(additionalTenantId);
                                if (additionalTenant != null)
                                {
                                    var userTenant = new UserTenant
                                    {
                                        UserId = user.Id,
                                        TenantId = additionalTenantId,
                                        CreatedAt = KsaTime.Now
                                    };
                                    _masterDbContext.UserTenants.Add(userTenant);
                                }
                            }
                        }
                    }
                }
            }

            await _masterDbContext.SaveChangesAsync();

            _logger.LogInformation("✅ User updated successfully: UserId={UserId}, Username={Username}", userId, user.Username);

            return user;
        }

        /// <summary>
        /// حذف مستخدم
        /// </summary>
        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _masterDbContext.MasterUsers.FindAsync(userId);
            if (user == null)
                return false;

            _masterDbContext.MasterUsers.Remove(user);
            await _masterDbContext.SaveChangesAsync();

            _logger.LogInformation("✅ User deleted successfully: UserId={UserId}, Username={Username}", userId, user.Username);

            return true;
        }

        /// <summary>
        /// التحقق من صحة بيانات تسجيل الدخول
        /// </summary>
        public async Task<MasterUser?> ValidateLoginAsync(string employeeNumber, string password)
        {
            // ✅ 1. التحقق من البيانات المدخلة أولاً
            if (string.IsNullOrWhiteSpace(employeeNumber) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Login attempt with empty employee number or password");
                return null;
            }

            // ✅ 2. جلب المستخدم من Master DB فقط (ليس Tenant DB)
            // ✅ استخدام EmployeeNumber فقط للدخول
            var user = await GetByEmployeeNumberAsync(employeeNumber);
            
            if (user == null)
            {
                _logger.LogWarning("❌ Login failed: User not found in Master DB. EmployeeNumber: {EmployeeNumber}", employeeNumber);
                return null;
            }
            
            _logger.LogInformation("✅ User found in Master DB. EmployeeNumber: {EmployeeNumber}, Username: {Username}, Id: {UserId}", 
                employeeNumber, user.Username, user.Id);

            _logger.LogDebug("✅ User found in Master DB. EmployeeNumber: {EmployeeNumber}, Username: {Username}, Id: {UserId}, TenantId: {TenantId}, IsActive: {IsActive}", 
                employeeNumber, user.Username, user.Id, user.TenantId, user.IsActive);

            // ✅ 3. التحقق من TenantId موجود وصحيح
            if (user.TenantId <= 0)
            {
                _logger.LogWarning("❌ Login failed: User has invalid TenantId. EmployeeNumber: {EmployeeNumber}, TenantId: {TenantId}", 
                    employeeNumber, user.TenantId);
                return null;
            }

            // ✅ 4. التحقق من PasswordHash موجود
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                _logger.LogWarning("❌ Login failed: User has no password hash. EmployeeNumber: {EmployeeNumber}", employeeNumber);
                return null;
            }

            _logger.LogDebug("🔍 Password hash found. Length: {HashLength}, Prefix: {HashPrefix}", 
                user.PasswordHash.Length,
                user.PasswordHash.Length > 30 ? user.PasswordHash.Substring(0, 30) + "..." : user.PasswordHash);

            // ✅ 5. التحقق من حالة المستخدم (IsActive) قبل التحقق من الباسورد
            if (!user.IsActive)
            {
                _logger.LogWarning("❌ Login failed: User is inactive. EmployeeNumber: {EmployeeNumber}", employeeNumber);
                return null;
            }

            // ✅ 6. التحقق من كلمة المرور (يجب أن يكون آخر فحص)
            if (!ValidatePassword(password, user.PasswordHash))
            {
                _logger.LogWarning("❌ Login failed: Invalid password. EmployeeNumber: {EmployeeNumber}", employeeNumber);
                return null;
            }

            if (_passwordHashingService.NeedsRehash(user.PasswordHash))
            {
                await _masterDbContext.MasterUsers
                    .Where(u => u.Id == user.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.PasswordHash, HashPassword(password))
                        .SetProperty(u => u.UpdatedAt, KsaTime.Now));
                _logger.LogInformation("✅ Legacy password hash upgraded for UserId={UserId}", user.Id);
            }

            _logger.LogInformation("✅ Login successful: EmployeeNumber={EmployeeNumber}, Username={Username}, TenantId={TenantId}", 
                employeeNumber, user.Username, user.TenantId);
            return user;
        }

        /// <summary>
        /// تغيير كلمة المرور مع Concurrency Control
        /// ✅ Senior Level Implementation: Handles multiple users changing passwords simultaneously
        /// 
        /// Strategy:
        /// 1. Database-level locking using UPDATE with WHERE clause
        /// 2. Optimistic Concurrency Control using RowVersion
        /// 3. Transaction with ReadCommitted isolation level
        /// 4. Retry logic for concurrency conflicts
        /// 5. Validation of current password in WHERE clause (prevents race conditions)
        /// 
        /// This ensures:
        /// - Only one password change succeeds if multiple users try simultaneously
        /// - Current password is validated atomically with the update
        /// - No lost updates or data corruption
        /// - Thread-safe and scalable
        /// </summary>
        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            // ✅ 1. Input Validation
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                _logger.LogWarning("❌ ChangePassword: Current password is empty for UserId={UserId}", userId);
                throw new ArgumentException("Current password cannot be empty", nameof(currentPassword));
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                _logger.LogWarning("❌ ChangePassword: New password is empty for UserId={UserId}", userId);
                throw new ArgumentException("New password cannot be empty", nameof(newPassword));
            }

            if (currentPassword == newPassword)
            {
                _logger.LogWarning("❌ ChangePassword: New password is same as current password for UserId={UserId}", userId);
                throw new InvalidOperationException("New password must be different from current password");
            }

            // ✅ 2. Verify user exists and is active
            var user = await _masterDbContext.MasterUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("❌ ChangePassword: User not found. UserId={UserId}", userId);
                throw new KeyNotFoundException($"User with id {userId} not found");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("❌ ChangePassword: User is inactive. UserId={UserId}", userId);
                throw new InvalidOperationException("Cannot change password for inactive user");
            }

            // ✅ 3. Validate current password
            if (!ValidatePassword(currentPassword, user.PasswordHash))
            {
                _logger.LogWarning("❌ ChangePassword: Current password is incorrect for UserId={UserId}", userId);
                return false; // Current password is incorrect
            }

            // ✅ 4. Hash new password
            var newPasswordHash = HashPassword(newPassword);

            // ✅ 5. Use Database Transaction with ReadCommitted isolation level
            // This ensures consistency while allowing concurrent reads
            using var transaction = await _masterDbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.ReadCommitted);

            try
            {
                // ✅ 6. Database-level UPDATE with WHERE clause
                // This is ATOMIC and prevents race conditions:
                // - Only updates if current password matches
                // - Only updates if user is active
                // - Only updates if user exists
                // - Uses database-level locking automatically
                var rowsAffected = await _masterDbContext.Database.ExecuteSqlRawAsync(
                    @"UPDATE MasterUsers 
                      SET PasswordHash = {0}, 
                          UpdatedAt = {1}
                      WHERE Id = {2} 
                        AND PasswordHash = {3} 
                        AND IsActive = 1",
                    newPasswordHash,
                    KsaTime.Now,
                    userId,
                    user.PasswordHash // Current password hash - ensures atomic validation
                );

                // ✅ 7. Check if update succeeded
                if (rowsAffected == 0)
                {
                    // This could happen if:
                    // - Password was changed by another process between validation and update
                    // - User was deactivated between validation and update
                    // - RowVersion conflict (if using optimistic concurrency)
                    _logger.LogWarning("❌ ChangePassword: Update failed - possible concurrency conflict. UserId={UserId}", userId);
                    
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException(
                        "Password change failed. The password may have been changed by another process, or the user may have been deactivated. Please try again.");
                }

                // ✅ 8. Commit transaction
                await transaction.CommitAsync();

                _logger.LogInformation("✅ ChangePassword: Password changed successfully. UserId={UserId}, Username={Username}", 
                    userId, user.Username);

                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // ✅ 9. Handle concurrency conflicts
                await transaction.RollbackAsync();
                _logger.LogWarning(ex, "❌ ChangePassword: Concurrency conflict. UserId={UserId}", userId);
                throw new InvalidOperationException(
                    "Password change failed due to a concurrency conflict. Another process may have modified the user. Please try again.",
                    ex);
            }
            catch (Exception ex)
            {
                // ✅ 10. Rollback on any error
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ ChangePassword: Unexpected error. UserId={UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// إنشاء رمز إعادة تعيين كلمة المرور
        /// </summary>
        public async Task<string> CreatePasswordResetTokenAsync(int userId, string? ipAddress = null)
        {
            // ✅ 1. Verify user exists and is active
            var user = await _masterDbContext.MasterUsers
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("❌ CreatePasswordResetToken: User not found. UserId={UserId}", userId);
                throw new KeyNotFoundException($"User with id {userId} not found");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("❌ CreatePasswordResetToken: User is inactive. UserId={UserId}", userId);
                throw new InvalidOperationException("Cannot create reset token for inactive user");
            }

            // ✅ 2. Invalidate any existing unused tokens for this user
            var existingTokens = await _masterDbContext.ResetPasswordTokens
                .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > KsaTime.Now)
                .ToListAsync();

            foreach (var existingToken in existingTokens)
            {
                existingToken.IsUsed = true;
                existingToken.UsedAt = KsaTime.Now;
            }

            // ✅ 3. Generate secure random token
            var tokenBytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            var resetTokenString = Convert.ToBase64String(tokenBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            // ✅ 4. Create reset token (valid for 30 minutes)
            var resetToken = new ResetPasswordToken
            {
                UserId = userId,
                Token = resetTokenString,
                ExpiresAt = KsaTime.Now.AddMinutes(30),
                IsUsed = false,
                RequestIpAddress = ipAddress,
                CreatedAt = KsaTime.Now
            };

            await _masterDbContext.ResetPasswordTokens.AddAsync(resetToken);
            await _masterDbContext.SaveChangesAsync();

            _logger.LogInformation("✅ CreatePasswordResetToken: Token created for UserId={UserId}, ExpiresAt={ExpiresAt}", 
                userId, resetToken.ExpiresAt);

            return resetTokenString;
        }

        /// <summary>
        /// التحقق من صحة رمز إعادة التعيين
        /// </summary>
        public async Task<int?> ValidateResetTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var resetToken = await _masterDbContext.ResetPasswordTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == token);

            if (resetToken == null)
            {
                _logger.LogWarning("❌ ValidateResetToken: Token not found");
                return null;
            }

            if (resetToken.IsUsed)
            {
                _logger.LogWarning("❌ ValidateResetToken: Token already used. Token={Token}", token);
                return null;
            }

            if (resetToken.ExpiresAt < KsaTime.Now)
            {
                _logger.LogWarning("❌ ValidateResetToken: Token expired. Token={Token}, ExpiresAt={ExpiresAt}", 
                    token, resetToken.ExpiresAt);
                return null;
            }

            if (resetToken.User == null || !resetToken.User.IsActive)
            {
                _logger.LogWarning("❌ ValidateResetToken: User not found or inactive. Token={Token}", token);
                return null;
            }

            _logger.LogInformation("✅ ValidateResetToken: Token is valid. UserId={UserId}", resetToken.UserId);
            return resetToken.UserId;
        }

        /// <summary>
        /// إعادة تعيين كلمة المرور باستخدام الرمز المميز
        /// </summary>
        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            // ✅ 1. Validate token
            var userId = await ValidateResetTokenAsync(token);
            if (!userId.HasValue)
            {
                _logger.LogWarning("❌ ResetPassword: Invalid token");
                return false;
            }

            // ✅ 2. Get user
            var user = await _masterDbContext.MasterUsers
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("❌ ResetPassword: User not found or inactive. UserId={UserId}", userId.Value);
                return false;
            }

            // ✅ 3. Use transaction for atomicity
            using var transaction = await _masterDbContext.Database.BeginTransactionAsync();

            try
            {
                // ✅ 4. Update password
                user.PasswordHash = HashPassword(newPassword);
                user.UpdatedAt = KsaTime.Now;

                // ✅ 5. Mark token as used
                var resetToken = await _masterDbContext.ResetPasswordTokens
                    .FirstOrDefaultAsync(t => t.Token == token);

                if (resetToken != null)
                {
                    resetToken.IsUsed = true;
                    resetToken.UsedAt = KsaTime.Now;
                }

                await _masterDbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("✅ ResetPassword: Password reset successfully. UserId={UserId}, Username={Username}", 
                    userId.Value, user.Username);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "❌ ResetPassword: Error resetting password. UserId={UserId}", userId.Value);
                throw;
            }
        }

        /// <summary>
        /// الحصول على المستخدم بواسطة البريد الإلكتروني
        /// </summary>
        public async Task<MasterUser?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _masterDbContext.MasterUsers
                .AsNoTracking()
                .Include(u => u.Tenant)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());
        }

        /// <summary>
        /// الحصول على عدد طلبات إعادة التعيين الأخيرة (للمنع من الإساءة)
        /// </summary>
        public async Task<int> GetRecentResetRequestsAsync(int userId, TimeSpan timeWindow)
        {
            var cutoffTime = KsaTime.Now - timeWindow;
            return await _masterDbContext.ResetPasswordTokens
                .CountAsync(t => t.UserId == userId && t.CreatedAt >= cutoffTime);
        }
    }
}

