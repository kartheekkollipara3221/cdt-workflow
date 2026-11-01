using System.Globalization;
using System.Text.Json;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Application.Interfaces;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Audit;
using BBH.CP.Atlas.Service.Platform.Options;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Rules;
using BBH.CP.Atlas.Service.Platform.Sections;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BBH.CP.Atlas.Service.Application
{
    /// <summary>The state of one load, for the <see cref="ConfigSectionService.OnLoad"/> hook.</summary>
    public sealed class AtlasLoadContext
    {
        public required AtlasAppManifest Manifest { get; init; }
        public required CPAtlasApplicationConfig Form { get; init; }
        public required CPApplicationSection Section { get; init; }
        public required AtlasFormFields Fields { get; init; }

        /// <summary>The stored record, or null for a new record.</summary>
        public IReadOnlyDictionary<string, object?>? Record { get; init; }

        public bool IsNew => Record == null;
    }

    /// <summary>The state of one save, for the <see cref="ConfigSectionService.OnBeforeSave"/> and OnAfterSave hooks.</summary>
    public sealed class AtlasSaveContext
    {
        public required AtlasAppManifest Manifest { get; init; }
        public required CPAtlasApplicationConfig Form { get; init; }
        public required CPApplicationSection Section { get; init; }
        public required AtlasFormFields Fields { get; init; }

        /// <summary>The record that is saved. A hook can change the values (column name to CLR value).</summary>
        public required Dictionary<string, object?> Record { get; init; }

        /// <summary>The wire values before the save (empty for a new record).</summary>
        public required IReadOnlyDictionary<string, string?> Previous { get; init; }

        public required bool IsNew { get; init; }

        /// <summary>A hook adds an error to stop the save (OnBeforeSave only).</summary>
        public List<CPPageError> Errors { get; } = [];
    }

    /// <summary>
    /// The generic Section Master service (spec 01, 5.7-5.13; spec 12). A new app needs no class: the manifest names
    /// the tables and the form JSON gives the behaviour. A module class inherits from this class only to add option
    /// providers or a hook (OnLoad, OnBeforeSave, OnAfterSave).
    /// </summary>
    public partial class ConfigSectionService : CPApplicationService, ISectionService, IAtlasModule
    {
        /// <summary>Columns that only the server writes (spec 01, 5.9 step 9).</summary>
        public static readonly IReadOnlySet<string> SystemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AtlasTable.KeyColumnName, "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate", AtlasTable.SoftDeleteColumnName, "RowVersion"
        };

        private Dictionary<string, IAtlasOptionProvider>? _providers;
        private AtlasAppManifest? _manifest;

        public ConfigSectionService(AtlasModuleServices services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Context = services.Context;
            Configuration = services.Configuration;
            Logger = services.LoggerFactory.CreateLogger("BBH.CP.Atlas.Platform.Section");
        }

        /// <summary>Set by the resolver (IAtlasModule).</summary>
        public string AppCode { get; set; } = "";

        protected AtlasModuleServices Services { get; }

        protected ILogger Logger { get; }

        protected AtlasAppManifest Manifest =>
            _manifest ??= Services.ConfigStore.FindManifest(AppCode) is { Enabled: true } manifest
                ? manifest
                : throw AtlasProblemException.NotFound($"App '{AppCode}' is not registered.");

        /// <summary>Option providers of the app. They win over a platform provider with the same name.</summary>
        protected virtual IEnumerable<IAtlasOptionProvider> AppOptionProviders => [];

        #region Hooks

        /// <summary>After the values, options and defaults; before the rules.</summary>
        protected virtual void OnLoad(AtlasLoadContext context)
        {
        }

        /// <summary>After the validation and the mapping; before the database save. Add to Errors to stop.</summary>
        protected virtual void OnBeforeSave(AtlasSaveContext context)
        {
        }

        /// <summary>
        /// After the commit. The hook must not throw: the record is already saved, so an exception is only logged
        /// (Atlas.AfterSaveFailed) and the save returns success with the ID (review SV-06).
        /// </summary>
        protected virtual void OnAfterSave(AtlasSaveContext context)
        {
        }

        #endregion Hooks

        /// <summary>The names of all option providers of this app (for the start-up check).</summary>
        public IEnumerable<string> OptionProviderNames() =>
            Services.OptionProviders.Concat(AppOptionProviders).Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase);

        #region Load

        public CPApplicationSection LoadPage(CPAtlasPage page)
        {
            ArgumentNullException.ThrowIfNull(page);
            AtlasAppManifest manifest = Manifest;
            CPAtlasApplicationConfig form = Services.ConfigStore.GetForm(AppCode);
            AtlasTable table = MainTable(manifest);

            Dictionary<string, object?>? record = null;
            if (page.ID > 0)
            {
                record = Services.PlatformContext.FindActive(table.FullName, page.ID, tracking: false)
                    ?? throw AtlasProblemException.NotFound($"Record {page.ID} of {manifest.Code} was not found.");
                Services.Access.Demand(manifest, form, AtlasOperation.View, record, page.ID);
            }
            else
            {
                Services.Access.Demand(manifest, form, AtlasOperation.Create, null, 0);
            }

            CPApplicationSection section = form.Section
                ?? throw AtlasProblemException.Config($"The form of {manifest.Code} has no Section.");
            AtlasFormFields fields = AtlasFormFields.From(section);

            BindValues(fields, table, record);
            LoadOptions(fields, form);

            OnLoad(new AtlasLoadContext { Manifest = manifest, Form = form, Section = section, Fields = fields, Record = record });

            Services.Rules.ApplyToSection(section, form.Rules, RuleContext(manifest, form, record), manifest.RuleMode, manifest.RuleDefault);
            ClearRuleHiddenValues(section, fields);

            string name = section.HeaderName ?? form.Name ?? manifest.Name ?? manifest.Code;
            section.HeaderName = record != null && !string.IsNullOrWhiteSpace(form.HeaderNameWithId)
                ? form.HeaderNameWithId.Replace("{Name}", name, StringComparison.OrdinalIgnoreCase)
                    .Replace("{ID}", page.ID.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                : name;
            section.AppCode = manifest.Code;
            section.Version = record == null ? null : VersionOf(table, record);
            section.UserPermissions = Services.Access.UserPermissions(manifest, form, record);
            section.Computed = form.Computed;
            return section;
        }

        /// <summary>
        /// Review 3 RV-02: a field that the JSON shows and a rule hides from this user gets no value and no options in
        /// the load. Hidden on the client only is not enough: the value was in the JSON of the response.
        /// A field that the JSON hides (an effect can show it) keeps its value.
        /// </summary>
        private static void ClearRuleHiddenValues(CPApplicationSection section, AtlasFormFields beforeRules)
        {
            AtlasFormFields fields = AtlasFormFields.From(section);
            fields.MarkHiddenByRules(beforeRules);
            foreach (AtlasFormField field in fields.All.Where(x => x.HiddenByRule))
            {
                if (field.IsGridField)
                {
                    foreach (Dictionary<string, string> row in field.Control.Grid?.Data ?? [])
                        if (row.ContainsKey(field.Name))
                            row[field.Name] = "";
                    continue;
                }

                field.Control.Value = null;
                field.Control.Options = [];
                field.Control.OptionsSource = null;
            }
        }

        /// <summary>Review 3 RV-02: the fields (Name and column) that the rules hide from this user for this record.</summary>
        private HashSet<string> RuleHiddenFields(AtlasAppManifest manifest, CPAtlasApplicationConfig form, Dictionary<string, object?>? record)
        {
            HashSet<string> hidden = new(StringComparer.OrdinalIgnoreCase);
            if (form.Section == null)
                return hidden;
            AtlasFormFields beforeRules = AtlasFormFields.From(form.Section);
            Services.Rules.ApplyToSection(form.Section, form.Rules, RuleContext(manifest, form, record), manifest.RuleMode, manifest.RuleDefault);
            AtlasFormFields fields = AtlasFormFields.From(form.Section);
            fields.MarkHiddenByRules(beforeRules);
            foreach (AtlasFormField field in fields.All.Where(x => x.HiddenByRule))
            {
                hidden.Add(field.Name);
                hidden.Add(field.Field);
            }
            return hidden;
        }

        private void BindValues(AtlasFormFields fields, AtlasTable table, Dictionary<string, object?>? record)
        {
            foreach (AtlasFormField field in fields.All.Where(x => !x.IsGridField))
            {
                AtlasColumn? column = table.Find(field.Field);
                if (record != null && column != null)
                    field.Control.Value = Wire(record.GetValueOrDefault(column.Name), column);
            }

            foreach (CPApplicationControl grid in fields.Grids)
            {
                Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
                foreach (AtlasFormField field in fields.FieldsOf(grid.Name))
                {
                    AtlasColumn? column = table.Find(field.Field);
                    row[field.Name] = record != null && column != null
                        ? Wire(record.GetValueOrDefault(column.Name), column) ?? ""
                        : "";
                }

                grid.Grid!.Data = [row];
            }
        }

        private void LoadOptions(AtlasFormFields fields, CPAtlasApplicationConfig form)
        {
            foreach (AtlasFormField field in fields.All.Where(x => !x.IsGridField))
            {
                CPApplicationControl control = field.Control;
                if (control.OptionsSource != null)
                {
                    AtlasOptionRequest request = new(Manifest.Code, control, control.OptionsSource);
                    IAtlasOptionProvider provider = Provider(control.OptionsSource.Provider);
                    List<CPKeyValue> options = [.. provider.GetOptions(request)];

                    if (control.KeepSavedValue && !string.IsNullOrEmpty(control.Value)
                        && !options.Any(x => string.Equals(x.Value, control.Value, StringComparison.OrdinalIgnoreCase)))
                        options.Insert(0, provider.DescribeValue(request, control.Value));

                    control.Options = options;
                }

                if (form.Defaults != null && form.Defaults.TryGetValue(control.Name, out string? token)
                    && string.IsNullOrEmpty(control.Value))
                    control.Value = ResolveDefault(token, control.Options);

                // A person shown as a name (spec 06, 4.5): the value stays the LAN ID.
                if (string.Equals(control.Type, "user", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(control.Value))
                    control.Options = [new CPKeyValue { Label = Services.Directory.DisplayName(control.Value), Value = control.Value }];
            }
        }

        /// <summary>Default tokens (spec 01, 10.2): {User.LanID}, {User.DisplayName}, {Today}, {Options[n]}; else the text.</summary>
        protected string? ResolveDefault(string token, IReadOnlyList<CPKeyValue>? options) =>
            AtlasDefaultTokens.Resolve(token, Services.User.LanId, Services.User.DisplayName, Services.Time.GetLocalNow(), options);

        #endregion Load

        #region Save

        public override CPAtlasPage SavePage(CPAtlasPage input, IFormFileCollection files)
        {
            ArgumentNullException.ThrowIfNull(input);
            AtlasAppManifest manifest = Manifest;
            CPAtlasApplicationConfig form = Services.ConfigStore.GetForm(AppCode);
            AtlasTable table = MainTable(manifest);
            CPAtlasPage result = new() { AppCode = manifest.Code, Code = input.Code, Step = input.Step, ID = 0, Outputs = [] };
            List<CPPageError> errors = [];

            // 1-2. Record and access.
            Dictionary<string, object?>? stored = null;
            if (input.ID > 0)
            {
                stored = Services.PlatformContext.FindActive(table.FullName, input.ID)
                    ?? throw AtlasProblemException.NotFound($"Record {input.ID} of {manifest.Code} was not found.");
                Services.Access.Demand(manifest, form, AtlasOperation.Edit, stored, input.ID);
            }
            else
            {
                Services.Access.Demand(manifest, form, AtlasOperation.Create, null, 0);
                // Review 3 RV-04: a new record is never only logged (as a Delete, review SV-02). In LogOnly the rules make
                // the fields read-only for such a user, so the save would insert a row of defaults only. The load of a
                // new form stays LogOnly.
                AtlasAccessDecision create = Services.Access.Evaluate(manifest, form, AtlasOperation.Create, null);
                if (!create.Allowed)
                    throw AtlasProblemException.Forbidden(create.Reason);
            }

            bool isNew = stored == null;
            Dictionary<string, string?> previous = Snapshot(table, stored);

            // 3. Effective section: rules for this user and record.
            CPApplicationSection section = form.Section
                ?? throw AtlasProblemException.Config($"The form of {manifest.Code} has no Section.");
            AtlasFormFields beforeRules = AtlasFormFields.From(section);
            Services.Rules.ApplyToSection(section, form.Rules, RuleContext(manifest, form, stored), manifest.RuleMode, manifest.RuleDefault);
            AtlasFormFields fields = AtlasFormFields.From(section);
            fields.MarkHiddenByRules(beforeRules);

            Dictionary<string, string?> submitted = ReadOutputs(input, fields, errors);
            string? Current(AtlasFormField field) =>
                submitted.TryGetValue(field.Name, out string? value) ? value : previous.GetValueOrDefault(field.Field);

            // Server part of Effects (Show, Hide, Require, Unrequire) and the SetFromOption targets.
            HashSet<string> derived = AtlasServerEffects.Apply(fields, Current);
            HashSet<string> computed = new((IEnumerable<string>?)form.Computed?.Keys ?? [], StringComparer.OrdinalIgnoreCase);

            // 4. Allow list.
            Dictionary<AtlasFormField, string?> accepted = AtlasAllowList.Check(submitted, fields, table, previous,
                field => SystemColumns.Contains(field.Field) || computed.Contains(field.Field) || derived.Contains(field.Name),
                (field, value, previous) => OptionError(field, value, previous, table), errors,
                (field, reason) =>
                {
                    if (reason == AtlasIgnoreReason.ServerSet)
                        AtlasLog.FieldIgnored(Logger, manifest.Code, field.Name, "ServerSet");
                });

            // Review SV-12: a refused field is a warning, so a probe or a client defect can be found in the log.
            foreach (CPPageError refusal in errors.Where(x => x.Code is "NotAllowed" or "NotInOptions" or "InvalidGrid"))
                AtlasLog.FieldRefused(Logger, Services.User.LanId, manifest.Code, input.ID, refusal.Field ?? "", refusal.Code);

            // 6. Validators and the Require effects (visible and enabled fields only; one error for each field).
            HashSet<string> refused = new(errors.Select(x => x.Field ?? ""), StringComparer.OrdinalIgnoreCase);
            foreach (AtlasFormField field in fields.All.Where(x => x.Visible && !x.Disabled))
            {
                if (SystemColumns.Contains(field.Field) || computed.Contains(field.Field) || derived.Contains(field.Name)
                    || refused.Contains(field.Name))
                    continue;

                string? value = accepted.TryGetValue(field, out string? sent) ? sent : previous.GetValueOrDefault(field.Field);
                if (isNew && string.IsNullOrEmpty(value) && form.Defaults != null && form.Defaults.ContainsKey(field.Name))
                    continue; // the default is set at step 9

                if (field.IsGridField && string.IsNullOrWhiteSpace(value) && field.Validator?.Integer == true)
                {
                    value = "0"; // spec 01, 10.3: an empty day value is 0
                    accepted[field] = value;
                }

                if (AtlasFieldValidator.Validate(field, value) is { } error)
                    errors.Add(error);
            }

            // 7. Version token.
            // Review DA-02: the rowversion column is found by its type, and an update with no Version is refused.
            if (stored != null && RowVersion(table) != null)
            {
                if (string.IsNullOrWhiteSpace(input.Version))
                {
                    if (manifest.RequireVersion)
                        errors.Add(VersionRequired());
                }
                else if (!string.Equals(input.Version, VersionOf(table, stored), StringComparison.Ordinal))
                    errors.Add(Conflict());
            }

            // 8. Conversion and mapping.
            Dictionary<string, object?> record = stored ?? table.NewRecord();
            HashSet<string> invalid = new(errors.Select(x => x.Field), StringComparer.OrdinalIgnoreCase);
            foreach ((AtlasFormField field, string? value) in accepted.Where(x => !invalid.Contains(x.Key.Name)))
                Map(table, record, field.Name, field.Field, value, errors);

            if (derived.Count > 0)
                foreach ((string target, string? value) in AtlasServerEffects.Values(fields, Current, OptionsOf))
                    if (fields.Find(target) is { } field)
                        Map(table, record, field.Name, field.Field, value, errors);

            if (isNew && form.Defaults != null)
            {
                foreach ((string name, string token) in form.Defaults)
                {
                    if (fields.Find(name) is not { } field || accepted.TryGetValue(field, out string? sent) && !string.IsNullOrEmpty(sent))
                        continue;
                    string? value = ResolveDefault(token, field.Control.OptionsSource == null ? field.Control.Options : OptionsOf(field.Control));
                    Map(table, record, field.Name, field.Field, value, errors);
                }
            }

            if (errors.Count > 0)
                return Failed(result, errors);

            // Computed fields (spec 01, 10.2).
            foreach ((string target, string expression) in form.Computed ?? new Dictionary<string, string>())
                if (AtlasComputedFields.Compute(table, record, target, expression) is { } computeError)
                    errors.Add(computeError);

            // 9. System fields: UTC from TimeProvider (or local time, SystemTime Local, review DA-11); people as LAN IDs.
            DateTime now = SystemNow();
            if (isNew)
            {
                SetIfColumn(table, record, "CreatedBy", Services.User.LanId);
                SetIfColumn(table, record, "CreatedDate", now);
                SetIfColumn(table, record, AtlasTable.SoftDeleteColumnName, false);
            }

            SetIfColumn(table, record, "ModifiedBy", Services.User.LanId);
            SetIfColumn(table, record, "ModifiedDate", now);

            AtlasSaveContext saveContext = new()
            {
                Manifest = manifest,
                Form = form,
                Section = section,
                Fields = fields,
                Record = record,
                Previous = previous,
                IsNew = isNew
            };
            OnBeforeSave(saveContext);
            errors.AddRange(saveContext.Errors);
            if (errors.Count > 0)
                return Failed(result, errors);

            // 11-12. Save and audit in one transaction.
            if (stored != null && !string.IsNullOrWhiteSpace(input.Version) && RowVersion(table) is { } rowVersion)
            {
                // The UPDATE uses the version from the client, so a change after the load gives a conflict.
                Services.PlatformContext.Entry(stored).Property(rowVersion.Name).OriginalValue =
                    Convert.FromBase64String(input.Version);
            }

            if (!SaveWithAudit(manifest, table, record, isNew, isNew ? AtlasAuditEvents.Create : AtlasAuditEvents.Update,
                    () => Changes(fields, table, previous, record, isNew), errors))
                return Failed(result, errors);

            try
            {
                OnAfterSave(saveContext);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Review SV-06: a 500 here would make the user save again with ID = 0 and create a duplicate.
                AtlasLog.AfterSaveFailed(Logger, ex, manifest.Code, record[table.Key!.Name], Services.User.LanId);
            }

            result.ID = (int)record[table.Key!.Name]!;
            result.Version = VersionOf(table, record);
            result.Outputs = [.. fields.All
                .Where(x => table.HasColumn(x.Field) && !x.HiddenByRule) // review 3 RV-02
                .Select(x => new CPPageOutput { Name = x.Name, Value = Wire(record.GetValueOrDefault(table.Find(x.Field)!.Name), table.Find(x.Field)!) })];
            return result;
        }

        /// <summary>
        /// A changed value of a control with options (Options or OptionsSource) must be one of the options that the server
        /// gives to this user (review SV-01). An unchanged saved value stays valid (KeepSavedValue, B10).
        /// </summary>
        private CPPageError? OptionError(AtlasFormField field, string? value, string? previousValue, AtlasTable table)
        {
            CPApplicationControl control = field.Control;
            if (field.IsGridField || string.IsNullOrEmpty(value)
                || AtlasValueConverter.SameValue(value, previousValue, table.Find(field.Field)) // review 3 RV-L06 (DA-06)
                || string.Equals(control.Type, "user", StringComparison.OrdinalIgnoreCase)
                || control.OptionsSource == null && control.Options is not { Count: > 0 })
                return null;

            IReadOnlyList<CPKeyValue> options = OptionsOf(control);
            string[] values = control.Multiple
                ? value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : [value];
            bool allKnown = values.All(v => options.Any(o => string.Equals(o.Value, v, StringComparison.OrdinalIgnoreCase)));
            return allKnown ? null : Error(field.Name, "NotInOptions", $"{field.Label ?? field.Name} must be one of the options.");
        }

        /// <summary>Reads the outputs. A grid control output is a JSON list; its first row gives one value for each column.</summary>
        private static Dictionary<string, string?> ReadOutputs(CPAtlasPage input, AtlasFormFields fields, List<CPPageError> errors)
        {
            Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
            foreach (CPPageOutput output in input.Outputs ?? [])
            {
                if (string.IsNullOrWhiteSpace(output.Name))
                    continue;

                CPApplicationControl? grid = fields.Grids.FirstOrDefault(x => string.Equals(x.Name, output.Name, StringComparison.OrdinalIgnoreCase));
                if (grid != null)
                {
                    if (ReadGridRow(output.Value) is not { } row)
                    {
                        // Review SV-12: grid JSON that does not parse is an error, not an empty row.
                        errors.Add(Error(grid.Name, "InvalidGrid", $"{grid.Label ?? grid.Name} has values that are not valid."));
                        continue;
                    }

                    foreach ((string name, string? value) in row)
                    {
                        if (fields.Find(name) is { } field && field.Control == grid)
                            values[field.Name] = value;
                        else
                            errors.Add(Error(name, "NotAllowed", $"{name} is not a field of {grid.Name}."));
                    }

                    continue;
                }

                if (fields.Find(output.Name) is { } known)
                    values[known.Name] = output.Value;
                else
                    errors.Add(Error(output.Name, "NotAllowed", $"{output.Name} is not a field of this form."));
            }

            return values;
        }

        /// <summary>The first row of a grid output; null when the text is not valid JSON.</summary>
        private static List<(string Name, string? Value)>? ReadGridRow(string? json)
        {
            List<(string Name, string? Value)> values = [];
            if (string.IsNullOrWhiteSpace(json))
                return values;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }

            // Review SV-10: the document is disposed (it uses pooled memory).
            using (document)
            {
                JsonElement root = document.RootElement;
                JsonElement row = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().FirstOrDefault() : root;
                if (row.ValueKind != JsonValueKind.Object)
                    return values;

                foreach (JsonProperty property in row.EnumerateObject())
                {
                    values.Add((property.Name, property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Null or JsonValueKind.Undefined => null,
                        _ => property.Value.GetRawText()
                    }));
                }
            }

            return values;
        }

        private static void Map(AtlasTable table, Dictionary<string, object?> record, string name, string fieldName, string? value, List<CPPageError> errors)
        {
            AtlasColumn? column = table.Find(fieldName);
            if (column == null)
                return; // the start-up check reports a field with no column (C8)

            AtlasConversionResult converted = AtlasValueConverter.FromWire(value, column);
            if (converted.Success)
                record[column.Name] = converted.Value;
            else
                errors.Add(Error(name, converted.ErrorCode!, converted.Message!));
        }

        private List<AtlasAuditChange> Changes(AtlasFormFields fields, AtlasTable table, IReadOnlyDictionary<string, string?> previous,
            Dictionary<string, object?> record, bool isNew)
        {
            List<AtlasAuditChange> changes = [];
            foreach (AtlasFormField field in fields.All.DistinctBy(x => x.Field, StringComparer.OrdinalIgnoreCase))
            {
                if (SystemColumns.Contains(field.Field) || table.Find(field.Field) is not { } column)
                    continue;

                string? old = previous.GetValueOrDefault(column.Name);
                string? value = Wire(record.GetValueOrDefault(column.Name), column);
                if (AtlasValueConverter.SameValue(old, value, column) || (isNew && string.IsNullOrEmpty(value)))
                    continue;

                bool hidden = field.Control.Audit == false;
                changes.Add(new AtlasAuditChange(field.Field, field.Label, hidden ? "***" : old, hidden ? "***" : value));
            }

            return changes;
        }

        private bool SaveWithAudit(AtlasAppManifest manifest, AtlasTable table, Dictionary<string, object?> record, bool isNew,
            string auditEvent, Func<List<AtlasAuditChange>> changes, List<CPPageError> errors)
        {
            AtlasPlatformContext platform = Services.PlatformContext;
            using IDbContextTransaction transaction = platform.BeginSharedTransaction(Services.Context);
            try
            {
                if (isNew)
                    platform.Records(table.FullName).Add(record);
                List<AtlasAuditChange> list = changes();
                platform.SaveChanges();

                if (manifest.AuditMode == AtlasAuditMode.Server && !string.IsNullOrWhiteSpace(manifest.AuditTable))
                {
                    int id = (int)record[table.Key!.Name]!;
                    if (Services.Audit.Add(manifest, id, auditEvent, list))
                        platform.SaveChanges();
                }

                transaction.Commit();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                transaction.Rollback();
                errors.Add(Conflict());
                return false;
            }
            catch (DbUpdateException ex)
            {
                transaction.Rollback();
                AtlasLog.SaveFailed(Logger, ex, manifest.Code, Services.User.LanId);
                errors.Add(Error("", "SaveFailed", "The record was not saved. Contact support if this occurs again."));
                return false;
            }
            finally
            {
                platform.ChangeTracker.Clear();
                // Review 3 RV-L01: the platform context does not own the shared transaction; let it go, so a second save
                // in the same scope can start a new one.
                platform.Database.UseTransaction(null);
            }
        }

        #endregion Save

        #region Dashboard

        /// <summary>The dashboard of the app (spec 03, 4.1-4.7).</summary>
        public override CPAtlasDashboard LoadDashboard() => Dashboard = Services.Dashboards.Load(Manifest.Code);

        #endregion Dashboard

        #region Remove and audit

        public override CPAtlasPage RemovePage(int id)
        {
            AtlasAppManifest manifest = Manifest;
            CPAtlasApplicationConfig form = Services.ConfigStore.GetForm(AppCode);
            AtlasTable table = MainTable(manifest);
            Dictionary<string, object?> stored = Services.PlatformContext.FindActive(table.FullName, id)
                ?? throw AtlasProblemException.NotFound($"Record {id} of {manifest.Code} was not found.");
            Services.Access.Demand(manifest, form, AtlasOperation.Delete, stored, id);

            string field = form.SoftDelete?.Field
                ?? throw AtlasProblemException.BadRequest($"App '{manifest.Code}' has no SoftDelete setting.");
            if (table.Find(field) is not { } column)
                throw AtlasProblemException.Config($"The SoftDelete field '{field}' is not a column of {table.FullName}.");

            stored[column.Name] = true;
            SetIfColumn(table, stored, "ModifiedBy", Services.User.LanId);
            SetIfColumn(table, stored, "ModifiedDate", SystemNow());

            List<CPPageError> errors = [];
            CPAtlasPage result = new() { AppCode = manifest.Code, ID = id, Outputs = [] };
            if (!SaveWithAudit(manifest, table, stored, isNew: false, AtlasAuditEvents.Delete, () => [], errors))
                return Failed(result, errors);

            result.Outputs.Add(new CPPageOutput { Name = column.Name, Value = "Yes" });
            return result;
        }

        /// <summary>The old client audit call. With AuditMode Server the server already wrote the entry (spec 01, 5.12).</summary>
        public override void AuditLog(CPAtlasPage page)
        {
            AtlasLog.ClientAuditIgnored(Logger, Manifest.Code, Manifest.AuditMode.ToString());
        }

        public override List<CPAuditLog> GetAuditLog(string appCode, int refID)
        {
            AtlasAppManifest manifest = Manifest;
            CPAtlasApplicationConfig form = Services.ConfigStore.GetForm(AppCode);
            Dictionary<string, object?>? record = Services.PlatformContext.FindActive(MainTable(manifest).FullName, refID, tracking: false);
            Services.Access.Demand(manifest, form, AtlasOperation.History, record, refID);
            List<CPAuditLog> entries = Services.Audit.Read(manifest, refID);

            // Review 3 RV-02: the History shows *** for a field that the rules hide from this user.
            HashSet<string> hidden = RuleHiddenFields(manifest, form, record);
            if (hidden.Count > 0)
                foreach (CPAuditLog entry in entries)
                    entry.LogMessage = AtlasAuditMask.Mask(entry.LogMessage, hidden);
            foreach (CPAuditLog entry in entries)
                entry.LogMessage = AtlasAuditWriter.ToText(entry.LogMessage);
            return entries;
        }

        #endregion Remove and audit

        #region Helpers

        protected AtlasTable MainTable(AtlasAppManifest manifest) =>
            string.IsNullOrWhiteSpace(manifest.Table)
                ? throw AtlasProblemException.Config($"App '{manifest.Code}' has no Table in its manifest.")
                : Services.PlatformContext.Registry.Get(manifest.Table);

        protected IAtlasOptionProvider Provider(string name)
        {
            _providers ??= Services.OptionProviders.Concat(AppOptionProviders)
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
            return _providers.TryGetValue(name, out IAtlasOptionProvider? provider)
                ? provider
                : throw AtlasProblemException.Config($"Option provider '{name}' is not registered (app {AppCode}).");
        }

        private IReadOnlyList<CPKeyValue> OptionsOf(CPApplicationControl control) =>
            control.OptionsSource == null
                ? control.Options ?? []
                : control.Options ??= [.. Provider(control.OptionsSource.Provider).GetOptions(new AtlasOptionRequest(Manifest.Code, control, control.OptionsSource))];

        private AtlasRuleContext RuleContext(AtlasAppManifest manifest, CPAtlasApplicationConfig form, IReadOnlyDictionary<string, object?>? record) =>
            new(Services.User.RolesFor(manifest.Code).Tokens, [], Services.Access.IsOwner(form, record));

        private Dictionary<string, string?> Snapshot(AtlasTable table, Dictionary<string, object?>? record)
        {
            Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase);
            if (record == null)
                return values;
            foreach (AtlasColumn column in table.Columns)
                values[column.Name] = Wire(record.GetValueOrDefault(column.Name), column);
            return values;
        }

        /// <summary>A column value on the wire, with the SystemTime of the app (review DA-11).</summary>
        private string? Wire(object? value, AtlasColumn column) => AtlasValueConverter.ToWire(value, column, Manifest.SystemTimeIsUtc);

        private DateTime SystemNow() => Manifest.SystemTimeIsUtc ? Services.Time.GetUtcNow().UtcDateTime : Services.Time.GetLocalNow().DateTime;

        private static AtlasColumn? RowVersion(AtlasTable table) => table.Columns.FirstOrDefault(x => x.IsRowVersion);

        private static string? VersionOf(AtlasTable table, IReadOnlyDictionary<string, object?> record) =>
            RowVersion(table) is { } column && record.GetValueOrDefault(column.Name) is byte[] bytes
                ? Convert.ToBase64String(bytes)
                : null;

        private static void SetIfColumn(AtlasTable table, Dictionary<string, object?> record, string name, object? value)
        {
            if (table.Find(name) is { } column)
                record[column.Name] = value;
        }

        private static CPPageError Error(string field, string code, string message) => new() { Field = field, Code = code, Message = message };

        private static CPPageError VersionRequired() =>
            Error("", "VersionRequired", "The record version is missing. Open the record again and repeat your change.");

        private static CPPageError Conflict() =>
            Error("", "Conflict", "Another user changed this record after you opened it. Open the record again and repeat your change.");

        /// <summary>
        /// A save that stops. The mapping may have changed the tracked record already, so the change tracker is cleared:
        /// a later SaveChanges in the same request (a hook, a future feature) cannot write refused values (review SV-10).
        /// </summary>
        private CPAtlasPage Failed(CPAtlasPage result, List<CPPageError> errors)
        {
            Services.PlatformContext.ChangeTracker.Clear();
            result.ID = 0;
            result.Outputs = [];
            result.Errors = errors;
            return result;
        }

        #endregion Helpers
    }
}
