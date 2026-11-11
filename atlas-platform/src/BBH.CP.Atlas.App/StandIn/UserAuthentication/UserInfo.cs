// STAND-IN: do not copy to the target.
// Target: Atlasapp/UserAuthentication/IUserInfo.cs and UserInfo.cs. The target reads Active Directory.
// Locally: the LAN ID comes from the Windows login (the part after "\"). The names come from the
// settings "StandIn:Users:<LanId>" (DisplayName, FirstName, LastName, Email, Department).
using BBH.CP.Atlas.Data.DTO;

namespace BBH.CP.Atlas.App.UserAuthentication
{
    /// <summary>Gives the user of the current request.</summary>
    public interface IUserInfo
    {
        /// <summary>Returns the user of the current request.</summary>
        CPUser GetCurrentUser();
    }

    /// <summary>Reads the current user from the Windows login and the StandIn:Users settings.</summary>
    public class UserInfo(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : IUserInfo
    {
        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The request has no identity name.</exception>
        public CPUser GetCurrentUser()
        {
            var username = httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException("The current user's identity name is unavailable.");
            }

            var lanId = username.Split('\\').Last();
            IConfigurationSection section = configuration.GetSection($"StandIn:Users:{lanId}");

            return new CPUser
            {
                LanID = lanId,
                DisplayName = section["DisplayName"] ?? lanId,
                FirstName = section["FirstName"],
                LastName = section["LastName"],
                EmailAddress = section["Email"],
                Department = section["Department"] ?? ""
            };
        }
    }
}
