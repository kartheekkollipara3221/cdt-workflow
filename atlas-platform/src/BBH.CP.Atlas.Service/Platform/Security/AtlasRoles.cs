namespace BBH.CP.Atlas.Service.Platform.Security;

/// <summary>One active role of a user, from dbo.vw_CP_Atlas_ActiveUserRole.</summary>
public sealed record AtlasRole(string AppCode, string RoleName, bool IsRead, bool IsWrite, bool IsAdmin);

/// <summary>
/// The roles of the user for one app and its RoleScope (the app and its parent), with the app-level flags.
/// </summary>
/// <remarks>
/// IsAdmin includes write and read. IsWrite includes read.
/// </remarks>
public sealed class AtlasAppRoles
{
    /// <summary>The token of a user that has read access.</summary>
    public const string ReadFlag = "@Read";
    /// <summary>The token of a user that has write access.</summary>
    public const string WriteFlag = "@Write";
    /// <summary>The token of a user that has admin access.</summary>
    public const string AdminFlag = "@Admin";

    /// <summary>No roles.</summary>
    public static AtlasAppRoles None { get; } = new([]);

    /// <summary>Creates the role set and calculates the app-level flags.</summary>
    public AtlasAppRoles(IEnumerable<AtlasRole> roles)
    {
        Roles = [.. roles];
        RoleNames = [.. Roles.Select(x => x.RoleName).Distinct(StringComparer.OrdinalIgnoreCase)];
        IsAdmin = Roles.Any(x => x.IsAdmin);
        IsWrite = IsAdmin || Roles.Any(x => x.IsWrite);
        IsRead = IsWrite || Roles.Any(x => x.IsRead);
    }

    /// <summary>The active roles.</summary>
    public IReadOnlyList<AtlasRole> Roles { get; }

    /// <summary>The distinct role names.</summary>
    public IReadOnlyList<string> RoleNames { get; }

    /// <summary>True when the user has read access.</summary>
    public bool IsRead { get; }

    /// <summary>True when the user has write access.</summary>
    public bool IsWrite { get; }

    /// <summary>True when the user has admin access.</summary>
    public bool IsAdmin { get; }

    /// <summary>True when the user has one or more roles.</summary>
    public bool HasAnyRole => RoleNames.Count > 0;

    /// <summary>The role names plus the flag names that the user has. Rules and Permissions match against these.</summary>
    public IReadOnlyList<string> Tokens
    {
        get
        {
            List<string> tokens = [.. RoleNames];
            if (IsRead) tokens.Add(ReadFlag);
            if (IsWrite) tokens.Add(WriteFlag);
            if (IsAdmin) tokens.Add(AdminFlag);
            return tokens;
        }
    }

    /// <summary>
    /// Returns true when one entry of the list is a role name or a flag of the user. A null or empty list matches nobody.
    /// </summary>
    public bool MatchesAny(IEnumerable<string>? entries) =>
        entries != null && entries.Any(entry => Tokens.Contains(entry, StringComparer.OrdinalIgnoreCase));
}
