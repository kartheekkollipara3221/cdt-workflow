using System.Data;
using Microsoft.Data.SqlClient;
using WorkflowEngine.Application.Inspection;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

public sealed class DatabaseFirstWorkflowStore(IWorkflowSqlConnectionFactory connectionFactory)
{
    public ActiveWorkflowDefinitionVersion? GetActiveDefinitionVersion(string workflowKey)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_WorkflowDefinition_GetActiveVersion");
        Add(command, "@WorkflowKey", workflowKey);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new ActiveWorkflowDefinitionVersion(
                GetInt64(reader, "WorkflowDefinitionVersionId"),
                GetInt32(reader, "VersionNumber"))
            : null;
    }

    public WorkflowDefinitionVersion? GetDefinitionVersion(long workflowDefinitionVersionId)
    {
        using var connection = OpenConnection();
        return GetDefinitionVersion(connection, null, workflowDefinitionVersionId);
    }

    public WorkflowInstance? GetWorkflowInstance(string workflowInstanceId)
    {
        using var connection = OpenConnection();
        return GetWorkflowInstance(connection, null, workflowInstanceId);
    }

    public WorkflowInstance? GetWorkflowInstance(SqlConnection connection, SqlTransaction? transaction, string workflowInstanceId)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowInstance_Get", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapWorkflowInstance(reader) : null;
    }

    public ExistingWorkflowInstance? GetExistingWorkflowInstance(
        string workflowKey,
        string businessObjectType,
        string businessObjectId)
    {
        using var connection = OpenConnection();
        return GetExistingWorkflowInstance(connection, null, workflowKey, businessObjectType, businessObjectId);
    }

    public ExistingWorkflowInstance? GetExistingWorkflowInstance(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowKey,
        string businessObjectType,
        string businessObjectId)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowInstance_GetByBusinessObject", transaction);
        Add(command, "@WorkflowKey", workflowKey);
        Add(command, "@BusinessObjectType", businessObjectType);
        Add(command, "@BusinessObjectId", businessObjectId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new ExistingWorkflowInstance(
                GetString(reader, "WorkflowInstanceId"),
                GetString(reader, "Status"),
                GetInt32(reader, "Version"),
                GetString(reader, "CurrentStateKey"),
                GetDateTimeOffset(reader, "StartedAtUtc"),
                GetNullableDateTimeOffset(reader, "CompletedAtUtc"))
            : null;
    }

    public WorkflowInstance InsertWorkflowInstance(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        string workflowKey,
        long workflowDefinitionVersionId,
        string businessObjectType,
        string businessObjectId,
        string currentStateKey,
        string status,
        DateTimeOffset startedAtUtc,
        string startedByUserId,
        DateTimeOffset? completedAtUtc,
        int version)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowInstance_Insert", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@WorkflowKey", workflowKey);
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        Add(command, "@BusinessObjectType", businessObjectType);
        Add(command, "@BusinessObjectId", businessObjectId);
        Add(command, "@CurrentStateKey", currentStateKey);
        Add(command, "@Status", status);
        Add(command, "@StartedAtUtc", startedAtUtc);
        Add(command, "@StartedByUserId", startedByUserId);
        Add(command, "@CompletedAtUtc", completedAtUtc);
        Add(command, "@Version", version);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? MapWorkflowInstance(reader)
            : throw new InvalidOperationException("Workflow instance insert did not return the inserted row.");
    }

    public int UpdateWorkflowInstanceState(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        int expectedVersion,
        string currentStateKey,
        string status,
        DateTimeOffset? completedAtUtc)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowInstance_UpdateState", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@ExpectedVersion", expectedVersion);
        Add(command, "@CurrentStateKey", currentStateKey);
        Add(command, "@Status", status);
        Add(command, "@CompletedAtUtc", completedAtUtc);
        return ExecuteRowsAffected(command);
    }

    public StartWorkflowResetCounts ResetRuntimeData(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        string businessObjectType,
        string businessObjectId)
    {
        using var command = StoredProcedure(connection, "usp_RuntimeData_Reset", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@BusinessObjectType", businessObjectType);
        Add(command, "@BusinessObjectId", businessObjectId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Runtime reset did not return row counts.");
        }

        return new StartWorkflowResetCounts(
            GetInt32(reader, "IdempotencyRecordRows"),
            GetInt32(reader, "NotificationOutboxRows"),
            GetInt32(reader, "WorkflowAuditRows"),
            GetInt32(reader, "MockSectionDataRows"),
            GetInt32(reader, "WorkflowTaskRows"),
            GetInt32(reader, "WorkflowInstanceRows"));
    }

    public WorkflowActionDefinition? GetActionDefinition(long workflowDefinitionVersionId, string actionKey)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_WorkflowActionDefinition_Get");
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        Add(command, "@ActionKey", actionKey);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapWorkflowActionDefinition(reader) : null;
    }

    public IReadOnlyList<WorkflowTransitionDefinition> GetTransitionsForAction(
        long workflowDefinitionVersionId,
        string fromStateKey,
        string actionKey)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_WorkflowTransitionDefinition_ListForAction");
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        Add(command, "@FromStateKey", fromStateKey);
        Add(command, "@ActionKey", actionKey);
        using var reader = command.ExecuteReader();
        var rows = new List<WorkflowTransitionDefinition>();
        while (reader.Read())
        {
            rows.Add(MapWorkflowTransitionDefinition(reader));
        }

        return rows;
    }

    public WorkflowTask? GetPendingTask(string workflowInstanceId)
    {
        using var connection = OpenConnection();
        return GetPendingTask(connection, null, workflowInstanceId);
    }

    public WorkflowTask? GetPendingTask(SqlConnection connection, SqlTransaction? transaction, string workflowInstanceId)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowTask_GetPending", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapWorkflowTask(reader) : null;
    }

    public WorkflowTaskDefinition GetTaskDefinition(
        SqlConnection connection,
        SqlTransaction? transaction,
        long workflowDefinitionVersionId,
        string taskKey)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowTaskDefinition_Get", transaction);
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        Add(command, "@TaskKey", taskKey);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? MapWorkflowTaskDefinition(reader)
            : throw new InvalidOperationException($"Task '{taskKey}' was not found for workflow definition version id {workflowDefinitionVersionId}.");
    }

    public bool TaskDefinitionHasSla(long workflowDefinitionVersionId, string taskKey)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_WorkflowTaskDefinition_HasSla");
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        Add(command, "@TaskKey", taskKey);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    public WorkflowTask InsertWorkflowTask(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        string taskKey,
        string assignedRoleKey,
        string? assignedUserId,
        string status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? dueAtUtc)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowTask_Insert", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@TaskKey", taskKey);
        Add(command, "@AssignedRoleKey", assignedRoleKey);
        Add(command, "@AssignedUserId", assignedUserId);
        Add(command, "@Status", status);
        Add(command, "@CreatedAtUtc", createdAtUtc);
        Add(command, "@DueAtUtc", dueAtUtc);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? MapWorkflowTask(reader)
            : throw new InvalidOperationException("Workflow task insert did not return the inserted row.");
    }

    public int CompleteWorkflowTask(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        string taskKey,
        DateTimeOffset completedAtUtc,
        string completedByUserId,
        string closedByActionKey)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowTask_Complete", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@TaskKey", taskKey);
        Add(command, "@CompletedAtUtc", completedAtUtc);
        Add(command, "@CompletedByUserId", completedByUserId);
        Add(command, "@ClosedByActionKey", closedByActionKey);
        return ExecuteRowsAffected(command);
    }

    public DateTimeOffset? ResolveDueAtUtc(
        SqlConnection connection,
        SqlTransaction? transaction,
        long workflowDefinitionVersionId,
        string? slaPolicyKey,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(slaPolicyKey))
        {
            return null;
        }

        using var command = StoredProcedure(connection, "usp_SlaPolicy_GetDurationMinutes", transaction);
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        Add(command, "@SlaPolicyKey", slaPolicyKey);
        var result = command.ExecuteScalar();
        return result is null || result is DBNull
            ? null
            : now.AddMinutes(Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture));
    }

    public NotificationRoute GetNotificationRoute(
        SqlConnection connection,
        SqlTransaction? transaction,
        long workflowDefinitionVersionId,
        string routeKey)
    {
        using var command = StoredProcedure(connection, "usp_NotificationRoute_GetByRouteKey", transaction);
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        Add(command, "@RouteKey", routeKey);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? MapNotificationRoute(reader)
            : throw new InvalidOperationException($"Notification route '{routeKey}' was not found for workflow definition version id {workflowDefinitionVersionId}.");
    }

    public NotificationOutbox InsertNotificationOutbox(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        long? workflowTaskId,
        string businessObjectType,
        string businessObjectId,
        string notificationType,
        string? recipientRoleKey,
        string? recipientUserId,
        string subject,
        string payloadJson,
        string status,
        string correlationId,
        DateTimeOffset createdAtUtc)
    {
        using var command = StoredProcedure(connection, "usp_NotificationOutbox_Insert", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@WorkflowTaskId", workflowTaskId);
        Add(command, "@BusinessObjectType", businessObjectType);
        Add(command, "@BusinessObjectId", businessObjectId);
        Add(command, "@NotificationType", notificationType);
        Add(command, "@RecipientRoleKey", recipientRoleKey);
        Add(command, "@RecipientUserId", recipientUserId);
        Add(command, "@Subject", subject);
        Add(command, "@PayloadJson", payloadJson);
        Add(command, "@Status", status);
        Add(command, "@CorrelationId", correlationId);
        Add(command, "@CreatedAtUtc", createdAtUtc);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? MapNotificationOutbox(reader)
            : throw new InvalidOperationException("Notification outbox insert did not return the inserted row.");
    }

    public void InsertMockSectionData(
        SqlConnection connection,
        SqlTransaction? transaction,
        string businessObjectType,
        string businessObjectId,
        string sectionKey,
        string payloadJson,
        string savedByUserId,
        DateTimeOffset savedAtUtc,
        string correlationId)
    {
        using var command = StoredProcedure(connection, "usp_MockSectionData_Insert", transaction);
        Add(command, "@BusinessObjectType", businessObjectType);
        Add(command, "@BusinessObjectId", businessObjectId);
        Add(command, "@SectionKey", sectionKey);
        Add(command, "@PayloadJson", payloadJson);
        Add(command, "@SavedByUserId", savedByUserId);
        Add(command, "@SavedAtUtc", savedAtUtc);
        Add(command, "@CorrelationId", correlationId);
        using var reader = command.ExecuteReader();
        _ = reader.Read();
    }

    public void InsertWorkflowAudit(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        string businessObjectType,
        string businessObjectId,
        string actionKey,
        string fromStateKey,
        string toStateKey,
        string performedByUserId,
        string performedByRoleKey,
        string? reason,
        string correlationId,
        DateTimeOffset createdAtUtc)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowAudit_Insert", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@BusinessObjectType", businessObjectType);
        Add(command, "@BusinessObjectId", businessObjectId);
        Add(command, "@ActionKey", actionKey);
        Add(command, "@FromStateKey", fromStateKey);
        Add(command, "@ToStateKey", toStateKey);
        Add(command, "@PerformedByUserId", performedByUserId);
        Add(command, "@PerformedByRoleKey", performedByRoleKey);
        Add(command, "@Reason", reason);
        Add(command, "@CorrelationId", correlationId);
        Add(command, "@CreatedAtUtc", createdAtUtc);
        using var reader = command.ExecuteReader();
        _ = reader.Read();
    }

    public IdempotencyRecord? GetIdempotencyRecord(string idempotencyKey)
    {
        using var connection = OpenConnection();
        return GetIdempotencyRecord(connection, null, idempotencyKey);
    }

    public IdempotencyRecord? GetIdempotencyRecord(SqlConnection connection, SqlTransaction? transaction, string idempotencyKey)
    {
        using var command = StoredProcedure(connection, "usp_IdempotencyRecord_GetByKey", transaction);
        Add(command, "@IdempotencyKey", idempotencyKey);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapIdempotencyRecord(reader) : null;
    }

    public void InsertIdempotencyRecord(
        SqlConnection connection,
        SqlTransaction? transaction,
        string idempotencyKey,
        string workflowInstanceId,
        string actionKey,
        string requestHash,
        string resultHash,
        string resultJson,
        DateTimeOffset createdAtUtc)
    {
        using var command = StoredProcedure(connection, "usp_IdempotencyRecord_Insert", transaction);
        Add(command, "@IdempotencyKey", idempotencyKey);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@ActionKey", actionKey);
        Add(command, "@RequestHash", requestHash);
        Add(command, "@ResultHash", resultHash);
        Add(command, "@ResultJson", resultJson);
        Add(command, "@CreatedAtUtc", createdAtUtc);
        using var reader = command.ExecuteReader();
        _ = reader.Read();
    }

    public WorkflowContextData? GetWorkflowContextData(
        string workflowInstanceId,
        IReadOnlyList<string> roleKeys)
    {
        using var connection = OpenConnection();
        return GetWorkflowContextData(connection, null, workflowInstanceId, roleKeys);
    }

    public WorkflowContextData? GetWorkflowContextData(
        SqlConnection connection,
        SqlTransaction? transaction,
        string workflowInstanceId,
        IReadOnlyList<string> roleKeys)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowContext_Get", transaction);
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        Add(command, "@RoleKeys", string.Join(",", roleKeys));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var instance = MapWorkflowInstance(reader);
        var definitionVersionNumber = GetInt32(reader, "WorkflowDefinitionVersionNumber");
        var currentStateLabel = GetString(reader, "CurrentStateLabel");

        WorkflowContextTaskData? pendingTask = null;
        if (reader.NextResult() && reader.Read())
        {
            pendingTask = new WorkflowContextTaskData(MapWorkflowTask(reader), GetString(reader, "TaskLabel"));
        }

        var actions = new List<WorkflowActionDefinition>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                actions.Add(MapWorkflowActionDefinition(reader));
            }
        }

        return new WorkflowContextData(instance, definitionVersionNumber, currentStateLabel, pendingTask, actions);
    }

    public WorkflowInspectionSnapshot GetInspectionSnapshot(string workflowInstanceId)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_WorkflowInspection_Get");
        Add(command, "@WorkflowInstanceId", workflowInstanceId);
        using var reader = command.ExecuteReader();
        WorkflowInstanceInspection? instance = null;
        if (reader.Read())
        {
            instance = new WorkflowInstanceInspection(
                GetString(reader, "WorkflowInstanceId"),
                GetString(reader, "WorkflowKey"),
                GetInt32(reader, "WorkflowDefinitionVersionNumber"),
                GetString(reader, "BusinessObjectType"),
                GetString(reader, "BusinessObjectId"),
                GetString(reader, "CurrentStateKey"),
                GetString(reader, "Status"),
                GetDateTimeOffset(reader, "StartedAtUtc"),
                GetString(reader, "StartedByUserId"),
                GetNullableDateTimeOffset(reader, "CompletedAtUtc"),
                GetInt32(reader, "Version"));
        }

        var tasks = new List<WorkflowTaskInspection>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                tasks.Add(new WorkflowTaskInspection(
                    GetInt64(reader, "Id"),
                    GetString(reader, "TaskKey"),
                    GetString(reader, "AssignedRoleKey"),
                    GetNullableString(reader, "AssignedUserId"),
                    GetString(reader, "Status"),
                    GetDateTimeOffset(reader, "CreatedAtUtc"),
                    GetNullableDateTimeOffset(reader, "DueAtUtc"),
                    GetNullableDateTimeOffset(reader, "CompletedAtUtc"),
                    GetNullableString(reader, "CompletedByUserId"),
                    GetNullableString(reader, "ClosedByActionKey")));
            }
        }

        var history = new List<WorkflowAuditInspection>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                history.Add(new WorkflowAuditInspection(
                    GetInt64(reader, "Id"),
                    GetString(reader, "ActionKey"),
                    GetString(reader, "FromStateKey"),
                    GetString(reader, "ToStateKey"),
                    GetString(reader, "PerformedByUserId"),
                    GetString(reader, "PerformedByRoleKey"),
                    GetNullableString(reader, "Reason"),
                    GetString(reader, "CorrelationId"),
                    GetDateTimeOffset(reader, "CreatedAtUtc")));
            }
        }

        var notifications = new List<NotificationInspection>();
        if (reader.NextResult())
        {
            while (reader.Read())
            {
                notifications.Add(new NotificationInspection(
                    GetInt64(reader, "Id"),
                    GetNullableInt64(reader, "WorkflowTaskId"),
                    GetString(reader, "NotificationType"),
                    GetNullableString(reader, "RecipientRoleKey"),
                    GetNullableString(reader, "RecipientUserId"),
                    GetString(reader, "Subject"),
                    GetString(reader, "Status"),
                    GetString(reader, "CorrelationId"),
                    GetDateTimeOffset(reader, "CreatedAtUtc"),
                    GetNullableDateTimeOffset(reader, "SentAtUtc")));
            }
        }

        return new WorkflowInspectionSnapshot(instance, tasks, history, notifications);
    }

    public SlaScanSummary GetSlaScanSummary(DateTimeOffset now)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_Sla_GetScanSummary");
        Add(command, "@NowUtc", now);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new SlaScanSummary(GetInt32(reader, "PendingTasksWithSla"), GetInt32(reader, "AlreadyQueued"))
            : new SlaScanSummary(0, 0);
    }

    public IReadOnlyList<SlaOverdueCandidate> GetSlaOverdueCandidates(DateTimeOffset now)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_Sla_GetOverdueCandidates");
        Add(command, "@NowUtc", now);
        using var reader = command.ExecuteReader();
        var rows = new List<SlaOverdueCandidate>();
        while (reader.Read())
        {
            rows.Add(new SlaOverdueCandidate(
                GetInt64(reader, "WorkflowTaskId"),
                GetString(reader, "WorkflowInstanceId"),
                GetString(reader, "TaskKey"),
                GetString(reader, "AssignedRoleKey"),
                GetDateTimeOffset(reader, "DueAtUtc")));
        }

        return rows;
    }

    public RunSlaQueuedNotification? InsertSlaOverdueNotification(long workflowTaskId, DateTimeOffset now)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_Sla_InsertOverdueNotification");
        Add(command, "@WorkflowTaskId", workflowTaskId);
        Add(command, "@NowUtc", now);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new RunSlaQueuedNotification(
                GetInt64(reader, "WorkflowTaskId"),
                GetString(reader, "WorkflowInstanceId"),
                GetString(reader, "TaskKey"),
                GetNullableString(reader, "AssignedRoleKey"),
                GetDateTimeOffset(reader, "DueAtUtc"),
                GetInt64(reader, "NotificationOutboxId"))
            : null;
    }

    public IReadOnlyList<NotificationOutbox> GetClaimableNotifications(DateTimeOffset leaseCutoffUtc)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_NotificationOutbox_GetClaimable");
        Add(command, "@LeaseCutoffUtc", leaseCutoffUtc);
        using var reader = command.ExecuteReader();
        var rows = new List<NotificationOutbox>();
        while (reader.Read())
        {
            rows.Add(MapNotificationOutbox(reader));
        }

        return rows;
    }

    public int ClaimNotification(long id, string processingToken, DateTimeOffset processingStartedAtUtc, DateTimeOffset leaseCutoffUtc)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_NotificationOutbox_Claim");
        Add(command, "@Id", id);
        Add(command, "@ProcessingToken", processingToken);
        Add(command, "@ProcessingStartedAtUtc", processingStartedAtUtc);
        Add(command, "@LeaseCutoffUtc", leaseCutoffUtc);
        return ExecuteRowsAffected(command);
    }

    public NotificationOutbox? GetClaimedNotification(long id, string processingToken)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_NotificationOutbox_GetByClaim");
        Add(command, "@Id", id);
        Add(command, "@ProcessingToken", processingToken);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapNotificationOutbox(reader) : null;
    }

    public int MarkNotificationSent(long id, string processingToken, DateTimeOffset sentAtUtc)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_NotificationOutbox_MarkSent");
        Add(command, "@Id", id);
        Add(command, "@ProcessingToken", processingToken);
        Add(command, "@SentAtUtc", sentAtUtc);
        return ExecuteRowsAffected(command);
    }

    public int ReleaseNotificationClaimAfterFailure(long id, string processingToken, string lastError)
    {
        using var connection = OpenConnection();
        using var command = StoredProcedure(connection, "usp_NotificationOutbox_ReleaseClaimAfterFailure");
        Add(command, "@Id", id);
        Add(command, "@ProcessingToken", processingToken);
        Add(command, "@LastError", lastError);
        return ExecuteRowsAffected(command);
    }

    public SqlConnection OpenConnection()
    {
        var connection = connectionFactory.CreateConnection();
        connection.Open();
        return connection;
    }

    private WorkflowDefinitionVersion? GetDefinitionVersion(
        SqlConnection connection,
        SqlTransaction? transaction,
        long workflowDefinitionVersionId)
    {
        using var command = StoredProcedure(connection, "usp_WorkflowDefinitionVersion_Get", transaction);
        Add(command, "@WorkflowDefinitionVersionId", workflowDefinitionVersionId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapWorkflowDefinitionVersion(reader) : null;
    }

    private static SqlCommand StoredProcedure(SqlConnection connection, string name, SqlTransaction? transaction = null)
    {
        return new SqlCommand("[dbo].[" + name + "]", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };
    }

    private static int ExecuteRowsAffected(SqlCommand command)
    {
        using var reader = command.ExecuteReader();
        return reader.Read() ? GetInt32(reader, "RowsAffected") : 0;
    }

    private static void Add(SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static WorkflowDefinitionVersion MapWorkflowDefinitionVersion(SqlDataReader reader)
    {
        return new WorkflowDefinitionVersion
        {
            Id = GetInt64(reader, "Id"),
            WorkflowDefinitionId = GetInt64(reader, "WorkflowDefinitionId"),
            VersionNumber = GetInt32(reader, "VersionNumber"),
            IsActive = GetBoolean(reader, "IsActive"),
            CreatedAtUtc = GetDateTimeOffset(reader, "CreatedAtUtc")
        };
    }

    private static WorkflowInstance MapWorkflowInstance(SqlDataReader reader)
    {
        return new WorkflowInstance
        {
            Id = GetInt64(reader, "Id"),
            WorkflowInstanceId = GetString(reader, "WorkflowInstanceId"),
            WorkflowKey = GetString(reader, "WorkflowKey"),
            WorkflowDefinitionVersionId = GetInt64(reader, "WorkflowDefinitionVersionId"),
            BusinessObjectType = GetString(reader, "BusinessObjectType"),
            BusinessObjectId = GetString(reader, "BusinessObjectId"),
            CurrentStateKey = GetString(reader, "CurrentStateKey"),
            Status = GetString(reader, "Status"),
            StartedAtUtc = GetDateTimeOffset(reader, "StartedAtUtc"),
            StartedByUserId = GetString(reader, "StartedByUserId"),
            CompletedAtUtc = GetNullableDateTimeOffset(reader, "CompletedAtUtc"),
            Version = GetInt32(reader, "Version")
        };
    }

    private static WorkflowActionDefinition MapWorkflowActionDefinition(SqlDataReader reader)
    {
        return new WorkflowActionDefinition
        {
            Id = GetInt64(reader, "Id"),
            WorkflowDefinitionVersionId = GetInt64(reader, "WorkflowDefinitionVersionId"),
            ActionKey = GetString(reader, "ActionKey"),
            DisplayName = GetString(reader, "DisplayName"),
            SectionKey = GetNullableString(reader, "SectionKey"),
            ButtonKey = GetNullableString(reader, "ButtonKey"),
            ButtonLabel = GetNullableString(reader, "ButtonLabel"),
            Intent = GetNullableString(reader, "Intent"),
            RequiresReason = GetBoolean(reader, "RequiresReason"),
            SortOrder = GetInt32(reader, "SortOrder"),
            IsActive = GetBoolean(reader, "IsActive")
        };
    }

    private static WorkflowTransitionDefinition MapWorkflowTransitionDefinition(SqlDataReader reader)
    {
        return new WorkflowTransitionDefinition
        {
            Id = GetInt64(reader, "Id"),
            WorkflowDefinitionVersionId = GetInt64(reader, "WorkflowDefinitionVersionId"),
            FromStateKey = GetString(reader, "FromStateKey"),
            ActionKey = GetString(reader, "ActionKey"),
            AllowedRoleKey = GetString(reader, "AllowedRoleKey"),
            ToStateKey = GetString(reader, "ToStateKey"),
            TaskToCloseKey = GetNullableString(reader, "TaskToCloseKey"),
            TaskToCreateKey = GetNullableString(reader, "TaskToCreateKey"),
            NotificationRouteKey = GetNullableString(reader, "NotificationRouteKey"),
            AuditLabel = GetString(reader, "AuditLabel"),
            IsActive = GetBoolean(reader, "IsActive")
        };
    }

    private static WorkflowTask MapWorkflowTask(SqlDataReader reader)
    {
        return new WorkflowTask
        {
            Id = GetInt64(reader, "Id"),
            WorkflowInstanceId = GetString(reader, "WorkflowInstanceId"),
            TaskKey = GetString(reader, "TaskKey"),
            AssignedRoleKey = GetString(reader, "AssignedRoleKey"),
            AssignedUserId = GetNullableString(reader, "AssignedUserId"),
            Status = GetString(reader, "Status"),
            CreatedAtUtc = GetDateTimeOffset(reader, "CreatedAtUtc"),
            DueAtUtc = GetNullableDateTimeOffset(reader, "DueAtUtc"),
            CompletedAtUtc = GetNullableDateTimeOffset(reader, "CompletedAtUtc"),
            CompletedByUserId = GetNullableString(reader, "CompletedByUserId"),
            ClosedByActionKey = GetNullableString(reader, "ClosedByActionKey")
        };
    }

    private static WorkflowTaskDefinition MapWorkflowTaskDefinition(SqlDataReader reader)
    {
        return new WorkflowTaskDefinition
        {
            Id = GetInt64(reader, "Id"),
            WorkflowDefinitionVersionId = GetInt64(reader, "WorkflowDefinitionVersionId"),
            TaskKey = GetString(reader, "TaskKey"),
            DisplayName = GetString(reader, "DisplayName"),
            OwnerRoleKey = GetString(reader, "OwnerRoleKey"),
            SlaPolicyKey = GetNullableString(reader, "SlaPolicyKey"),
            IsActive = GetBoolean(reader, "IsActive")
        };
    }

    private static NotificationRoute MapNotificationRoute(SqlDataReader reader)
    {
        return new NotificationRoute
        {
            Id = GetInt64(reader, "Id"),
            WorkflowDefinitionVersionId = GetInt64(reader, "WorkflowDefinitionVersionId"),
            RouteKey = GetString(reader, "RouteKey"),
            NotificationType = GetString(reader, "NotificationType"),
            RecipientRoleKey = GetNullableString(reader, "RecipientRoleKey"),
            RecipientUserId = GetNullableString(reader, "RecipientUserId"),
            SubjectTemplate = GetString(reader, "SubjectTemplate"),
            IsActive = GetBoolean(reader, "IsActive")
        };
    }

    private static NotificationOutbox MapNotificationOutbox(SqlDataReader reader)
    {
        return new NotificationOutbox
        {
            Id = GetInt64(reader, "Id"),
            WorkflowInstanceId = GetString(reader, "WorkflowInstanceId"),
            WorkflowTaskId = GetNullableInt64(reader, "WorkflowTaskId"),
            BusinessObjectType = GetString(reader, "BusinessObjectType"),
            BusinessObjectId = GetString(reader, "BusinessObjectId"),
            NotificationType = GetString(reader, "NotificationType"),
            RecipientRoleKey = GetNullableString(reader, "RecipientRoleKey"),
            RecipientUserId = GetNullableString(reader, "RecipientUserId"),
            Subject = GetString(reader, "Subject"),
            PayloadJson = GetString(reader, "PayloadJson"),
            Status = GetString(reader, "Status"),
            CorrelationId = GetString(reader, "CorrelationId"),
            CreatedAtUtc = GetDateTimeOffset(reader, "CreatedAtUtc"),
            ProcessingToken = GetNullableString(reader, "ProcessingToken"),
            ProcessingStartedAtUtc = GetNullableDateTimeOffset(reader, "ProcessingStartedAtUtc"),
            RetryCount = GetInt32(reader, "RetryCount"),
            LastError = GetNullableString(reader, "LastError"),
            SentAtUtc = GetNullableDateTimeOffset(reader, "SentAtUtc")
        };
    }

    private static IdempotencyRecord MapIdempotencyRecord(SqlDataReader reader)
    {
        return new IdempotencyRecord
        {
            Id = GetInt64(reader, "Id"),
            IdempotencyKey = GetString(reader, "IdempotencyKey"),
            WorkflowInstanceId = GetString(reader, "WorkflowInstanceId"),
            ActionKey = GetString(reader, "ActionKey"),
            RequestHash = GetString(reader, "RequestHash"),
            ResultHash = GetString(reader, "ResultHash"),
            ResultJson = GetString(reader, "ResultJson"),
            CreatedAtUtc = GetDateTimeOffset(reader, "CreatedAtUtc")
        };
    }

    private static string GetString(SqlDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    private static string? GetNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static long GetInt64(SqlDataReader reader, string name)
    {
        return reader.GetInt64(reader.GetOrdinal(name));
    }

    private static long? GetNullableInt64(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static int GetInt32(SqlDataReader reader, string name)
    {
        return reader.GetInt32(reader.GetOrdinal(name));
    }

    private static bool GetBoolean(SqlDataReader reader, string name)
    {
        return reader.GetBoolean(reader.GetOrdinal(name));
    }

    private static DateTimeOffset GetDateTimeOffset(SqlDataReader reader, string name)
    {
        return reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal(name));
    }

    private static DateTimeOffset? GetNullableDateTimeOffset(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateTimeOffset>(ordinal);
    }
}

public sealed record ActiveWorkflowDefinitionVersion(long WorkflowDefinitionVersionId, int VersionNumber);

public sealed record WorkflowContextData(
    WorkflowInstance Instance,
    int WorkflowDefinitionVersionNumber,
    string CurrentStateLabel,
    WorkflowContextTaskData? PendingTask,
    IReadOnlyList<WorkflowActionDefinition> AvailableActions);

public sealed record WorkflowContextTaskData(WorkflowTask Task, string TaskLabel);

public sealed record SlaScanSummary(int PendingTasksWithSla, int AlreadyQueued);

public sealed record SlaOverdueCandidate(
    long WorkflowTaskId,
    string WorkflowInstanceId,
    string TaskKey,
    string AssignedRoleKey,
    DateTimeOffset DueAtUtc);
