using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service.Platform.Security;

public enum AtlasOperation { View, Create, Edit, Delete, History }

public sealed record AtlasAccessDecision(bool Allowed, string Reason);

/// <summary>
/// One access check for each operation (spec 06, 4.3). The platform calls it before each operation; a module does
/// not call it itself. AccessMode LogOnly writes Atlas.AccessDenied and continues; Enforce returns 403.
/// </summary>
public interface IAtlasAccessService
{
    AtlasAccessDecision Evaluate(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, AtlasOperation operation,
        IReadOnlyDictionary<string, object?>? record);

    /// <summary>Evaluates, and on denial logs (LogOnly) or throws 403 (Enforce).</summary>
    void Demand(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, AtlasOperation operation,
        IReadOnlyDictionary<string, object?>? record, int recordId);

    /// <summary>True if the current user is an owner of the record (OwnerFields, LAN ID match). A new record: true.</summary>
    bool IsOwner(CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record);

    CPUserPermissions UserPermissions(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record);
}

public sealed class AtlasAccessService(IAtlasUserContext user, ILogger<AtlasAccessService> logger) : IAtlasAccessService
{
    public static readonly EventId AccessDenied = new(41001, "Atlas.AccessDenied");

    public AtlasAccessDecision Evaluate(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, AtlasOperation operation,
        IReadOnlyDictionary<string, object?>? record)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        AtlasAppRoles roles = user.RolesFor(manifest.Code);
        if (!roles.HasAnyRole)
            return new(false, $"The user has no role in {manifest.Code}.");

        CPPermissions? permissions = form?.Permissions;
        bool canView = permissions?.View == null || roles.MatchesAny(permissions.View);

        return operation switch
        {
            AtlasOperation.View => Decide(canView, "View"),
            AtlasOperation.History => Decide(permissions?.History == null ? canView : roles.MatchesAny(permissions.History), "History"),
            AtlasOperation.Create => Decide(permissions?.Create == null || roles.MatchesAny(permissions.Create), "Create"),
            AtlasOperation.Delete => Decide(permissions?.Delete == null || roles.MatchesAny(permissions.Delete), "Delete"),
            AtlasOperation.Edit => Decide(CanEdit(permissions?.Edit, roles, form, record), "Edit"),
            _ => new(false, "Unknown operation.")
        };
    }

    public void Demand(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, AtlasOperation operation,
        IReadOnlyDictionary<string, object?>? record, int recordId)
    {
        AtlasAccessDecision decision = Evaluate(manifest, form, operation, record);
        if (decision.Allowed)
            return;

        logger.LogWarning(AccessDenied, "Atlas.AccessDenied LanId={LanId} App={App} Record={Record} Operation={Operation} Mode={Mode} Reason={Reason}",
            user.LanId, manifest.Code, recordId, operation, manifest.AccessMode, decision.Reason);

        if (manifest.AccessMode == AtlasAccessMode.Enforce)
            throw AtlasProblemException.Forbidden(decision.Reason);
    }

    public bool IsOwner(CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record)
    {
        if (record == null)
            return true;
        if (form?.OwnerFields is not { Count: > 0 } fields)
            return false;

        return fields.Any(field => record.TryGetValue(field, out object? value)
            && value is string text
            && string.Equals(text.Trim(), user.LanId, StringComparison.OrdinalIgnoreCase));
    }

    public CPUserPermissions UserPermissions(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record) =>
        new()
        {
            CanCreate = Evaluate(manifest, form, AtlasOperation.Create, null).Allowed,
            CanEdit = Evaluate(manifest, form, AtlasOperation.Edit, record).Allowed,
            CanDelete = record != null && Evaluate(manifest, form, AtlasOperation.Delete, record).Allowed,
            CanViewHistory = record != null && Evaluate(manifest, form, AtlasOperation.History, record).Allowed
        };

    private bool CanEdit(CPEditPermission? edit, AtlasAppRoles roles, CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record)
    {
        if (edit == null)
            return true;
        if (roles.MatchesAny(edit.All))
            return true;
        return roles.MatchesAny(edit.Own) && IsOwner(form, record);
    }

    private static AtlasAccessDecision Decide(bool allowed, string permission) =>
        new(allowed, allowed ? "" : $"The user's roles do not have the {permission} permission.");
}
