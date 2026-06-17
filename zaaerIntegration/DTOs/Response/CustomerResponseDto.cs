namespace zaaerIntegration.DTOs.Response
{
    /// <summary>
    /// Customer Response DTO
    /// ‰„Ê–Ã «” Ã«»… «·⁄„Ì·
    /// </summary>
    public class CustomerResponseDto
    {
        public int CustomerId { get; set; }

        /// <summary>External Zaaer integration id (<c>customers.zaaer_id</c>).</summary>
        public int? ZaaerId { get; set; }

        public string? CustomerNo { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public int? GtypeId { get; set; }
        public int? NId { get; set; }
        public int? IdType { get; set; }
        public int? GuestCategoryId { get; set; }
        public string? VisaNo { get; set; }
        public string? MobileNo { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Comments { get; set; }
        public int? EnteredBy { get; set; }
        public DateTime? EnteredAt { get; set; }
        public string? Gender { get; set; }
        public DateTime? Birthday { get; set; }
        public DateTime? BirthdateGregorian { get; set; }

        /// <summary>Hijri birth date stored as text (e.g. yyyy/MM/dd per Um Al Qura).</summary>
        public string? BirthdateHijri { get; set; }

        // Related data
        public string? GuestTypeName { get; set; }
        public string? NationalityName { get; set; }

        /// <summary>Arabic nationality label (<c>n_name_ar</c>).</summary>
        public string? NationalityNameAr { get; set; }
        public string? IdTypeName { get; set; }
        public string? GuestCategoryName { get; set; }

        public IReadOnlyList<CustomerIdentificationResponseDto>? Identifications { get; set; }
    }
}
