using System.ComponentModel.DataAnnotations;

namespace zaaerIntegration.DTOs.Auth
{
  public class SwitchHotelRequestDto
  {
    public string? HotelCode { get; set; }

    public int? TenantId { get; set; }
  }
}
