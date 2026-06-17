using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Integrations;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Integrations
{
    public interface IPmsIntegrationResponsesService
    {
        Task<IReadOnlyList<PmsIntegrationResponseRowDto>> SearchAsync(
            PmsIntegrationResponseQueryDto query,
            CancellationToken cancellationToken = default);

        Task<PmsIntegrationResponseDetailDto?> GetByIdAsync(
            int responseId,
            CancellationToken cancellationToken = default);
    }

    public sealed class PmsIntegrationResponsesService : PmsHotelScopeService, IPmsIntegrationResponsesService
    {
        private readonly ApplicationDbContext _context;
        private readonly INtmpIntegrationSchemaEnsurer _schemaEnsurer;

        public PmsIntegrationResponsesService(
            ApplicationDbContext context,
            ITenantService tenantService,
            INtmpIntegrationSchemaEnsurer schemaEnsurer)
            : base(context, tenantService)
        {
            _context = context;
            _schemaEnsurer = schemaEnsurer;
        }

        public async Task<IReadOnlyList<PmsIntegrationResponseRowDto>> SearchAsync(
            PmsIntegrationResponseQueryDto query,
            CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelZaaerId = await GetCurrentHotelZaaerIdAsync(cancellationToken);
            var q = _context.IntegrationResponses.AsNoTracking()
                .Where(x => x.HotelId == hotelZaaerId
                    || (hotel.HotelId != hotelZaaerId && x.HotelId == hotel.HotelId));

            if (TryParseLocalDate(query.FromDate, out var from))
            {
                q = q.Where(x => x.CreatedAt >= from);
            }

            if (TryParseLocalDate(query.ToDate, out var to))
            {
                var end = to.Date.AddDays(1).AddTicks(-1);
                q = q.Where(x => x.CreatedAt <= end);
            }

            if (!string.IsNullOrWhiteSpace(query.BookingNo))
            {
                var bn = query.BookingNo.Trim();
                q = q.Where(x => x.ResNo != null && x.ResNo.Contains(bn));
            }

            if (!string.IsNullOrWhiteSpace(query.Service) && !string.Equals(query.Service, "all", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => x.Service == query.Service);
            }

            if (!string.IsNullOrWhiteSpace(query.EventType))
            {
                var et = query.EventType.Trim();
                q = q.Where(x => x.EventType != null && x.EventType.Contains(et));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                q = q.Where(x => x.Status == query.Status);
            }

            var take = query.Take <= 0 ? 100 : Math.Min(query.Take, 500);
            var skip = Math.Max(0, query.Skip);

            var rows = await q.OrderByDescending(x => x.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return await MapRowsWithReservationLinksAsync(rows, hotel, cancellationToken);
        }

        public async Task<PmsIntegrationResponseDetailDto?> GetByIdAsync(
            int responseId,
            CancellationToken cancellationToken = default)
        {
            await _schemaEnsurer.EnsureAsync(cancellationToken);
            var hotel = await GetCurrentHotelSettingsAsync(cancellationToken);
            var hotelZaaerId = await GetCurrentHotelZaaerIdAsync(cancellationToken);
            var row = await _context.IntegrationResponses.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ResponseId == responseId
                        && (x.HotelId == hotelZaaerId
                            || (hotel.HotelId != hotelZaaerId && x.HotelId == hotel.HotelId)),
                    cancellationToken);
            if (row == null)
            {
                return null;
            }

            var mapped = await MapRowsWithReservationLinksAsync(
                new[] { row },
                hotel,
                cancellationToken);
            var enriched = mapped[0];
            return new PmsIntegrationResponseDetailDto
            {
                ResponseId = enriched.ResponseId,
                ResNo = enriched.ResNo,
                ReservationRouteId = enriched.ReservationRouteId,
                HotelCode = enriched.HotelCode,
                Service = enriched.Service,
                EventType = enriched.EventType,
                UnitNumber = enriched.UnitNumber,
                Guest = enriched.Guest,
                ErrorMessage = enriched.ErrorMessage,
                Status = enriched.Status,
                CreatedAt = enriched.CreatedAt,
                HttpStatusCode = enriched.HttpStatusCode,
                CorrelationId = enriched.CorrelationId,
                RequestPayload = row.RequestPayload,
                ResponsePayload = row.ResponsePayload
            };
        }

        private static bool TryParseLocalDate(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out date))
            {
                return true;
            }

            return DateTime.TryParse(value, out date);
        }

        private static PmsIntegrationResponseRowDto MapRow(FinanceLedgerAPI.Models.IntegrationResponse e) => new()
        {
            ResponseId = e.ResponseId,
            ResNo = e.ResNo,
            Service = e.Service,
            EventType = e.EventType,
            UnitNumber = e.UnitNumber,
            Guest = e.Guest,
            ErrorMessage = e.ErrorMessage,
            Status = e.Status,
            CreatedAt = e.CreatedAt,
            HttpStatusCode = e.HttpStatusCode,
            CorrelationId = e.CorrelationId
        };

        private async Task<IReadOnlyList<PmsIntegrationResponseRowDto>> MapRowsWithReservationLinksAsync(
            IReadOnlyList<FinanceLedgerAPI.Models.IntegrationResponse> entities,
            HotelSettings hotel,
            CancellationToken cancellationToken)
        {
            if (entities.Count == 0)
            {
                return Array.Empty<PmsIntegrationResponseRowDto>();
            }

            var hotelZaaerId = hotel.ZaaerId ?? hotel.HotelId;
            var hotelIds = new HashSet<int> { hotel.HotelId, hotelZaaerId };
            var hotelCode = string.IsNullOrWhiteSpace(hotel.HotelCode) ? null : hotel.HotelCode.Trim();

            var resNos = entities
                .Select(e => e.ResNo?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var routeByResNo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (resNos.Count > 0)
            {
                var matches = await _context.Reservations.AsNoTracking()
                    .Where(r => hotelIds.Contains(r.HotelId) && r.ReservationNo != null && resNos.Contains(r.ReservationNo))
                    .Select(r => new { r.ReservationNo, r.ReservationId, r.ZaaerId })
                    .ToListAsync(cancellationToken);

                foreach (var match in matches)
                {
                    if (string.IsNullOrWhiteSpace(match.ReservationNo))
                    {
                        continue;
                    }

                    var routeId = match.ZaaerId is > 0 ? match.ZaaerId.Value : match.ReservationId;
                    routeByResNo[match.ReservationNo] = routeId;
                }
            }

            return entities
                .Select(e =>
                {
                    var row = MapRow(e);
                    row.HotelCode = hotelCode;
                    var resNo = e.ResNo?.Trim();
                    if (!string.IsNullOrWhiteSpace(resNo) && routeByResNo.TryGetValue(resNo, out var routeId))
                    {
                        row.ReservationRouteId = routeId;
                    }

                    return row;
                })
                .ToList();
        }
    }
}
