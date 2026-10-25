// STAND-IN: do not copy to the target.
// Target: Controllers/V1/CPAtlasController.cs. Only the endpoints that P1 uses, with the same routes and response
// shapes. Integration change at the target (spec 07, 4.4): the "code" parameters change from CPApplicationCode to
// string, and the controller passes the resolver and the config store to CPAtlasService (INTEGRATION.md, section 2).
using BBH.CP.Atlas.App.UserAuthentication;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BBH.CP.Atlas.App.Controllers.V1
{
    [Authorize]
    [ApiController]
    public class CPAtlasController(CPSharepointContext context, IConfiguration configuration, IUserInfo userInfo,
        IAtlasModuleResolver resolver, IAtlasConfigStore configStore) : ControllerBase
    {
        [HttpGet]
        [Route("v1/Application")]
        public ActionResult GetApplication()
        {
            return Ok(new { result = Service().GetApplication() });
        }

        [HttpGet]
        [Route("v1/Dashboard")]
        public ActionResult<CPAtlasDashboard> LoadDashboard(string code)
        {
            return Ok(new { result = Service().LoadDashboard(code) });
        }

        [HttpGet]
        [Route("v1/SectionPage")]
        public ActionResult<CPApplicationSection> LoadSectionPage([FromQuery] CPAtlasPage page)
        {
            return Ok(new { result = Service().LoadSectionPage(page) });
        }

        [HttpPost]
        [Route("v1/ManagePage")]
        public CPAtlasPage ManagePage([FromForm] IFormFileCollection files, [FromForm] string input)
        {
            return Service().ManagePage(files, input);
        }

        [HttpPost]
        [Route("v1/AuditLog")]
        public ActionResult<string> AuditLog([FromForm] string input)
        {
            Service().AuditLog(input);
            return Ok(new { result = "AuditSuccess" });
        }

        [HttpGet]
        [Route("v1/GetAuditLog")]
        public ActionResult<List<CPAuditLog>> GetAuditLog(string appCode, int refID)
        {
            return Ok(new { result = Service().GetAuditLog(appCode, refID) });
        }

        [HttpPost]
        [Route("v1/RemovePage")]
        public CPAtlasPage RemovePage(string code, int id)
        {
            return Service().RemovePage(code, id);
        }

        private CPAtlasService Service() =>
            new(context, null, configuration, userInfo.GetCurrentUser(), resolver, configStore);
    }
}
