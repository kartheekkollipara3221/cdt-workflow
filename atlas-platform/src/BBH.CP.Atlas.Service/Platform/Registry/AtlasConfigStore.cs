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

    // Environment values win (spec 07, 4.1; spec 11, 4.5).
    private void ApplyEnvironmentOverrides(AtlasAppManifest manifest)
    {
        IConfigurationSection section = _configuration.GetSection($"AppSettings:Modules:{manifest.Code}");
        if (!section.Exists())
            return;

        if (bool.TryParse(section["Enabled"], out bool enabled))
            manifest.Enabled = enabled;
        if (Enum.TryParse(section["AccessMode"], true, out AtlasAccessMode accessMode))
            manifest.AccessMode = accessMode;
        if (Enum.TryParse(section["AuditMode"], true, out AtlasAuditMode auditMode))
            manifest.AuditMode = auditMode;
        if (Enum.TryParse(section["RuleMode"], true, out AtlasRuleMode ruleMode))
            manifest.RuleMode = ruleMode;
        if (Enum.TryParse(section["RuleDefault"], true, out AtlasRuleDefault ruleDefault))
            manifest.RuleDefault = ruleDefault;
    }
}
