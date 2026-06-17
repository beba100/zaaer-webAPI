using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// NTMP gateway credentials/settings per hotel
    /// </summary>
    [Table("ntmp_details")]
    public class NtmpDetails
    {
        [Key]
        [Column("details_id")]
        public int DetailsId { get; set; }

        [Required]
        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(300)]
        [Column("gateway_api_key")]
        public string? GatewayApiKey { get; set; }

        [MaxLength(150)]
        [Column("user_name")]
        public string? UserName { get; set; }

        [MaxLength(300)]
        [Column("password_hash")]
        public string? PasswordHash { get; set; }

        /// <summary>Reversible secret for NTMP Basic Auth (ASP.NET Data Protection).</summary>
        [Column("password_encrypted")]
        public byte[]? PasswordEncrypted { get; set; }

        /// <summary>dev | staging | production</summary>
        [MaxLength(20)]
        [Column("api_environment")]
        public string ApiEnvironment { get; set; } = "production";

        [MaxLength(256)]
        [Column("channel_name")]
        public string? ChannelName { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Zaaer System ID (معرف Zaaer)
        /// External ID from Zaaer integration system
        /// </summary>
        [Column("zaaer_id")]
        public int? ZaaerId { get; set; }

        /// <summary>Not mapped — <see cref="HotelId"/> is Zaaer property id, not <c>hotel_settings.hotel_id</c>.</summary>
        [NotMapped]
        public HotelSettings? HotelSettings { get; set; }

    }
}


