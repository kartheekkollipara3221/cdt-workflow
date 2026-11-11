using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service.Platform.Security;

/// <summary>The operations that the access check controls.</summary>
public enum AtlasOperation { View, Create, Edit, Delete, History }

/// <summary>The result of an access check. Reason is empty when the operation is allowed.</summary>
public sealed record AtlasAccessDecision(bool Allowed, string Reason);

/// <summary>
/// Checks the access of the current user for each operation.
/// </summary>
/// <remarks>
/// The platform calls the check before each operation. A module does not call it.
/// In AccessMode LogOnly, a refusal writes Atlas.AccessDenied and the operation continues.
/// In AccessMode Enforce, a refusal gives 403.
/// A refused Delete always gives 403. LogOnly keeps users at work while the role grants are checked.
/// That work does not need a delete. A refused save of a new record also gives 403 in all modes (see the save).
/// </remarks>
public sealed class AtlasAccessService(IAtlasUserContext user, ILogger<AtlasAccessService> logger)
{
    /// <summary>Returns the access decision for an operation. It does not log and does not throw.</summary>
    /// <param name="record">The stored record, or null for a new record or no record.</param>
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

    /// <summary>Checks the access for an operation and logs a refusal.</summary>
    /// <exception cref="AtlasProblemException">403 when the access is refused in Enforce mode, or for a Delete.</exception>
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

    /// <summary>
    /// Returns true when the LAN ID of the current user is in one of the OwnerFields of the record.
    /// For a new record, it returns true.
    /// </summary>
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
    /// Returns the value of a column. The name can be in any case.
    /// </summary>
    /// <remarks>
    /// The record keys are the SQL column names, and the start-up check also ignores case.
    /// Thus "TaxPreparerLanID" must find "TaxPreparerLanId".
    /// </remarks>
    private static object? ValueOf(IReadOnlyDictionary<string, object?> record, string field)
    {
        if (record.TryGetValue(field, out object? value))
            return value;
        foreach ((string key, object? item) in record)
            if (string.Equals(key, field, StringComparison.OrdinalIgnoreCase))
                return item;
        return null;
    }

    /// <summary>Returns the permissions of the current user for a record, for the client.</summary>
    /// <param name="record">The stored record, or null for a new record.</param>
    public CPUserPermissions UserPermissions(AtlasAppManifest manifest, CPAtlasApplicationConfig? form, IReadOnlyDictionary<string, object?>? record) =>
        new()
        {
            CanCreate = Evaluate(manifest, form, AtlasOperation.Create, null).Allowed,
            // CanEdit tells what the save accepts. In LogOnly, the save only logs a refused Edit.
            // Thus the client keeps the Save button, and the rules still make the fields read-only.
            // In Enforce, the client hides the Save button.
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
