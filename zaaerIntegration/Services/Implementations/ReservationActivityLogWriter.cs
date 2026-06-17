using System.Text.Json;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;
using zaaerIntegration.Security;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class ReservationActivityLogWriter : IReservationActivityLogWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly ApplicationDbContext _db;
        private readonly MasterDbContext _masterDb;
        private readonly ICurrentUserContext _currentUser;
        private readonly ILogger<ReservationActivityLogWriter> _logger;

        public ReservationActivityLogWriter(
            ApplicationDbContext db,
            MasterDbContext masterDb,
            ICurrentUserContext currentUser,
            ILogger<ReservationActivityLogWriter> logger)
        {
            _db = db;
            _masterDb = masterDb;
            _currentUser = currentUser;
            _logger = logger;
        }

        public async Task LogAsync(ReservationActivityLogEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EventKey))
            {
                return;
            }

            try
            {
                var profile = await ResolveActorProfileAsync(cancellationToken);
                var actorName = !string.IsNullOrWhiteSpace(entry.ActorDisplayName)
                    ? entry.ActorDisplayName.Trim()
                    : profile.DisplayLabel;

                var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser);
                var payload = BuildPayload(entry, profile, pmsUserId);
                var payloadJson = payload == null
                    ? null
                    : JsonSerializer.Serialize(payload, JsonOptions);

                var iconKey = string.IsNullOrWhiteSpace(entry.IconKey)
                    ? ReservationActivityEvents.DefaultIcon(entry.EventKey)
                    : entry.IconKey.Trim();

                var entity = new FinanceLedgerAPI.Models.ActivityLog
                {
                    HotelId = entry.HotelId,
                    EventKey = entry.EventKey.Trim(),
                    Message = string.Empty,
                    ReservationId = entry.ReservationId,
                    ReservationNo = entry.ReservationNo,
                    UnitId = entry.UnitId,
                    RefType = entry.RefType,
                    RefId = entry.RefId,
                    RefNo = entry.RefNo,
                    AmountFrom = entry.AmountFrom,
                    AmountTo = entry.AmountTo,
                    CreatedBy = actorName,
                    ActorUserId = pmsUserId,
                    PayloadJson = payloadJson,
                    IconKey = iconKey,
                    Source = string.IsNullOrWhiteSpace(entry.Source) ? "pms" : entry.Source.Trim(),
                    ZaaerId = entry.ZaaerId,
                    CreatedAt = KsaTime.Now
                };

                _db.ActivityLogs.Add(entity);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Activity log write failed for event {EventKey} reservation {ReservationId}",
                    entry.EventKey,
                    entry.ReservationId);
            }
        }

        private async Task<(string Username, string FirstName, string LastName, string DisplayLabel)> ResolveActorProfileAsync(
            CancellationToken cancellationToken)
        {
            var userId = PmsCurrentUser.ResolveUserId(_currentUser);
            if (userId is > 0)
            {
                var user = await _masterDb.RbacUsers.AsNoTracking()
                    .Where(u => u.UserId == userId.Value)
                    .Select(u => new { u.Username, u.FirstName, u.LastName })
                    .FirstOrDefaultAsync(cancellationToken);

                if (user != null)
                {
                    var username = (user.Username ?? string.Empty).Trim();
                    var first = (user.FirstName ?? string.Empty).Trim();
                    var last = (user.LastName ?? string.Empty).Trim();
                    var full = $"{first} {last}".Trim();
                    var display = !string.IsNullOrWhiteSpace(username) ? username : full;
                    if (string.IsNullOrWhiteSpace(display))
                    {
                        display = $"User #{userId.Value}";
                    }

                    return (username, first, last, display);
                }
            }

            if (!string.IsNullOrWhiteSpace(_currentUser.Username))
            {
                var u = _currentUser.Username.Trim();
                return (u, string.Empty, string.Empty, u);
            }

            var fallback = userId.HasValue ? $"User #{userId.Value}" : "System";
            return (string.Empty, string.Empty, string.Empty, fallback);
        }

        private static Dictionary<string, object?>? BuildPayload(
            ReservationActivityLogEntry entry,
            (string Username, string FirstName, string LastName, string DisplayLabel) profile,
            int? pmsUserId)
        {
            void ApplyActorFields(Dictionary<string, object?> dict)
            {
                if (!dict.ContainsKey("actorName") || dict["actorName"] == null)
                {
                    dict["actorName"] = profile.DisplayLabel;
                }

                if (!string.IsNullOrWhiteSpace(profile.Username))
                {
                    dict["actorUsername"] = profile.Username;
                }

                if (!string.IsNullOrWhiteSpace(profile.FirstName))
                {
                    dict["actorFirstName"] = profile.FirstName;
                }

                if (!string.IsNullOrWhiteSpace(profile.LastName))
                {
                    dict["actorLastName"] = profile.LastName;
                }

                var full = $"{profile.FirstName} {profile.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(full))
                {
                    dict["actorNameAr"] = full;
                }

                if (pmsUserId is > 0 && !dict.ContainsKey("pmsUserId"))
                {
                    dict["pmsUserId"] = pmsUserId.Value;
                }
            }

            if (entry.Payload is Dictionary<string, object?> dict)
            {
                ApplyActorFields(dict);
                return dict;
            }

            if (entry.Payload == null)
            {
                var created = new Dictionary<string, object?>
                {
                    ["reservationNo"] = entry.ReservationNo
                };
                ApplyActorFields(created);
                return created;
            }

            try
            {
                var json = JsonSerializer.Serialize(entry.Payload, JsonOptions);
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions)
                    ?? new Dictionary<string, object?>();
                ApplyActorFields(parsed);
                return parsed;
            }
            catch
            {
                var created = new Dictionary<string, object?>
                {
                    ["reservationNo"] = entry.ReservationNo
                };
                ApplyActorFields(created);
                return created;
            }
        }
    }
}
