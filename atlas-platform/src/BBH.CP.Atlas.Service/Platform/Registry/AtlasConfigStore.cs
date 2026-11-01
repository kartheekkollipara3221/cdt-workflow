using BBH.CP.Atlas.Data.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// Reads the manifests, form JSON and dashboard JSON from one folder (AppSettings:ConfigFolder, default "Config",
/// relative to the content root; spec 07, 4.7) one time, at the first use (the platform start-up), and keeps them
/// (review FABLE F04). A change to a config file is seen after a restart of the app pool, as a table change.
/// Each form and dashboard call returns a new object, so a request can change it (spec 01, 5.6). The manifests are
/// shared (review HO-07): do not change a manifest object.
/// One manifest file that cannot be read does not stop the others (review HO-02): only that app is missing,
/// Atlas.ConfigInvalid is logged and ConfigErrors names the file for the start-up check.
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

    /// <summary>Manifest files that could not be read (review HO-02).</summary>
    IReadOnlyList<string> ConfigErrors => [];
}

public sealed class AtlasConfigStore : IAtlasConfigStore
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private Catalog? _catalog;

    public AtlasConfigStore(IConfiguration configuration, IHostEnvironment environment, ILogger<AtlasConfigStore>? logger = null)
    {
        _configuration = configuration;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        string folder = AtlasPlatformOptions.From(configuration).ConfigFolder;
        ConfigFolder = Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(environment.ContentRootPath, folder));
    }

    public string ConfigFolder { get; }

    public IReadOnlyList<AtlasAppManifest> GetManifests() => Read().Manifests;

    public AtlasAppManifest? FindManifest(string appCode) =>
        Read().ByCode.GetValueOrDefault(AtlasAppCode.Normalize(appCode));

    public IReadOnlyList<string> ConfigErrors => Read().Errors;

    /// <summary>The text of a form or dashboard file, or the error of its read (thrown again on each use).</summary>
    private sealed record FileText(string? Text, Exception? Error);

    private sealed record Catalog(
        IReadOnlyList<AtlasAppManifest> Manifests, IReadOnlyDictionary<string, AtlasAppManifest> ByCode,
        IReadOnlyList<string> Errors, IReadOnlyDictionary<string, FileText> Files);

    /// <summary>The config, read at the first call. A read that fails (the folder cannot be listed) is tried again at the next call.</summary>
    private Catalog Read()
    {
        Catalog? current = Volatile.Read(ref _catalog);
        if (current != null)
            return current;

        lock (_lock)
        {
            current = _catalog ?? Build();
            Volatile.Write(ref _catalog, current);
            return current;
        }
    }

    private Catalog Build()
    {
        string[] paths = Directory.Exists(ConfigFolder)
            ? [.. Directory.GetFiles(ConfigFolder, "*.app.json").Order(StringComparer.OrdinalIgnoreCase)]
            : [];

        List<AtlasAppManifest> manifests = [];
        List<string> errors = [];
        foreach (string path in paths)
        {
            try
            {
                AtlasAppManifest manifest = AtlasJson.Deserialize<AtlasAppManifest>(File.ReadAllText(path), Path.GetFileName(path));
                if (string.IsNullOrWhiteSpace(manifest.Code))
                    throw new InvalidOperationException($"{Path.GetFileName(path)} has no Code.");
                manifest.Code = AtlasAppCode.Normalize(manifest.Code);
                manifest.SourceFile = path;
                ApplyEnvironmentOverrides(manifest);
                manifests.Add(manifest);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                string message = $"{Path.GetFileName(path)} cannot be read: {ex.Message} The app of this file is not available.";
                errors.Add(message);
                AtlasLog.ConfigInvalid(_logger, Path.GetFileName(path), message);
            }
        }

        Dictionary<string, AtlasAppManifest> byCode = [];
        foreach (AtlasAppManifest manifest in manifests)
        {
            if (byCode.TryAdd(manifest.Code, manifest))
                continue; // the first file wins, as FirstOrDefault did
            // Review 3 RV-L09: a second file with the same Code was ignored with no message.
            string message = $"{Path.GetFileName(manifest.SourceFile)} has the Code {manifest.Code}, which {Path.GetFileName(byCode[manifest.Code].SourceFile)} already has. The second file is not used.";
            errors.Add(message);
            AtlasLog.ConfigInvalid(_logger, Path.GetFileName(manifest.SourceFile) ?? "", message);
        }

        Dictionary<string, FileText> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (string? name in byCode.Values.SelectMany(x => new[] { x.Form, x.Dashboard }))
            if (!string.IsNullOrWhiteSpace(name) && !files.ContainsKey(name))
                files[name] = ReadFile(name);

        return new Catalog(manifests, byCode, errors, files);
    }

    private FileText ReadFile(string fileName)
    {
        try
        {
            return new FileText(File.ReadAllText(Resolve(fileName)), null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new FileText(null, ex);
        }
    }

    private string Text(string fileName)
    {
        FileText file = Read().Files.GetValueOrDefault(fileName) ?? ReadFile(fileName);
        if (file.Error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(file.Error);
        return file.Text!;
    }

    public CPAtlasApplicationConfig GetForm(string appCode)
    {
        AtlasAppManifest manifest = FindManifest(appCode)
            ?? throw AtlasProblemException.NotFound($"App '{appCode}' is not registered.");
        if (string.IsNullOrWhiteSpace(manifest.Form))
            throw AtlasProblemException.NotFound($"App '{manifest.Code}' has no form.");

        return AtlasJson.Deserialize<CPAtlasApplicationConfig>(Text(manifest.Form), manifest.Form);
    }

    public CPAtlasDashboard? GetDashboard(string appCode)
    {
        AtlasAppManifest? manifest = FindManifest(appCode);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Dashboard))
            return null;

        CPAtlasDashboard dashboard = AtlasJson.Deserialize<CPAtlasDashboard>(Text(manifest.Dashboard), manifest.Dashboard);
        dashboard.AppCode ??= manifest.Code;
        return dashboard;
    }

    private string Resolve(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(ConfigFolder, fileName));
        // Review HO-09: compare with the folder and a separator, so "..\ConfigOld\x.json" is outside "Config".
        string folder = Path.EndsInDirectorySeparator(ConfigFolder) ? ConfigFolder : ConfigFolder + Path.DirectorySeparatorChar;
        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Config file '{fileName}' is outside the config folder.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file '{fileName}' was not found in {ConfigFolder}.", path);
        return path;
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

        if (AtlasEnumText.TryParse(text, out T parsed))
        {
            value = parsed;
            return true;
        }

        manifest.SettingErrors.Add(Invalid(manifest.Code, key, text, string.Join(", ", Enum.GetNames<T>())) + $" The value {whenInvalid} is used.");
        return true;
    }

    private static string Invalid(string code, string key, string text, string allowed) =>
        $"the setting AppSettings:Modules:{code}:{key} = '{text}' is not valid (allowed: {allowed}).";
}
