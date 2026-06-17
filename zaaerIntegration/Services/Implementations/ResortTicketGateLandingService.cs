using System.Text.Json;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class ResortTicketGateLandingService : IResortTicketGateLandingService
    {
        private const string ValidatePermission = "resort_tickets.validate";

        private readonly MasterDbContext _masterDb;
        private readonly ITenantService _tenantService;
        private readonly ILogger<ResortTicketGateLandingService> _logger;

        public ResortTicketGateLandingService(
            MasterDbContext masterDb,
            ITenantService tenantService,
            ILogger<ResortTicketGateLandingService> logger)
        {
            _masterDb = masterDb;
            _tenantService = tenantService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<PmsResortTicketGateStationDto>> GetStationCatalogAsync(CancellationToken cancellationToken = default)
        {
            await using var tenantDb = await CreateTenantDbAsync(null, cancellationToken);
            if (tenantDb == null)
            {
                return Array.Empty<PmsResortTicketGateStationDto>();
            }

            var catalog = await LoadCatalogMetaAsync(tenantDb, cancellationToken);
            return catalog.Values
                .OrderBy(x => x.TicketCategory)
                .ThenBy(x => x.StationCode)
                .Select(x => TryBuildStationDto(x.StationCode, catalog)!)
                .ToList();
        }

        public async Task<IReadOnlyList<PmsResortTicketGateStationDto>> GetUserGateStationsAsync(
            int userId,
            int tenantId,
            IReadOnlyCollection<string> permissions,
            CancellationToken cancellationToken = default)
        {
            if (!permissions.Contains(ValidatePermission, StringComparer.OrdinalIgnoreCase))
            {
                return Array.Empty<PmsResortTicketGateStationDto>();
            }

            var codes = await GetUserStationCodesAsync(userId, cancellationToken);
            if (codes.Count == 0)
            {
                return Array.Empty<PmsResortTicketGateStationDto>();
            }

            await using var tenantDb = await CreateTenantDbAsync(tenantId, cancellationToken);
            if (tenantDb == null)
            {
                return Array.Empty<PmsResortTicketGateStationDto>();
            }

            var catalog = await LoadCatalogMetaAsync(tenantDb, cancellationToken);
            return codes
                .Select(code => TryBuildStationDto(code, catalog))
                .Where(x => x != null)
                .Cast<PmsResortTicketGateStationDto>()
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetRoleGateStationCodesAsync(int roleId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _masterDb.RbacRoleGateStations.AsNoTracking()
                    .Where(x => x.RoleId == roleId)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.StationCode)
                    .Select(x => x.StationCode)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to read gate stations for role {RoleId}. Run Database/AddRoleGateStations.sql on Master DB if the table is missing.",
                    roleId);
                return Array.Empty<string>();
            }
        }

        public async Task SaveRoleGateStationCodesAsync(
            int roleId,
            IReadOnlyList<string> stationCodes,
            CancellationToken cancellationToken = default)
        {
            var normalized = (stationCodes ?? Array.Empty<string>())
                .Select(NormalizeStationCode)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            try
            {
                var existing = await _masterDb.RbacRoleGateStations
                    .Where(x => x.RoleId == roleId)
                    .ToListAsync(cancellationToken);

                _masterDb.RemoveRange(existing);

                for (var i = 0; i < normalized.Count; i++)
                {
                    _masterDb.Add(new MasterRbacRoleGateStation
                    {
                        RoleId = roleId,
                        StationCode = normalized[i],
                        SortOrder = i * 10,
                        CreatedAt = KsaTime.Now
                    });
                }

                await _masterDb.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to save gate stations for role {RoleId}. Run Database/AddRoleGateStations.sql on Master DB if the table is missing.",
                    roleId);
                throw new InvalidOperationException(
                    "Gate station assignments could not be saved. Ensure Master DB migration AddRoleGateStations.sql has been applied.",
                    ex);
            }
        }

        public PmsResortTicketGateStationDto? TryBuildStationDto(
            string stationCode,
            IReadOnlyDictionary<string, GateStationMeta> catalog)
        {
            var normalized = NormalizeStationCode(stationCode);
            if (string.IsNullOrWhiteSpace(normalized) || !catalog.TryGetValue(normalized, out var meta))
            {
                return null;
            }

            var theme = ResortTicketGateIconGenerator.ResolveThemeColor(normalized);
            var gateUrl = $"/resort-ticket-gate.html?station={Uri.EscapeDataString(normalized)}";
            return new PmsResortTicketGateStationDto
            {
                StationCode = normalized,
                NameAr = meta.NameAr,
                NameEn = meta.NameEn,
                TicketCategory = meta.TicketCategory,
                GateUrl = gateUrl,
                GateHomeTileUrl = gateUrl,
                ManifestUrl = $"/api/v1/pms/resort-tickets/gate/manifest?station={Uri.EscapeDataString(normalized)}",
                IconUrl192 = $"/api/v1/pms/resort-tickets/gate/icon?station={Uri.EscapeDataString(normalized)}&size=192",
                IconUrl512 = $"/api/v1/pms/resort-tickets/gate/icon?station={Uri.EscapeDataString(normalized)}&size=512",
                ThemeColor = theme
            };
        }

        public byte[] RenderStationIcon(string stationCode, int size) =>
            ResortTicketGateIconGenerator.RenderPng(NormalizeStationCode(stationCode), Math.Clamp(size, 64, 512));

        public string BuildManifestJson(string stationCode, IReadOnlyDictionary<string, GateStationMeta> catalog)
        {
            var normalized = NormalizeStationCode(stationCode);
            catalog.TryGetValue(normalized, out var meta);
            var dto = TryBuildStationDto(normalized, catalog);
            var nameAr = meta?.NameAr ?? normalized;
            var nameEn = meta?.NameEn ?? normalized;
            var startUrl = dto?.GateUrl ?? $"/resort-ticket-gate.html?station={Uri.EscapeDataString(normalized)}";
            var manifest = new
            {
                id = startUrl,
                name = $"{nameAr} — بوابة",
                short_name = nameAr.Length > 12 ? nameAr[..12] : nameAr,
                start_url = $"{startUrl}&source=pwa",
                scope = "/",
                display = "standalone",
                orientation = "portrait",
                background_color = dto?.ThemeColor ?? "#0f172a",
                theme_color = dto?.ThemeColor ?? "#0f172a",
                lang = "ar",
                dir = "rtl",
                icons = new object[]
                {
                    new
                    {
                        src = $"/api/v1/pms/resort-tickets/gate/icon?station={Uri.EscapeDataString(normalized)}&size=192",
                        sizes = "192x192",
                        type = "image/png",
                        purpose = "any"
                    },
                    new
                    {
                        src = $"/api/v1/pms/resort-tickets/gate/icon?station={Uri.EscapeDataString(normalized)}&size=512",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "any maskable"
                    }
                },
                description = nameEn
            };

            return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        public string? ResolvePreferredLandingUrl(IReadOnlyList<PmsResortTicketGateStationDto> stations)
        {
            if (stations == null || stations.Count == 0)
            {
                return null;
            }

            if (stations.Count == 1)
            {
                return stations[0].GateUrl;
            }

            return "/resort-ticket-gate-home.html";
        }

        public async Task<IReadOnlyDictionary<string, GateStationMeta>> LoadCatalogMapAsync(
            int? tenantId,
            CancellationToken cancellationToken = default)
        {
            await using var tenantDb = await CreateTenantDbAsync(tenantId, cancellationToken);
            if (tenantDb == null)
            {
                return new Dictionary<string, GateStationMeta>(StringComparer.OrdinalIgnoreCase);
            }

            return await LoadCatalogMetaAsync(tenantDb, cancellationToken);
        }

        private async Task<List<string>> GetUserStationCodesAsync(int userId, CancellationToken cancellationToken)
        {
            var roleIds = await _masterDb.RbacUserRoles.AsNoTracking()
                .Where(x => x.IsActive && x.UserId == userId)
                .Select(x => x.RoleId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (roleIds.Count == 0)
            {
                return new List<string>();
            }

            return await _masterDb.RbacRoleGateStations.AsNoTracking()
                .Where(x => roleIds.Contains(x.RoleId))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.StationCode)
                .Select(x => x.StationCode)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        private async Task<ApplicationDbContext?> CreateTenantDbAsync(int? tenantId, CancellationToken cancellationToken)
        {
            Tenant? tenant = null;
            try
            {
                tenant = _tenantService.GetTenant();
            }
            catch
            {
                // Login and some master endpoints run without resolved tenant middleware context.
            }

            if (tenant == null && tenantId.HasValue)
            {
                tenant = await _masterDb.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
            }

            if (tenant == null)
            {
                return null;
            }

            var connectionString = _tenantService.BuildConnectionStringForTenant(tenant);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        private static async Task<Dictionary<string, GateStationMeta>> LoadCatalogMetaAsync(
            ApplicationDbContext tenantDb,
            CancellationToken cancellationToken)
        {
            var map = new Dictionary<string, GateStationMeta>(StringComparer.OrdinalIgnoreCase)
            {
                ["entry"] = new GateStationMeta
                {
                    StationCode = "entry",
                    NameAr = "بوابة الدخول",
                    NameEn = "Entry gate",
                    TicketCategory = ResortTicketCategories.Entry
                },
                ["games"] = new GateStationMeta
                {
                    StationCode = "games",
                    NameAr = "ألعاب (عام)",
                    NameEn = "Games (general)",
                    TicketCategory = ResortTicketCategories.Games
                },
                ["pool"] = new GateStationMeta
                {
                    StationCode = "pool",
                    NameAr = "المسبح",
                    NameEn = "Pool",
                    TicketCategory = ResortTicketCategories.Pool
                }
            };

            if (!await tenantDb.Database.CanConnectAsync(cancellationToken))
            {
                return map;
            }

            var types = new List<ResortTicketType>();
            try
            {
                types = await tenantDb.ResortTicketTypes.AsNoTracking()
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.Code)
                    .ToListAsync(cancellationToken);
            }
            catch
            {
                // Non-resort tenants may not have resort ticket tables yet.
            }

            foreach (var type in types)
            {
                var code = NormalizeStationCode(type.Code);
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                map[code] = new GateStationMeta
                {
                    StationCode = code,
                    NameAr = type.NameAr,
                    NameEn = type.NameEn,
                    TicketCategory = ResortTicketCategories.Normalize(type.TicketCategory)
                };
            }

            return map;
        }

        private static string NormalizeStationCode(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
