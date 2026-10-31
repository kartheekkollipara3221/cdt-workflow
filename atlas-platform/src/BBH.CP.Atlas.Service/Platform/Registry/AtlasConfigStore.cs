using System.Collections.Concurrent;
using BBH.CP.Atlas.Data.DTO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BBH.CP.Atlas.Service.Platform.Registry;

/// <summary>
/// Reads the manifests, form JSON and dashboard JSON from one folder (AppSettings:ConfigFolder, default "Config",
/// relative to the content root; spec 07, 4.7). The file text is kept in memory and read again when the file
/// changes. Each form and dashboard call returns a new object, so a request can change it (spec 01, 5.6).
/// The manifests are one parsed, shared snapshot (review HO-07): do not change a manifest object. The store looks
/// at the folder (names, times, sizes) at most every AppSettings:ConfigRefreshSeconds (default 2; 0 = each call)
/// and parses the manifests again only when the folder changed.
/// One manifest file that cannot be read does not stop the others (review HO-02): the last good version of that file
/// stays in use (or, with no good version, only that app is missing), Atlas.ConfigInvalid is logged and ConfigErrors
/// names the file for the start-up check.
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

    /// <summary>Manifest files that could not be read in the current snapshot (review HO-02).</summary>
    IReadOnlyList<string> ConfigErrors => [];
}

public sealed class AtlasConfigStore : IAtlasConfigStore
{
    public static readonly EventId ConfigInvalid = new(41021, "Atlas.ConfigInvalid");

    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly TimeSpan _refresh;
    private readonly ConcurrentDictionary<string, (DateTime Stamp, string Text)> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private ManifestSnapshot? _snapshot;

    public AtlasConfigStore(IConfiguration configuration, IHostEnvironment environment, TimeProvider? time = null,
        ILogger<AtlasConfigStore>? logger = null)
    {
        _configuration = configuration;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _time = time ?? TimeProvider.System;
        string folder = configuration["AppSettings:ConfigFolder"] is { Length: > 0 } configured ? configured : "Config";
        ConfigFolder = Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(environment.ContentRootPath, folder));
        _refresh = TimeSpan.FromSeconds(int.TryParse(configuration["AppSettings:ConfigRefreshSeconds"], out int seconds) && seconds >= 0 ? seconds : 2);
    }

    public string ConfigFolder { get; }

    public IReadOnlyList<AtlasAppManifest> GetManifests() => Snapshot().Manifests;

    public AtlasAppManifest? FindManifest(string appCode) =>
        Snapshot().ByCode.GetValueOrDefault(AtlasAppCode.Normalize(appCode));

    public IReadOnlyList<string> ConfigErrors => Snapshot().Errors;

    private sealed record ManifestSnapshot(
        string Signature, IReadOnlyList<AtlasAppManifest> Manifests, IReadOnlyDictionary<string, AtlasAppManifest> ByCode,
        IReadOnlyList<string> Errors, DateTimeOffset CheckedAt);

    private ManifestSnapshot Snapshot()
    {
        DateTimeOffset now = _time.GetUtcNow();
        ManifestSnapshot? current = Volatile.Read(ref _snapshot);
        if (current != null && now - current.CheckedAt < _refresh)
            return current;

        lock (_lock)
        {
            current = _snapshot;
            if (current != null && now - current.CheckedAt < _refresh)
                return current;

            string[] paths;
            string signature;
            try
            {
                paths = Directory.Exists(ConfigFolder)
                    ? [.. Directory.GetFiles(ConfigFolder, "*.app.json").Order(StringComparer.OrdinalIgnoreCase)]
                    : [];
                signature = string.Join("|", paths.Select(path =>
                {
                    FileInfo file = new(path);
                    return $"{path}:{file.LastWriteTimeUtc.Ticks}:{file.Length}";
                }));
            }
            catch (Exception ex) when (current != null && ex is IOException or UnauthorizedAccessException)
            {
                // Review 3 RV-L05: the folder cannot be listed now (a share or ACL error). The last good apps stay in use;
                // the next refresh tries again.
                AtlasLog.ConfigInvalid(_logger, ConfigFolder, $"The config folder cannot be read: {ex.Message} The last good version stays in use.");
                ManifestSnapshot kept = current with { CheckedAt = now };
                Volatile.Write(ref _snapshot, kept);
                return kept;
            }

            ManifestSnapshot next = current != null && current.Signature == signature
                ? current with { CheckedAt = now }
                : Build(paths, signature, now, current);
            Volatile.Write(ref _snapshot, next);
            return next;
        }
    }

    private ManifestSnapshot Build(string[] paths, string signature, DateTimeOffset now, ManifestSnapshot? previous)
    {
        List<AtlasAppManifest> manifests = [];
        List<string> errors = [];
        foreach (string path in paths)
        {
            try
            {
                AtlasAppManifest manifest = AtlasJson.Deserialize<AtlasAppManifest>(ReadText(path), Path.GetFileName(path));
                if (string.IsNullOrWhiteSpace(manifest.Code))
                    throw new InvalidOperationException($"{Path.GetFileName(path)} has no Code.");
                manifest.Code = AtlasAppCode.Normalize(manifest.Code);
                manifest.SourceFile = path;
                ApplyEnvironmentOverrides(manifest);
                manifests.Add(manifest);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                AtlasAppManifest? lastGood = previous?.Manifests.FirstOrDefault(x => string.Equals(x.SourceFile, path, StringComparison.OrdinalIgnoreCase));
                string result = lastGood != null
                    ? $"The last good version ({lastGood.Code}) stays in use."
                    : "The app of this file is not available.";
                string message = $"{Path.GetFileName(path)} cannot be read: {ex.Message} {result}";
                errors.Add(message);
                AtlasLog.ConfigInvalid(_logger, Path.GetFileName(path), message);
                if (lastGood != null)
                    manifests.Add(lastGood);
            }
        }

        Dictionary<string, AtlasAppManifest> byCode = [];
        foreach (AtlasAppManifest manifest in manifests)
        {
            if (byCode.TryAdd(manifest.Code, manifest))
                continue; // the first file wins, as FirstOrDefault did
            // Review 3 RV-L09: a second file with the same Code was ignored with no message.
            string message = $"{Path.GetFileName(manifest.SourceFile)} has the Code {manifest.Code}, which {Path.GetFileName(byCode[manifest.Code].SourceFile)} already has. The second file is not used.";
            if (!errors.Contains(message))
            {
                errors.Add(message);
                AtlasLog.ConfigInvalid(_logger, Path.GetFileName(manifest.SourceFile) ?? "", message);
            }
        }

        return new ManifestSnapshot(signature, manifests, byCode, errors, now);
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
        // Review HO-09: compare with the folder and a separator, so "..\ConfigOld\x.json" is outside "Config".
        string folder = Path.EndsInDirectorySeparator(ConfigFolder) ? ConfigFolder : ConfigFolder + Path.DirectorySeparatorChar;
        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
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
