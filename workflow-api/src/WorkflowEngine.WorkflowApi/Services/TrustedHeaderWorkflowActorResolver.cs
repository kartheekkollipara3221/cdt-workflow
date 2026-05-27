using Microsoft.Extensions.Options;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class TrustedHeaderWorkflowActorResolver(IOptions<WorkflowActorHeaderOptions> options) : IWorkflowActorResolver
{
    public WorkflowActorSnapshot Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var headerOptions = options.Value;
        var userId = HeaderValue(httpContext, headerOptions.UserIdHeaderName);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new WorkflowActorResolutionException(
                StatusCodes.Status401Unauthorized,
                "Workflow actor is required.",
                $"The trusted upstream actor header '{headerOptions.UserIdHeaderName}' was not provided.");
        }

        var roles = SplitRoles(HeaderValue(httpContext, headerOptions.RolesHeaderName))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (roles.Count == 0)
        {
            throw new WorkflowActorResolutionException(
                StatusCodes.Status403Forbidden,
                "Workflow roles are required.",
                $"The trusted upstream role header '{headerOptions.RolesHeaderName}' did not contain any roles.");
        }

        var displayName = HeaderValue(httpContext, headerOptions.DisplayNameHeaderName);
        return new WorkflowActorSnapshot(
            userId.Trim(),
            string.IsNullOrWhiteSpace(displayName) ? userId.Trim() : displayName.Trim(),
            roles);
    }

    private static string? HeaderValue(HttpContext httpContext, string headerName)
    {
        return httpContext.Request.Headers.TryGetValue(headerName, out var value)
            ? value.FirstOrDefault()
            : null;
    }

    private static IEnumerable<string> SplitRoles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var role in value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                yield return role;
            }
        }
    }
}
