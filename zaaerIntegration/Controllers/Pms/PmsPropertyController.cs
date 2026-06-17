#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms.Property;
using zaaerIntegration.Security;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/property")]
    [Produces("application/json")]
    public sealed class PmsPropertyController : ControllerBase
    {
        private readonly IPmsPropertyService _propertyService;

        public PmsPropertyController(IPmsPropertyService propertyService)
        {
            _propertyService = propertyService;
        }

        [HttpGet("mode")]
        [Authorize]
        public async Task<IActionResult> GetMode(CancellationToken cancellationToken)
        {
            var data = await _propertyService.GetPropertyModeAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("lookups")]
        [RequirePermission("property.settings.view")]
        public async Task<IActionResult> GetLookups(CancellationToken cancellationToken)
        {
            var data = await _propertyService.GetLookupsAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("upload-image")]
        [RequirePermission("property.settings.manage")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(PropertyImageStorage.MaxFileBytes)]
        public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest(new { success = false, message = "No image file provided." });
            }

            try
            {
                var hotelId = await _propertyService.ResolveCurrentHotelIdAsync(cancellationToken);
                var saved = await PropertyImageStorage.SaveAsync(file, hotelId, cancellationToken);
                return Ok(new { success = true, message = "Image uploaded.", data = new { imageUrl = saved.RelativePath } });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("buildings")]
        [RequirePermission("property.buildings.list")]
        public async Task<IActionResult> ListBuildings(CancellationToken cancellationToken)
        {
            var data = await _propertyService.ListBuildingsAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("buildings/{id:int}")]
        [RequirePermission("property.buildings.view")]
        public async Task<IActionResult> GetBuilding(int id, CancellationToken cancellationToken)
        {
            var data = await _propertyService.GetBuildingAsync(id, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Block not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPost("buildings")]
        [RequirePermission("property.buildings.create")]
        public async Task<IActionResult> CreateBuilding([FromBody] PmsUpsertBuildingDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.CreateBuildingAsync(dto, cancellationToken);
            return Created(string.Empty, new { success = true, data });
        }

        [HttpPut("buildings/{id:int}")]
        [RequirePermission("property.buildings.update")]
        public async Task<IActionResult> UpdateBuilding(int id, [FromBody] PmsUpsertBuildingDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.UpdateBuildingAsync(id, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Block not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpDelete("buildings/{id:int}")]
        [RequirePermission("property.buildings.delete")]
        public async Task<IActionResult> DeleteBuilding(int id, CancellationToken cancellationToken)
        {
            var ok = await _propertyService.DeleteBuildingAsync(id, cancellationToken);
            if (!ok)
            {
                return NotFound(new { success = false, message = "Block not found." });
            }

            return Ok(new { success = true, message = "Block deleted." });
        }

        [HttpGet("room-types")]
        [RequirePermission("property.room_types.list")]
        public async Task<IActionResult> ListRoomTypes(CancellationToken cancellationToken)
        {
            var data = await _propertyService.ListRoomTypesAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("room-types/{id:int}")]
        [RequirePermission("property.room_types.view")]
        public async Task<IActionResult> GetRoomType(int id, CancellationToken cancellationToken)
        {
            var data = await _propertyService.GetRoomTypeAsync(id, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Room type not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPost("room-types")]
        [RequirePermission("property.room_types.create")]
        public async Task<IActionResult> CreateRoomType([FromBody] PmsUpsertRoomTypeDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.CreateRoomTypeAsync(dto, cancellationToken);
            return Created(string.Empty, new { success = true, data });
        }

        [HttpPut("room-types/{id:int}")]
        [RequirePermission("property.room_types.update")]
        public async Task<IActionResult> UpdateRoomType(int id, [FromBody] PmsUpsertRoomTypeDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.UpdateRoomTypeAsync(id, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Room type not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpDelete("room-types/{id:int}")]
        [RequirePermission("property.room_types.delete")]
        public async Task<IActionResult> DeleteRoomType(int id, CancellationToken cancellationToken)
        {
            var ok = await _propertyService.DeleteRoomTypeAsync(id, cancellationToken);
            if (!ok)
            {
                return NotFound(new { success = false, message = "Room type not found." });
            }

            return Ok(new { success = true, message = "Room type deleted." });
        }

        [HttpGet("apartments")]
        [RequirePermission("property.units.list")]
        public async Task<IActionResult> ListApartments(
            [FromQuery] string? search,
            [FromQuery] int? buildingZaaerId,
            [FromQuery] int? floorZaaerId,
            [FromQuery] int? roomTypeZaaerId,
            [FromQuery] int? parentApartmentZaaerId,
            CancellationToken cancellationToken)
        {
            var data = await _propertyService.ListApartmentsAsync(
                search,
                buildingZaaerId,
                floorZaaerId,
                roomTypeZaaerId,
                parentApartmentZaaerId,
                cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("apartments/{id:int}")]
        [RequirePermission("property.units.view")]
        public async Task<IActionResult> GetApartment(int id, CancellationToken cancellationToken)
        {
            var data = await _propertyService.GetApartmentAsync(id, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Unit not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPost("apartments")]
        [RequirePermission("property.units.create")]
        public async Task<IActionResult> CreateApartment([FromBody] PmsUpsertApartmentDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.CreateApartmentAsync(dto, cancellationToken);
            return Created(string.Empty, new { success = true, data });
        }

        [HttpPut("apartments/{id:int}")]
        [RequirePermission("property.units.update")]
        public async Task<IActionResult> UpdateApartment(int id, [FromBody] PmsUpsertApartmentDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.UpdateApartmentAsync(id, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Unit not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpDelete("apartments/{id:int}")]
        [RequirePermission("property.units.delete")]
        public async Task<IActionResult> DeleteApartment(int id, CancellationToken cancellationToken)
        {
            var ok = await _propertyService.DeleteApartmentAsync(id, cancellationToken);
            if (!ok)
            {
                return NotFound(new { success = false, message = "Unit not found." });
            }

            return Ok(new { success = true, message = "Unit deleted." });
        }

        [HttpGet("facilities")]
        [RequirePermission("property.facilities.list")]
        public async Task<IActionResult> ListFacilities(CancellationToken cancellationToken)
        {
            var data = await _propertyService.ListFacilitiesAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpGet("facilities/{id:int}")]
        [RequirePermission("property.facilities.view")]
        public async Task<IActionResult> GetFacility(int id, CancellationToken cancellationToken)
        {
            var data = await _propertyService.GetFacilityAsync(id, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Facility not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpPost("facilities")]
        [RequirePermission("property.facilities.create")]
        public async Task<IActionResult> CreateFacility([FromBody] PmsUpsertFacilityDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.CreateFacilityAsync(dto, cancellationToken);
            return Created(string.Empty, new { success = true, data });
        }

        [HttpPut("facilities/{id:int}")]
        [RequirePermission("property.facilities.update")]
        public async Task<IActionResult> UpdateFacility(int id, [FromBody] PmsUpsertFacilityDto dto, CancellationToken cancellationToken)
        {
            var data = await _propertyService.UpdateFacilityAsync(id, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Facility not found." });
            }

            return Ok(new { success = true, data });
        }

        [HttpDelete("facilities/{id:int}")]
        [RequirePermission("property.facilities.delete")]
        public async Task<IActionResult> DeleteFacility(int id, CancellationToken cancellationToken)
        {
            var ok = await _propertyService.DeleteFacilityAsync(id, cancellationToken);
            if (!ok)
            {
                return NotFound(new { success = false, message = "Facility not found." });
            }

            return Ok(new { success = true, message = "Facility deleted." });
        }
    }
}
