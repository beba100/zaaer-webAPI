using FinanceLedgerAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    public sealed class ReservationNotesService : IReservationNotesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IReservationActivityLogWriter _activityLog;

        public ReservationNotesService(
            ApplicationDbContext context,
            IReservationActivityLogWriter activityLog)
        {
            _context = context;
            _activityLog = activityLog;
        }

        public async Task<ReservationNotesListDto?> ListAsync(
            int reservationRouteId,
            int? hotelId,
            int? currentUserId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await FindReservationAsync(reservationRouteId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var matchIds = GetReservationNoteMatchIds(reservation);
            var rows = await _context.ReservationNotes
                .AsNoTracking()
                .Where(n => n.HotelId == reservation.HotelId && matchIds.Contains(n.ReservationId))
                .OrderBy(n => n.CreatedAt)
                .ThenBy(n => n.NoteId)
                .ToListAsync(cancellationToken);

            var notes = rows
                .Select(n => ToDto(n, currentUserId))
                .ToList();

            return new ReservationNotesListDto
            {
                Count = notes.Count,
                Notes = notes
            };
        }

        public async Task<int> CountAsync(
            int reservationRouteId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await FindReservationAsync(reservationRouteId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return 0;
            }

            var matchIds = GetReservationNoteMatchIds(reservation);
            return await _context.ReservationNotes
                .AsNoTracking()
                .CountAsync(
                    n => n.HotelId == reservation.HotelId && matchIds.Contains(n.ReservationId),
                    cancellationToken);
        }

        public async Task<ReservationNoteDto?> CreateAsync(
            CreateReservationNoteDto request,
            int? currentUserId,
            string? currentUserDisplayName,
            IFormFile? attachment = null,
            CancellationToken cancellationToken = default)
        {
            if (request.ReservationId <= 0)
            {
                throw new ArgumentException("ReservationId is required.");
            }

            var text = NormalizeNoteText(request.NoteText);
            var hasAttachment = attachment != null && attachment.Length > 0;
            if (string.IsNullOrWhiteSpace(text) && !hasAttachment)
            {
                throw new ArgumentException("Note text or an attachment is required.");
            }

            var reservation = await FindReservationAsync(request.ReservationId, request.HotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var storageReservationId = GetNoteStorageReservationId(reservation);
            var now = KsaTime.Now;
            var entity = new ReservationNote
            {
                HotelId = reservation.HotelId,
                ReservationId = storageReservationId,
                NoteType = NormalizeNoteType(request.NoteType),
                NoteText = text,
                CreatedByUserId = currentUserId is > 0 ? currentUserId : null,
                CreatedByName = ResolveDisplayName(currentUserId, currentUserDisplayName),
                CreatedAt = now
            };

            _context.ReservationNotes.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);

            if (hasAttachment)
            {
                await ApplyAttachmentAsync(entity, attachment!, cancellationToken);
            }

            var preview = text.Length > 80 ? text[..80] + "…" : text;
            await _activityLog.LogAsync(
                new ReservationActivityLogEntry
                {
                    EventKey = ReservationActivityEvents.NoteAdded,
                    HotelId = reservation.HotelId,
                    ReservationId = reservation.ReservationId,
                    ReservationNo = reservation.ReservationNo,
                    RefType = "Note",
                    RefId = entity.NoteId,
                    IconKey = "comment",
                    ActorDisplayName = entity.CreatedByName,
                    Payload = new Dictionary<string, object?>
                    {
                        ["reservationNo"] = reservation.ReservationNo,
                        ["noteType"] = entity.NoteType,
                        ["preview"] = preview
                    },
                    ZaaerId = reservation.ZaaerId
                },
                cancellationToken);

            return ToDto(entity, currentUserId);
        }

        public async Task<ReservationNoteDto?> UpdateAsync(
            int noteId,
            UpdateReservationNoteDto request,
            int? currentUserId,
            IFormFile? attachment = null,
            CancellationToken cancellationToken = default)
        {
            if (noteId <= 0)
            {
                throw new ArgumentException("NoteId is required.");
            }

            var text = NormalizeNoteText(request.NoteText);
            var hasNewAttachment = attachment != null && attachment.Length > 0;

            var reservation = await FindReservationAsync(request.ReservationId, request.HotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var matchIds = GetReservationNoteMatchIds(reservation);
            var entity = await _context.ReservationNotes
                .FirstOrDefaultAsync(
                    n =>
                        n.NoteId == noteId &&
                        n.HotelId == reservation.HotelId &&
                        matchIds.Contains(n.ReservationId),
                    cancellationToken);

            if (entity == null)
            {
                return null;
            }

            if (!CanMutateNote(entity, currentUserId))
            {
                throw new InvalidOperationException("You can only edit your own notes.");
            }

            if (string.IsNullOrWhiteSpace(text) && !hasNewAttachment && !HasStoredAttachment(entity) && !request.RemoveAttachment)
            {
                throw new ArgumentException("Note text or an attachment is required.");
            }

            if (string.IsNullOrWhiteSpace(text) && !hasNewAttachment && request.RemoveAttachment)
            {
                throw new ArgumentException("Note text or an attachment is required.");
            }

            if (!string.IsNullOrWhiteSpace(request.NoteType))
            {
                entity.NoteType = NormalizeNoteType(request.NoteType);
            }

            entity.NoteText = text;
            entity.UpdatedAt = KsaTime.Now;

            if (request.RemoveAttachment && !hasNewAttachment)
            {
                ReservationNoteAttachmentStorage.DeleteIfExists(entity.AttachmentPath);
                ClearAttachmentFields(entity);
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (hasNewAttachment)
            {
                if (HasStoredAttachment(entity))
                {
                    ReservationNoteAttachmentStorage.DeleteIfExists(entity.AttachmentPath);
                }

                await ApplyAttachmentAsync(entity, attachment!, cancellationToken);
            }

            return ToDto(entity, currentUserId);
        }

        public async Task<bool> DeleteAsync(
            int noteId,
            int reservationRouteId,
            int? hotelId,
            int? currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (noteId <= 0)
            {
                throw new ArgumentException("NoteId is required.");
            }

            var reservation = await FindReservationAsync(reservationRouteId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return false;
            }

            var matchIds = GetReservationNoteMatchIds(reservation);
            var entity = await _context.ReservationNotes
                .FirstOrDefaultAsync(
                    n =>
                        n.NoteId == noteId &&
                        n.HotelId == reservation.HotelId &&
                        matchIds.Contains(n.ReservationId),
                    cancellationToken);

            if (entity == null)
            {
                return false;
            }

            if (!CanMutateNote(entity, currentUserId))
            {
                throw new InvalidOperationException("You can only delete your own notes.");
            }

            ReservationNoteAttachmentStorage.DeleteIfExists(entity.AttachmentPath);
            _context.ReservationNotes.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task ApplyAttachmentAsync(
            ReservationNote entity,
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var saved = await ReservationNoteAttachmentStorage.SaveAsync(
                file,
                entity.HotelId,
                entity.ReservationId,
                entity.NoteId,
                cancellationToken);

            entity.AttachmentPath = saved.RelativePath;
            entity.AttachmentOriginalName = saved.OriginalFileName;
            entity.AttachmentContentType = saved.ContentType;
            entity.AttachmentFileSize = saved.FileSize;
            entity.UpdatedAt = KsaTime.Now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        private static void ClearAttachmentFields(ReservationNote entity)
        {
            entity.AttachmentPath = null;
            entity.AttachmentOriginalName = null;
            entity.AttachmentContentType = null;
            entity.AttachmentFileSize = null;
        }

        private static bool HasStoredAttachment(ReservationNote entity) =>
            !string.IsNullOrWhiteSpace(entity.AttachmentPath);

        private static bool CanMutateNote(ReservationNote entity, int? currentUserId) =>
            currentUserId is > 0 &&
            entity.CreatedByUserId is > 0 &&
            entity.CreatedByUserId.Value == currentUserId.Value;

        private static ReservationNoteDto ToDto(ReservationNote entity, int? currentUserId) =>
            new()
            {
                NoteId = entity.NoteId,
                NoteType = entity.NoteType,
                NoteText = entity.NoteText,
                AttachmentPath = entity.AttachmentPath,
                AttachmentOriginalName = entity.AttachmentOriginalName,
                AttachmentContentType = entity.AttachmentContentType,
                AttachmentFileSize = entity.AttachmentFileSize,
                HasAttachment = HasStoredAttachment(entity),
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByDisplayName = entity.CreatedByName,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                CanEdit = CanMutateNote(entity, currentUserId),
                CanDelete = CanMutateNote(entity, currentUserId)
            };

        private static string NormalizeNoteType(string? noteType)
        {
            var t = (noteType ?? ReservationNoteTypes.Internal).Trim().ToLowerInvariant();
            return t == ReservationNoteTypes.Guest ? ReservationNoteTypes.Guest : ReservationNoteTypes.Internal;
        }

        private static string NormalizeNoteText(string? text) => (text ?? string.Empty).Trim();

        private static string? ResolveDisplayName(int? userId, string? displayName)
        {
            var name = (displayName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Length > 200 ? name[..200] : name;
            }

            return userId is > 0 ? $"User #{userId}" : "System";
        }

        /// <summary>
        /// Global reservation id for storage: <c>zaaer_id</c> when present, else internal <c>reservation_id</c>.
        /// </summary>
        private static int GetNoteStorageReservationId(Reservation reservation) =>
            reservation.ZaaerId is > 0 ? reservation.ZaaerId.Value : reservation.ReservationId;

        /// <summary>
        /// Match keys for reads — same as discounts / detail (internal id + zaaer_id when both exist).
        /// New rows are stored with <see cref="GetNoteStorageReservationId"/> (zaaer_id when set).
        /// </summary>
        private static IReadOnlyList<int> GetReservationNoteMatchIds(Reservation reservation)
        {
            var refs = new List<int> { reservation.ReservationId };
            if (reservation.ZaaerId.HasValue && reservation.ZaaerId.Value != reservation.ReservationId)
            {
                refs.Add(reservation.ZaaerId.Value);
            }

            return refs;
        }

        private Task<Reservation?> FindReservationAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken) =>
            _context.Reservations
                .AsNoTracking()
                .Where(r =>
                    (r.ZaaerId == id || r.ReservationId == id) &&
                    (!hotelId.HasValue || r.HotelId == hotelId.Value))
                .OrderByDescending(r => r.ZaaerId == id ? 1 : 0)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
