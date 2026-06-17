using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceLedgerAPI.Models
{
    /// <summary>
    /// EGS (Electronic Generation Solution) unit registered with ZATCA per hotel and environment.
    /// </summary>
    [Table("zatca_devices")]
    public class ZatcaDevice
    {
        [Key]
        [Column("device_id")]
        public int DeviceId { get; set; }

        [Column("hotel_id")]
        public int HotelId { get; set; }

        [Column("device_uuid")]
        [MaxLength(100)]
        public string DeviceUuid { get; set; } = string.Empty;

        /// <summary>sandbox | simulation | production</summary>
        [Column("environment")]
        [MaxLength(20)]
        public string Environment { get; set; } = "sandbox";

        [Column("device_status")]
        [MaxLength(30)]
        public string DeviceStatus { get; set; } = "pending_onboarding";

        [Column("csr_pem")]
        public string? CsrPem { get; set; }

        [Column("private_key_encrypted")]
        public byte[]? PrivateKeyEncrypted { get; set; }

        [Column("certificate_pem")]
        public string? CertificatePem { get; set; }

        [Column("compliance_request_id")]
        [MaxLength(100)]
        public string? ComplianceRequestId { get; set; }

        /// <summary>Base64 certificate token from ZATCA (binarySecurityToken) — can exceed 2KB.</summary>
        [Column("compliance_csid")]
        public string? ComplianceCsid { get; set; }

        [Column("compliance_secret")]
        [MaxLength(1000)]
        public string? ComplianceSecret { get; set; }

        /// <summary>Base64 certificate token from ZATCA (binarySecurityToken).</summary>
        [Column("production_csid")]
        public string? ProductionCsid { get; set; }

        [Column("production_secret")]
        [MaxLength(1000)]
        public string? ProductionSecret { get; set; }

        [Column("last_invoice_hash")]
        [MaxLength(512)]
        public string? LastInvoiceHash { get; set; }

        [Column("last_icv")]
        public int LastIcv { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = KsaTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ZatcaInvoiceHashHistory> HashHistory { get; set; } = new List<ZatcaInvoiceHashHistory>();
    }
}
