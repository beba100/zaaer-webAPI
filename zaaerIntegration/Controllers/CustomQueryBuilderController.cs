using DevExpress.AspNetCore.Reporting.QueryBuilder;
using DevExpress.AspNetCore.Reporting.QueryBuilder.Native.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Custom Query Builder Controller for DevExpress Reporting
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class CustomQueryBuilderController : QueryBuilderController
    {
        public CustomQueryBuilderController(IQueryBuilderMvcControllerService controllerService)
            : base(controllerService)
        {
        }
    }
}