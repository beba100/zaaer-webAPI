using DevExpress.AspNetCore.Reporting.WebDocumentViewer;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer.Native.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace zaaerIntegration.Controllers
{
    /// <summary>
    /// Custom Web Document Viewer Controller for DevExpress Reporting
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class CustomWebDocumentViewerController : WebDocumentViewerController
    {
        public CustomWebDocumentViewerController(IWebDocumentViewerMvcControllerService controllerService)
            : base(controllerService)
        {
        }
    }
}