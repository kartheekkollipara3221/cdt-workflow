using System.Security.Claims;
using Microsoft.Extensions.Options;
using WorkflowEngine.Api.Options;

namespace WorkflowEngine.Api.Services;

public sealed class ClaimsWorkflowActorResolver(IOptions<WorkflowApiOptions> options) : IWorkflowActorResolver
{
    private static readonly string[] UserIdClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        "sub",
        "oid",
        "user_id",
        "uid"
    ];

    private static readonly string[] DisplayNameClaimTypes =
    [
        ClaimTypes.Name,
        "name",
        "display_name",
        "preferred_username",
        "email"
    ];

    private static readonly string[] RoleClaimTypes =
    [
        ClaimTypes.Role,
        "role",
        "roles",
        "workflow_role",
        "workflow_roles"
    ];

    public WorkflowActorSnapshot Resolve(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            return ResolveFromClaims(principal);
        }

        if (options.Value.AllowDevelopmentHeaderActorResolver)
        {
            return ResolveFromDevelopmentHeaders(httpContext);
        }

        throw new WorkflowActorResolutionException(
            StatusCodes.Status401Unauthorized,
            "Workflow actor is required.",
            "No authenticated workflow actor identity was found.");
    }

    private static WorkflowActorSnapshot ResolveFromClaims(ClaimsPrincipal principal)
    {
        var userId = FirstClaimValue(principal, UserIdClaimTypes);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new WorkflowActorResolutionException(
                StatusCodes.Status401Unauthorized,
                "Workflow actor is required.",
                "The authenticated principal does not contain a usable user identifier claim.");
        }

        var roles = ResolveRoles(principal.Claims);
        if (roles.Count == 0)
        {
            throw new WorkflowActorResolutionException(
                StatusCodes.Status403Forbidden,
                "Workflow roles are required.",
                "The authenticated principal does not contain any workflow role claims.");
        }

        var displayName = FirstClaimValue(principal, DisplayNameClaimTypes);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = principal.Identity?.Name;
        }

        return new WorkflowActorSnapshot(
            userId.Trim(),
            string.IsNullOrWhiteSpace(displayName) ? userId.Trim() : displayName.Trim(),
            roles);
    }

    private static string? FirstClaimValue(ClaimsPrincipal principal, IReadOnlyList<string> claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.Claims
                .FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal))?
                .Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveRoles(IEnumerable<Claim> claims)
    {
        var roleTypes = RoleClaimTypes.ToHashSet(StringComparer.Ordinal);
        return claims
            .Where(claim => roleTypes.Contains(claim.Type))
            .SelectMany(claim => SplitRoles(claim.Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
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

    private static WorkflowActorSnapshot ResolveFromDevelopmentHeaders(HttpContext httpContext)
    {
        var userId = HeaderValue(httpContext, "X-Workflow-UserId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new WorkflowActorResolutionException(
                StatusCodes.Status401Unauthorized,
                "Workflow actor is required.",
                "Development workflow actor headers are enabled, but X-Workflow-UserId was not provided.");
        }

        var roles = SplitRoles(HeaderValue(httpContext, "X-Workflow-Roles"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (roles.Count == 0)
        {
            throw new WorkflowActorResolutionException(
                StatusCodes.Status403Forbidden,
                "Workflow roles are required.",
                "Development workflow actor headers are enabled, but X-Workflow-Roles did not contain any roles.");
        }

        var displayName = HeaderValue(httpContext, "X-Workflow-DisplayName");
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
}
