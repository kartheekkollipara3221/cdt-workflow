using System.Collections.Concurrent;
using BBH.CP.Atlas.Data.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// Reads the manifests, form JSON and dashboard JSON from one folder (AppSettings:ConfigFolder, default "Config",
/// relative to the content root; spec 07, 4.7). The file text is kept in memory and read again when the file
/// changes. Each call returns a new object, so a request can change it (spec 01, 5.6).
/// </summary>
public interface IAtlasConfigStore
{
    string ConfigFolder { get; }

    IReadOnlyList<AtlasAppManifest> GetManifests();

    AtlasAppManifest? FindManifest(string appCode);

    /// <summary>The form JSON of the app (a new copy). Throws 404 if the app has no form.</summary>
    CPAtlasApplicationConfig GetForm(string appCode);

    /// <summary>The dashboard JSON of the app (a new copy), or null if the app has no dashboard.</summary>
    CPAtlasDashboard? GetDashboard(string appCode);
}

public sealed class AtlasConfigStore : IAtlasConfigStore
{
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, (DateTime Stamp, string Text)> _files = new(StringComparer.OrdinalIgnoreCase);

    public AtlasConfigStore(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        string folder = configuration["AppSettings:ConfigFolder"] is { Length: > 0 } configured ? configured : "Config";
        ConfigFolder = Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(environment.ContentRootPath, folder));
    }

    public string ConfigFolder { get; }

    public IReadOnlyList<AtlasAppManifest> GetManifests()
    {
        if (!Directory.Exists(ConfigFolder))
            return [];

        List<AtlasAppManifest> manifests = [];
        foreach (string path in Directory.GetFiles(ConfigFolder, "*.app.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            AtlasAppManifest manifest = AtlasJson.Deserialize<AtlasAppManifest>(ReadText(path), Path.GetFileName(path));
            manifest.Code = AtlasAppCode.Normalize(manifest.Code);
            manifest.SourceFile = path;
            ApplyEnvironmentOverrides(manifest);
            manifests.Add(manifest);
        }

        return manifests;
    }

    public AtlasAppManifest? FindManifest(string appCode)
    {
        string code = AtlasAppCode.Normalize(appCode);
        return GetManifests().FirstOrDefault(x => x.Code == code);
    }

    public CPAtlasApplicationConfig GetForm(string appCode)
    {
        AtlasAppManifest manifest = FindManifest(appCode)
            ?? throw AtlasProblemException.NotFound($"App '{appCode}' is not registered.");
        if (string.IsNullOrWhiteSpace(manifest.Form))
            throw AtlasProblemException.NotFound($"App '{manifest.Code}' has no form.");

        string path = Resolve(manifest.Form);
        return AtlasJson.Deserialize<CPAtlasApplicationConfig>(ReadText(path), manifest.Form);
    }

    public CPAtlasDashboard? GetDashboard(string appCode)
    {
        AtlasAppManifest? manifest = FindManifest(appCode);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Dashboard))
            return null;

        string path = Resolve(manifest.Dashboard);
        CPAtlasDashboard dashboard = AtlasJson.Deserialize<CPAtlasDashboard>(ReadText(path), manifest.Dashboard);
        dashboard.AppCode ??= manifest.Code;
        return dashboard;
    }

    private string Resolve(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(ConfigFolder, fileName));
        if (!path.StartsWith(ConfigFolder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Config file '{fileName}' is outside the config folder.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file '{fileName}' was not found in {ConfigFolder}.", path);
        return path;
    }

    private string ReadText(string path)
    {
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        if (_files.TryGetValue(path, out var cached) && cached.Stamp == stamp)
            return cached.Text;

        string text = File.ReadAllText(path);
        _files[path] = (stamp, text);
        return text;
    }

    // Environment values win (spec 07, 4.1; spec 11, 4.5). A value that is present but not valid fails closed
    // (review HO-03): Enabled = false, AccessMode = Enforce; the other modes keep the manifest value. Each one is a
    // setting error that the start-up check reports. Only the names of the enum values are accepted (not numbers).
    private void ApplyEnvironmentOverrides(AtlasAppManifest manifest)
    {
        IConfigurationSection section = _configuration.GetSection($"AppSettings:Modules:{manifest.Code}");
        if (!section.Exists())
            return;

        if (section["Enabled"] is { } enabledText)
        {
            if (bool.TryParse(enabledText.Trim(), out bool enabled))
                manifest.Enabled = enabled;
            else
            {
                manifest.Enabled = false;
                manifest.SettingErrors.Add(Invalid(manifest.Code, "Enabled", enabledText, "true, false") + " The app is disabled.");
            }
        }

        if (Override(manifest, section, "AccessMode", out AtlasAccessMode accessMode, AtlasAccessMode.Enforce))
            manifest.AccessMode = accessMode;
        if (Override(manifest, section, "AuditMode", out AtlasAuditMode auditMode, manifest.AuditMode))
            manifest.AuditMode = auditMode;
        if (Override(manifest, section, "RuleMode", out AtlasRuleMode ruleMode, manifest.RuleMode))
            manifest.RuleMode = ruleMode;
        if (Override(manifest, section, "RuleDefault", out AtlasRuleDefault ruleDefault, manifest.RuleDefault))
            manifest.RuleDefault = ruleDefault;
    }

    /// <summary>True when the key is present: value = the named enum value, or <paramref name="whenInvalid"/> with a setting error.</summary>
    private static bool Override<T>(AtlasAppManifest manifest, IConfigurationSection section, string key, out T value, T whenInvalid)
        where T : struct, Enum
    {
        value = whenInvalid;
        if (section[key] is not { } text)
            return false;

        string? name = Enum.GetNames<T>().FirstOrDefault(x => string.Equals(x, text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name != null)
        {
            value = Enum.Parse<T>(name);
            return true;
        }

        manifest.SettingErrors.Add(Invalid(manifest.Code, key, text, string.Join(", ", Enum.GetNames<T>())) + $" The value {whenInvalid} is used.");
        return true;
    }

    private static string Invalid(string code, string key, string text, string allowed) =>
        $"the setting AppSettings:Modules:{code}:{key} = '{text}' is not valid (allowed: {allowed}).";
}
