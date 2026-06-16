using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Display;
using zaaerIntegration.Configuration;
using zaaerIntegration.Data;
using zaaerIntegration.Repositories.Implementations;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Implementations;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Services.Zaaer;
using zaaerIntegration.Services.Expense;
using zaaerIntegration.Services.VoM;
using zaaerIntegration.Services.Auth;
using zaaerIntegration.Middleware;
using zaaerIntegration.Services.PartnerQueueing;
using zaaerIntegration.Services.PartnerQueueing.Handlers;
using zaaerIntegration.Utilities;
using zaaerIntegration.Reporting.Extensions;
using DevExpress.AspNetCore;
using DevExpress.AspNetCore.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Dapper;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog with category-based file separation
// Production mode: Warning level minimum (Error, Critical, Warning only)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .MinimumLevel.Warning() // ✅ Production: Only Warning, Error, Critical
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
    // Main log file (all warnings, errors, critical)
    // Use KsaTime for file rotation to ensure logs are created based on Saudi Arabia timezone
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Sink(new KsaTimeFileSink(
        pathTemplate: Path.Combine("logs", "log-{Date}.txt"),
        formatter: new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null),
        retainedFileCountLimit: 30
    ), restrictedToMinimumLevel: LogEventLevel.Warning)
    // ✅ errors.log - All errors and critical
    // Use KsaTime for file rotation
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt => evt.Level >= LogEventLevel.Error)
        .WriteTo.Sink(new KsaTimeFileSink(
            pathTemplate: Path.Combine("logs", "errors", "errors-{Date}.txt"),
            formatter: new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null),
            retainedFileCountLimit: 30
        ))
    )
    // ✅ security.log - Security-related warnings and errors
    // Use KsaTime for file rotation
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt => 
            (evt.Level >= LogEventLevel.Warning) && 
            (evt.MessageTemplate.Text.Contains("[SECURITY]") || evt.MessageTemplate.Text.Contains("[AUTH]")))
        .WriteTo.Sink(new KsaTimeFileSink(
            pathTemplate: Path.Combine("logs", "security", "security-{Date}.txt"),
            formatter: new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null),
            retainedFileCountLimit: 90 // Keep security logs longer (90 days)
        ))
    )
    // ✅ sync.log - External system sync (VoM, Zaaer)
    // Use KsaTime for file rotation
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt => 
            (evt.Level >= LogEventLevel.Error) && 
            (evt.MessageTemplate.Text.Contains("[SYNC]") || 
             evt.MessageTemplate.Text.Contains("[VoM]") ||
             evt.MessageTemplate.Text.Contains("[Zaaer]")))
        .WriteTo.Sink(new KsaTimeFileSink(
            pathTemplate: Path.Combine("logs", "sync", "sync-{Date}.txt"),
            formatter: new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null),
            retainedFileCountLimit: 30
        ))
    )
    // ✅ database.log - Database operations and failures
    // Use KsaTime for file rotation
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(evt => 
            (evt.Level >= LogEventLevel.Error) && 
            (evt.MessageTemplate.Text.Contains("[DB]") || 
             evt.MessageTemplate.Text.Contains("[DATABASE]") ||
             evt.Properties.ContainsKey("SourceContext") && 
             evt.Properties["SourceContext"].ToString().Contains("EntityFrameworkCore")))
        .WriteTo.Sink(new KsaTimeFileSink(
            pathTemplate: Path.Combine("logs", "database", "database-{Date}.txt"),
            formatter: new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null),
            retainedFileCountLimit: 30
        ))
    )
    // Performance monitoring log file - separate file for performance metrics
    // ملف منفصل لمراقبة الأداء - يتنشأ كل يوم باسم جديد
    // Use KsaTime for file rotation
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(logEvent => logEvent.MessageTemplate.Text.Contains("[PERFORMANCE]"))
        .WriteTo.Sink(new KsaTimeFileSink(
            pathTemplate: Path.Combine("logs", "performance", "performance-{Date}.txt"),
            formatter: new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}", null),
            fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB per file
            retainedFileCountLimit: 30 // Keep last 30 days
        ))
    )
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Add custom converters to handle nullable int values
        options.JsonSerializerOptions.Converters.Add(new zaaerIntegration.Converters.NullableIntJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new zaaerIntegration.Converters.NullableIntConverter());
    });

// ? ===== Multi-Tenant Configuration =====
// Configure Master Database Context (قاعدة البيانات المركزية)
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterDb")));

// HttpContextAccessor - للوصول إلى HTTP Request
builder.Services.AddHttpContextAccessor();

// Tenant Services - للحصول على معلومات الفندق الحالي
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IQueueSettingsProvider, QueueSettingsProvider>();

// Tenant DB Context Resolver - لإنشاء DbContext ديناميكي للـ Tenant
builder.Services.AddScoped<TenantDbContextResolver>();

// Configure Dynamic ApplicationDbContext for each Tenant
// ? لا يوجد هنا! سيُنشأ DbContext لكل request ديناميكياً
builder.Services.AddScoped<ApplicationDbContext>(sp =>
{
    var resolver = sp.GetRequiredService<TenantDbContextResolver>();
    return resolver.GetCurrentDbContext();
});

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add Memory Cache
builder.Services.AddMemoryCache();

// JWT authentication - required by Hybrid RBAC.
var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ZaaerIntegration";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ZaaerIntegration";
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<JwtSessionValidationService>();
                try
                {
                    if (context.Principal != null)
                    {
                        await validator.ValidatePrincipalAsync(context.Principal);
                    }
                }
                catch (SecurityTokenValidationException ex)
                {
                    context.Fail(ex.Message);
                }
            },
            OnChallenge = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Error))
                {
                    context.Error = "SESSION_REVOKED";
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add controllers
builder.Services.AddControllers();

// Configure DevExpress Reporting
builder.Services.AddDevExpressControls();
builder.Services.ConfigureReportingServices(configurator => {
    if(builder.Environment.IsDevelopment()) {
        configurator.UseDevelopmentMode();
    }
    configurator.ConfigureWebDocumentViewer(viewerConfigurator => {
        viewerConfigurator.UseCachedReportSourceBuilder();
    });
});

// PMS enterprise reporting (DTO + provider + PDF export)
builder.Services.AddPmsReporting();

// Register custom report storage
builder.Services.AddScoped<DevExpress.XtraReports.Web.Extensions.ReportStorageWebExtension, zaaerIntegration.Services.CustomReportStorageWebExtension>();

// Register repositories
builder.Services.AddScoped<IGenericRepository<FinanceLedgerAPI.Models.Customer>, GenericRepository<FinanceLedgerAPI.Models.Customer>>();
builder.Services.AddScoped<IGenericRepository<FinanceLedgerAPI.Models.Floor>, GenericRepository<FinanceLedgerAPI.Models.Floor>>();
builder.Services.AddScoped<IGenericRepository<FinanceLedgerAPI.Models.Apartment>, GenericRepository<FinanceLedgerAPI.Models.Apartment>>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IInvoiceRepository, InvoiceRepository>();
builder.Services.AddScoped<IPaymentReceiptRepository, PaymentReceiptRepository>();
builder.Services.AddScoped<ICorporateCustomerRepository, CorporateCustomerRepository>();
builder.Services.AddScoped<IReservationUnitRepository, ReservationUnitRepository>();
builder.Services.AddScoped<IApartmentRepository, ApartmentRepository>();
builder.Services.AddScoped<IBuildingRepository, BuildingRepository>();
builder.Services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
builder.Services.AddScoped<IRefundRepository, RefundRepository>();
builder.Services.AddScoped<ICreditNoteRepository, CreditNoteRepository>();
builder.Services.AddScoped<ICustomerIdentificationRepository, CustomerIdentificationRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Register services
builder.Services.AddScoped<INumberingService, NumberingService>();
builder.Services.AddScoped<INumberingAuditReconciliationService, NumberingAuditReconciliationService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IPaymentReceiptService, PaymentReceiptService>();
builder.Services.AddScoped<IPaymentAllocationService, PaymentAllocationService>();
builder.Services.AddScoped<ICustomerLedgerService, CustomerLedgerService>();
builder.Services.AddScoped<IPerformanceLogger, PerformanceLogger>();
builder.Services.AddScoped<ICorporateCustomerService, CorporateCustomerService>();
builder.Services.AddScoped<IReservationUnitService, ReservationUnitService>();
builder.Services.AddScoped<IApartmentService, ApartmentService>();
builder.Services.AddScoped<IBuildingService, BuildingService>();
builder.Services.AddScoped<IRoomTypeService, RoomTypeService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<ICreditNoteService, CreditNoteService>();
builder.Services.AddScoped<IRoomBoardService, RoomBoardService>();
builder.Services.AddScoped<IReservationPeriodService, ReservationPeriodService>();
builder.Services.AddScoped<IReservationDetailService, ReservationDetailService>();
builder.Services.AddScoped<zaaerIntegration.Services.BookingEngine.BookingEngineDbFactory>();
builder.Services.AddScoped<IBookingEngineService, BookingEngineService>();
builder.Services.AddScoped<zaaerIntegration.Security.ReservationPermissionGuard>();
builder.Services.AddScoped<IReservationNotesService, ReservationNotesService>();
builder.Services.AddScoped<IPmsCustomerService, PmsCustomerService>();
builder.Services.AddScoped<IPmsCorporateCustomerService, PmsCorporateCustomerService>();
builder.Services.AddScoped<IReservationFinancialSyncService, ReservationFinancialSyncService>();
builder.Services.AddScoped<IPmsPaymentReceiptService, PmsPaymentReceiptService>();
builder.Services.AddScoped<IPmsPromissoryNoteService, PmsPromissoryNoteService>();
builder.Services.AddScoped<IPmsInvoiceService, PmsInvoiceService>();
builder.Services.AddScoped<IPmsCreditNoteService, PmsCreditNoteService>();
builder.Services.AddScoped<IPmsDebitNoteService, PmsDebitNoteService>();
builder.Services.AddScoped<IPmsExpenseService, PmsExpenseService>();
builder.Services.AddScoped<IDepositImageService, DepositImageService>();
builder.Services.AddScoped<IPmsDepositService, PmsDepositService>();
builder.Services.AddScoped<IPmsCashLedgerService, PmsCashLedgerService>();
builder.Services.Configure<zaaerIntegration.Configuration.IntegrationSecretsOptions>(
    builder.Configuration.GetSection(zaaerIntegration.Configuration.IntegrationSecretsOptions.SectionName));
builder.Services.Configure<zaaerIntegration.Configuration.PaymentDailyNetExTaxOptions>(
    builder.Configuration.GetSection(zaaerIntegration.Configuration.PaymentDailyNetExTaxOptions.SectionName));
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("zaaerIntegration");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysDirectory"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    var keysDir = DataProtectionKeysDirectoryResolver.Resolve(
        builder.Environment.ContentRootPath,
        dataProtectionKeysPath);
    Directory.CreateDirectory(keysDir);
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keysDir));

    var keyCount = DataProtectionKeysDirectoryResolver.CountKeyFiles(keysDir);
    Console.WriteLine(
        $"[Startup] Data Protection keys directory: {keysDir} ({keyCount} key file(s)).");
}
builder.Services.AddHttpClient("NtmpGateway", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IIntegrationSecretProtector, zaaerIntegration.Services.Integrations.IntegrationSecretProtector>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.INtmpPasswordResolver, zaaerIntegration.Services.Integrations.NtmpPasswordResolver>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.INtmpIntegrationSchemaEnsurer, zaaerIntegration.Services.Integrations.NtmpIntegrationSchemaEnsurer>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.NtmpLookupMapper>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.INtmpBookingPayloadBuilder, zaaerIntegration.Services.Integrations.NtmpBookingPayloadBuilder>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.INtmpGatewayClient, zaaerIntegration.Services.Integrations.NtmpGatewayClient>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.INtmpIntegrationOrchestrator, zaaerIntegration.Services.Integrations.NtmpIntegrationOrchestrator>();

// ZATCA e-invoicing
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.Zatca.IZatcaIntegrationSchemaEnsurer, zaaerIntegration.Services.Integrations.Zatca.ZatcaIntegrationSchemaEnsurer>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.Zatca.IZatcaProfileResolver, zaaerIntegration.Services.Integrations.Zatca.ZatcaProfileResolver>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.Zatca.IZatcaUblBuilder, zaaerIntegration.Services.Integrations.Zatca.ZatcaUblBuilder>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.Zatca.IZatcaAcceptLanguageResolver, zaaerIntegration.Services.Integrations.Zatca.ZatcaAcceptLanguageResolver>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.Zatca.IZatcaSubmissionOrchestrator, zaaerIntegration.Services.Integrations.Zatca.ZatcaSubmissionOrchestrator>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.Zatca.IZatcaComplianceService, zaaerIntegration.Services.Integrations.Zatca.ZatcaComplianceService>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.Zatca.IZatcaGatewayClient, zaaerIntegration.Services.Integrations.Zatca.ZatcaGatewayClient>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IPmsNtmpSettingsService, zaaerIntegration.Services.Integrations.PmsNtmpSettingsService>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IPmsShomoosSettingsService, zaaerIntegration.Services.Integrations.PmsShomoosSettingsService>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IPmsZatcaSettingsService, zaaerIntegration.Services.Integrations.PmsZatcaSettingsService>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IPmsZatcaOnboardingService, zaaerIntegration.Services.Integrations.PmsZatcaOnboardingService>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IPmsZatcaComplianceService, zaaerIntegration.Services.Integrations.PmsZatcaComplianceService>();
builder.Services.Configure<zaaerIntegration.Configuration.ZatcaOptions>(
    builder.Configuration.GetSection(zaaerIntegration.Configuration.ZatcaOptions.SectionName));
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IPmsIntegrationResponsesService, zaaerIntegration.Services.Integrations.PmsIntegrationResponsesService>();
builder.Services.AddScoped<zaaerIntegration.Services.Integrations.IPmsBaladyReportService, zaaerIntegration.Services.Integrations.PmsBaladyReportService>();
builder.Services.AddScoped<IPmsPropertyService, PmsPropertyService>();
builder.Services.AddScoped<IPmsRoomTypeRatesService, PmsRoomTypeRatesService>();
builder.Services.AddSingleton<zaaerIntegration.Utilities.ResortTicketQrSecurity>();
builder.Services.AddScoped<IPmsResortTicketService, PmsResortTicketService>();
builder.Services.AddScoped<IPmsHallEventService, PmsHallEventService>();
builder.Services.AddScoped<IPmsHallReportService, PmsHallReportService>();
builder.Services.AddScoped<IPmsHotelReportService, PmsHotelReportService>();
builder.Services.AddScoped<IPmsHotelTargetService, PmsHotelTargetService>();
builder.Services.AddScoped<IHallNotificationService, HallNotificationService>();
builder.Services.AddScoped<IResortTicketGateLandingService, ResortTicketGateLandingService>();
builder.Services.AddScoped<IPmsOutletCatalogService, PmsOutletCatalogService>();
builder.Services.AddScoped<IPmsPosOrderService, PmsPosOrderService>();

// Register Zaaer services
builder.Services.AddScoped<IZaaerCustomerService, ZaaerCustomerService>();
builder.Services.AddScoped<IZaaerReservationService, ZaaerReservationService>();
builder.Services.AddScoped<IZaaerPaymentReceiptService, ZaaerPaymentReceiptService>();
builder.Services.AddScoped<IZaaerInvoiceService, ZaaerInvoiceService>();
builder.Services.AddScoped<IZaaerRefundService, ZaaerRefundService>();
builder.Services.AddScoped<IZaaerCreditNoteService, ZaaerCreditNoteService>();
builder.Services.AddScoped<IZaaerOrderService, ZaaerOrderService>();
builder.Services.AddScoped<IZaaerRoomTypeService, ZaaerRoomTypeService>();

builder.Services.AddScoped<IZaaerFloorService, ZaaerFloorService>();
builder.Services.AddScoped<IZaaerApartmentService, ZaaerApartmentService>();
builder.Services.AddScoped<IZaaerMaintenanceService, ZaaerMaintenanceService>();
builder.Services.AddScoped<IZaaerTaxService, ZaaerTaxService>();
builder.Services.AddScoped<IZaaerHotelSettingsService, ZaaerHotelSettingsService>();
builder.Services.AddScoped<IZaaerBuildingService, ZaaerBuildingService>();
builder.Services.AddScoped<IZaaerUserService, ZaaerUserService>();
builder.Services.AddScoped<IZaaerRoleService, ZaaerRoleService>();
builder.Services.AddScoped<IZaaerSeasonalRateService, ZaaerSeasonalRateService>();
builder.Services.AddScoped<IZaaerRateTypeService, ZaaerRateTypeService>();
builder.Services.AddScoped<IReservationRatesService, ReservationRatesService>();
builder.Services.AddScoped<IReservationUnitSwitchService, ReservationUnitSwitchService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IReservationActivityLogWriter, ReservationActivityLogWriter>();
builder.Services.AddScoped<IReservationActivityLogQueryService, ReservationActivityLogQueryService>();
builder.Services.AddScoped<IZaaerZatcaDetailsService, ZaaerZatcaDetailsService>();
builder.Services.AddScoped<IZaaerNtmpDetailsService, ZaaerNtmpDetailsService>();
builder.Services.AddScoped<IZaaerShomoosDetailsService, ZaaerShomoosDetailsService>();
builder.Services.AddScoped<IZaaerIntegrationResponseService, ZaaerIntegrationResponseService>();
builder.Services.AddScoped<IZaaerRoomTypeRateService, ZaaerRoomTypeRateService>();
builder.Services.AddScoped<IZaaerBankService, ZaaerBankService>();
builder.Services.AddScoped<IZaaerExpenseService, ZaaerExpenseService>();

// Register Expense Dapper Service (optimized for heavy queries)
builder.Services.AddScoped<ExpenseDapperService>();

// Register VoM Expenses Dapper Service (optimized for VoM queries)
builder.Services.AddScoped<zaaerIntegration.Services.VoM.VoMExpensesDapperService>();

// Register Expense Service (new CRUD operations with X-Hotel-Code header)
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IExpenseImageService, ExpenseImageService>();

// Register Expense Approval Rule Service (for database-driven approval rules)
builder.Services.AddScoped<IExpenseApprovalRuleService, ExpenseApprovalRuleService>();

// Register Auth Services (for user authentication)
builder.Services.AddScoped<IPasswordHashingService, zaaerIntegration.Services.Implementations.PasswordHashingService>();
builder.Services.AddScoped<IMasterUserService, zaaerIntegration.Services.Implementations.MasterUserService>();
builder.Services.AddScoped<IRbacUserService, zaaerIntegration.Services.Implementations.RbacUserService>();
builder.Services.AddScoped<IJwtService, zaaerIntegration.Services.Implementations.JwtService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<JwtSessionValidationService>();
builder.Services.AddScoped<IEmailService, zaaerIntegration.Services.Implementations.EmailService>();
builder.Services.AddScoped<IWhatsAppService, zaaerIntegration.Services.Implementations.WhatsAppService>();
builder.Services.AddScoped<ICurrentUserContext, zaaerIntegration.Services.Implementations.CurrentUserContext>();
builder.Services.AddScoped<IAuthModeResolver, zaaerIntegration.Services.Implementations.AuthModeResolver>();
builder.Services.AddScoped<IHotelAccessService, zaaerIntegration.Services.Implementations.HotelAccessService>();
builder.Services.AddScoped<IPermissionService, zaaerIntegration.Services.Implementations.PermissionService>();
builder.Services.AddScoped<IRbacSyncService, zaaerIntegration.Services.Implementations.RbacSyncService>();

// Register Invoice Journal Entry Service (sends journal entries to VoM after invoice creation)
builder.Services.AddScoped<IInvoiceJournalEntryService, InvoiceJournalEntryService>();
builder.Services.AddScoped<IPaymentReceiptJournalEntryService, PaymentReceiptJournalEntryService>();

// Register Credit Note Journal Entry Service (sends reverse journal entries to VoM after credit note creation)
builder.Services.AddScoped<ICreditNoteJournalEntryService, CreditNoteJournalEntryService>();

// Register Expense Journal Entry Service (sends journal entries to VoM after expense creation)
builder.Services.AddScoped<zaaerIntegration.Services.IExpenseJournalEntryService, zaaerIntegration.Services.ExpenseJournalEntryService>();

// Register VoM Logger (dedicated logging for VoM operations)
builder.Services.AddScoped<IVoMLogger, VoMLogger>();

// Register Smart Logger (duplicate error detection + context builder)
// Singleton to maintain error occurrence cache across requests
builder.Services.AddSingleton<SmartLogger>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("SmartLogger");
    return new SmartLogger(logger);
});

// Register VoM Services
builder.Services.AddHttpClient<IVoMAuthService, VoMAuthService>(client =>
{
    var baseUrl = builder.Configuration["VoM:BaseUrl"] ?? "https://kimoo.getvom.com";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

builder.Services.AddHttpClient<IVoMAccountService, VoMAccountService>(client =>
{
    var baseUrl = builder.Configuration["VoM:BaseUrl"] ?? "https://kimoo.getvom.com";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

builder.Services.AddHttpClient<IVoMSettingsService, VoMSettingsService>(client =>
{
    var baseUrl = builder.Configuration["VoM:BaseUrl"] ?? "https://kimoo.getvom.com";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

builder.Services.AddHttpClient<IVoMJournalEntryService, VoMJournalEntryService>(client =>
{
    var baseUrl = builder.Configuration["VoM:BaseUrl"] ?? "https://kimoo.getvom.com";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

builder.Services.AddHttpClient<IVoMInvoiceReturnService, VoMInvoiceReturnService>(client =>
{
    var baseUrl = builder.Configuration["VoM:BaseUrl"] ?? "https://kimoo.getvom.com";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

builder.Services.AddScoped<ICreditNoteInvoiceReturnService, CreditNoteInvoiceReturnService>();

// Partner Queue services
builder.Services.AddScoped<IPartnerQueueService, PartnerQueueService>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerReservationCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerReservationUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerReservationUpdateByNumberHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerCustomerUpdateByNumberHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerActivityLogCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerReservationUnitSwitchCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerReservationRatesApplyAllHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerReservationRatesUpsertHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRoomTypeRateCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRoomTypeRateUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRoomTypeRateUpdateByZaaerIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerCustomerCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerCustomerUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerInvoiceCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerInvoiceUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerPaymentReceiptCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerPaymentReceiptUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerPaymentReceiptUpdateByNumberHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRefundCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRefundUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRefundUpdateByNumberHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerCreditNoteCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerCreditNoteUpdateByZaaerIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerApartmentCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerApartmentCreateBulkHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerApartmentUpdateByZaaerIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerApartmentUpdateByCodeHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, AppReservationCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, AppReservationUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerFloorCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerFloorCreateBulkHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerFloorUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerHotelSettingsCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerHotelSettingsUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerUserCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerUserUpdateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRoleCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRoleUpdateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerBankCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerBankUpdateByIdHandler>();
// TODO: Expense handlers - need to be added to ZaaerGenericHandlers.cs
// builder.Services.AddScoped<IQueuedOperationHandler, ZaaerExpenseCreateHandler>();
// builder.Services.AddScoped<IQueuedOperationHandler, ZaaerExpenseUpdateByIdHandler>();
// builder.Services.AddScoped<IQueuedOperationHandler, ExpenseCreateHandler>();
// builder.Services.AddScoped<IQueuedOperationHandler, ExpenseUpdateByIdHandler>();
// builder.Services.AddScoped<IQueuedOperationHandler, ExpenseDeleteHandler>();
// builder.Services.AddScoped<IQueuedOperationHandler, ExpenseRoomAddHandler>();
// builder.Services.AddScoped<IQueuedOperationHandler, ExpenseRoomUpdateHandler>();
// builder.Services.AddScoped<IQueuedOperationHandler, ExpenseRoomDeleteHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerMaintenanceCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerMaintenanceUpdateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRoomTypeCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRoomTypeUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerBuildingCreateWithFloorsHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerBuildingUpdateWithFloorsHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerBuildingUpdateWithFloorsSafeHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerSeasonalRateCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerSeasonalRateUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRateTypeCreateHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRateTypeUpdateByIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRateTypeUpdateByZaaerIdHandler>();
builder.Services.AddScoped<IQueuedOperationHandler, ZaaerRateTypeDeleteByZaaerIdHandler>();
builder.Services.AddHostedService<PartnerQueueBackgroundWorker>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Zaaer Integration API - Multi-Tenant", Version = "v1" });
    
    // ? إضافة X-Hotel-Code Header في Swagger
    c.AddSecurityDefinition("X-Hotel-Code", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Hotel Code Header (e.g., Dammam1, Dammam2)",
        Name = "X-Hotel-Code",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "X-Hotel-Code"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "X-Hotel-Code"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // Include XML comments if the file exists
    var xmlFile = Path.Combine(AppContext.BaseDirectory, "zaaerIntegration.xml");
    if (File.Exists(xmlFile))
    {
        c.IncludeXmlComments(xmlFile);
    }

    // Examples for Reservation Tools payloads
    c.SchemaFilter<zaaerIntegration.Filters.ReservationToolSchemaExample>();
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

{
    var integrationSecrets = app.Configuration
        .GetSection(IntegrationSecretsOptions.SectionName)
        .Get<IntegrationSecretsOptions>();
    var masterKeyConfigured = !string.IsNullOrWhiteSpace(integrationSecrets?.MasterKey)
        && Convert.TryFromBase64String(integrationSecrets.MasterKey.Trim(), new Span<byte>(new byte[32]), out var len)
        && len == 32;
    if (masterKeyConfigured)
    {
        Log.Information(
            "[Startup] IntegrationSecrets:MasterKey is configured — ZATCA/NTMP secrets use durable encryption.");
    }
    else if (!app.Environment.IsDevelopment())
    {
        Log.Warning(
            "[Startup] IntegrationSecrets:MasterKey is missing or invalid. " +
            "Configure it once on this server before onboarding ZATCA for hotels.");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Zaaer Integration API v1");
    });
}

app.UseHttpsRedirection();

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isReportDesignerPath = path.StartsWith("/DXXRD", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/DXXRDV", StringComparison.OrdinalIgnoreCase);
        var isQueryBuilderPath = path.StartsWith("/DXXQB", StringComparison.OrdinalIgnoreCase);
        var isReportTestPage = path.Equals("/test-reports.html", StringComparison.OrdinalIgnoreCase);

        if (isReportDesignerPath || isQueryBuilderPath || isReportTestPage)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}

// Enable static files for frontend
var staticContentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticContentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticContentTypes });

// Developer-only Numbering Admin page toggle (hide page when disabled)
var isNumberingAdminEnabled = builder.Configuration.GetValue<bool>("Features:NumberingAdmin");
if (!isNumberingAdminEnabled)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.Equals("/numbering-admin.html", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/js/pages/numbering-admin.js", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/css/numbering-admin.css", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}

// Default site entry: login page
var defaultFilesOptions = new DefaultFilesOptions();
defaultFilesOptions.DefaultFileNames.Clear();
defaultFilesOptions.DefaultFileNames.Add("index.html");
defaultFilesOptions.DefaultFileNames.Add("login.html");
app.UseDefaultFiles(defaultFilesOptions);

// Enable routing
app.UseRouting();

// Enable DevExpress Reporting
app.UseDevExpressControls();

app.UseCors("AllowAll");

app.UseAuthentication();

// Logs slow PMS API requests to logs/performance without affecting non-PMS traffic.
app.UseMiddleware<PmsSlowRequestLoggingMiddleware>();

// ? تشغيل Multi-Tenant Middleware
// يجب أن يكون بعد CORS وقبل Authorization
app.UseTenantMiddleware();

app.UseAuthorization();

// Optional global queue middleware (proxy). Use separate flag so controllers can still enqueue while global proxy is off
if (builder.Configuration.GetValue<bool>("PartnerQueue:UseMiddleware"))
{
    app.UseMiddleware<PartnerQueueMiddleware>();
}

app.MapControllers();

// ? Ensure Master Database connection and verify tenants exist
// النظام يعتمد فقط على Master DB (db29328) - لا يضيف بيانات
using (var scope = app.Services.CreateScope())
{
    try
    {
        var masterContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        
        // التأكد من وجود الجداول فقط (لا يضيف بيانات)
        await masterContext.Database.EnsureCreatedAsync();
        
        // التحقق من وجود بيانات Tenants في Master DB فقط
        // البيانات موجودة بالفعل في Master DB - لا نضيف بيانات هنا
        // 🔴 DEBUG BREAKPOINT: ضع Breakpoint هنا لاختبار Master DB Connection
        var tenantsCount = await masterContext.Tenants.CountAsync();
        
        // 🔴 DEBUG BREAKPOINT: ضع Breakpoint هنا لرؤية عدد Tenants
        if (tenantsCount == 0)
        {
            Log.Warning("⚠️ No tenants found in Master DB (db29328).");
            Log.Warning("⚠️ Please add tenants manually to Master Database table 'Tenants'.");
            Log.Warning("⚠️ Required columns: Code, Name, DatabaseName");
            Log.Warning("⚠️ Optional columns: BaseUrl, ConnectionString (system uses DatabaseName instead)");
        }
        else
        {
            Log.Information("✅ Master Database connection successful. Found {Count} tenant(s) in Master DB", tenantsCount);
            
            // Log available tenants for debugging
            // 🔴 DEBUG BREAKPOINT: ضع Breakpoint هنا لرؤية Tenants في Master DB
            var tenantCodes = await masterContext.Tenants
                .Select(t => t.Code)
                .ToListAsync();
            Log.Information("✅ Available tenant codes: {Codes}", string.Join(", ", tenantCodes));
            
            // 🔴 DEBUG: يمكنك إضافة هذا الكود لرؤية Tenants في Debug
            // var allTenants = await masterContext.Tenants
            //     .Select(t => new { t.Id, t.Code, t.Name, t.DatabaseName })
            //     .ToListAsync();
            // Log.Information("✅ Tenants: {Tenants}", System.Text.Json.JsonSerializer.Serialize(allTenants));
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Failed to connect to Master Database: {Message}", ex.Message);
        Log.Error("❌ Make sure Master DB (db29328) is accessible and connection string is correct in appsettings.json");
        // لا نوقف التطبيق - فقط نسجل الخطأ
    }
}

// Hall platform: tenant schema + Master RBAC (idempotent)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        logger.LogInformation("🔧 [Startup] Checking hall platform schema and permissions...");
        await zaaerIntegration.Services.Startup.HallPlatformStartup.ApplyAsync(
            scope.ServiceProvider,
            configuration,
            logger);
        logger.LogInformation("🔧 [Startup] Hall platform setup check completed");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ [Startup] Error during hall platform setup: {Message}", ex.Message);
    }
}

app.Run();
