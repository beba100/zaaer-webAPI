using System.Text.Json;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ActivityLog;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class ReservationActivityLogQueryService : IReservationActivityLogQueryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _db;
        private readonly MasterDbContext _masterDb;

        public ReservationActivityLogQueryService(ApplicationDbContext db, MasterDbContext masterDb)
        {
            _db = db;
            _masterDb = masterDb;
        }

        public async Task<IReadOnlyList<PmsActivityLogItemDto>> ListForReservationAsync(
            int reservationRouteId,
            int? hotelId,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            var reservation = await _db.Reservations.AsNoTracking()
                .Where(r =>
                    (r.ZaaerId == reservationRouteId || r.ReservationId == reservationRouteId) &&
                    (!hotelId.HasValue || r.HotelId == hotelId.Value))
                .OrderByDescending(r => r.ZaaerId == reservationRouteId ? 1 : 0)
                .Select(r => new { r.ReservationId, r.HotelId, r.ZaaerId })
                .FirstOrDefaultAsync(cancellationToken);

            if (reservation == null)
            {
                return Array.Empty<PmsActivityLogItemDto>();
            }

            var keys = new List<int> { reservation.ReservationId };
            if (reservation.ZaaerId.HasValue && reservation.ZaaerId.Value != reservation.ReservationId)
            {
                keys.Add(reservation.ZaaerId.Value);
            }

            take = Math.Clamp(take, 1, 200);
            skip = Math.Max(0, skip);

            var rows = await _db.ActivityLogs.AsNoTracking()
                .Where(a =>
                    a.HotelId == reservation.HotelId &&
                    a.ReservationId.HasValue &&
                    keys.Contains(a.ReservationId.Value))
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.LogId)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return await EnrichActorProfilesAsync(rows.Select(ToDto).ToList(), cancellationToken);
        }

        public async Task<IReadOnlyList<PmsActivityLogItemDto>> SearchAsync(
            PmsActivityLogQueryDto query,
            CancellationToken cancellationToken = default)
        {
            query ??= new PmsActivityLogQueryDto();
            var take = Math.Clamp(query.Take, 1, 200);
            var skip = Math.Max(0, query.Skip);

            var q = _db.ActivityLogs.AsNoTracking().AsQueryable();

            if (query.HotelId.HasValue)
            {
                q = q.Where(a => a.HotelId == query.HotelId.Value);
            }

            if (query.ReservationId.HasValue)
            {
                q = q.Where(a => a.ReservationId == query.ReservationId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.ReservationNo))
            {
                var no = query.ReservationNo.Trim();
                q = q.Where(a => a.ReservationNo == no);
            }

            if (!string.IsNullOrWhiteSpace(query.EventKey))
            {
                q = q.Where(a => a.EventKey == query.EventKey.Trim());
            }

            if (query.ActorUserId.HasValue)
            {
                q = q.Where(a => a.ActorUserId == query.ActorUserId.Value);
            }

            if (query.DateFrom.HasValue)
            {
                q = q.Where(a => a.CreatedAt >= query.DateFrom.Value);
            }

            if (query.DateTo.HasValue)
            {
                q = q.Where(a => a.CreatedAt <= query.DateTo.Value);
            }

            var rows = await q
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.LogId)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return await EnrichActorProfilesAsync(rows.Select(ToDto).ToList(), cancellationToken);
        }

        private async Task<IReadOnlyList<PmsActivityLogItemDto>> EnrichActorProfilesAsync(
            List<PmsActivityLogItemDto> items,
            CancellationToken cancellationToken)
        {
            if (items.Count == 0)
            {
                return items;
            }

            var actorIds = items
                .Where(i => i.ActorUserId is > 0)
                .Select(i => i.ActorUserId!.Value)
                .Distinct()
                .ToList();

            if (actorIds.Count == 0)
            {
                return items;
            }

            var users = await _masterDb.RbacUsers.AsNoTracking()
                .Where(u => actorIds.Contains(u.UserId))
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.FirstName,
                    u.LastName
                })
                .ToListAsync(cancellationToken);

            var map = users.ToDictionary(u => u.UserId);

            var enriched = new List<PmsActivityLogItemDto>(items.Count);
            foreach (var item in items)
            {
                if (item.ActorUserId is not > 0 || !map.TryGetValue(item.ActorUserId.Value, out var user))
                {
                    enriched.Add(item);
                    continue;
                }

                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                var payload = new Dictionary<string, object?>(item.Payload, StringComparer.OrdinalIgnoreCase)
                {
                    ["actorUsername"] = user.Username,
                    ["actorFirstName"] = user.FirstName,
                    ["actorLastName"] = user.LastName
                };

                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    payload["actorNameAr"] = fullName;
                }

                enriched.Add(new PmsActivityLogItemDto
                {
                    LogId = item.LogId,
                    HotelId = item.HotelId,
                    EventKey = item.EventKey,
                    IconKey = item.IconKey,
                    Payload = payload,
                    ActorDisplayName = item.ActorDisplayName,
                    ActorUsername = user.Username,
                    ActorFirstName = user.FirstName,
                    ActorLastName = user.LastName,
                    ActorUserId = item.ActorUserId,
                    ReservationId = item.ReservationId,
                    ReservationNo = item.ReservationNo,
                    UnitId = item.UnitId,
                    RefType = item.RefType,
                    RefId = item.RefId,
                    RefNo = item.RefNo,
                    AmountFrom = item.AmountFrom,
                    AmountTo = item.AmountTo,
                    CreatedAt = item.CreatedAt,
                    Source = item.Source
                });
            }

            return enriched;
        }

        private static PmsActivityLogItemDto ToDto(FinanceLedgerAPI.Models.ActivityLog row)
        {
            return new PmsActivityLogItemDto
            {
                LogId = row.LogId,
                HotelId = row.HotelId,
                EventKey = row.EventKey,
                IconKey = row.IconKey,
                Payload = ParsePayload(row.PayloadJson, row.CreatedBy),
                ActorDisplayName = row.CreatedBy,
                ActorUserId = row.ActorUserId,
                ReservationId = row.ReservationId,
                ReservationNo = row.ReservationNo,
                UnitId = row.UnitId,
                RefType = row.RefType,
                RefId = row.RefId,
                RefNo = row.RefNo,
                AmountFrom = row.AmountFrom,
                AmountTo = row.AmountTo,
                CreatedAt = row.CreatedAt,
                Source = row.Source
            };
        }

        private static Dictionary<string, object?> ParsePayload(string? json, string? createdBy)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.IsNullOrWhiteSpace(createdBy)
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?> { ["actorName"] = createdBy };
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions)
                    ?? new Dictionary<string, object?>();
            }
            catch
            {
                return new Dictionary<string, object?> { ["raw"] = json };
            }
        }
    }
}
