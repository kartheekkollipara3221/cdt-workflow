using BBH.CP.Atlas.Data.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// Reads and keeps the manifests, form JSON and dashboard JSON of the platform apps.
/// </summary>
/// <remarks>
/// The store reads one folder (AppSettings:ConfigFolder, default "Config", relative to the content root).
/// It reads the files one time, at the first use (the platform start-up), and keeps them.
/// A change to a config file takes effect after a restart of the app pool, as a table change does.
/// Each form and dashboard call returns a new object, so a request can change it.
/// The manifests are shared objects. Do not change a manifest object.
/// A manifest file that cannot be read does not stop the others. Only the app of that file is missing.
/// The store then logs Atlas.ConfigInvalid, and ConfigErrors names the file for the start-up check.
/// </remarks>
public interface IAtlasConfigStore
{
    /// <summary>The full path of the config folder.</summary>
    string ConfigFolder { get; }

    /// <summary>Returns all manifests that the store could read.</summary>
    IReadOnlyList<AtlasAppManifest> GetManifests();

    /// <summary>Returns the manifest of an app code, or null when there is none.</summary>
    AtlasAppManifest? FindManifest(string appCode);

    /// <summary>Returns a new copy of the form JSON of the app.</summary>
    /// <exception cref="AtlasProblemException">404 when the app is not registered or has no form.</exception>
    CPAtlasApplicationConfig GetForm(string appCode);

    /// <summary>Returns a new copy of the dashboard JSON of the app, or null when the app has no dashboard.</summary>
    CPAtlasDashboard? GetDashboard(string appCode);

    /// <summary>The errors of the manifest files that the store could not use.</summary>
    IReadOnlyList<string> ConfigErrors => [];
}

/// <summary>Reads the config files from the config folder. See <see cref="IAtlasConfigStore"/>.</summary>
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

    /// <summary>Returns the config, read at the first call.</summary>
    /// <remarks>When the read fails (the folder cannot be listed), the next call tries again.</remarks>
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
                continue; // The first file wins.
            // A second file with the same Code is not used. Report it, so the operator can see it.
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
        // Compare with the folder and a separator, so "..\ConfigOld\x.json" is outside "Config".
        string folder = Path.EndsInDirectorySeparator(ConfigFolder) ? ConfigFolder : ConfigFolder + Path.DirectorySeparatorChar;
        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Config file '{fileName}' is outside the config folder.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file '{fileName}' was not found in {ConfigFolder}.", path);
        return path;
    }

    // An environment value has priority over the manifest value. A value that is present but not valid fails closed:
    // Enabled becomes false and AccessMode becomes Enforce. The other modes keep the manifest value.
    // Each such value is a setting error that the start-up check reports.
    // Only the names of the enum values are accepted, not numbers.
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

    /// <summary>Returns true when the key is present.</summary>
    /// <remarks>
    /// The value is the named enum value. When the text is not valid, the value is <paramref name="whenInvalid"/>,
    /// and the method adds a setting error.
    /// </remarks>
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
