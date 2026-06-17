using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace zaaerIntegration.Filters
{
    /// <summary>
    /// Provides Swagger examples for Reservation Tools DTOs to match the frontend payloads
    /// </summary>
    public class ReservationToolSchemaExample : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            // Zaaer reservation tool DTOs were removed in Sprint 3B.
            // Keep filter registered without mutating schemas.
        }
    }
}


