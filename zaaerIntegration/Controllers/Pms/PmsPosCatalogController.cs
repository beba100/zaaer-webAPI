#pragma warning disable CS1591

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using zaaerIntegration.DTOs.Pms;
using zaaerIntegration.Security;
using zaaerIntegration.Services;
using zaaerIntegration.Services.Interfaces;

namespace zaaerIntegration.Controllers.Pms
{
    [ApiController]
    [Authorize]
    [Route("api/v1/pms/pos/catalog")]
    [Produces("application/json")]
    public sealed class PmsPosCatalogController : ControllerBase
    {
        private readonly IPmsOutletCatalogService _catalog;

        public PmsPosCatalogController(IPmsOutletCatalogService catalog)
        {
            _catalog = catalog;
        }

        [HttpGet("outlets")]
        [RequirePermission("pos.settings.view")]
        public async Task<IActionResult> ListOutlets(CancellationToken cancellationToken)
        {
            var data = await _catalog.ListOutletsAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("outlets")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> CreateOutlet([FromBody] PmsUpsertOutletDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request." });
            }

            var data = await _catalog.CreateOutletAsync(dto, cancellationToken);
            return Ok(new { success = true, message = "Outlet created.", data });
        }

        [HttpPut("outlets/{outletId:int}")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> UpdateOutlet(int outletId, [FromBody] PmsUpsertOutletDto dto, CancellationToken cancellationToken)
        {
            var data = await _catalog.UpdateOutletAsync(outletId, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Outlet not found." });
            }

            return Ok(new { success = true, message = "Outlet updated.", data });
        }

        [HttpGet("categories")]
        [RequirePermission("pos.settings.view")]
        public async Task<IActionResult> ListCategories(CancellationToken cancellationToken)
        {
            var data = await _catalog.ListCategoriesAsync(cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("categories")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> CreateCategory([FromBody] PmsUpsertOutletCategoryDto dto, CancellationToken cancellationToken)
        {
            var data = await _catalog.CreateCategoryAsync(dto, cancellationToken);
            return Ok(new { success = true, message = "Category created.", data });
        }

        [HttpPut("categories/{categoryId:int}")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> UpdateCategory(int categoryId, [FromBody] PmsUpsertOutletCategoryDto dto, CancellationToken cancellationToken)
        {
            var data = await _catalog.UpdateCategoryAsync(categoryId, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Category not found." });
            }

            return Ok(new { success = true, message = "Category updated.", data });
        }

        [HttpGet("items")]
        [RequirePermission("pos.settings.view")]
        public async Task<IActionResult> ListItems([FromQuery] int? outletId, [FromQuery] int? categoryId, CancellationToken cancellationToken)
        {
            var data = await _catalog.ListItemsAsync(outletId, categoryId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("items/upload-image")]
        [RequirePermission("pos.settings.manage")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(PosItemImageStorage.MaxFileBytes)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadItemImage(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest(new { success = false, message = "No image file provided." });
            }

            try
            {
                var hotelId = await _catalog.ResolveCurrentHotelIdAsync(cancellationToken);
                var saved = await PosItemImageStorage.SaveAsync(file, hotelId, cancellationToken);
                return Ok(new
                {
                    success = true,
                    message = "Image uploaded.",
                    data = new { imageUrl = saved.RelativePath }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("items")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> CreateItem([FromBody] PmsUpsertOutletItemDto dto, CancellationToken cancellationToken)
        {
            var data = await _catalog.CreateItemAsync(dto, cancellationToken);
            return Ok(new { success = true, message = "Item created.", data });
        }

        [HttpPut("items/{itemId:int}")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> UpdateItem(int itemId, [FromBody] PmsUpsertOutletItemDto dto, CancellationToken cancellationToken)
        {
            var data = await _catalog.UpdateItemAsync(itemId, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Item not found." });
            }

            return Ok(new { success = true, message = "Item updated.", data });
        }

        [HttpGet("tables")]
        [RequirePermission("pos.settings.view")]
        public async Task<IActionResult> ListTables([FromQuery] int? outletId, CancellationToken cancellationToken)
        {
            var data = await _catalog.ListTablesAsync(outletId, cancellationToken);
            return Ok(new { success = true, data });
        }

        [HttpPost("tables")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> CreateTable([FromBody] PmsUpsertOutletTableDto dto, CancellationToken cancellationToken)
        {
            var data = await _catalog.CreateTableAsync(dto, cancellationToken);
            return Ok(new { success = true, message = "Table created.", data });
        }

        [HttpPut("tables/{tableId:int}")]
        [RequirePermission("pos.settings.manage")]
        public async Task<IActionResult> UpdateTable(int tableId, [FromBody] PmsUpsertOutletTableDto dto, CancellationToken cancellationToken)
        {
            var data = await _catalog.UpdateTableAsync(tableId, dto, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Table not found." });
            }

            return Ok(new { success = true, message = "Table updated.", data });
        }

        [HttpGet("pos-menu/{outletId:int}")]
        [RequirePermission("pos.view")]
        public async Task<IActionResult> GetPosMenu(int outletId, CancellationToken cancellationToken)
        {
            var data = await _catalog.GetPosCatalogAsync(outletId, cancellationToken);
            if (data == null)
            {
                return NotFound(new { success = false, message = "Outlet not found." });
            }

            return Ok(new { success = true, data });
        }
    }
}
