namespace zaaerIntegration.Services
{
    /// <summary>
    /// Saves reservation note attachments (images / PDF) under wwwroot/uploads/reservation-notes.
    /// </summary>
    public static class ReservationNoteAttachmentStorage
    {
        public const long MaxFileBytes = 10 * 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".pdf"
        };

        public sealed record SavedAttachment(
            string RelativePath,
            string OriginalFileName,
            string ContentType,
            long FileSize);

        public static bool IsAllowedExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrWhiteSpace(ext) && AllowedExtensions.Contains(ext);
        }

        public static async Task<SavedAttachment> SaveAsync(
            IFormFile file,
            int hotelId,
            int storageReservationId,
            int noteId,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
            {
                throw new ArgumentException("Attachment file is empty.");
            }

            if (file.Length > MaxFileBytes)
            {
                throw new ArgumentException($"Attachment exceeds maximum size ({MaxFileBytes / (1024 * 1024)} MB).");
            }

            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName) || !IsAllowedExtension(originalName))
            {
                throw new ArgumentException("Only image files (JPG, PNG, GIF, WebP, BMP) and PDF are allowed.");
            }

            var ext = Path.GetExtension(originalName);
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reservation-notes");
            Directory.CreateDirectory(uploadsRoot);

            var safeHotel = hotelId > 0 ? hotelId : 0;
            var fileName = $"{safeHotel}_{storageReservationId}_{noteId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(uploadsRoot, fileName);
            var relativePath = $"/uploads/reservation-notes/{fileName}";

            await using (var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? GuessContentType(ext)
                : file.ContentType;

            return new SavedAttachment(
                relativePath,
                originalName.Length > 255 ? originalName[..255] : originalName,
                contentType.Length > 100 ? contentType[..100] : contentType,
                file.Length);
        }

        public static void DeleteIfExists(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var normalized = relativePath.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("/uploads/reservation-notes/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var physical = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "reservation-notes",
                fileName);

            if (File.Exists(physical))
            {
                File.Delete(physical);
            }
        }

        private static string GuessContentType(string ext) =>
            ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "image/jpeg";
    }
}
