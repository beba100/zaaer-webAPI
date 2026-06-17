using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Integrations;
using zaaerIntegration.Services.Integrations.Zatca;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations
{
    public interface IPmsZatcaSettingsService
    {
        Task<PmsZatcaSettingsDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
        Task<PmsZatcaSettingsDto> UpsertCurrentAsync(PmsUpsertZatcaSettingsDto dto, CancellationToken cancellationToken = default);
    }

    public sealed class PmsZatcaSettingsService : PmsHotelScopeService, IPmsZatcaSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IZatcaIntegrationSchemaEnsurer _schemaEnsurer;

        public PmsZatcaSettingsService(
            ApplicationDbContext context,
            ITenantService tenantService,
            IZatcaIntegrationSchemaEnsurer schemaEnsurer)
            : base(context, tenantService)
        {
            _context = context;
            _schemaEnsurer = schemaEnsurer;
        }

        public async Task<PmsZatcaSettingsDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await ZatcaDetailsEnvironmentSync.LoadAlignedForHotelAsync(
                _context,
                hotelId,
                cancellationToken);
            return entity == null ? null : Map(entity);
        }

        public async Task<PmsZatcaSettingsDto> UpsertCurrentAsync(
            PmsUpsertZatcaSettingsDto dto,
            CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await _context.ZatcaDetails.FirstOrDefaultAsync(z => z.HotelId == hotelId, cancellationToken);
            if (entity == null)
            {
                entity = new ZatcaDetails
                {
                    HotelId = hotelId,
                    CompanyName = dto.CompanyName,
                    CreatedAt = KsaTime.Now
                };
                _context.ZatcaDetails.Add(entity);
            }

            entity.IsActive = dto.IsActive;
            entity.CompanyName = dto.CompanyName;
            entity.TaxNumber = dto.TaxNumber;
            entity.GroupTaxId = dto.GroupTaxId;
            entity.CorporateRegistrationNumber = dto.CorporateRegistrationNumber;
            entity.DeviceCommonName = string.IsNullOrWhiteSpace(dto.DeviceCommonName)
                ? null
                : dto.DeviceCommonName.Trim();
            ZatcaDetailsEnvironmentSync.ApplyUnified(
                entity,
                string.IsNullOrWhiteSpace(dto.ApiEnvironment) ? "sandbox" : dto.ApiEnvironment.Trim());
            entity.DeviceUuid = dto.DeviceUuid;
            entity.Otp = dto.Otp;
            entity.Address = dto.Address;
            entity.StreetName = dto.StreetName;
            entity.BuildingNumber = dto.BuildingNumber;
            entity.PlotIdentification = dto.PlotIdentification;
            entity.CitySubdivisionName = dto.CitySubdivisionName;
            entity.City = dto.City;
            entity.PostalZone = dto.PostalZone;
            entity.CountrySubEntity = dto.CountrySubEntity;
            entity.CompanyRegistrationName = dto.CompanyRegistrationName;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            return Map(entity);
        }

        private static PmsZatcaSettingsDto Map(ZatcaDetails e) => new()
        {
            DetailsId = e.DetailsId,
            HotelId = e.HotelId,
            IsActive = e.IsActive,
            CompanyName = e.CompanyName,
            TaxNumber = e.TaxNumber,
            GroupTaxId = e.GroupTaxId,
            CorporateRegistrationNumber = e.CorporateRegistrationNumber,
            DeviceCommonName = e.DeviceCommonName,
            Environment = ZatcaDetailsEnvironmentSync.ResolveEffective(e.ApiEnvironment, e.Environment),
            ApiEnvironment = ZatcaDetailsEnvironmentSync.ResolveEffective(e.ApiEnvironment, e.Environment),
            DeviceUuid = e.DeviceUuid,
            Otp = e.Otp,
            Address = e.Address,
            StreetName = e.StreetName,
            BuildingNumber = e.BuildingNumber,
            PlotIdentification = e.PlotIdentification,
            CitySubdivisionName = e.CitySubdivisionName,
            City = e.City,
            PostalZone = e.PostalZone,
            CountrySubEntity = e.CountrySubEntity,
            CompanyRegistrationName = e.CompanyRegistrationName,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
