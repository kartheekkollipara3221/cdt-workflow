using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service.Platform.Security;

public enum AtlasOperation { View, Create, Edit, Delete, History }

public sealed record AtlasAccessDecision(bool Allowed, string Reason);

/// <summary>
/// One access check for each operation (spec 06, 4.3). The platform calls it before each operation; a module does
/// not call it itself. AccessMode LogOnly writes Atlas.AccessDenied and continues; Enforce returns 403.
/// A Delete is never only logged (review SV-02): LogOnly keeps users at work while the role grants are checked, and
/// that does not need a delete. The save of a new record is not only logged either (review 3 RV-04, in the save).
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

        AtlasLog.AccessDenied(logger, user.LanId, manifest.Code, recordId, operation.ToString(), manifest.AccessMode.ToString(), decision.Reason);

        if (manifest.AccessMode == AtlasAccessMode.Enforce || operation == AtlasOperation.Delete)
            throw AtlasProblemException.Forbidden(decision.Reason);
    }

    public bool IsOwner(CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record)
    {
        if (record == null)
            return true;
        if (form?.OwnerFields is not { Count: > 0 } fields)
            return false;

        return fields.Any(field => ValueOf(record, field) is string text
            && string.Equals(text.Trim(), user.LanId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The value of a column, with the name in any case (review SV-09): the record keys are the SQL column names, and
    /// the start-up check also ignores case, so "TaxPreparerLanID" must find "TaxPreparerLanId".
    /// </summary>
    private static object? ValueOf(IReadOnlyDictionary<string, object?> record, string field)
    {
        if (record.TryGetValue(field, out object? value))
            return value;
        foreach ((string key, object? item) in record)
            if (string.Equals(key, field, StringComparison.OrdinalIgnoreCase))
                return item;
        return null;
    }

    public CPUserPermissions UserPermissions(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record) =>
        new()
        {
            CanCreate = Evaluate(manifest, form, AtlasOperation.Create, null).Allowed,
            // Review 3 RV-17: what the save will accept. In LogOnly a denied Edit is only logged, so the client keeps the
            // Save button (the rules still make the fields read-only); in Enforce the client hides it.
            CanEdit = Evaluate(manifest, form, AtlasOperation.Edit, record).Allowed || manifest.AccessMode == AtlasAccessMode.LogOnly,
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
