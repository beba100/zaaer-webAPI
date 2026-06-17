using DevExpress.AspNetCore.Reporting.ReportDesigner;
using DevExpress.AspNetCore.Reporting.ReportDesigner.Native.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Custom Report Designer Controller for DevExpress Reporting
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class CustomReportDesignerController : ReportDesignerController
    {
        public CustomReportDesignerController(IReportDesignerMvcControllerService controllerService)
            : base(controllerService)
        {
        }
    }
}