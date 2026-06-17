using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace zaaerIntegration.Controllers
{
	/// <summary>
	/// Configuration controller for frontend settings
	/// </summary>
	[ApiController]
	[Route("api/config")]
	public class ConfigController : ControllerBase
	{
		private readonly IConfiguration _configuration;
		private readonly ILogger<ConfigController> _logger;

		public ConfigController(IConfiguration configuration, ILogger<ConfigController> logger)
		{
			_configuration = configuration;
			_logger = logger;
		}

		/// <summary>
		/// Get DevExtreme license key
		/// </summary>
		[HttpGet("devextreme-license")]
		[AllowAnonymous]
		public IActionResult GetDevExtremeLicense()
		{
			try
			{
				var licenseKey = _configuration["DevExtreme:LicenseKey"];
				if (string.IsNullOrWhiteSpace(licenseKey))
				{
					_logger.LogWarning("DevExtreme license key is not configured");
					return NotFound(new { error = "DevExtreme license key is not configured" });
				}

				return Ok(new { licenseKey });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving DevExtreme license key");
				return StatusCode(500, new { error = "Failed to retrieve license key" });
			}
		}
	}
}

