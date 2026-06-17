using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Integrations
{
    public sealed class PmsNtmpSettingsDto
    {
        public int? DetailsId { get; set; }
        /// <summary>Zaaer property id (<c>hotel_settings.zaaer_id</c>) — same value stored in <c>ntmp_details.hotel_id</c>.</summary>
        public int HotelId { get; set; }
        /// <summary>Same as <see cref="HotelId"/> (explicit alias for UI).</summary>
        public int? HotelZaaerId { get; set; }
        public string? HotelCode { get; set; }
        public bool IsActive { get; set; }
        public string? GatewayApiKey { get; set; }
        public string? UserName { get; set; }
        public bool HasPassword { get; set; }
        public string ApiEnvironment { get; set; } = "production";
        public string? ChannelName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class PmsUpsertNtmpSettingsDto
    {
        public bool IsActive { get; set; } = true;

        [MaxLength(300)]
        public string? GatewayApiKey { get; set; }

        [MaxLength(150)]
        public string? UserName { get; set; }

        [MaxLength(300)]
        public string? Password { get; set; }

        [MaxLength(20)]
        public string ApiEnvironment { get; set; } = "production";

        [MaxLength(256)]
        public string? ChannelName { get; set; }
    }

    public sealed class PmsShomoosSettingsDto
    {
        public int? DetailsId { get; set; }
        public int HotelId { get; set; }
        public bool IsActive { get; set; }
        public string? UserId { get; set; }
        public string? BranchCode { get; set; }
        public string? BranchSecret { get; set; }
        public string? LanguageCode { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class PmsUpsertShomoosSettingsDto
    {
        public bool IsActive { get; set; } = true;
        [MaxLength(150)] public string? UserId { get; set; }
        [MaxLength(100)] public string? BranchCode { get; set; }
        [MaxLength(300)] public string? BranchSecret { get; set; }
        [MaxLength(20)] public string? LanguageCode { get; set; }
    }

    public sealed class PmsZatcaSettingsDto
    {
        public int? DetailsId { get; set; }
        public int HotelId { get; set; }
        public bool IsActive { get; set; } = true;
        public string CompanyName { get; set; } = string.Empty;
        public string? TaxNumber { get; set; }
        public string? GroupTaxId { get; set; }
        public string? CorporateRegistrationNumber { get; set; }
        /// <summary>CSR Common Name (الاسم العام) for EGS onboarding.</summary>
        public string? DeviceCommonName { get; set; }
        public string? Environment { get; set; }
        /// <summary>sandbox | simulation | production</summary>
        public string ApiEnvironment { get; set; } = "sandbox";
        public string? DeviceUuid { get; set; }
        public string? Otp { get; set; }
        public string? Address { get; set; }
        public string? StreetName { get; set; }
        public string? BuildingNumber { get; set; }
        public string? PlotIdentification { get; set; }
        public string? CitySubdivisionName { get; set; }
        public string? City { get; set; }
        public string? PostalZone { get; set; }
        public string? CountrySubEntity { get; set; }
        public string? CompanyRegistrationName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public sealed class PmsZatcaOnboardRequestDto
    {
        [MaxLength(20)]
        public string? Otp { get; set; }

        [MaxLength(100)]
        public string? DeviceUuid { get; set; }

        [MaxLength(20)]
        public string? ApiEnvironment { get; set; }

        [MaxLength(100)]
        public string? SolutionName { get; set; }

        [MaxLength(20)]
        public string? SolutionVersion { get; set; }

        [MaxLength(100)]
        public string? BranchName { get; set; }

        [MaxLength(200)]
        public string? CommonName { get; set; }

        [MaxLength(100)]
        public string? BusinessCategory { get; set; }
    }

    public sealed class PmsZatcaOnboardResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? DeviceUuid { get; set; }
        public string? Environment { get; set; }
        public string? ComplianceRequestId { get; set; }
        public string? DeviceStatus { get; set; }
        public bool HasComplianceCsid { get; set; }
        public bool HasProductionCsid { get; set; }
    }

    public sealed class PmsZatcaDeviceStatusDto
    {
        public int HotelId { get; set; }
        public string ApiEnvironment { get; set; } = "sandbox";
        public string? DeviceUuid { get; set; }
        public string DeviceStatus { get; set; } = "not_onboarded";
        public bool HasComplianceCsid { get; set; }
        public bool HasProductionCsid { get; set; }
        public bool? CanDecryptPrivateKey { get; set; }

        /// <summary>True when private key uses platform MasterKey (ZIS1) — survives server redeploy.</summary>
        public bool? UsesDurablePrivateKey { get; set; }

        public bool IsMasterKeyConfigured { get; set; }

        public string? ComplianceRequestId { get; set; }
        public int LastIcv { get; set; }
        public string? LastInvoiceHash { get; set; }
    }

    public sealed class PmsZatcaComplianceRunRequestDto
    {
        /// <summary>sandbox | simulation | production — defaults to current api_environment.</summary>
        [MaxLength(20)]
        public string? ApiEnvironment { get; set; }
    }

    public sealed class PmsZatcaComplianceItemResultDto
    {
        public string DocumentType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? HttpStatusCode { get; set; }
    }

    public sealed class PmsZatcaComplianceRunResultDto
    {
        public bool Success { get; set; }
        public bool AllPassed { get; set; }
        public string Environment { get; set; } = "simulation";
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<PmsZatcaComplianceItemResultDto> Items { get; set; } =
            Array.Empty<PmsZatcaComplianceItemResultDto>();
    }

    public sealed class PmsUpsertZatcaSettingsDto
    {
        public bool IsActive { get; set; } = true;

        [Required, MaxLength(200)] public string CompanyName { get; set; } = string.Empty;
        [MaxLength(100)] public string? TaxNumber { get; set; }
        [MaxLength(100)] public string? GroupTaxId { get; set; }
        [MaxLength(100)] public string? CorporateRegistrationNumber { get; set; }
        [MaxLength(200)] public string? DeviceCommonName { get; set; }
        [MaxLength(50)] public string? Environment { get; set; }
        [MaxLength(20)] public string ApiEnvironment { get; set; } = "sandbox";
        [MaxLength(100)] public string? DeviceUuid { get; set; }
        [MaxLength(50)] public string? Otp { get; set; }
        [MaxLength(500)] public string? Address { get; set; }
        [MaxLength(200)] public string? StreetName { get; set; }
        [MaxLength(50)] public string? BuildingNumber { get; set; }
        [MaxLength(100)] public string? PlotIdentification { get; set; }
        [MaxLength(100)] public string? CitySubdivisionName { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        [MaxLength(20)] public string? PostalZone { get; set; }
        [MaxLength(100)] public string? CountrySubEntity { get; set; }
        [MaxLength(200)] public string? CompanyRegistrationName { get; set; }
    }

    public class PmsIntegrationResponseRowDto
    {
        public int ResponseId { get; set; }
        public string? ResNo { get; set; }

        /// <summary>Route id for reservation-detail (Zaaer id when set, else internal reservation id).</summary>
        public int? ReservationRouteId { get; set; }

        /// <summary>Tenant hotel code for building reservation-detail links.</summary>
        public string? HotelCode { get; set; }

        public string Service { get; set; } = string.Empty;
        public string? EventType { get; set; }
        public string? UnitNumber { get; set; }
        public string? Guest { get; set; }
        public string? ErrorMessage { get; set; }
        public string Status { get; set; } = "Success";
        public DateTime CreatedAt { get; set; }
        public int? HttpStatusCode { get; set; }
        public string? CorrelationId { get; set; }
    }

    public sealed class PmsIntegrationResponseDetailDto : PmsIntegrationResponseRowDto
    {
        public string? RequestPayload { get; set; }
        public string? ResponsePayload { get; set; }
    }

    public sealed class PmsIntegrationResponseQueryDto
    {
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public string? BookingNo { get; set; }
        public string? Service { get; set; }
        public string? EventType { get; set; }
        public string? Status { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 100;
    }

    public sealed class NtmpConnectionTestResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
    }
}
