using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using zaaerIntegration.DTOs.Zaaer;
using zaaerIntegration.Services.Zaaer;
using zaaerIntegration.Services.PartnerQueueing;

namespace zaaerIntegration.Controllers.Zaaer
{
	[ApiController]
	[Route("api/zaaer/[controller]")]
	public class ZaaerRateTypeController : ControllerBase
	{
		private readonly IZaaerRateTypeService _service;
		private readonly ILogger<ZaaerRateTypeController> _logger;
		private readonly IPartnerQueueService _queueService;
		private readonly IQueueSettingsProvider _queueSettings;

		public ZaaerRateTypeController(IZaaerRateTypeService service, ILogger<ZaaerRateTypeController> logger, IPartnerQueueService queueService, IQueueSettingsProvider queueSettings)
		{
			_service = service;
			_logger = logger;
			_queueService = queueService;
			_queueSettings = queueSettings;
		}

	[HttpPost]
	public async Task<IActionResult> Create([FromBody] ZaaerCreateRateTypeDto dto)
	{
		if (!ModelState.IsValid) return BadRequest(ModelState);
		var queueSettings = _queueSettings.GetSettings();
		if (queueSettings.EnableQueueMode)
		{
			var q = new EnqueuePartnerRequestDto
			{
				Partner = queueSettings.DefaultPartner,
				Operation = "/api/zaaer/ZaaerRateType",
				OperationKey = "Zaaer.RateType.Create",
				PayloadType = nameof(ZaaerCreateRateTypeDto),
				PayloadJson = JsonSerializer.Serialize(dto),
				HotelId = dto.HotelId
			};
			await _queueService.EnqueueAsync(q);
			return Accepted(new { queued = true, requestRef = q.RequestRef });
		}
		var result = await _service.CreateAsync(dto);
		return Ok(result);
	}

	/// <summary>
	/// Updates a rate type by ZaaerId from route parameter. This is the primary endpoint that Zaaer uses: PUT /api/zaaer/ZaaerRateType/{zaaerId}
	/// Zaaer sends: PUT https://aleairy.tryasp.net/api/zaaer/ZaaerRateType/12
	/// IMPORTANT: Route parameter is without type constraint to ensure proper routing matching.
	/// </summary>
	[HttpPut("{zaaerId}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Update([FromRoute] int zaaerId, [FromBody] ZaaerUpdateRateTypeDto dto)
	{
		// If zaaerId is provided in body, it should match the route parameter
		if (dto.ZaaerId.HasValue && dto.ZaaerId.Value != zaaerId)
		{
			return BadRequest("ZaaerId mismatch between route and body");
		}

		// Set zaaerId from route to body if not provided
		if (!dto.ZaaerId.HasValue)
		{
			dto.ZaaerId = zaaerId;
		}

		// Handle Zaaer's alternative format: if UnitItems is null/empty but root-level unit fields exist, create UnitItems array
		if ((dto.UnitItems == null || dto.UnitItems.Count == 0) && 
		    !string.IsNullOrWhiteSpace(dto.UnitTypeName) && 
		    dto.Rate.HasValue)
		{
			dto.UnitItems = new List<ZaaerRateTypeUnitItemDto>
			{
				new ZaaerRateTypeUnitItemDto
				{
					UnitTypeName = dto.UnitTypeName!,
					Rate = dto.Rate.Value,
					IsEnabled = dto.IsEnabled ?? true
				}
			};
		}

		// Ensure UnitItems is not null (default to empty list if still null)
		dto.UnitItems ??= new List<ZaaerRateTypeUnitItemDto>();

		if (!ModelState.IsValid) return BadRequest(ModelState);

		var queueSettings = _queueSettings.GetSettings();
		if (queueSettings.EnableQueueMode)
		{
			var q = new EnqueuePartnerRequestDto
			{
				Partner = queueSettings.DefaultPartner,
				Operation = $"/api/zaaer/ZaaerRateType/{zaaerId}",
				OperationKey = "Zaaer.RateType.UpdateByZaaerId",
				TargetId = zaaerId,
				PayloadType = nameof(ZaaerUpdateRateTypeDto),
				PayloadJson = JsonSerializer.Serialize(dto),
				HotelId = dto.HotelId
			};
			await _queueService.EnqueueAsync(q);
			return Accepted(new { queued = true, requestRef = q.RequestRef });
		}

		// Use update by zaaerId directly
		var result = await _service.UpdateByZaaerIdAsync(zaaerId, dto);
		if (result == null) return NotFound($"Rate type with ZaaerId {zaaerId} not found");
		return Ok(result);
	}

		/// <summary>
		/// Deletes a rate type by ZaaerId.
		/// Zaaer sends: DELETE https://aleairy.tryasp.net/api/zaaer/ZaaerRateType/{zaaerId}
		/// </summary>
		[HttpDelete("{zaaerId}")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Delete([FromRoute] int zaaerId)
		{
			var queueSettings = _queueSettings.GetSettings();
			if (queueSettings.EnableQueueMode)
			{
				var q = new EnqueuePartnerRequestDto
				{
					Partner = queueSettings.DefaultPartner,
					Operation = $"/api/zaaer/ZaaerRateType/{zaaerId}",
					OperationKey = "Zaaer.RateType.DeleteByZaaerId",
					TargetId = zaaerId,
					PayloadType = nameof(Delete),
					PayloadJson = "{}"
				};
				await _queueService.EnqueueAsync(q);
				return Accepted(new { queued = true, requestRef = q.RequestRef });
			}

			var result = await _service.DeleteByZaaerIdAsync(zaaerId);
			if (!result)
			{
				return NotFound($"Rate type with ZaaerId {zaaerId} not found");
			}

			return NoContent();
		}

		/// <summary>
		/// Gets a rate type by internal ID (not ZaaerId). For internal use only.
		/// This endpoint should be placed after PUT and DELETE to avoid routing conflicts.
		/// </summary>
		[HttpGet("{rateTypeId:int}")]
		public async Task<IActionResult> GetById([FromRoute] int rateTypeId)
		{
			var result = await _service.GetByIdAsync(rateTypeId);
			if (result == null) return NotFound();
			return Ok(result);
		}

		/// <summary>
		/// Gets all rate types for a specific hotel.
		/// </summary>
		[HttpGet("hotel/{hotelId:int}")]
		public async Task<IActionResult> GetByHotel([FromRoute] int hotelId)
		{
			var list = await _service.GetAllByHotelIdAsync(hotelId);
			return Ok(list);
		}
	}
}

