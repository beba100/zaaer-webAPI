using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using zaaerIntegration.Configuration;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Integrations;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;
using Zatca.EInvoice.Api;
using Zatca.EInvoice.Certificates;

namespace zaaerIntegration.Services.Integrations
{
    public interface IPmsZatcaOnboardingService
    {
        Task<PmsZatcaDeviceStatusDto?> GetDeviceStatusAsync(CancellationToken cancellationToken = default);
        Task<PmsZatcaOnboardResultDto> OnboardDeviceAsync(
            PmsZatcaOnboardRequestDto dto,
            CancellationToken cancellationToken = default);
        Task<PmsZatcaOnboardResultDto> RequestProductionCsidAsync(
            CancellationToken cancellationToken = default);
    }

    public sealed class PmsZatcaOnboardingService : PmsHotelScopeService, IPmsZatcaOnboardingService
    {
        private const string DefaultSolutionName = "AleairyPMS";
        private const string DefaultSolutionVersion = "1.0";

        private readonly ApplicationDbContext _context;
        private readonly IZatcaIntegrationSchemaEnsurer _schemaEnsurer;
        private readonly IIntegrationSecretProtector _secretProtector;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ZatcaOptions _zatcaOptions;
        private readonly ILogger<PmsZatcaOnboardingService> _logger;

        public PmsZatcaOnboardingService(
            ApplicationDbContext context,
            ITenantService tenantService,
            IZatcaIntegrationSchemaEnsurer schemaEnsurer,
            IIntegrationSecretProtector secretProtector,
            IHostEnvironment hostEnvironment,
            IOptions<ZatcaOptions> zatcaOptions,
            ILogger<PmsZatcaOnboardingService> logger)
            : base(context, tenantService)
        {
            _context = context;
            _schemaEnsurer = schemaEnsurer;
            _secretProtector = secretProtector;
            _hostEnvironment = hostEnvironment;
            _zatcaOptions = zatcaOptions.Value;
            _logger = logger;
        }

        public async Task<PmsZatcaDeviceStatusDto?> GetDeviceStatusAsync(CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotelSettings = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelId = hotelSettings.ZaaerId
                ?? throw new InvalidOperationException(
                    $"ZaaerId is not configured for hotel code: {hotelSettings.HotelCode}. Set hotel_settings.zaaer_id.");
            var details = await ZatcaDetailsEnvironmentSync.LoadAlignedForHotelAsync(
                _context,
                hotelId,
                cancellationToken);
            if (details == null)
            {
                return null;
            }

            var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(details.ApiEnvironment, details.Environment);
            ZatcaDevice? device = null;
            if (!string.IsNullOrWhiteSpace(details.DeviceUuid))
            {
                device = await _context.ZatcaDevices
                    .FirstOrDefaultAsync(
                        d => d.HotelId == hotelId
                             && d.Environment == environment
                             && d.DeviceUuid == details.DeviceUuid,
                        cancellationToken);

                if (device != null
                    && IntegrationSecretMigration.TryMigrateZatcaDevicePrivateKey(device, _secretProtector))
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            return MapDeviceStatus(details, device, environment);
        }

        public async Task<PmsZatcaOnboardResultDto> OnboardDeviceAsync(
            PmsZatcaOnboardRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotelSettings = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelId = hotelSettings.ZaaerId
                ?? throw new InvalidOperationException(
                    $"ZaaerId is not configured for hotel code: {hotelSettings.HotelCode}. Set hotel_settings.zaaer_id.");

            if (!_secretProtector.IsMasterKeyConfigured && !_hostEnvironment.IsDevelopment())
            {
                _logger.LogWarning(
                    "[ZATCA Onboard] IntegrationSecrets:MasterKey is not set — storing private key with Data Protection. " +
                    "Set IntegrationSecrets__MasterKey on the server for durable multi-hotel encryption.");
            }

            var details = await ZatcaDetailsEnvironmentSync.LoadAlignedForHotelAsync(
                _context,
                hotelId,
                cancellationToken);
            if (details == null)
            {
                return Fail("Save ZATCA seller settings before onboarding the device.");
            }

            if (!details.IsActive)
            {
                return Fail(IntegrationPlatformGuard.ZatcaInactiveMessage);
            }

            var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(
                dto.ApiEnvironment ?? details.ApiEnvironment,
                details.Environment);
            ZatcaDetailsEnvironmentSync.ApplyUnified(details, environment);

            if (string.IsNullOrWhiteSpace(dto.Otp))
            {
                if (environment == "simulation" && _zatcaOptions.DeferSimulationOtpOnboarding)
                {
                    return Fail(
                        "Simulation OTP onboarding is temporarily deferred. Save api_environment=simulation, " +
                        "run compliance on Sandbox if needed, then register Simulation OTP from Fatoora when ready.");
                }

                return Fail("OTP is required. Generate it from the ZATCA Fatoora / Developer portal.");
            }

            if (string.IsNullOrWhiteSpace(details.TaxNumber))
            {
                return Fail("Seller tax number is required in ZATCA settings.");
            }

            var deviceUuid = string.IsNullOrWhiteSpace(dto.DeviceUuid)
                ? (string.IsNullOrWhiteSpace(details.DeviceUuid) ? Guid.NewGuid().ToString() : details.DeviceUuid)
                : dto.DeviceUuid.Trim();

            var solutionName = string.IsNullOrWhiteSpace(dto.SolutionName) ? DefaultSolutionName : dto.SolutionName.Trim();
            var solutionVersion = string.IsNullOrWhiteSpace(dto.SolutionVersion) ? DefaultSolutionVersion : dto.SolutionVersion.Trim();
            var organizationalUnit = ResolveCsrOrganizationalUnitName(dto, details);
            var address = BuildAddress(details);
            var businessCategory = string.IsNullOrWhiteSpace(dto.BusinessCategory)
                ? "Hospitality"
                : dto.BusinessCategory.Trim();
            var commonName = ResolveCsrCommonName(
                dto,
                details,
                solutionName,
                hotelSettings.HotelCode?.Trim());

            string csr;
            string privateKeyPem;
            try
            {
                var builder = new CertificateBuilder()
                    .SetOrganizationIdentifier(details.TaxNumber.Trim())
                    .SetSerialNumber(solutionName, solutionVersion, deviceUuid)
                    .SetCommonName(commonName)
                    .SetCountryName("SA")
                    .SetOrganizationName(details.CompanyName)
                    .SetOrganizationalUnitName(organizationalUnit)
                    .SetAddress(address)
                    .SetInvoiceType(1100)
                    .SetBusinessCategory(businessCategory)
                    .SetEnvironmentMode(MapEnvironmentMode(environment));

                builder.Generate();
                csr = builder.GetCsr();
                privateKeyPem = builder.GetPrivateKey();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZATCA Onboard] CSR generation failed for hotel {HotelId}", hotelId);
                return Fail($"CSR generation failed: {ex.Message}");
            }

            ComplianceCertificateResult certResult;
            try
            {
                var apiClient = new ZatcaApiClient(ZatcaEInvoiceEnvironmentMapper.ToApiEnvironment(environment));
                certResult = await apiClient.RequestComplianceCertificateAsync(csr, dto.Otp!.Trim(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZATCA Onboard] Compliance CSID request failed for hotel {HotelId}", hotelId);
                LogIntegration(hotelId, "Onboard_ComplianceCsid", "Error", ex.Message, null);
                await _context.SaveChangesAsync(cancellationToken);
                return Fail($"ZATCA compliance request failed: {ex.Message}");
            }

            if (!certResult.IsSuccess
                || string.IsNullOrWhiteSpace(certResult.BinarySecurityToken)
                || string.IsNullOrWhiteSpace(certResult.Secret))
            {
                var err = certResult.Errors != null && certResult.Errors.Count > 0
                    ? string.Join("; ", certResult.Errors)
                    : certResult.DispositionMessage ?? "Unknown ZATCA error";
                LogIntegration(hotelId, "Onboard_ComplianceCsid", "Error", err, null);
                await _context.SaveChangesAsync(cancellationToken);
                return Fail(err);
            }

            var device = await _context.ZatcaDevices.FirstOrDefaultAsync(
                d => d.HotelId == hotelId && d.Environment == environment && d.DeviceUuid == deviceUuid,
                cancellationToken);

            if (device == null)
            {
                device = new ZatcaDevice
                {
                    HotelId = hotelId,
                    DeviceUuid = deviceUuid,
                    Environment = environment,
                    CreatedAt = KsaTime.Now
                };
                _context.ZatcaDevices.Add(device);
            }

            device.DeviceStatus = "compliance_active";
            device.ComplianceRequestId = certResult.RequestId;
            device.ComplianceCsid = certResult.BinarySecurityToken;
            device.ComplianceSecret = certResult.Secret;
            device.CertificatePem = certResult.BinarySecurityToken;
            device.PrivateKeyEncrypted = _secretProtector.Protect(privateKeyPem);
            device.CsrPem = csr;
            // New CSR/key invalidates any Production CSID issued from a previous onboarding.
            device.ProductionCsid = null;
            device.ProductionSecret = null;
            device.LastIcv = 0;
            device.LastInvoiceHash = null;
            device.UpdatedAt = KsaTime.Now;

            details.DeviceUuid = deviceUuid;
            ZatcaDetailsEnvironmentSync.ApplyUnified(details, environment);
            details.Otp = null;
            details.UpdatedAt = KsaTime.Now;

            LogIntegration(
                hotelId,
                "Onboard_ComplianceCsid",
                "Success",
                certResult.DispositionMessage,
                certResult.RequestId);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[ZATCA Onboard] Compliance CSID stored for hotel {HotelId}, device {DeviceUuid}, env {Env}",
                hotelId,
                deviceUuid,
                environment);

            return new PmsZatcaOnboardResultDto
            {
                Success = true,
                Message = certResult.DispositionMessage ?? "Compliance CSID obtained successfully.",
                DeviceUuid = deviceUuid,
                Environment = environment,
                ComplianceRequestId = certResult.RequestId,
                DeviceStatus = device.DeviceStatus,
                HasComplianceCsid = true
            };
        }

        public async Task<PmsZatcaOnboardResultDto> RequestProductionCsidAsync(
            CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);

            var details = await ZatcaDetailsEnvironmentSync.LoadAlignedForHotelAsync(
                _context,
                hotelId,
                cancellationToken);
            if (details == null || string.IsNullOrWhiteSpace(details.DeviceUuid))
            {
                return Fail("Complete compliance onboarding first.");
            }

            if (!details.IsActive)
            {
                return Fail(IntegrationPlatformGuard.ZatcaInactiveMessage);
            }

            var environment = ZatcaDetailsEnvironmentSync.ResolveEffective(
                details.ApiEnvironment,
                details.Environment);

            var device = await _context.ZatcaDevices.FirstOrDefaultAsync(
                d => d.HotelId == hotelId
                     && d.Environment == environment
                     && d.DeviceUuid == details.DeviceUuid,
                cancellationToken);

            if (device == null
                || string.IsNullOrWhiteSpace(device.ComplianceRequestId)
                || string.IsNullOrWhiteSpace(device.ComplianceCsid)
                || string.IsNullOrWhiteSpace(device.ComplianceSecret))
            {
                return Fail("Compliance CSID is missing. Run device onboarding first.");
            }

            if (!string.Equals(device.DeviceStatus, "compliance_tests_passed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(device.DeviceStatus, "production_active", StringComparison.OrdinalIgnoreCase))
            {
                return Fail(
                    "Run the six compliance sample tests successfully before requesting Production CSID.");
            }

            ProductionCertificateResult prodResult;
            try
            {
                var apiClient = new ZatcaApiClient(ZatcaEInvoiceEnvironmentMapper.ToApiEnvironment(environment));
                prodResult = await apiClient.RequestProductionCertificateAsync(
                    device.ComplianceRequestId,
                    device.ComplianceCsid,
                    device.ComplianceSecret,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ZATCA Onboard] Production CSID request failed for hotel {HotelId}", hotelId);
                LogIntegration(hotelId, "Onboard_ProductionCsid", "Error", ex.Message, null);
                await _context.SaveChangesAsync(cancellationToken);
                return Fail($"ZATCA production CSID request failed: {ex.Message}");
            }

            if (!prodResult.IsSuccess
                || string.IsNullOrWhiteSpace(prodResult.BinarySecurityToken)
                || string.IsNullOrWhiteSpace(prodResult.Secret))
            {
                var err = prodResult.Errors != null && prodResult.Errors.Count > 0
                    ? string.Join("; ", prodResult.Errors)
                    : prodResult.DispositionMessage ?? "Unknown ZATCA error";
                LogIntegration(hotelId, "Onboard_ProductionCsid", "Error", err, null);
                await _context.SaveChangesAsync(cancellationToken);
                return Fail(err);
            }

            device.ProductionCsid = prodResult.BinarySecurityToken;
            device.ProductionSecret = prodResult.Secret;
            device.CertificatePem = prodResult.BinarySecurityToken;
            device.DeviceStatus = "production_active";
            device.UpdatedAt = KsaTime.Now;

            LogIntegration(
                hotelId,
                "Onboard_ProductionCsid",
                "Success",
                prodResult.DispositionMessage,
                prodResult.RequestId);

            await _context.SaveChangesAsync(cancellationToken);

            return new PmsZatcaOnboardResultDto
            {
                Success = true,
                Message = prodResult.DispositionMessage ?? "Production CSID obtained successfully.",
                DeviceUuid = device.DeviceUuid,
                Environment = environment,
                DeviceStatus = device.DeviceStatus,
                HasComplianceCsid = true,
                HasProductionCsid = true
            };
        }

        private PmsZatcaDeviceStatusDto MapDeviceStatus(
            ZatcaDetails details,
            ZatcaDevice? device,
            string environment) =>
            new()
            {
                HotelId = details.HotelId,
                ApiEnvironment = environment,
                DeviceUuid = details.DeviceUuid,
                DeviceStatus = device?.DeviceStatus ?? "not_onboarded",
                HasComplianceCsid = !string.IsNullOrWhiteSpace(device?.ComplianceCsid),
                HasProductionCsid = !string.IsNullOrWhiteSpace(device?.ProductionCsid),
                CanDecryptPrivateKey = device?.PrivateKeyEncrypted == null
                    ? null
                    : _secretProtector.CanUnprotect(device.PrivateKeyEncrypted),
                UsesDurablePrivateKey = device?.PrivateKeyEncrypted == null
                    ? null
                    : _secretProtector.IsDurableFormat(device.PrivateKeyEncrypted),
                IsMasterKeyConfigured = _secretProtector.IsMasterKeyConfigured,
                ComplianceRequestId = device?.ComplianceRequestId,
                LastIcv = device?.LastIcv ?? 0,
                LastInvoiceHash = device?.LastInvoiceHash
            };

        private static string ResolveCsrCommonName(
            PmsZatcaOnboardRequestDto dto,
            ZatcaDetails details,
            string solutionName,
            string? hotelCode)
        {
            if (!string.IsNullOrWhiteSpace(dto.CommonName))
            {
                return TruncateCsrField(dto.CommonName.Trim(), 64);
            }

            if (!string.IsNullOrWhiteSpace(details.DeviceCommonName))
            {
                return TruncateCsrField(details.DeviceCommonName.Trim(), 64);
            }

            var tax = details.TaxNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(hotelCode) && !string.IsNullOrWhiteSpace(tax))
            {
                var prefix = solutionName.Replace(" ", "", StringComparison.Ordinal);
                return TruncateCsrField($"{prefix}-{hotelCode}-{tax}", 64);
            }

            var name = details.CompanyName?.Trim() ?? "EGS";
            return TruncateCsrField(name, 64);
        }

        private static string ResolveCsrOrganizationalUnitName(PmsZatcaOnboardRequestDto dto, ZatcaDetails details)
        {
            if (!string.IsNullOrWhiteSpace(details.CorporateRegistrationNumber))
            {
                return TruncateCsrField(details.CorporateRegistrationNumber.Trim(), 64);
            }

            if (!string.IsNullOrWhiteSpace(dto.BranchName))
            {
                return TruncateCsrField(dto.BranchName.Trim(), 64);
            }

            if (!string.IsNullOrWhiteSpace(details.CitySubdivisionName))
            {
                return TruncateCsrField(details.CitySubdivisionName.Trim(), 64);
            }

            return TruncateCsrField(details.City?.Trim() ?? "Main Branch", 64);
        }

        private static string TruncateCsrField(string value, int maxLen) =>
            value.Length <= maxLen ? value : value[..maxLen];

        private static string BuildAddress(ZatcaDetails details)
        {
            if (!string.IsNullOrWhiteSpace(details.StreetName))
            {
                var parts = new[]
                {
                    details.BuildingNumber,
                    details.StreetName,
                    details.CitySubdivisionName,
                    details.City,
                    details.PostalZone
                };
                return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            return details.Address ?? details.City ?? "SA";
        }

        private static ZatcaEnvironmentMode MapEnvironmentMode(string environment) =>
            ZatcaEInvoiceEnvironmentMapper.ToCertificateEnvironmentMode(environment);

        private void LogIntegration(
            int hotelId,
            string eventType,
            string status,
            string? message,
            string? correlationId)
        {
            _context.IntegrationResponses.Add(new IntegrationResponse
            {
                HotelId = hotelId,
                Service = ZatcaApiConstants.ServiceName,
                EventType = eventType,
                Status = status,
                ErrorMessage = status == "Error" ? message : null,
                ResponsePayload = status == "Success" ? message : null,
                CorrelationId = correlationId,
                CreatedAt = KsaTime.Now
            });
        }

        private static PmsZatcaOnboardResultDto Fail(string message) =>
            new() { Success = false, Message = message };
    }
}
