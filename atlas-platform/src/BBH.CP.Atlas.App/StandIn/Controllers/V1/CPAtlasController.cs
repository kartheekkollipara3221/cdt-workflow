// STAND-IN: do not copy to the target.
// Target: Controllers/V1/CPAtlasController.cs. Only the endpoints that release P1 uses, with the same routes and
// response shapes. Integration change at the target: change the "code" parameters from CPApplicationCode to string.
// Then pass the resolver and the config store to CPAtlasService.
using BBH.CP.Atlas.App.UserAuthentication;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BBH.CP.Atlas.App.Controllers.V1
{
    /// <summary>The local stand-in of the Atlas endpoints: apps, dashboard, form pages and audit log.</summary>
    [Authorize]
    [ApiController]
    public class CPAtlasController(CPSharepointContext context, IConfiguration configuration, IUserInfo userInfo,
        IAtlasModuleResolver resolver, IAtlasConfigStore configStore, ILogger<CPAtlasController> logger) : ControllerBase
    {
        /// <summary>Returns the list of apps.</summary>
        [HttpGet]
        [Route("v1/Application")]
        public ActionResult GetApplication()
        {
            return Ok(new { result = Service().GetApplication() });
        }

        /// <summary>Returns the dashboard of an app.</summary>
        [HttpGet]
        [Route("v1/Dashboard")]
        public ActionResult<CPAtlasDashboard> LoadDashboard(string code)
        {
            return Ok(new { result = Service().LoadDashboard(code) });
        }

        /// <summary>Returns the form section of a record.</summary>
        [HttpGet]
        [Route("v1/SectionPage")]
        public ActionResult<CPApplicationSection> LoadSectionPage([FromQuery] CPAtlasPage page)
        {
            return Ok(new { result = Service().LoadSectionPage(page) });
        }

        /// <summary>Saves a record and its files.</summary>
        [HttpPost]
        [Route("v1/ManagePage")]
        public CPAtlasPage ManagePage([FromForm] IFormFileCollection files, [FromForm] string input)
        {
            return Service().ManagePage(files, input);
        }

        /// <summary>Writes an audit log entry.</summary>
        [HttpPost]
        [Route("v1/AuditLog")]
        public ActionResult<string> AuditLog([FromForm] string input)
        {
            Service().AuditLog(input);
            return Ok(new { result = "AuditSuccess" });
        }

        /// <summary>Returns the audit log of a record.</summary>
        [HttpGet]
        [Route("v1/GetAuditLog")]
        public ActionResult<List<CPAuditLog>> GetAuditLog(string appCode, int refID)
        {
            return Ok(new { result = Service().GetAuditLog(appCode, refID) });
        }

        /// <summary>Deletes a record.</summary>
        [HttpPost]
        [Route("v1/RemovePage")]
        public CPAtlasPage RemovePage(string code, int id)
        {
            return Service().RemovePage(code, id);
        }

        private CPAtlasService Service() =>
            new(context, null, configuration, userInfo.GetCurrentUser(), resolver, configStore, logger);
    }
}
