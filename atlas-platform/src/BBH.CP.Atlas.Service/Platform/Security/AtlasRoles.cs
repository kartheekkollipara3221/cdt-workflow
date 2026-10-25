namespace BBH.CP.Atlas.Service.Platform.Security;

/// <summary>One active role of a user, from dbo.vw_CP_Atlas_ActiveUserRole (spec 06, 4.4).</summary>
public sealed record AtlasRole(string AppCode, string RoleName, bool IsRead, bool IsWrite, bool IsAdmin);

/// <summary>
/// The roles of the user for one app and its RoleScope (own app + parent, spec 01, 11.2), with the app-level
/// flags. Flag order (spec 06, question 6): IsAdmin includes write and read; IsWrite includes read.
/// </summary>
public sealed class AtlasAppRoles
{
    public const string ReadFlag = "@Read";
    public const string WriteFlag = "@Write";
    public const string AdminFlag = "@Admin";

    public static AtlasAppRoles None { get; } = new([]);

    public AtlasAppRoles(IEnumerable<AtlasRole> roles)
    {
        Roles = [.. roles];
        RoleNames = [.. Roles.Select(x => x.RoleName).Distinct(StringComparer.OrdinalIgnoreCase)];
        IsAdmin = Roles.Any(x => x.IsAdmin);
        IsWrite = IsAdmin || Roles.Any(x => x.IsWrite);
        IsRead = IsWrite || Roles.Any(x => x.IsRead);
    }

    public IReadOnlyList<AtlasRole> Roles { get; }

    public IReadOnlyList<string> RoleNames { get; }

    public bool IsRead { get; }

    public bool IsWrite { get; }

    public bool IsAdmin { get; }

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

    /// <summary>True if one entry of the list is a role name or a flag of the user. A null or empty list matches nobody.</summary>
    public bool MatchesAny(IEnumerable<string>? entries) =>
        entries != null && entries.Any(entry => Tokens.Contains(entry, StringComparer.OrdinalIgnoreCase));
}
