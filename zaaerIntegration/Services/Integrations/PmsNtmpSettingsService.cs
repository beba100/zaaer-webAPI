using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Integrations;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations
{
    public interface IPmsNtmpSettingsService
    {
        Task<PmsNtmpSettingsDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
        Task<PmsNtmpSettingsDto> UpsertCurrentAsync(PmsUpsertNtmpSettingsDto dto, CancellationToken cancellationToken = default);
        Task<NtmpConnectionTestResultDto> TestConnectionAsync(CancellationToken cancellationToken = default);
    }

    public sealed class PmsNtmpSettingsService : PmsHotelScopeService, IPmsNtmpSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IIntegrationSecretProtector _secretProtector;
        private readonly INtmpGatewayClient _gateway;
        private readonly INtmpPasswordResolver _passwordResolver;
        private readonly INtmpIntegrationSchemaEnsurer _schemaEnsurer;

        public PmsNtmpSettingsService(
            ApplicationDbContext context,
            ITenantService tenantService,
            IIntegrationSecretProtector secretProtector,
            INtmpPasswordResolver passwordResolver,
            INtmpGatewayClient gateway,
            INtmpIntegrationSchemaEnsurer schemaEnsurer)
            : base(context, tenantService)
        {
            _context = context;
            _secretProtector = secretProtector;
            _passwordResolver = passwordResolver;
            _gateway = gateway;
            _schemaEnsurer = schemaEnsurer;
        }

        public async Task<PmsNtmpSettingsDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelZaaerId = await GetCurrentHotelZaaerIdAsync(cancellationToken);
            var entity = await ForCurrentHotel(_context.NtmpDetails.AsNoTracking(), hotelZaaerId, hotel.HotelId)
                .FirstOrDefaultAsync(cancellationToken);
            return entity == null ? null : Map(entity, hotel, hotelZaaerId);
        }

        public async Task<PmsNtmpSettingsDto> UpsertCurrentAsync(
            PmsUpsertNtmpSettingsDto dto,
            CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelZaaerId = await GetCurrentHotelZaaerIdAsync(cancellationToken);
            var entity = await ForCurrentHotel(_context.NtmpDetails, hotelZaaerId, hotel.HotelId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                entity = new NtmpDetails
                {
                    HotelId = hotelZaaerId,
                    ZaaerId = hotelZaaerId,
                    CreatedAt = KsaTime.Now
                };
                _context.NtmpDetails.Add(entity);
            }

            entity.HotelId = hotelZaaerId;
            entity.ZaaerId = hotelZaaerId;
            entity.IsActive = dto.IsActive;
            if (dto.GatewayApiKey != null)
            {
                entity.GatewayApiKey = dto.GatewayApiKey;
            }

            if (dto.UserName != null)
            {
                entity.UserName = dto.UserName;
            }

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                entity.PasswordEncrypted = _secretProtector.Protect(dto.Password);
                entity.PasswordHash = HashPasswordLegacy(dto.Password);
            }

            entity.ApiEnvironment = NormalizeEnvironment(dto.ApiEnvironment);
            entity.ChannelName = NtmpApiConstants.ChannelName;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
            return Map(entity, hotel, hotelZaaerId);
        }

        public async Task<NtmpConnectionTestResultDto> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelZaaerId = await GetCurrentHotelZaaerIdAsync(cancellationToken);
            var entity = await ForCurrentHotel(_context.NtmpDetails.AsNoTracking(), hotelZaaerId, hotel.HotelId)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null || !entity.IsActive)
            {
                return new NtmpConnectionTestResultDto
                {
                    Success = false,
                    Message = "NTMP integration is not configured or inactive."
                };
            }

            var passwordResult = _passwordResolver.Resolve(entity);
            var password = passwordResult.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                return new NtmpConnectionTestResultDto
                {
                    Success = false,
                    Message = passwordResult.ErrorMessage ?? "NTMP password is missing. Save credentials first."
                };
            }

            var request = new NtmpGetTransactionIdRequest
            {
                BookingNo = new List<string> { "__connection_test__" }
            };

            var response = await _gateway.GetTransactionIdByBookingNoAsync(entity, password, request, cancellationToken);
            var authOk = response.HttpStatusCode != 401 && response.HttpStatusCode != 403
                && !response.ErrorCodes.Contains("100");

            return new NtmpConnectionTestResultDto
            {
                Success = authOk,
                Message = authOk
                    ? $"NTMP gateway accepted credentials for hotel {hotel.HotelCode} (environment: {entity.ApiEnvironment})."
                    : (response.ErrorMessage ?? "Authentication failed. Check API key, username, password, and environment."),
                CorrelationId = response.CorrelationId
            };
        }

        private static string HashPasswordLegacy(string password)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password);
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }

        private static IQueryable<NtmpDetails> ForCurrentHotel(
            IQueryable<NtmpDetails> query,
            int hotelZaaerId,
            int internalHotelId) =>
            query.Where(n =>
                n.ZaaerId == hotelZaaerId
                || n.HotelId == hotelZaaerId
                || (internalHotelId != hotelZaaerId && n.HotelId == internalHotelId));

        private static PmsNtmpSettingsDto Map(NtmpDetails e, HotelSettings hotel, int hotelZaaerId) => new()
        {
            DetailsId = e.DetailsId,
            HotelId = hotelZaaerId,
            HotelZaaerId = hotelZaaerId,
            HotelCode = hotel.HotelCode,
            IsActive = e.IsActive,
            GatewayApiKey = e.GatewayApiKey,
            UserName = e.UserName,
            HasPassword = e.PasswordEncrypted != null && e.PasswordEncrypted.Length > 0,
            ApiEnvironment = string.IsNullOrWhiteSpace(e.ApiEnvironment) ? "production" : e.ApiEnvironment,
            ChannelName = string.IsNullOrWhiteSpace(e.ChannelName) ? NtmpApiConstants.ChannelName : e.ChannelName,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt
        };

        private static string NormalizeEnvironment(string? value)
        {
            var v = (value ?? "production").Trim().ToLowerInvariant();
            return v switch
            {
                "dev" or "development" => "dev",
                "staging" or "stage" or "stg" => "staging",
                _ => "production"
            };
        }
    }
}
