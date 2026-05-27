using Microsoft.Extensions.Options;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Services;

public sealed class TrustedHeaderWorkflowActorResolverTests
{
    [Fact]
    public void ResolveReadsActorFromTrustedHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Workflow-UserId"] = "checker1";
        context.Request.Headers["X-Workflow-DisplayName"] = "Checker One";
        context.Request.Headers["X-Workflow-Roles"] = " Checker, TradingDesk ; Checker ";

        var actor = CreateResolver().Resolve(context);

        Assert.Equal("checker1", actor.UserId);
        Assert.Equal("Checker One", actor.DisplayName);
        Assert.Equal(["Checker", "TradingDesk"], actor.Roles);
    }

    [Fact]
    public void ResolveSupportsConfiguredHeaderNames()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Upstream-User"] = "maker1";
        context.Request.Headers["X-Upstream-Roles"] = "Maker";
        context.Request.Headers["X-Upstream-DisplayName"] = "Maker One";

        var actor = CreateResolver(new WorkflowActorHeaderOptions
        {
            UserIdHeaderName = "X-Upstream-User",
            RolesHeaderName = "X-Upstream-Roles",
            DisplayNameHeaderName = "X-Upstream-DisplayName"
        }).Resolve(context);

        Assert.Equal("maker1", actor.UserId);
        Assert.Equal("Maker One", actor.DisplayName);
        Assert.Equal(["Maker"], actor.Roles);
    }

    [Fact]
    public void ResolveFailsWhenActorHeaderIsMissing()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Workflow-Roles"] = "Maker";

        var exception = Assert.Throws<WorkflowActorResolutionException>(() => CreateResolver().Resolve(context));

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
        Assert.Equal("Workflow actor is required.", exception.Title);
    }

    [Fact]
    public void ResolveFailsWhenRolesHeaderIsMissing()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Workflow-UserId"] = "maker1";

        var exception = Assert.Throws<WorkflowActorResolutionException>(() => CreateResolver().Resolve(context));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("Workflow roles are required.", exception.Title);
    }

    private static TrustedHeaderWorkflowActorResolver CreateResolver(WorkflowActorHeaderOptions? options = null)
    {
        return new TrustedHeaderWorkflowActorResolver(Options.Create(options ?? new WorkflowActorHeaderOptions()));
    }
}
