namespace WorkflowEngine.WorkflowApi.Services;

public interface IWorkflowActionNotificationDispatcher
{
    WorkflowActionNotificationDispatchResult DispatchCreatedNotifications(
        string workflowInstanceId,
        string correlationId);
}

public sealed record WorkflowActionNotificationDispatchResult(
    bool Success,
    string? FailureMessage,
    IReadOnlyList<long> NotificationIds)
{
    public static WorkflowActionNotificationDispatchResult Delivered(IReadOnlyList<long> notificationIds)
    {
        return new WorkflowActionNotificationDispatchResult(true, null, notificationIds);
    }

    public static WorkflowActionNotificationDispatchResult Failed(string failureMessage)
    {
        return new WorkflowActionNotificationDispatchResult(false, failureMessage, []);
    }
}
