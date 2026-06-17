namespace zaaerIntegration.Services
{
    /// <summary>
    /// Saves booking engine images under <c>wwwroot/uploads/booking/{hotelId}/</c>.
    /// </summary>
    public static class BookingEngineImageStorage
    {
        public const long MaxFileBytes = 2 * 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        public sealed record SavedImage(string RelativePath, string ContentType, long FileSize);

        public static async Task<SavedImage> SaveAsync(
            IFormFile file,
            int hotelId,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length <= 0)
            {
                throw new ArgumentException("Image file is empty.");
            }

            if (file.Length > MaxFileBytes)
            {
                throw new ArgumentException($"Image exceeds maximum size ({MaxFileBytes / (1024 * 1024)} MB).");
            }

            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName) || !AllowedExtensions.Contains(Path.GetExtension(originalName)))
            {
                throw new ArgumentException("Only image files (JPG, PNG, GIF, WebP, BMP) are allowed.");
            }

            var ext = Path.GetExtension(originalName).ToLowerInvariant();
            var hotelFolder = hotelId > 0 ? hotelId.ToString() : "0";
            var uploadsRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "booking",
                hotelFolder);
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(uploadsRoot, fileName);
            var relativePath = $"/uploads/booking/{hotelFolder}/{fileName}";

            await using (var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            return new SavedImage(relativePath, file.ContentType ?? "image/jpeg", file.Length);
        }
    }
}
