// STAND-IN: do not copy to the target.
// Local authentication. The target uses IIS Windows authentication and Active Directory.
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BBH.CP.Atlas.App.StandIn;

/// <summary>
/// Authenticates each request as the Windows login of the API process, or as StandIn:FixedUser when it is set.
/// </summary>
/// <remarks>
/// The browser needs no Windows sign-in, so local tests work in any browser.
/// </remarks>
public sealed class StandInFixedUserHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The name of the "Fixed" authentication scheme.</summary>
    public const string SchemeName = "StandInFixed";

    /// <summary>Returns a successful result with the fixed user name.</summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string name = configuration["StandIn:FixedUser"] is { Length: > 0 } fixedUser
            ? fixedUser
            : Environment.UserDomainName + "\\" + Environment.UserName;
        ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, name)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>
/// Runs a request as one of the test users in StandIn:Users when the request has the header X-Atlas-Act-As.
/// </summary>
/// <remarks>
/// Use it to test the access rules for each role. It works only in Development
/// and only when StandIn:ActAs:Enabled is true.
/// </remarks>
public sealed class StandInActAsMiddleware(RequestDelegate next, IWebHostEnvironment environment, IConfiguration configuration)
{
    /// <summary>The name of the header that holds the LAN ID of the test user.</summary>
    public const string HeaderName = "X-Atlas-Act-As";

    /// <summary>Replaces the user of the request with the test user, when act-as is on and the user is known.</summary>
    public Task InvokeAsync(HttpContext context)
    {
        if (environment.IsDevelopment()
            && string.Equals(configuration["StandIn:ActAs:Enabled"], "true", StringComparison.OrdinalIgnoreCase)
            && context.User.Identity?.IsAuthenticated == true
            && context.Request.Headers.TryGetValue(HeaderName, out var values)
            && values.ToString() is { Length: > 0 } lanId
            && configuration.GetSection($"StandIn:Users:{lanId}").Exists())
        {
            ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, "LOCAL\\" + lanId), new Claim("atlas:actas-by", context.User.Identity.Name ?? "")], "StandInActAs");
            context.User = new ClaimsPrincipal(identity);
        }

        return next(context);
    }
}
