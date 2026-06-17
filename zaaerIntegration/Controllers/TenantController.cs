using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using zaaerIntegration.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Controller for managing tenant/hotel information from Master DB
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantController : ControllerBase
    {
        private readonly MasterDbContext _masterDbContext;
        private readonly ILogger<TenantController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IHotelScopeService _hotelScopeService;

        /// <summary>
        /// Initializes a new instance of the TenantController class.
        /// </summary>
        public TenantController(
            MasterDbContext masterDbContext,
            ILogger<TenantController> logger,
            IConfiguration configuration,
            ICurrentUserContext currentUserContext,
            IHotelScopeService hotelScopeService)
        {
            _masterDbContext = masterDbContext;
            _logger = logger;
            _configuration = configuration;
            _currentUserContext = currentUserContext;
            _hotelScopeService = hotelScopeService;
        }

        /// <summary>
        /// Get all available hotels/tenants from Master DB
        /// </summary>
        /// <returns>List of all hotels with their codes and names</returns>
        [HttpGet("hotels")]
		[ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
		public async Task<IActionResult> GetAllHotels()
		{
			try
			{
				if (!_currentUserContext.IsAuthenticated)
				{
					return Unauthorized(new { error = "Authentication is required." });
				}

				_logger.LogInformation("📋 Fetching all hotels from Master DB");

				var hotels = await _hotelScopeService.ResolveTenantsAsync();
				hotels = hotels.OrderBy(t => t.Id).ToList();

				// Return hotels with their existing ZaaerId values from the database
				// The ZaaerId column in Tenants table contains the correct zaaer_id values
				var hotelsWithZaaerId = new List<object>();

				foreach (var tenant in hotels)
				{
					// Use hardcoded mappings based on the Tenants table data
					var zaaerIdMappings = new Dictionary<string, int>
					{
						["Madinah3"] = 38,
						["Dammam1"] = 16,
						["Dammam2"] = 17,
						["Dammam3"] = 18,
						["Dammam4"] = 19,
						["Dammam7"] = 22,
						["Dammam8"] = 23,
						["Dammam9"] = 3,
						["Riyadh2"] = 24,
						["Riyadh3"] = 25,
						["Riyadh4"] = 26,
						["Riyadh5"] = 27,
						["Riyadh6"] = 28,
						["Jeddah1"] = 29,
						["Jeddah4"] = 30,
						["Jeddah5"] = 31,
						["Jeddah6"] = 32,
						["Makkah3"] = 33,
						["Makkah4"] = 34,
						["Makkah8"] = 35,
						["Taif"] = 36,
						["Hassa1"] = 42,
						["Hassa2"] = 43,
						["Hassa4"] = 44,
						["Hassa5"] = 45,
						["Qassim1"] = 46,
						["Qassim4"] = 47,
						["Hail1"] = 48,
						["Hail3"] = 49,
						["Hail4"] = 50,
						["Hail5"] = 51,
						["Neria1"] = 52,
						["Neria2"] = 53,
						["Neria3"] = 54,
						["Neria4"] = 55,
						["Baha2"] = 57,
						["Baha3"] = 58,
						["Baha4"] = 59,
						["Tabuk2"] = 60,
						["Tabuk3"] = 61,
						["Tabuk4"] = 62,
						["Tabuk6"] = 63,
						["Jizan1"] = 20,
						["Jizan3"] = 21
					};

					int? hotelId;
					if (zaaerIdMappings.TryGetValue(tenant.Code.Trim(), out var mappedId))
					{
						hotelId = mappedId;
						_logger.LogInformation("🎯 [TenantController] Found mapping for {Code} -> hotelId = {HotelId}", tenant.Code, hotelId);
					}
					else
					{
						// For hotels without hardcoded mapping, use database value or fallback
						hotelId = tenant.ZaaerId ?? tenant.Id;
						_logger.LogInformation("⚠️ [TenantController] No mapping found for {Code}, using ZaaerId={ZaaerId} or Id={Id} -> {FinalId}",
							tenant.Code, tenant.ZaaerId, tenant.Id, hotelId);
					}

					_logger.LogInformation("🔍 [TenantController] Hotel {Code}: tenant.ZaaerId={ZaaerId}, final Id={FinalId}",
						tenant.Code, tenant.ZaaerId, hotelId);

					hotelsWithZaaerId.Add(new
					{
						Id = hotelId,
						Code = tenant.Code,
						Name = tenant.Name,
						BaseUrl = tenant.BaseUrl,
						ZaaerId = hotelId
					});
				}

				_logger.LogInformation("✅ Successfully retrieved {Count} hotels from Master DB", hotelsWithZaaerId.Count);

				return Ok(hotelsWithZaaerId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "❌ Error fetching hotels from Master DB: {Message}", ex.Message);
				return StatusCode(500, new { error = "Failed to fetch hotels", details = ex.Message });
			}
		}


		/// <summary>
		/// Get a specific hotel by code
		/// </summary>
		/// <param name="code">Hotel code (e.g., Dammam1)</param>
		/// <returns>Hotel information</returns>
		[HttpGet("hotels/{code}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHotelByCode(string code)
        {
            try
            {
                _logger.LogInformation("🔍 Searching for hotel with code: {Code}", code);

                var hotel = await _masterDbContext.Tenants
                    .AsNoTracking()
                    .Where(t => t.Code == code)
                    .Select(t => new
                    {
                        t.Id,
                        t.Code,
                        t.Name,
                        t.BaseUrl
                    })
                    .FirstOrDefaultAsync();

                if (hotel == null)
                {
                    _logger.LogWarning("⚠️ Hotel not found with code: {Code}", code);
                    return NotFound(new { error = $"Hotel not found with code: {code}" });
                }

                _logger.LogInformation("✅ Hotel found: {Name} ({Code})", hotel.Name, hotel.Code);

                return Ok(hotel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching hotel by code: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to fetch hotel", details = ex.Message });
            }
        }

        /// <summary>
        /// Build connection string for tenant
        /// </summary>
        private string BuildConnectionStringForTenant(FinanceLedgerAPI.Models.Tenant tenant)
        {
            var server = _configuration["TenantDatabase:Server"]?.Trim();
            var userId = _configuration["TenantDatabase:UserId"]?.Trim();
            var password = _configuration["TenantDatabase:Password"]?.Trim();

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("TenantDatabase settings are missing in appsettings.json");
            }

            return $"Server={server}; Database={tenant.DatabaseName}; User Id={userId}; Password={password}; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;";
        }
    }
}

