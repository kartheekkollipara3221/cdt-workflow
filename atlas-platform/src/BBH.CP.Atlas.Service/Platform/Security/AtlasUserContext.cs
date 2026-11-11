using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace BBH.CP.Atlas.Service.Platform.Security;

/// <summary>
/// Supplies the current Windows user. At the target, this is the existing IUserInfo (Active Directory).
/// Locally, it is a stand-in.
/// </summary>
public interface IAtlasCurrentUserSource
{
    /// <summary>Returns the current user.</summary>
    CPUser GetCurrentUser();
}

/// <summary>The user context of one request. The LAN ID is the key of the user in all checks.</summary>
public interface IAtlasUserContext
{
    /// <summary>The LAN ID of the current user.</summary>
    string LanId { get; }

    /// <summary>The display name of the current user, or the LAN ID when the name is empty.</summary>
    string DisplayName { get; }

    /// <summary>The current user.</summary>
    CPUser User { get; }

    /// <summary>Returns the active roles of the user for the app and its parent (one query for each request).</summary>
    AtlasAppRoles RolesFor(string appCode);
}

/// <summary>Finds users by LAN ID: the display names (cached) and the users in roles of an app.</summary>
public interface IAtlasUserDirectory
{
    /// <summary>Returns the display name from CP_Atlas_User, or the LAN ID when the user is not found.</summary>
    string DisplayName(string? lanId);

    /// <summary>Returns the display names of many LAN IDs. The default implementation asks for one at a time.</summary>
    IReadOnlyDictionary<string, string> DisplayNames(IEnumerable<string> lanIds) =>
        lanIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x, DisplayName, StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the active users that have one of the roles in the app, ordered by display name.</summary>
    IReadOnlyList<(string LanId, string DisplayName)> UsersInRoles(string appCode, IEnumerable<string> roles);
}

/// <summary>The user context of one request. It reads the roles from dbo.vw_CP_Atlas_ActiveUserRole.</summary>
/// <remarks>A role row with no app code or no role name gives no role, not an error.</remarks>
public sealed class AtlasUserContext(
    IAtlasCurrentUserSource userSource,
    CPSharepointContext context,
    IAtlasConfigStore configStore) : IAtlasUserContext
{
    private CPUser? _user;
    private List<(string AppCode, string? ParentCode, AtlasRole Role)>? _roles;

    public CPUser User => _user ??= userSource.GetCurrentUser();

    public string LanId => User.LanID ?? throw new InvalidOperationException("The current user has no LAN ID.");

    public string DisplayName => string.IsNullOrWhiteSpace(User.DisplayName) ? LanId : User.DisplayName;

    public AtlasAppRoles RolesFor(string appCode)
    {
        string code = AtlasAppCode.Normalize(appCode);
        string? parent = configStore.FindManifest(code)?.Parent;
        _roles ??= LoadRoles();

        return new AtlasAppRoles(_roles
            .Where(x => string.Equals(x.AppCode, code, StringComparison.OrdinalIgnoreCase)
                || (parent != null && string.Equals(x.AppCode, parent, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Role));
    }

    private List<(string, string?, AtlasRole)> LoadRoles() =>
        AtlasSql.Query(context, """
            SELECT r.ApplicationCode, p.Code AS ParentCode, r.RoleName, r.IsRead, r.IsWrite, r.IsAdmin
            FROM dbo.vw_CP_Atlas_ActiveUserRole AS r
            LEFT JOIN dbo.CP_Atlas_Application AS p ON p.ID = r.ParentApplicationId
            WHERE r.LanId = @LanId
              AND r.ApplicationCode IS NOT NULL AND r.RoleName IS NOT NULL; -- review 3 RV-08: a bad row gives no role, not an error
            """,
            reader => (reader.GetString(0), reader.NullableString(1),
                new AtlasRole(reader.GetString(0), reader.GetString(2), reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5))),
            new SqlParameter("@LanId", System.Data.SqlDbType.NVarChar, 50) { Value = LanId });
}

/// <summary>Finds users in CP_Atlas_User and dbo.vw_CP_Atlas_ActiveUserRole, with a memory cache for the names.</summary>
public sealed class AtlasUserDirectory(CPSharepointContext context, IMemoryCache cache) : IAtlasUserDirectory
{
    private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The cache time of a LAN ID with no name. It is short, so a user that is added later soon shows the name.
    /// </summary>
    private static readonly TimeSpan MissTime = TimeSpan.FromMinutes(2);

    /// <summary>Returns the cache options. Each entry has a size, so a host cache with a SizeLimit accepts it.</summary>
    private static MemoryCacheEntryOptions Entry(bool found) =>
        new() { AbsoluteExpirationRelativeToNow = found ? CacheTime : MissTime, Size = 1 };

    public string DisplayName(string? lanId)
    {
        if (string.IsNullOrWhiteSpace(lanId))
            return "";

        string key = "atlas:user:" + lanId.ToUpperInvariant();
        if (cache.TryGetValue(key, out string? cached) && cached != null)
            return cached;

        string? name = AtlasSql.Query(context,
            "SELECT TOP (1) DisplayName FROM dbo.CP_Atlas_User WHERE LanID = @LanId AND ISNULL(IsDeleted, 0) = 0;",
            reader => reader.NullableString(0),
            new SqlParameter("@LanId", System.Data.SqlDbType.NVarChar, 50) { Value = lanId }).FirstOrDefault();
        bool found = !string.IsNullOrWhiteSpace(name);
        return cache.Set(key, found ? name! : lanId, Entry(found));
    }

    /// <summary>Returns the display names of many LAN IDs.</summary>
    /// <remarks>The names that are not in the cache come from one query for each 500 LAN IDs.</remarks>
    public IReadOnlyDictionary<string, string> DisplayNames(IEnumerable<string> lanIds)
    {
        ArgumentNullException.ThrowIfNull(lanIds);
        Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
        List<string> missing = [];
        foreach (string lanId in lanIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (cache.TryGetValue("atlas:user:" + lanId.ToUpperInvariant(), out string? cached) && cached != null)
                names[lanId] = cached;
            else
                missing.Add(lanId);
        }

        foreach (string[] chunk in missing.Chunk(500))
        {
            List<SqlParameter> parameters = [.. chunk.Select((x, i) => new SqlParameter("@L" + i, System.Data.SqlDbType.NVarChar, 50) { Value = x })];
            Dictionary<string, string> found = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string lanId, string? name) in AtlasSql.Query(context,
                $"SELECT LanID, DisplayName FROM dbo.CP_Atlas_User WHERE LanID IN ({string.Join(", ", parameters.Select(x => x.ParameterName))}) AND ISNULL(IsDeleted, 0) = 0;",
                reader => (reader.GetString(0), reader.NullableString(1)), [.. parameters]))
            {
                if (!string.IsNullOrWhiteSpace(name))
                    found.TryAdd(lanId, name);
            }

            foreach (string lanId in chunk)
            {
                string? hit = found.GetValueOrDefault(lanId);
                string name = hit ?? lanId;
                cache.Set("atlas:user:" + lanId.ToUpperInvariant(), name, Entry(hit != null));
                names[lanId] = name;
            }
        }

        return names;
    }

    public IReadOnlyList<(string LanId, string DisplayName)> UsersInRoles(string appCode, IEnumerable<string> roles)
    {
        string[] roleList = [.. roles];
        if (roleList.Length == 0)
            return [];

        List<SqlParameter> parameters = [new("@AppCode", System.Data.SqlDbType.VarChar, 20) { Value = AtlasAppCode.Normalize(appCode) }];
        List<string> names = [];
        for (int i = 0; i < roleList.Length; i++)
        {
            names.Add("@Role" + i);
            parameters.Add(new SqlParameter("@Role" + i, System.Data.SqlDbType.VarChar, 100) { Value = roleList[i] });
        }

        return AtlasSql.Query(context, $"""
            SELECT DISTINCT LanId, ISNULL(NULLIF(DisplayName, ''), LanId)
            FROM dbo.vw_CP_Atlas_ActiveUserRole
            WHERE ApplicationCode = @AppCode AND RoleName IN ({string.Join(", ", names)}) AND LanId IS NOT NULL
            ORDER BY 2;
            """,
            reader => (reader.GetString(0), reader.GetString(1)),
            [.. parameters]);
    }
}
