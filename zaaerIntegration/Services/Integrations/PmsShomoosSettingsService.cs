using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Integrations;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations
{
    public interface IPmsShomoosSettingsService
    {
        Task<PmsShomoosSettingsDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
        Task<PmsShomoosSettingsDto> UpsertCurrentAsync(PmsUpsertShomoosSettingsDto dto, CancellationToken cancellationToken = default);
    }

    public sealed class PmsShomoosSettingsService : PmsHotelScopeService, IPmsShomoosSettingsService
    {
        private readonly ApplicationDbContext _context;

        public PmsShomoosSettingsService(ApplicationDbContext context, ITenantService tenantService)
            : base(context, tenantService)
        {
            _context = context;
        }

        public async Task<PmsShomoosSettingsDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await _context.ShomoosDetails.AsNoTracking()
                .FirstOrDefaultAsync(n => n.HotelId == hotelId, cancellationToken);
            return entity == null ? null : Map(entity);
        }

        public async Task<PmsShomoosSettingsDto> UpsertCurrentAsync(
            PmsUpsertShomoosSettingsDto dto,
            CancellationToken cancellationToken = default)
        {
            var hotelId = await GetCurrentHotelIdAsync(cancellationToken);
            var entity = await _context.ShomoosDetails.FirstOrDefaultAsync(n => n.HotelId == hotelId, cancellationToken);
            if (entity == null)
            {
                entity = new ShomoosDetails { HotelId = hotelId, CreatedAt = KsaTime.Now };
                _context.ShomoosDetails.Add(entity);
            }

            entity.IsActive = dto.IsActive;
            if (dto.UserId != null) entity.UserId = dto.UserId;
            if (dto.BranchCode != null) entity.BranchCode = dto.BranchCode;
            if (dto.BranchSecret != null) entity.BranchSecret = dto.BranchSecret;
            if (dto.LanguageCode != null) entity.LanguageCode = dto.LanguageCode;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            return Map(entity);
        }

        private static PmsShomoosSettingsDto Map(ShomoosDetails e) => new()
        {
            DetailsId = e.DetailsId,
            HotelId = e.HotelId,
            IsActive = e.IsActive,
            UserId = e.UserId,
            BranchCode = e.BranchCode,
            BranchSecret = e.BranchSecret,
            LanguageCode = e.LanguageCode,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };
    }
}
