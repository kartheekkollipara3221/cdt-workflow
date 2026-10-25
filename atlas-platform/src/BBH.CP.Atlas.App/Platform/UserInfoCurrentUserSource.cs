using BBH.CP.Atlas.App.UserAuthentication;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Security;

namespace BBH.CP.Atlas.App.Platform;

/// <summary>The platform gets the current user from the existing IUserInfo (Active Directory at the target).</summary>
public sealed class UserInfoCurrentUserSource(IUserInfo userInfo) : IAtlasCurrentUserSource
{
    public CPUser GetCurrentUser() => userInfo.GetCurrentUser();
}
