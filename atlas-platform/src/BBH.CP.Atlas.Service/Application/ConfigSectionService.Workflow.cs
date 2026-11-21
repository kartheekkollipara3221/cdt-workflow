using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Audit;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Sections;
using BBH.CP.Atlas.Service.Platform.Workflow;
using WorkflowEngine.Application.Contracts;

namespace BBH.CP.Atlas.Service.Application
{
    public partial class ConfigSectionService
    {
        /// <summary>The value of ActorRoles that sends the active roles of the user in the app.</summary>
        public const string UserActorRoles = "User";

        /// <summary>The engine part of one submit: the binding, the actions, the actor roles, the inputs and the results.</summary>
        private sealed class SaveEngine
        {
            public required CPWorkflow Binding { get; init; }
            public required List<CPWorkflowEngineAction> Actions { get; init; }
            public required List<string> Roles { get; init; }
            public required string RequestId { get; init; }
            public Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> Recipients { get; set; } = [];
            public List<AtlasWorkflowResult> Results { get; } = [];

            /// <summary>The names that matched no single user. They are logged when the record ID is known.</summary>
            public List<AtlasSkippedRecipient> Skipped { get; } = [];

            /// <summary>The state that the first action gave, or null before it.</summary>
            public string? NewState { get; set; }
        }

        /// <summary>The engine refused the first action of a submit. The save rolls back.</summary>
        private sealed class WorkflowRefusedException(CPPageError error) : Exception(error.Message)
        {
            public CPPageError Error { get; } = error;
        }

        private IAtlasWorkflowAdapter WorkflowAdapter => Services.Workflow
            ?? throw AtlasProblemException.Config($"App '{AppCode}' has engine actions, but no workflow adapter is registered.");

        private AtlasWorkflowInputs WorkflowInputs => new(Services.Configuration, Services.Directory, Logger);

        /// <summary>
        /// Makes the engine part of a submit and checks its inputs after the OnBeforeSave hook (save step 10) and before
        /// the write.
        /// </summary>
        /// <remarks>
        /// A recipient key with no address and no Optional, or an empty template value, is an error. Nothing is written.
        /// </remarks>
        private void PrepareEngine(SaveState state)
        {
            if (state.WorkflowStep?.EngineActions is not { Count: > 0 } actions)
                return;

            CPWorkflow binding = state.Section.Workflow!;
            SaveEngine engine = new()
            {
                Binding = binding,
                Actions = actions,
                Roles = ActorRolesOf(binding, state.Manifest),
                RequestId = state.RequestId!
            };
            state.Engine = engine;
            engine.Recipients = WorkflowInputs.Recipients(state.Manifest.Code, binding, actions.SelectMany(x => x.Recipients ?? []),
                field => FieldValue(state, field), state.Input.ID, state.Errors, engine.Skipped);
            WorkflowInputs.TemplateData(state.Manifest.Code, binding, field => FieldValue(state, field), field => FieldLabel(state, field), 0, state.Errors);
            if (state.Errors.Count > 0)
                LogSkippedRecipients(state, state.Input.ID); // the save stops; 0 = no record
        }

        /// <summary>Logs the recipient names that matched no single user, with the record ID, one time for each save.</summary>
        private void LogSkippedRecipients(SaveState state, int recordId)
        {
            if (state.Engine is not { Skipped.Count: > 0 } engine)
                return;
            WorkflowInputs.LogSkipped(state.Manifest.Code, recordId, engine.Skipped);
            engine.Skipped.Clear();
        }

        /// <summary>
        /// Runs the first engine action in the transaction of the record, and sets the new state and status on the record.
        /// </summary>
        /// <remarks>
        /// The record, the files and the audit entries are saved before this call. The caller saves the state change.
        /// </remarks>
        /// <returns>The status line for the audit entry of the save. Empty for a save with no engine action.</returns>
        /// <exception cref="WorkflowRefusedException">The engine refused the action. The caller rolls back.</exception>
        private List<AtlasAuditChange> RunFirstEngineAction(SaveState state, int recordId)
        {
            if (state.Engine is not { } engine)
                return [];

            AtlasWorkflowResult result;
            try
            {
                result = RunEngineAction(state, engine, 0, recordId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AtlasLog.WorkflowStepFailed(Logger, ex, state.Manifest.Code, recordId, InstanceIdOf(engine.Binding, recordId), ActionKeyOf(engine.Actions[0]), "EngineError");
                throw new WorkflowRefusedException(Error("", "EngineError", "The workflow engine did not accept the submit. Contact support."));
            }

            engine.Results.Add(result);
            if (!result.Success)
            {
                string message = result.ResultCode == "IdempotencyConflict"
                    ? "This request was already processed."
                    : $"The workflow did not accept the submit: {result.Message ?? result.ResultCode}";
                throw new WorkflowRefusedException(Error("", result.ResultCode, message));
            }

            engine.NewState = result.NewState;
            string? oldLabel = AtlasWorkflowSteps.LabelOf(state.Section, state.WorkflowState);
            string? newLabel = AtlasWorkflowSteps.LabelOf(state.Section, result.NewState);
            WriteState(state.Table, state.Record, engine.Binding, result.NewState, newLabel);
            return [new AtlasAuditChange(engine.Binding.StatusField ?? engine.Binding.StateField ?? "Status", "Status", oldLabel, newLabel)];
        }

        /// <summary>
        /// Runs the later engine actions after the commit, then sends the emails of all actions.
        /// </summary>
        /// <remarks>
        /// The record is saved, so a failure here does not undo the save. A refused action or a state difference is the
        /// error log Atlas.WorkflowStepFailed; the support team sends the email by hand. A failed send is a warning log;
        /// the engine outbox sends it again.
        /// </remarks>
        private void RunLaterEngineActions(SaveState state, int recordId)
        {
            LogSkippedRecipients(state, recordId);
            if (state.Engine is not { Results.Count: > 0 } engine)
                return;

            for (int index = 1; index < engine.Actions.Count; index++)
            {
                string action = ActionKeyOf(engine.Actions[index]);
                try
                {
                    AtlasWorkflowResult result = RunEngineAction(state, engine, index, recordId);
                    engine.Results.Add(result);
                    if (!result.Success)
                    {
                        AtlasLog.WorkflowStepFailed(Logger, null, state.Manifest.Code, recordId, result.InstanceId, action, $"{result.ResultCode}: {result.Message}");
                        break;
                    }

                    if (!string.Equals(result.NewState, engine.NewState, StringComparison.Ordinal))
                        AtlasLog.WorkflowStepFailed(Logger, null, state.Manifest.Code, recordId, result.InstanceId, action,
                            $"StateDifference: the record has {engine.NewState}, the engine has {result.NewState}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AtlasLog.WorkflowStepFailed(Logger, ex, state.Manifest.Code, recordId, InstanceIdOf(engine.Binding, recordId), action, "EngineError");
                    break;
                }
            }

            foreach (AtlasWorkflowResult result in engine.Results.Where(x => x.Success))
            {
                try
                {
                    if (WorkflowAdapter.DispatchNotifications(result) is { } failure)
                        AtlasLog.NotificationDispatchFailed(Logger, null, state.Manifest.Code, recordId, result.InstanceId, failure);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    AtlasLog.NotificationDispatchFailed(Logger, ex, state.Manifest.Code, recordId, result.InstanceId, ex.Message);
                }
            }
        }

        /// <summary>Logs a submit whose first engine action committed, but whose record save then failed.</summary>
        private void LogEngineWithoutSave(SaveState state)
        {
            if (state.Engine?.Results.FirstOrDefault() is { Success: true } first)
                AtlasLog.WorkflowStepFailed(Logger, null, state.Manifest.Code, state.Input.ID, first.InstanceId,
                    ActionKeyOf(state.Engine.Actions[0]), "AtlasCommitFailed: the engine changed the state, the record was not saved");
        }

        private AtlasWorkflowResult RunEngineAction(SaveState state, SaveEngine engine, int index, int recordId)
        {
            CPWorkflowEngineAction action = engine.Actions[index];
            List<CPPageError> ignored = []; // the data was checked before the write
            Dictionary<string, string?> data = WorkflowInputs.TemplateData(state.Manifest.Code, engine.Binding,
                field => FieldValue(state, field), field => FieldLabel(state, field), recordId, ignored);
            Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> recipients = (action.Recipients ?? [])
                .Where(engine.Recipients.ContainsKey)
                .ToDictionary(x => x, x => engine.Recipients[x], StringComparer.Ordinal);

            return WorkflowAdapter.Run(new AtlasWorkflowCall(
                engine.Binding.Key,
                BusinessObjectIdOf(engine.Binding, recordId),
                ActionKeyOf(action),
                IsStart: !string.IsNullOrWhiteSpace(action.Start),
                Services.User.LanId,
                Services.User.DisplayName,
                engine.Roles,
                // One key for each action of the request: a repeated request gives the same keys.
                $"{engine.RequestId}:{(index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                new WorkflowNotificationInput { RecipientInputs = recipients, TemplateData = data }));
        }

        /// <summary>
        /// Writes the engine state to a record whose state differs from it, with one WorkflowSync audit entry.
        /// </summary>
        /// <remarks>
        /// The engine is the source of truth for the state. A record with no engine instance is not checked. When the
        /// engine cannot be read, the load continues with the record state and a warning log.
        /// </remarks>
        /// <returns>The record after the change, or null when nothing changed.</returns>
        private Dictionary<string, object?>? SyncWorkflowState(AtlasAppManifest manifest, CPAtlasApplicationConfig form, AtlasTable table,
            int recordId, IReadOnlyDictionary<string, object?> record)
        {
            CPWorkflow? binding = form.Section?.Workflow;
            if (binding?.BusinessObjectPrefix == null || Services.Workflow == null || form.Section == null)
                return null;

            string instanceId = InstanceIdOf(binding, recordId);
            string? engineState;
            try
            {
                engineState = Services.Workflow.ReadState(instanceId, Services.User.LanId, ActorRolesOf(binding, manifest));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AtlasLog.WorkflowSync(Logger, ex, manifest.Code, recordId, instanceId, "", "", "EngineNotRead");
                return null;
            }

            string? recordState = AtlasWorkflowSteps.StateOf(form.Section, table, record, isNew: false);
            if (engineState == null || string.Equals(engineState, recordState, StringComparison.Ordinal))
                return null;

            Dictionary<string, object?> tracked = Services.PlatformContext.FindActive(table.FullName, recordId)!;
            string? newLabel = AtlasWorkflowSteps.LabelOf(form.Section, engineState);
            WriteState(table, tracked, binding, engineState, newLabel);
            List<CPPageError> errors = [];
            AtlasAuditChange line = new(binding.StatusField ?? binding.StateField ?? "Status", "Status",
                AtlasWorkflowSteps.LabelOf(form.Section, recordState), newLabel);
            if (!SaveWithAudit(manifest, table, tracked, isNew: false, AtlasAuditEvents.WorkflowSync, () => [line], errors))
            {
                AtlasLog.WorkflowSync(Logger, null, manifest.Code, recordId, instanceId, recordState ?? "", engineState, "SaveFailed");
                return null;
            }

            AtlasLog.WorkflowSync(Logger, null, manifest.Code, recordId, instanceId, recordState ?? "", engineState, "Written");
            return Services.PlatformContext.FindActive(table.FullName, recordId, tracking: false);
        }

        /// <summary>Returns the roles that the engine gets for the user: the active role names of the user in the app.</summary>
        private List<string> ActorRolesOf(CPWorkflow binding, AtlasAppManifest manifest) =>
            binding.ActorRoles is null || string.Equals(binding.ActorRoles, UserActorRoles, StringComparison.OrdinalIgnoreCase)
                ? [.. Services.User.RolesFor(manifest.Code).RoleNames]
                : throw AtlasProblemException.Config($"App '{manifest.Code}': ActorRoles '{binding.ActorRoles}' is not known (use \"{UserActorRoles}\").");

        private static void WriteState(AtlasTable table, Dictionary<string, object?> record, CPWorkflow binding, string? state, string? label)
        {
            if (binding.StateField != null && table.Find(binding.StateField) is { } stateColumn)
                record[stateColumn.Name] = state;
            if (binding.StatusField != null && table.Find(binding.StatusField) is { } statusColumn)
                record[statusColumn.Name] = label;
        }

        /// <summary>Returns the record value of a binding name: a column, or a control with a column.</summary>
        private static object? FieldValue(SaveState state, string field) =>
            ColumnOf(state, field) is { } column ? state.Record.GetValueOrDefault(column.Name) : null;

        private static string FieldLabel(SaveState state, string field) =>
            (state.Fields.Find(field) ?? (ColumnOf(state, field) is { } column ? state.Fields.Find(column.Name) : null))?.Label ?? field;

        private static AtlasColumn? ColumnOf(SaveState state, string name) =>
            state.Table.Find(name) ?? (state.Fields.Find(name) is { } field ? state.Table.Find(field.Field) : null);

        private static string ActionKeyOf(CPWorkflowEngineAction action) => action.Start ?? action.Execute ?? "";

        private static string BusinessObjectIdOf(CPWorkflow binding, int recordId) =>
            (binding.BusinessObjectPrefix ?? "") + recordId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string InstanceIdOf(CPWorkflow binding, int recordId) => "WF-" + BusinessObjectIdOf(binding, recordId);
    }
}
