namespace zaaerIntegration.DTOs.Auth
{
  public class AvailableHotelDto
  {
    public int TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameEn { get; set; }
  }
}
