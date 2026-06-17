#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.Pms.ReservationDetail
{
    public sealed class ReservationNoteDto
    {
        public int NoteId { get; init; }

        public string NoteType { get; init; } = "internal";

        public string NoteText { get; init; } = string.Empty;

        public string? AttachmentPath { get; init; }

        public string? AttachmentOriginalName { get; init; }

        public string? AttachmentContentType { get; init; }

        public long? AttachmentFileSize { get; init; }

        public bool HasAttachment { get; init; }

        public int? CreatedByUserId { get; init; }

        public string? CreatedByDisplayName { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? UpdatedAt { get; init; }

        public bool CanEdit { get; init; }

        public bool CanDelete { get; init; }
    }

    public sealed class ReservationNotesListDto
    {
        public int Count { get; init; }

        public IReadOnlyList<ReservationNoteDto> Notes { get; init; } = Array.Empty<ReservationNoteDto>();
    }

    public sealed class CreateReservationNoteDto
    {
        public int ReservationId { get; set; }

        public int? HotelId { get; set; }

        public string NoteType { get; set; } = "internal";

        public string NoteText { get; set; } = string.Empty;
    }

    public sealed class UpdateReservationNoteDto
    {
        public int ReservationId { get; set; }

        public int? HotelId { get; set; }

        public string? NoteType { get; set; }

        public string NoteText { get; set; } = string.Empty;

        public bool RemoveAttachment { get; set; }
    }

    /// <summary>Multipart form for create/update note with optional file.</summary>
    public sealed class ReservationNoteFormRequest
    {
        public int ReservationId { get; set; }

        public int? HotelId { get; set; }

        public string NoteType { get; set; } = "internal";

        public string? NoteText { get; set; }

        public bool RemoveAttachment { get; set; }
    }
}
