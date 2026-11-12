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
        /// <summary>The manifest of the app.</summary>
        public required AtlasAppManifest Manifest { get; init; }

        /// <summary>The form configuration of the app.</summary>
        public required CPAtlasApplicationConfig Form { get; init; }

        /// <summary>The section that the load returns. A hook can change it.</summary>
        public required CPApplicationSection Section { get; init; }

        /// <summary>The fields of the section.</summary>
        public required AtlasFormFields Fields { get; init; }

        /// <summary>The stored record, or null for a new record.</summary>
        public IReadOnlyDictionary<string, object?>? Record { get; init; }

        /// <summary>True when the load is for a new record.</summary>
        public bool IsNew => Record == null;
    }

    /// <summary>The state of one save, for the <see cref="ConfigSectionService.OnBeforeSave"/> and OnAfterSave hooks.</summary>
    public sealed class AtlasSaveContext
    {
        /// <summary>The manifest of the app.</summary>
        public required AtlasAppManifest Manifest { get; init; }

        /// <summary>The form configuration of the app.</summary>
        public required CPAtlasApplicationConfig Form { get; init; }

        /// <summary>The section of the form.</summary>
        public required CPApplicationSection Section { get; init; }

        /// <summary>The fields of the section, with the rules for this user and record.</summary>
        public required AtlasFormFields Fields { get; init; }

        /// <summary>The record that is saved. A hook can change the values (column name to CLR value).</summary>
        public required Dictionary<string, object?> Record { get; init; }

        /// <summary>The wire values before the save (empty for a new record).</summary>
        public required IReadOnlyDictionary<string, string?> Previous { get; init; }

        /// <summary>True when the save creates a new record.</summary>
        public required bool IsNew { get; init; }

        /// <summary>The errors that stop the save. Only OnBeforeSave can add to them.</summary>
        public List<CPPageError> Errors { get; } = [];
    }

    /// <summary>
    /// Loads, saves, deletes and audits the records of a platform app from its metadata.
    /// </summary>
    /// <remarks>
    /// A new app needs no class. The manifest names the tables, and the form JSON gives the behaviour.
    /// A module class inherits from this class only to add option providers or a hook
    /// (OnLoad, OnBeforeSave, OnAfterSave).
    /// </remarks>
    public partial class ConfigSectionService : CPApplicationService, ISectionService, IAtlasModule
    {
        /// <summary>The columns that only the server writes.</summary>
        public static readonly IReadOnlySet<string> SystemColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AtlasTable.KeyColumnName, "CreatedBy", "CreatedDate", "ModifiedBy", "ModifiedDate", AtlasTable.SoftDeleteColumnName, "RowVersion"
        };

        private Dictionary<string, IAtlasOptionProvider>? _providers;
        private AtlasAppManifest? _manifest;

        /// <summary>Creates the service with the shared platform services.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public ConfigSectionService(AtlasModuleServices services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Context = services.Context;
            Configuration = services.Configuration;
            Logger = services.LoggerFactory.CreateLogger("BBH.CP.Atlas.Platform.Section");
        }

        /// <summary>The code of the app. The module resolver sets it (IAtlasModule).</summary>
        public string AppCode { get; set; } = "";

        /// <summary>The shared platform services.</summary>
        protected AtlasModuleServices Services { get; }

        /// <summary>The logger of the section service.</summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// The manifest of the app. A missing or disabled app gives a "not found" problem.
        /// </summary>
        protected AtlasAppManifest Manifest =>
            _manifest ??= Services.ConfigStore.FindManifest(AppCode) is { Enabled: true } manifest
                ? manifest
                : throw AtlasProblemException.NotFound($"App '{AppCode}' is not registered.");

        /// <summary>The option providers of the app. They have priority over a platform provider with the same name.</summary>
        protected virtual IEnumerable<IAtlasOptionProvider> AppOptionProviders => [];

        #region Hooks

        /// <summary>Runs in a load after the values, options and defaults, and before the rules.</summary>
        protected virtual void OnLoad(AtlasLoadContext context)
        {
        }

        /// <summary>
        /// Runs in a save after the validation and the mapping, and before the database save.
        /// Add an error to <see cref="AtlasSaveContext.Errors"/> to stop the save.
        /// </summary>
        protected virtual void OnBeforeSave(AtlasSaveContext context)
        {
        }

        /// <summary>
        /// Runs in a save after the commit.
        /// </summary>
        /// <remarks>
        /// The hook must not throw. The record is already saved, so the service only logs an exception
        /// (Atlas.AfterSaveFailed). The save then returns success with the ID.
        /// </remarks>
        protected virtual void OnAfterSave(AtlasSaveContext context)
        {
        }

        #endregion Hooks

        /// <summary>Returns the names of all option providers of this app. The start-up check uses them.</summary>
        public IEnumerable<string> OptionProviderNames() =>
            Services.OptionProviders.Concat(AppOptionProviders).Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase);

        #region Load

        /// <summary>
        /// Returns the form section of a record (ID greater than 0) or of a new record, with values, options and rules.
        /// </summary>
        /// <remarks>
        /// The load demands View access for a record and Create access for a new record.
        /// A field that a rule hides from the user gets no value and no options.
        /// </remarks>
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

            AtlasRuleOutcome outcome = Services.Rules.ApplyToSection(section, form.Rules, RuleContext(manifest, form, record), manifest.RuleMode, manifest.RuleDefault);
            fields.ApplyOutcome(outcome);
            ClearRuleHiddenValues(fields);

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
        /// Removes the value and the options of each field that a rule hides from the current user.
        /// </summary>
        /// <remarks>
        /// A field that is hidden only on the client is not safe, because its value is in the response JSON.
        /// A field that the JSON hides keeps its value, because an effect can show it.
        /// </remarks>
        private static void ClearRuleHiddenValues(AtlasFormFields fields)
        {
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

        /// <summary>Returns the fields (Name and column) that the rules hide from this user for this record.</summary>
        private HashSet<string> RuleHiddenFields(AtlasAppManifest manifest, CPAtlasApplicationConfig form, Dictionary<string, object?>? record)
        {
            HashSet<string> hidden = new(StringComparer.OrdinalIgnoreCase);
            if (form.Section == null)
                return hidden;
            AtlasRuleOutcome outcome = Services.Rules.ApplyToSection(form.Section, form.Rules, RuleContext(manifest, form, record), manifest.RuleMode, manifest.RuleDefault);
            foreach (AtlasFormField field in AtlasFormFields.From(form.Section, outcome).All.Where(x => x.HiddenByRule))
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

                // The client shows a person as a name. The value stays the LAN ID.
                if (string.Equals(control.Type, "user", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(control.Value))
                    control.Options = [new CPKeyValue { Label = Services.Directory.DisplayName(control.Value), Value = control.Value }];
            }
        }

        /// <summary>
        /// Returns the value of a default token: {User.LanID}, {User.DisplayName}, {Today}, {Today.Next(Day)} or {Options[n]}.
        /// Other text is the value.
        /// </summary>
        protected string? ResolveDefault(string token, IReadOnlyList<CPKeyValue>? options) =>
            AtlasDefaultTokens.Resolve(token, Services.User.LanId, Services.User.DisplayName, Services.Time.GetLocalNow(), options);

        #endregion Load

        #region Save

        /// <summary>
        /// Saves a new or changed record and writes the audit entry.
        /// </summary>
        /// <remarks>
        /// The server accepts only the fields that the user can change, and checks the options, validators and version.
        /// When a check fails, the result has ID 0, no outputs and the errors. Nothing is saved.
        /// </remarks>
        public override CPAtlasPage SavePage(CPAtlasPage input, IFormFileCollection files)
        {
            ArgumentNullException.ThrowIfNull(input);
            AtlasAppManifest manifest = Manifest;
            AtlasTable table = MainTable(manifest);
            SaveState state = new()
            {
                Input = input,
                Manifest = manifest,
                Form = Services.ConfigStore.GetForm(AppCode),
                Table = table,
                Result = new() { AppCode = manifest.Code, Code = input.Code, Step = input.Step, ID = 0, Outputs = [] }
            };

            LoadStored(state);
            ApplyRules(state);
            ReadAndFilter(state);
            ValidateFields(state);
            CheckVersion(state);
            MapRecord(state);
            if (state.Errors.Count > 0)
                return Failed(state.Result, state.Errors);

            ComputeAndSystemFields(state);
            RunBeforeSave(state);
            if (state.Errors.Count > 0)
                return Failed(state.Result, state.Errors);

            if (!Persist(state))
                return Failed(state.Result, state.Errors);
            return Complete(state);
        }

        /// <summary>The state of one save, passed from step to step of <see cref="SavePage"/>.</summary>
        private sealed class SaveState
        {
            public required CPAtlasPage Input { get; init; }
            public required AtlasAppManifest Manifest { get; init; }
            public required CPAtlasApplicationConfig Form { get; init; }
            public required AtlasTable Table { get; init; }
            public required CPAtlasPage Result { get; init; }
            public List<CPPageError> Errors { get; } = [];

            /// <summary>The stored record, or null for a new record.</summary>
            public Dictionary<string, object?>? Stored { get; set; }
            public bool IsNew => Stored == null;
            public Dictionary<string, string?> Previous { get; set; } = [];
            public CPApplicationSection Section { get; set; } = null!;
            public AtlasFormFields Fields { get; set; } = null!;
            public Dictionary<string, string?> Submitted { get; set; } = [];

            /// <summary>The fields that the server effects set (SetFromOption, Set, Clear).</summary>
            public HashSet<string> Derived { get; set; } = [];

            /// <summary>The Computed columns.</summary>
            public HashSet<string> Computed { get; set; } = [];
            public Dictionary<AtlasFormField, string?> Accepted { get; set; } = [];
            public Dictionary<string, object?> Record { get; set; } = null!;
            public AtlasSaveContext Context { get; set; } = null!;

            /// <summary>The submitted value of a field, else its stored value.</summary>
            public string? Current(AtlasFormField field) =>
                Submitted.TryGetValue(field.Name, out string? value) ? value : Previous.GetValueOrDefault(field.Field);
        }

        /// <summary>Reads the stored record and checks the access (save steps 1 and 2).</summary>
        private void LoadStored(SaveState state)
        {
            AtlasAppManifest manifest = state.Manifest;
            if (state.Input.ID > 0)
            {
                state.Stored = Services.PlatformContext.FindActive(state.Table.FullName, state.Input.ID)
                    ?? throw AtlasProblemException.NotFound($"Record {state.Input.ID} of {manifest.Code} was not found.");
                Services.Access.Demand(manifest, state.Form, AtlasOperation.Edit, state.Stored, state.Input.ID);
            }
            else
            {
                Services.Access.Demand(manifest, state.Form, AtlasOperation.Create, null, 0);
                // A denied create is always refused, also in LogOnly (the same as a Delete). In LogOnly the rules make
                // the fields read-only for such a user, so the save would insert a row of defaults only.
                // The load of a new form stays LogOnly.
                AtlasAccessDecision create = Services.Access.Evaluate(manifest, state.Form, AtlasOperation.Create, null);
                if (!create.Allowed)
                    throw AtlasProblemException.Forbidden(create.Reason);
            }

            state.Previous = Snapshot(state.Table, state.Stored);
        }

        /// <summary>Applies the rules for this user and record to the section (save step 3).</summary>
        private void ApplyRules(SaveState state)
        {
            AtlasAppManifest manifest = state.Manifest;
            state.Section = state.Form.Section
                ?? throw AtlasProblemException.Config($"The form of {manifest.Code} has no Section.");
            AtlasRuleOutcome outcome = Services.Rules.ApplyToSection(state.Section, state.Form.Rules,
                RuleContext(manifest, state.Form, state.Stored), manifest.RuleMode, manifest.RuleDefault);
            state.Fields = AtlasFormFields.From(state.Section, outcome);
        }

        /// <summary>Reads the outputs, applies the server effects and checks the allow list (save steps 4 and 5).</summary>
        private void ReadAndFilter(SaveState state)
        {
            state.Submitted = ReadOutputs(state.Input, state.Fields, state.Errors);

            // Server part of Effects (Show, Hide, Require, Unrequire) and the SetFromOption targets.
            state.Derived = AtlasServerEffects.Apply(state.Fields, state.Current);
            state.Computed = new((IEnumerable<string>?)state.Form.Computed?.Keys ?? [], StringComparer.OrdinalIgnoreCase);

            state.Accepted = AtlasAllowList.Check(state.Submitted, state.Fields, state.Table, state.Previous,
                field => IsServerSet(state, field),
                (field, value, previous) => OptionError(field, value, previous, state.Table), state.Errors,
                (field, reason) =>
                {
                    if (reason == AtlasIgnoreReason.ServerSet)
                        AtlasLog.FieldIgnored(Logger, state.Manifest.Code, field.Name, "ServerSet");
                });

            // Log a refused field as a warning. Thus the log shows a probe or a client defect.
            foreach (CPPageError refusal in state.Errors.Where(x => x.Code is "NotAllowed" or "NotInOptions" or "InvalidGrid"))
                AtlasLog.FieldRefused(Logger, Services.User.LanId, state.Manifest.Code, state.Input.ID, refusal.Field ?? "", refusal.Code);
        }

        /// <summary>Returns true for a field that only the server writes: a system column, a Computed column or a server effect target.</summary>
        private static bool IsServerSet(SaveState state, AtlasFormField field) =>
            SystemColumns.Contains(field.Field) || state.Computed.Contains(field.Field) || state.Derived.Contains(field.Name);

        /// <summary>
        /// Runs the validators and the Require effects on the visible and enabled fields (save step 6).
        /// Each field gets one error at most.
        /// </summary>
        private void ValidateFields(SaveState state)
        {
            HashSet<string> refused = new(state.Errors.Select(x => x.Field ?? ""), StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, string>? defaults = state.Form.Defaults;
            foreach (AtlasFormField field in state.Fields.All.Where(x => x.Visible && !x.Disabled))
            {
                if (IsServerSet(state, field) || refused.Contains(field.Name))
                    continue;

                string? value = state.Accepted.TryGetValue(field, out string? sent) ? sent : state.Previous.GetValueOrDefault(field.Field);
                if (state.IsNew && string.IsNullOrEmpty(value) && defaults != null && defaults.ContainsKey(field.Name))
                    continue; // the default is set at step 8

                if (field.IsGridField && string.IsNullOrWhiteSpace(value) && field.Validator?.Integer == true)
                {
                    value = "0"; // an empty day value is 0
                    state.Accepted[field] = value;
                }

                if (AtlasFieldValidator.Validate(field, value) is { } error)
                    state.Errors.Add(error);
            }
        }

        /// <summary>
        /// Checks the version token (save step 7). A different version gives a conflict.
        /// </summary>
        /// <remarks>
        /// The service finds the rowversion column by its type. With RequireVersion, an update with no Version is refused.
        /// </remarks>
        private void CheckVersion(SaveState state)
        {
            if (state.Stored == null || RowVersion(state.Table) == null)
                return;

            if (string.IsNullOrWhiteSpace(state.Input.Version))
            {
                if (state.Manifest.RequireVersion)
                    state.Errors.Add(VersionRequired());
            }
            else if (!string.Equals(state.Input.Version, VersionOf(state.Table, state.Stored), StringComparison.Ordinal))
                state.Errors.Add(Conflict());
        }

        /// <summary>Converts and maps the accepted values, the server effect values and the defaults to the record (save step 8).</summary>
        private void MapRecord(SaveState state)
        {
            AtlasTable table = state.Table;
            AtlasFormFields fields = state.Fields;
            List<CPPageError> errors = state.Errors;
            Dictionary<string, object?> record = state.Record = state.Stored ?? table.NewRecord();

            HashSet<string> invalid = new(errors.Select(x => x.Field), StringComparer.OrdinalIgnoreCase);
            foreach ((AtlasFormField field, string? value) in state.Accepted.Where(x => !invalid.Contains(x.Key.Name)))
                Map(table, record, field.Name, field.Field, value, errors);

            if (state.Derived.Count > 0)
                foreach ((string target, string? value) in AtlasServerEffects.Values(fields, state.Current, OptionsOf))
                    if (fields.Find(target) is { } field)
                        Map(table, record, field.Name, field.Field, value, errors);

            if (!state.IsNew || state.Form.Defaults == null)
                return;

            foreach ((string name, string token) in state.Form.Defaults)
            {
                if (fields.Find(name) is not { } field || state.Accepted.TryGetValue(field, out string? sent) && !string.IsNullOrEmpty(sent))
                    continue;
                string? value = ResolveDefault(token, field.Control.OptionsSource == null ? field.Control.Options : OptionsOf(field.Control));
                Map(table, record, field.Name, field.Field, value, errors);
            }
        }

        /// <summary>
        /// Sets the Computed fields and the system fields (save step 9).
        /// </summary>
        /// <remarks>
        /// The times come from TimeProvider: UTC, or local time when SystemTime is Local. People are LAN IDs.
        /// </remarks>
        private void ComputeAndSystemFields(SaveState state)
        {
            AtlasTable table = state.Table;
            Dictionary<string, object?> record = state.Record;
            foreach ((string target, string expression) in state.Form.Computed ?? new Dictionary<string, string>())
                if (AtlasComputedFields.Compute(table, record, target, expression) is { } computeError)
                    state.Errors.Add(computeError);

            DateTime now = SystemNow();
            if (state.IsNew)
            {
                SetIfColumn(table, record, "CreatedBy", Services.User.LanId);
                SetIfColumn(table, record, "CreatedDate", now);
                SetIfColumn(table, record, AtlasTable.SoftDeleteColumnName, false);
            }

            SetIfColumn(table, record, "ModifiedBy", Services.User.LanId);
            SetIfColumn(table, record, "ModifiedDate", now);
        }

        /// <summary>Runs the OnBeforeSave hook (save step 10). Its errors stop the save.</summary>
        private void RunBeforeSave(SaveState state)
        {
            state.Context = new AtlasSaveContext
            {
                Manifest = state.Manifest,
                Form = state.Form,
                Section = state.Section,
                Fields = state.Fields,
                Record = state.Record,
                Previous = state.Previous,
                IsNew = state.IsNew
            };
            OnBeforeSave(state.Context);
            state.Errors.AddRange(state.Context.Errors);
        }

        /// <summary>Saves the record and the audit entry in one transaction (save steps 11 and 12). Returns false when the save fails; the errors tell why.</summary>
        private bool Persist(SaveState state)
        {
            AtlasTable table = state.Table;
            if (state.Stored != null && !string.IsNullOrWhiteSpace(state.Input.Version) && RowVersion(table) is { } rowVersion)
            {
                // The UPDATE uses the version from the client, so a change after the load gives a conflict.
                Services.PlatformContext.Entry(state.Stored).Property(rowVersion.Name).OriginalValue =
                    Convert.FromBase64String(state.Input.Version);
            }

            return SaveWithAudit(state.Manifest, table, state.Record, state.IsNew,
                state.IsNew ? AtlasAuditEvents.Create : AtlasAuditEvents.Update,
                () => Changes(state.Fields, table, state.Previous, state.Record, state.IsNew), state.Errors);
        }

        /// <summary>Runs the OnAfterSave hook after the commit and returns the result (ID, Version, Outputs).</summary>
        private CPAtlasPage Complete(SaveState state)
        {
            AtlasTable table = state.Table;
            Dictionary<string, object?> record = state.Record;
            try
            {
                OnAfterSave(state.Context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The record is saved. A 500 here would make the user save again with ID = 0 and create a duplicate.
                AtlasLog.AfterSaveFailed(Logger, ex, state.Manifest.Code, record[table.Key!.Name], Services.User.LanId);
            }

            CPAtlasPage result = state.Result;
            result.ID = (int)record[table.Key!.Name]!;
            result.Version = VersionOf(table, record);
            result.Outputs = [.. state.Fields.All
                .Where(x => table.HasColumn(x.Field) && !x.HiddenByRule) // no value of a rule-hidden field
                .Select(x => new CPPageOutput { Name = x.Name, Value = Wire(record.GetValueOrDefault(table.Find(x.Field)!.Name), table.Find(x.Field)!) })];
            return result;
        }

        /// <summary>
        /// Returns an error when a changed value of a control with options is not one of the options for this user.
        /// </summary>
        /// <remarks>
        /// The options come from Options or OptionsSource on the server. An unchanged saved value stays valid (KeepSavedValue).
        /// </remarks>
        private CPPageError? OptionError(AtlasFormField field, string? value, string? previousValue, AtlasTable table)
        {
            CPApplicationControl control = field.Control;
            if (field.IsGridField || string.IsNullOrEmpty(value)
                || AtlasValueConverter.SameValue(value, previousValue, table.Find(field.Field)) // compare by column type, not as text
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
                        // Grid JSON that does not parse is an error, not an empty row.
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

        /// <summary>Returns the first row of a grid output, or null when the text is not valid JSON.</summary>
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

            // Dispose the document, because it uses pooled memory.
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
                return; // the start-up check reports a field with no column

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
                // The platform context does not own the shared transaction. Release it, so that a second save
                // in the same scope can start a new one.
                platform.Database.UseTransaction(null);
            }
        }

        #endregion Save

        #region Dashboard

        /// <summary>Returns the dashboard of the app.</summary>
        public override CPAtlasDashboard LoadDashboard() => Dashboard = Services.Dashboards.Load(Manifest.Code);

        #endregion Dashboard

        #region Remove and audit

        /// <summary>
        /// Marks a record as deleted (soft delete) and writes the audit entry.
        /// </summary>
        /// <remarks>
        /// The app must have a SoftDelete setting. The delete demands Delete access.
        /// </remarks>
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

        /// <summary>Logs and ignores the old client audit call. With AuditMode Server, the server already wrote the entry.</summary>
        public override void AuditLog(CPAtlasPage page)
        {
            AtlasLog.ClientAuditIgnored(Logger, Manifest.Code, Manifest.AuditMode.ToString());
        }

        /// <summary>
        /// Returns the History of a record. The value of a field that the rules hide from the user shows as ***.
        /// </summary>
        public override List<CPAuditLog> GetAuditLog(string appCode, int refID)
        {
            AtlasAppManifest manifest = Manifest;
            CPAtlasApplicationConfig form = Services.ConfigStore.GetForm(AppCode);
            Dictionary<string, object?>? record = Services.PlatformContext.FindActive(MainTable(manifest).FullName, refID, tracking: false);
            Services.Access.Demand(manifest, form, AtlasOperation.History, record, refID);
            List<CPAuditLog> entries = Services.Audit.Read(manifest, refID);

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

        /// <summary>Returns the main table of the app. A manifest with no Table gives a configuration problem.</summary>
        protected AtlasTable MainTable(AtlasAppManifest manifest) =>
            string.IsNullOrWhiteSpace(manifest.Table)
                ? throw AtlasProblemException.Config($"App '{manifest.Code}' has no Table in its manifest.")
                : Services.PlatformContext.Registry.Get(manifest.Table);

        /// <summary>
        /// Returns the option provider with this name. An app provider has priority over a platform provider.
        /// An unknown name gives a configuration problem.
        /// </summary>
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

        /// <summary>Returns a column value as wire text, with the SystemTime of the app.</summary>
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
        /// Returns the result of a save that stops: ID 0, no outputs and the errors.
        /// </summary>
        /// <remarks>
        /// The mapping can already change the tracked record, so the method clears the change tracker.
        /// Thus a later SaveChanges in the same request cannot write refused values.
        /// </remarks>
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
