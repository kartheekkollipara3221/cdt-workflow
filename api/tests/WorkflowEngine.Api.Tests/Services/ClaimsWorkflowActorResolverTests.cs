using System.Security.Claims;
using Microsoft.Extensions.Options;
using WorkflowEngine.Api.Options;
using WorkflowEngine.Api.Services;

namespace WorkflowEngine.Api.Tests.Services;

public sealed class ClaimsWorkflowActorResolverTests
{
    [Fact]
    public void ResolvesUserIdFromClaims()
    {
        var context = HttpContextWithClaims(new Claim(ClaimTypes.NameIdentifier, "maker1"), new Claim(ClaimTypes.Role, "Maker"));

        var actor = CreateResolver().Resolve(context);

        Assert.Equal("maker1", actor.UserId);
    }

    [Fact]
    public void ResolvesDisplayNameFromClaimsWhenAvailable()
    {
        var context = HttpContextWithClaims(
            new Claim(ClaimTypes.NameIdentifier, "maker1"),
            new Claim(ClaimTypes.Name, "Maker One"),
            new Claim(ClaimTypes.Role, "Maker"));

        var actor = CreateResolver().Resolve(context);

        Assert.Equal("Maker One", actor.DisplayName);
    }

    [Fact]
    public void ResolvesWorkflowRolesFromRoleClaims()
    {
        var context = HttpContextWithClaims(
            new Claim(ClaimTypes.NameIdentifier, "checker1"),
            new Claim(ClaimTypes.Role, "Checker"),
            new Claim("workflow_role", "TradingDesk"));

        var actor = CreateResolver().Resolve(context);

        Assert.Equal(["Checker", "TradingDesk"], actor.Roles);
    }

    [Fact]
    public void ReturnsDistinctTrimmedRoles()
    {
        var context = HttpContextWithClaims(
            new Claim(ClaimTypes.NameIdentifier, "user1"),
            new Claim("roles", " Maker, Checker ;Maker "));

        var actor = CreateResolver().Resolve(context);

        Assert.Equal(["Maker", "Checker"], actor.Roles);
    }

    [Fact]
    public void FailsWhenNoUsableActorExists()
    {
        var context = new DefaultHttpContext();

        var exception = Assert.Throws<WorkflowActorResolutionException>(() => CreateResolver().Resolve(context));

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
    }

    [Fact]
    public void IgnoresDevelopmentHeadersWhenDisabled()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Workflow-UserId"] = "dev1";
        context.Request.Headers["X-Workflow-Roles"] = "Maker";

        var exception = Assert.Throws<WorkflowActorResolutionException>(() => CreateResolver(allowDevelopmentHeaders: false).Resolve(context));

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
    }

    [Fact]
    public void UsesDevelopmentHeadersOnlyWhenExplicitlyEnabled()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Workflow-UserId"] = "dev1";
        context.Request.Headers["X-Workflow-DisplayName"] = "Developer One";
        context.Request.Headers["X-Workflow-Roles"] = "Maker,Checker";

        var actor = CreateResolver(allowDevelopmentHeaders: true).Resolve(context);

        Assert.Equal("dev1", actor.UserId);
        Assert.Equal("Developer One", actor.DisplayName);
        Assert.Equal(["Maker", "Checker"], actor.Roles);
    }

    private static ClaimsWorkflowActorResolver CreateResolver(bool allowDevelopmentHeaders = false)
    {
        return new ClaimsWorkflowActorResolver(Microsoft.Extensions.Options.Options.Create(new WorkflowApiOptions
        {
            AllowDevelopmentHeaderActorResolver = allowDevelopmentHeaders
        }));
    }

    private static DefaultHttpContext HttpContextWithClaims(params Claim[] claims)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
        };
    }
}
