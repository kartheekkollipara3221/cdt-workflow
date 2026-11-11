// STAND-IN: do not copy to the target.
// Target: Controllers/V1/CPSecurityController.cs (Active Directory). Locally: the user from IUserInfo, the roles from
// the active roles view. "v1/standin/users" lists the test users for the development "act as" switch.
using BBH.CP.Atlas.App.UserAuthentication;
using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BBH.CP.Atlas.App.Controllers.V1
{
    /// <summary>The local stand-in of the security endpoints: the current user, the roles and the test users.</summary>
    [ApiController]
    public class CPSecurityController(CPSharepointContext context, IConfiguration configuration, IUserInfo userInfo,
        IWebHostEnvironment environment) : ControllerBase
    {
        /// <summary>Returns the current user with the roles from dbo.vw_CP_Atlas_ActiveUserRole.</summary>
        [HttpGet]
        [Route("v1/authorize")]
        public ActionResult<CPUser> Get()
        {
            CPUser user = userInfo.GetCurrentUser();
            user.UserGroups = ["local"];
            user.Roles = [.. context.Database
                .SqlQuery<string>($"SELECT RoleName AS Value FROM dbo.vw_CP_Atlas_ActiveUserRole WHERE LanId = {user.LanID}")
                .ToList()
                .Distinct(StringComparer.OrdinalIgnoreCase)];
            return Ok(user);
        }

        /// <summary>
        /// Returns the test users for the "act as" switch. The list is empty outside Development or when ActAs is off.
        /// </summary>
        [HttpGet]
        [Route("v1/standin/users")]
        public ActionResult GetTestUsers()
        {
            bool enabled = environment.IsDevelopment()
                && string.Equals(configuration["StandIn:ActAs:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (!enabled)
                return Ok(new { result = Array.Empty<object>() });

            var users = configuration.GetSection("StandIn:Users").GetChildren()
                .Select(x => new { LanId = x.Key, DisplayName = x["DisplayName"] ?? x.Key, Note = x["Note"] })
                .ToList();
            return Ok(new { result = users });
        }
    }
}
