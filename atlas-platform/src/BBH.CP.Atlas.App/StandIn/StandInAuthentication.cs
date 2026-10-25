// STAND-IN: do not copy to the target.
// Local authentication (spec 12, 11.3). The target uses IIS Windows authentication and Active Directory.
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BBH.CP.Atlas.App.StandIn;

/// <summary>
/// "Fixed" authentication: each request is the Windows login of the API process (or StandIn:FixedUser).
/// The browser needs no Windows sign-in, so the local review works in any browser.
/// </summary>
public sealed class StandInFixedUserHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "StandInFixed";

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
/// Development only: the header X-Atlas-Act-As makes the request run as one of the test users in StandIn:Users,
/// to test the access table (spec 01, 10.5). Off outside Development or when StandIn:ActAs:Enabled is not true.
/// </summary>
public sealed class StandInActAsMiddleware(RequestDelegate next, IWebHostEnvironment environment, IConfiguration configuration)
{
    public const string HeaderName = "X-Atlas-Act-As";

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
