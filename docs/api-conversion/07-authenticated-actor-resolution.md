# Authenticated Actor Resolution

This is a critical migration point. The console app accepts `actor.userId` and `actor.roles` from JSON/CLI because it is a proof of concept. A production API must not trust those request body fields.

## Current Console Actor Behavior

| Source | Current Console Behavior | API Behavior | Notes |
|---|---|---|---|
| `execute-json` sample body `actor.userId` | Used as performed-by user and section saved-by user. | Overwrite from authenticated user. | Body value is untrusted. |
| `execute-json` sample body `actor.displayName` | Deserialized but not authoritative. | Overwrite from authenticated profile if used. | Context reader currently defaults display name to user ID. |
| `execute-json` sample body `actor.roles` | Used for transition authorization. | Overwrite from trusted claims/groups/API authorization. | Body roles are a privilege escalation risk. |
| `show-context --user` | Simulates current user. | Resolve from `ClaimsPrincipal`. | Do not accept user ID query for normal users. |
| `show-context --roles` | Simulates current workflow roles. | Resolve from claims/groups/authorization. | Do not accept roles query for normal users. |
| `start-workflow --user` | Persists `StartedByUserId`. | Use authenticated user ID. | Do not accept body user ID for normal users. |

## Security Risk

If an API trusts request body roles, any caller could submit:

```json
{
  "actor": {
    "userId": "attacker",
    "roles": ["Checker", "TradingDesk", "BDRMClientService"]
  }
}
```

and bypass workflow authorization. The engine will correctly enforce the roles it receives, so the API must ensure those roles are trusted.

## Recommended Abstraction

Add an API-layer actor resolver in the external API project:

```csharp
public sealed record WorkflowActorSnapshot(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> WorkflowRoles);

public interface IWorkflowActorResolver
{
    WorkflowActorSnapshot Resolve(ClaimsPrincipal user);
}
```

Implementation skeleton:

```csharp
using System.Security.Claims;

public sealed class ClaimsWorkflowActorResolver : IWorkflowActorResolver
{
    public WorkflowActorSnapshot Resolve(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Authenticated user is required.");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID claim is missing.");

        var displayName = user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("name")
            ?? userId;

        var roles = ResolveWorkflowRoles(user);
        if (roles.Count == 0)
        {
            return new WorkflowActorSnapshot(userId, displayName, Array.Empty<string>());
        }

        return new WorkflowActorSnapshot(userId, displayName, roles);
    }

    private static IReadOnlyList<string> ResolveWorkflowRoles(ClaimsPrincipal user)
    {
        var rawRoles = user.Claims
            .Where(claim => claim.Type == ClaimTypes.Role || claim.Type == "groups" || claim.Type == "roles")
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var workflowRoles = new List<string>();

        if (rawRoles.Contains("<MakerClaimOrGroup>"))
        {
            workflowRoles.Add("Maker");
        }

        if (rawRoles.Contains("<CheckerClaimOrGroup>"))
        {
            workflowRoles.Add("Checker");
        }

        if (rawRoles.Contains("<TradingDeskClaimOrGroup>"))
        {
            workflowRoles.Add("TradingDesk");
        }

        if (rawRoles.Contains("<BDRMClientServiceClaimOrGroup>"))
        {
            workflowRoles.Add("BDRMClientService");
        }

        return workflowRoles;
    }
}
```

Replace placeholder claim/group names with the real API authorization model.

## Claims-To-Workflow-Role Mapping

Recommended workflow roles must match seed metadata exactly:

- `Maker`
- `Checker`
- `TradingDesk`
- `BDRMClientService`

Do not add a SQL user-role table to the workflow schema. Role mapping should use the existing API authorization/claims system or enterprise authorization service.

## Stamping `WorkflowActionEnvelope.Actor`

Stamp actor before validation, hashing, and execution:

```csharp
var actor = actorResolver.Resolve(User);
envelope.Actor = new WorkflowActor
{
    UserId = actor.UserId,
    DisplayName = actor.DisplayName,
    Roles = actor.WorkflowRoles.ToList()
};

var normalized = normalizer.Normalize(envelope, null, null);
var errors = validator.Validate(normalized.Envelope);
var requestHash = hasher.ComputeHash(normalized.Envelope);
```

If you hash before stamping, idempotency and replay semantics can be wrong.

## Context Reads

Use the same actor resolver for context:

```csharp
var actor = actorResolver.Resolve(User);
var response = contextReader.Read(new WorkflowContextReadRequest(
    workflowInstanceId,
    actor.UserId,
    actor.WorkflowRoles));
```

Do not accept `roles` query parameters for normal production context reads.

## Display Name

The current `WorkflowContextReader` sets `ActorAccess.DisplayName` to `request.UserId`. If the API requires a display name, there are two options:

- Continue returning user ID as display name for v1.
- Add an API response wrapper that decorates display name from the authenticated profile.

Do not add a workflow SQL user lookup table for display names.

## Multiple Roles

Multiple roles are valid. The engine handles them this way:

- If no actor role matches the transition, result is `Forbidden`.
- If exactly one actor role matches, execution proceeds.
- If multiple actor roles match different transition roles for the same action/state, result is `AmbiguousRoleMatch`.

The API should not try to guess which role to use. Prefer returning the engine result and asking the user to act under a less ambiguous role context if the enterprise auth model allows it.

## Missing Required Roles

If authenticated user has no workflow role:

- Context endpoint can return `availableActions = []` with `actorAccess.canAct = false`.
- Execute endpoint will return validation failed if roles are empty after stamping, because `WorkflowActionEnvelopeValidator` requires at least one role.

If your API wants a clearer `403`, handle empty workflow roles before building the envelope:

```csharp
if (actor.WorkflowRoles.Count == 0)
{
    return Forbid();
}
```

Use this consistently.

## Testing Actor Resolution

Add API tests:

- Authenticated Maker claim maps to `Maker`.
- Authenticated Checker claim maps to `Checker`.
- Multiple claims map to multiple workflow roles.
- Unknown claims produce no workflow roles.
- Body `actor.roles` is overwritten by claims.
- Body `actor.userId` is overwritten by claims.
- Execute with body `Checker` but authenticated `Maker` returns `Forbidden` or `InvalidTransition` according to current state/action.
- Context roles come from claims, not query or body.

