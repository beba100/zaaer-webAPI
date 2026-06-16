using Microsoft.EntityFrameworkCore;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Models.VoM;

namespace zaaerIntegration.Data
{
    /// <summary>
    /// Master Database Context - للتعامل مع قاعدة البيانات المركزية التي تحتوي على معلومات الفنادق
    /// </summary>
    public class MasterDbContext : DbContext
    {
        /// <summary>
        /// Constructor for MasterDbContext
        /// </summary>
        /// <param name="options">DbContext options</param>
        public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// جدول الفنادق (Tenants)
        /// </summary>
        public DbSet<Tenant> Tenants { get; set; }

        /// <summary>
        /// جدول المستخدمين الرئيسيين (MasterUsers)
        /// </summary>
        public DbSet<MasterUser> MasterUsers { get; set; }

        /// <summary>
        /// جدول الأدوار (Roles)
        /// </summary>
        public DbSet<Role> Roles { get; set; }

        /// <summary>
        /// جدول ربط المستخدمين بالأدوار (UserRoles)
        /// </summary>
        public DbSet<UserRole> UserRoles { get; set; }

        /// <summary>
        /// جدول ربط المستخدمين بالفنادق (UserTenants) - للصلاحيات
        /// يحدد أي فنادق يمكن للمستخدم الوصول إليها
        /// </summary>
        public DbSet<UserTenant> UserTenants { get; set; }

        /// <summary>
        /// Central PMS RBAC users.
        /// </summary>
        public DbSet<MasterRbacUser> RbacUsers { get; set; }

        /// <summary>
        /// Central PMS RBAC roles.
        /// </summary>
        public DbSet<MasterRbacRole> RbacRoles { get; set; }

        /// <summary>
        /// Central PMS RBAC permissions.
        /// </summary>
        public DbSet<MasterRbacPermission> RbacPermissions { get; set; }

        /// <summary>
        /// Central PMS RBAC role-permission assignments.
        /// </summary>
        public DbSet<MasterRbacRolePermission> RbacRolePermissions { get; set; }

        /// <summary>
        /// Central PMS RBAC user-role assignments.
        /// </summary>
        public DbSet<MasterRbacUserRole> RbacUserRoles { get; set; }

        public DbSet<MasterRbacRoleGateStation> RbacRoleGateStations { get; set; }

        /// <summary>
        /// Hotels (tenants) assigned to a PMS user.
        /// </summary>
        public DbSet<PmsUserHotel> PmsUserHotels { get; set; }

        /// <summary>
        /// Active/revoked login sessions (refresh tokens).
        /// </summary>
        public DbSet<PmsUserSession> PmsUserSessions { get; set; }

        /// <summary>
        /// Security audit trail (login, logout, force logout, etc.).
        /// </summary>
        public DbSet<PmsSecurityAudit> PmsSecurityAudits { get; set; }

        /// <summary>
        /// Monthly hotel sales targets (Master DB).
        /// </summary>
        public DbSet<PmsHotelMonthlyTarget> HotelMonthlyTargets { get; set; }

        /// <summary>
        /// جدول فئات المصروفات (ExpenseCategories)
        /// </summary>
        public DbSet<MasterExpenseCategory> ExpenseCategories { get; set; }

        /// <summary>
        /// جدول رموز إعادة تعيين كلمة المرور (ResetPasswordTokens)
        /// </summary>
        public DbSet<ResetPasswordToken> ResetPasswordTokens { get; set; }

        /// <summary>
        /// جدول قواعد موافقة المصروفات (ExpenseApprovalRules)
        /// </summary>
        public DbSet<ExpenseApprovalRule> ExpenseApprovalRules { get; set; }

        /// <summary>
        /// Master supplier / corporate companies.
        /// </summary>
        public DbSet<MasterCompany> Companies { get; set; }

        // ================================================================
        // VoM Integration DbSets
        // ================================================================

        /// <summary>
        /// جدول دليل الحسابات من VoM (Chart of Accounts)
        /// </summary>
        public DbSet<ChartOfAccounts> ChartOfAccounts { get; set; }

        /// <summary>
        /// جدول مراكز التكلفة (Cost Centers) - مطابقة للفنادق
        /// </summary>
        public DbSet<CostCenter> CostCenters { get; set; }

        /// <summary>
        /// جدول الضرائب من VoM (VoM Taxes)
        /// </summary>
        public DbSet<zaaerIntegration.Models.VoM.Tax> VoMTaxes { get; set; }

        /// <summary>
        /// جدول أنواع الضرائب من VoM (VoM Tax Types)
        /// </summary>
        public DbSet<zaaerIntegration.Models.VoM.TaxType> VoMTaxTypes { get; set; }

        /// <summary>
        /// تكوين نموذج البيانات
        /// </summary>
        /// <param name="modelBuilder">Model builder</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // تكوين جدول Tenants
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(t => t.Id);
            entity.Property(t => t.Code).IsRequired().HasMaxLength(50);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(200);
            entity.Property(t => t.NameEn).HasColumnName("name_en").HasMaxLength(200);
            entity.Property(t => t.ConnectionString).HasMaxLength(500); // Optional - system uses DatabaseName instead
            entity.Property(t => t.DatabaseName).IsRequired().HasMaxLength(100);
                entity.Property(t => t.BaseUrl).HasMaxLength(200);
                entity.Property(t => t.EnableQueueMode).HasColumnName("EnableQueueMode");
                entity.Property(t => t.EnableQueueWorker).HasColumnName("EnableQueueWorker");
                entity.Property(t => t.QueueWorkerIntervalSeconds).HasColumnName("QueueWorkerIntervalSeconds");
                entity.Property(t => t.QueueWorkerBatchSize).HasColumnName("QueueWorkerBatchSize");
                entity.Property(t => t.UseQueueMiddleware).HasColumnName("UseQueueMiddleware");
                entity.Property(t => t.DefaultPartner).HasColumnName("DefaultPartner").HasMaxLength(100);

                // إنشاء Index على Code للبحث السريع
                entity.HasIndex(t => t.Code).IsUnique();
            });

            // تكوين جدول MasterUsers
            modelBuilder.Entity<MasterUser>(entity =>
            {
                entity.ToTable("MasterUsers");
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
                entity.Property(u => u.TenantId).IsRequired();
                entity.Property(u => u.IsActive).IsRequired();
                entity.Property(u => u.CreatedAt).IsRequired();
                entity.Property(u => u.UpdatedAt);
                entity.Property(u => u.PhoneNumber).HasMaxLength(50);
                entity.Property(u => u.Email).HasMaxLength(200);
                entity.Property(u => u.EmployeeNumber).HasMaxLength(50);
                entity.Property(u => u.FullName).HasMaxLength(100);

                // FirstName and LastName are not stored in database - they're in-memory properties only
                entity.Ignore(u => u.FirstName);
                entity.Ignore(u => u.LastName);

                // Configure RowVersion for Optimistic Concurrency Control
                entity.Property(u => u.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();

                // Foreign Key إلى Tenants
                entity.HasOne(u => u.Tenant)
                    .WithMany()
                    .HasForeignKey(u => u.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Index على Username للبحث السريع
                entity.HasIndex(u => u.Username).IsUnique();
            });

            // Legacy roles (expense / myMainProject MasterUsers).
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
                entity.Property(r => r.Code).IsRequired().HasMaxLength(50);

                // Index على Code للبحث السريع
                entity.HasIndex(r => r.Code).IsUnique();
            });

            // Legacy user-role links (myMainProject). Separate from PMS dbo.pms_user_roles.
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("UserRoles");
                entity.HasKey(ur => ur.Id);
                entity.Property(ur => ur.UserId).IsRequired();
                entity.Property(ur => ur.RoleId).IsRequired();

                // Foreign Keys
                entity.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unique constraint لمنع تكرار نفس الدور لنفس المستخدم
                entity.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
            });

            // تكوين جدول UserTenants
            modelBuilder.Entity<UserTenant>(entity =>
            {
                entity.ToTable("UserTenants");
                entity.HasKey(ut => ut.Id);
                entity.Property(ut => ut.UserId).IsRequired();
                entity.Property(ut => ut.TenantId).IsRequired();
                entity.Property(ut => ut.CreatedAt).IsRequired();

                // Foreign Keys
                entity.HasOne(ut => ut.User)
                    .WithMany()
                    .HasForeignKey(ut => ut.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ut => ut.Tenant)
                    .WithMany()
                    .HasForeignKey(ut => ut.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unique constraint لمنع تكرار نفس الفندق لنفس المستخدم
                entity.HasIndex(ut => new { ut.UserId, ut.TenantId }).IsUnique();
            });

            ConfigureRbacModel(modelBuilder);
            ConfigureHotelMonthlyTargetsModel(modelBuilder);

            // تكوين جدول ExpenseCategories
            modelBuilder.Entity<MasterExpenseCategory>(entity =>
            {
                entity.ToTable("ExpenseCategories");
                entity.HasKey(ec => ec.Id);
                entity.Property(ec => ec.MainCategory).IsRequired().HasMaxLength(200);
                entity.Property(ec => ec.Details).HasMaxLength(1000);
                entity.Property(ec => ec.IsActive).IsRequired();
                entity.Property(ec => ec.CreatedAt).IsRequired();
                entity.Property(ec => ec.AccountId).HasColumnName("account_id");

                // Index على MainCategory للبحث السريع
                entity.HasIndex(ec => ec.MainCategory);
                
                // Index على IsActive
                entity.HasIndex(ec => ec.IsActive);

                // Index على AccountId للبحث السريع
                entity.HasIndex(ec => ec.AccountId)
                    .HasDatabaseName("IX_ExpenseCategories_AccountId");
            });

            modelBuilder.Entity<MasterCompany>(entity =>
            {
                entity.ToTable("Companies");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.TaxNumber).HasColumnName("TaxNumber").HasMaxLength(50);
                entity.Property(c => c.CompanyName).HasColumnName("CompanyName").IsRequired().HasMaxLength(300);
                entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt");
                entity.Property(c => c.UpdatedAt).HasColumnName("UpdatedAt");
                entity.Property(c => c.CountryCode).HasColumnName("CountryCode").HasMaxLength(20);
                entity.Property(c => c.Mobile).HasColumnName("Mobile").HasMaxLength(50);
                entity.Property(c => c.Email).HasColumnName("Email").HasMaxLength(200);
                entity.Property(c => c.Street).HasColumnName("Street").HasMaxLength(300);
                entity.Property(c => c.City).HasColumnName("City").HasMaxLength(100);
                entity.Property(c => c.Country).HasColumnName("Country").HasMaxLength(100);
                entity.Property(c => c.PostalCode).HasColumnName("PostalCode").HasMaxLength(30);
                entity.HasIndex(c => c.TaxNumber);
            });

            // تكوين جدول ResetPasswordTokens
            modelBuilder.Entity<ResetPasswordToken>(entity =>
            {
                entity.ToTable("ResetPasswordTokens");
                entity.HasKey(rpt => rpt.Id);
                entity.Property(rpt => rpt.UserId).IsRequired();
                entity.Property(rpt => rpt.Token).IsRequired().HasMaxLength(256);
                entity.Property(rpt => rpt.ExpiresAt).IsRequired();
                entity.Property(rpt => rpt.IsUsed).IsRequired();
                entity.Property(rpt => rpt.CreatedAt).IsRequired();
                entity.Property(rpt => rpt.RequestIpAddress).HasMaxLength(50);

                // Foreign Key إلى MasterUsers
                entity.HasOne(rpt => rpt.User)
                    .WithMany()
                    .HasForeignKey(rpt => rpt.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Index على Token للبحث السريع
                entity.HasIndex(rpt => rpt.Token).IsUnique();
                
                // Index على UserId و IsUsed للبحث السريع
                entity.HasIndex(rpt => new { rpt.UserId, rpt.IsUsed });
                
                // Index على ExpiresAt لتنظيف الرموز المنتهية
                entity.HasIndex(rpt => rpt.ExpiresAt);
            });

            // تكوين جدول ExpenseApprovalRules
            modelBuilder.Entity<ExpenseApprovalRule>(entity =>
            {
                entity.ToTable("ExpenseApprovalRules");
                entity.HasKey(r => r.RuleId);
                entity.Property(r => r.RoleCode).IsRequired().HasMaxLength(50);
                entity.Property(r => r.FromStatus).IsRequired().HasMaxLength(50);
                entity.Property(r => r.MinAmount).HasColumnType("decimal(12,2)");
                entity.Property(r => r.MaxAmount).HasColumnType("decimal(12,2)");
                entity.Property(r => r.AmountComparisonOperator).HasMaxLength(10);
                entity.Property(r => r.ExpenseCategoryCondition).HasMaxLength(20);
                entity.Property(r => r.NextStatus).IsRequired().HasMaxLength(50);
                entity.Property(r => r.Priority).IsRequired();
                entity.Property(r => r.IsActive).IsRequired();
                entity.Property(r => r.Description).HasMaxLength(500);
                entity.Property(r => r.CreatedAt).IsRequired();

                // Index على RoleCode و FromStatus و IsActive و Priority للبحث السريع
                entity.HasIndex(r => new { r.RoleCode, r.FromStatus, r.IsActive, r.Priority });

                // Index على ExpenseCategoryId للبحث السريع (عندما يكون غير NULL)
                entity.HasIndex(r => r.ExpenseCategoryId)
                    .HasFilter("[ExpenseCategoryId] IS NOT NULL");
            });

            // ================================================================
            // VoM Integration Table Configurations
            // ================================================================

            // تكوين جدول ChartOfAccounts
            modelBuilder.Entity<ChartOfAccounts>(entity =>
            {
                entity.ToTable("ChartOfAccounts");
                entity.HasKey(c => c.Id);
                
                // تكوين أسماء الأعمدة بشكل صريح
                entity.Property(c => c.Id).HasColumnName("id");
                entity.Property(c => c.NameAr).HasColumnName("name_ar").HasMaxLength(255);
                entity.Property(c => c.NameEn).HasColumnName("name_en").HasMaxLength(255);
                entity.Property(c => c.Code).HasColumnName("code").HasMaxLength(50);
                entity.Property(c => c.AccountType).HasColumnName("account_type").HasMaxLength(50);
                entity.Property(c => c.ParentId).HasColumnName("parent_id");
                entity.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
                entity.Property(c => c.CreatedAt).HasColumnName("created_at");
                entity.Property(c => c.UpdatedAt).HasColumnName("updated_at");
                entity.Property(c => c.DeletedAt).HasColumnName("deleted_at");
                entity.Property(c => c.Level).HasColumnName("level");

                // Index على Code للبحث السريع
                entity.HasIndex(c => c.Code);

                // Index على IsActive
                entity.HasIndex(c => c.IsActive);

                // Index على ParentId للتسلسل الهرمي
                entity.HasIndex(c => c.ParentId);
            });

            // تكوين جدول CostCenters
            modelBuilder.Entity<CostCenter>(entity =>
            {
                entity.ToTable("CostCenters");
                entity.HasKey(c => c.Id);
                
                // تكوين أسماء الأعمدة بشكل صريح - فقط الأعمدة الموجودة في قاعدة البيانات
                entity.Property(c => c.Id).HasColumnName("id");
                entity.Property(c => c.HotelId).HasColumnName("hotel_id").IsRequired();
                entity.Property(c => c.NameAr).HasColumnName("name_ar").IsRequired().HasMaxLength(255);
                entity.Property(c => c.MainCenterId).HasColumnName("main_center_id");
                entity.Property(c => c.AccountId).HasColumnName("AccountId"); // PascalCase في قاعدة البيانات
                entity.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
                entity.Property(c => c.CreatedAt).HasColumnName("created_at");
                
                // الأعمدة التالية غير موجودة في قاعدة البيانات - لا يتم تكوينها
                // NameEn, VoMCostCenterId, UpdatedAt, Notes

                // Index على HotelId للبحث السريع
                entity.HasIndex(c => c.HotelId);

                // Index على IsActive
                entity.HasIndex(c => c.IsActive);

                // Unique constraint على HotelId للفنادق النشطة (فندق واحد = مركز تكلفة واحد نشط)
                entity.HasIndex(c => new { c.HotelId, c.IsActive })
                    .HasFilter("[is_active] = 1")
                    .IsUnique();
            });

            // تكوين جدول VoM Taxes
            modelBuilder.Entity<zaaerIntegration.Models.VoM.Tax>(entity =>
            {
                entity.ToTable("TaxesChart");
                entity.HasKey(t => t.Id);

                // تكوين أسماء الأعمدة بشكل صريح
                entity.Property(t => t.Id).HasColumnName("id");
                entity.Property(t => t.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
                entity.Property(t => t.NameEn).HasColumnName("name_en").HasMaxLength(200).IsRequired();
                entity.Property(t => t.Percentage).HasColumnName("percentage").HasColumnType("decimal(5,2)").IsRequired();
                entity.Property(t => t.IsDefault).HasColumnName("is_default");
                entity.Property(t => t.AccountId).HasColumnName("account_id").IsRequired();
                entity.Property(t => t.TypeId).HasColumnName("type_id").IsRequired();
                entity.Property(t => t.ZatcaTaxType).HasColumnName("zatca_tax_type").HasMaxLength(50);
                entity.Property(t => t.ZatcaExemptionReason).HasColumnName("zatca_exemption_reason").HasMaxLength(200);
                entity.Property(t => t.Description).HasColumnName("description");
                entity.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
                entity.Property(t => t.CreatedAt).HasColumnName("created_at");
                entity.Property(t => t.UpdatedAt).HasColumnName("updated_at");
                entity.Property(t => t.DeletedAt).HasColumnName("deleted_at");

                // Index على AccountId للبحث السريع
                entity.HasIndex(t => t.AccountId);

                // Index على TypeId للبحث السريع
                entity.HasIndex(t => t.TypeId);

                // Index على IsActive
                entity.HasIndex(t => t.IsActive);
            });

            // تكوين جدول VoM TaxTypes
            modelBuilder.Entity<zaaerIntegration.Models.VoM.TaxType>(entity =>
            {
                entity.ToTable("TaxTypes");
                entity.HasKey(tt => tt.Id);

                // تكوين أسماء الأعمدة بشكل صريح
                entity.Property(tt => tt.Id).HasColumnName("id");
                entity.Property(tt => tt.NameAr).HasColumnName("name_ar").HasMaxLength(100).IsRequired();
                entity.Property(tt => tt.NameEn).HasColumnName("name_en").HasMaxLength(100).IsRequired();
                entity.Property(tt => tt.CreatedAt).HasColumnName("created_at");
                entity.Property(tt => tt.UpdatedAt).HasColumnName("updated_at");
            });

            // تكوين العلاقات بين VoM Tax و TaxType و ChartOfAccounts
            modelBuilder.Entity<zaaerIntegration.Models.VoM.Tax>()
                .HasOne(t => t.Account)
                .WithMany() // ChartOfAccounts doesn't have navigation to Taxes
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<zaaerIntegration.Models.VoM.Tax>()
                .HasOne(t => t.TaxType)
                .WithMany(tt => tt.Taxes)
                .HasForeignKey(t => t.TypeId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        private static void ConfigureRbacModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MasterRbacUser>(entity =>
            {
                entity.ToTable("pms_users");
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.Username);
                entity.HasIndex(e => e.EmployeeNumber);
                entity.HasIndex(e => e.ZaaerId);
                entity.HasIndex(e => e.MasterUserId);
                entity.HasOne(e => e.MasterUser)
                    .WithMany()
                    .HasForeignKey(e => e.MasterUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<MasterRbacRole>(entity =>
            {
                entity.ToTable("pms_roles");
                entity.HasIndex(e => e.RoleCode);
            });

            modelBuilder.Entity<MasterRbacPermission>(entity =>
            {
                entity.ToTable("pms_permissions");
                entity.HasIndex(e => e.PermissionCode).IsUnique();
                entity.HasIndex(e => new { e.ModuleName, e.SubmoduleName, e.SortOrder });
            });

            modelBuilder.Entity<MasterRbacRolePermission>(entity =>
            {
                entity.ToTable("pms_role_permissions");
                entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
                entity.HasOne(e => e.Role)
                    .WithMany()
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Permission)
                    .WithMany()
                    .HasForeignKey(e => e.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PmsUserHotel>(entity =>
            {
                entity.ToTable("pms_user_hotels");
                entity.HasIndex(e => new { e.UserId, e.TenantId }).IsUnique();
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Tenant)
                    .WithMany()
                    .HasForeignKey(e => e.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MasterRbacUserRole>(entity =>
            {
                entity.ToTable("pms_user_roles");
                entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Role)
                    .WithMany()
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PmsUserSession>(entity =>
            {
                entity.ToTable("pms_user_sessions");
                entity.HasIndex(e => new { e.UserId, e.RevokedAt, e.ExpiresAt });
                entity.HasIndex(e => e.RefreshTokenHash).IsUnique();
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PmsSecurityAudit>(entity =>
            {
                entity.ToTable("pms_security_audit");
                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            });
        }

        private static void ConfigureHotelMonthlyTargetsModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PmsHotelMonthlyTarget>(entity =>
            {
                entity.ToTable("pms_hotel_monthly_targets");
                entity.HasIndex(e => new { e.HotelZaaerId, e.MonthYear }).IsUnique();
                entity.HasIndex(e => e.MonthYear);
            });
        }
    }
}

