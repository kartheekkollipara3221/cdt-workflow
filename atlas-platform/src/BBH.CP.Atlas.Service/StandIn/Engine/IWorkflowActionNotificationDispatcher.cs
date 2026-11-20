// STAND-IN: do not copy to the target. The target engine copy has this interface in the namespace
// WorkflowEngine.Application.WorkflowNotification (the target CDT and SQMS services use it). This repo has the same code in
// workflow-api (WorkflowEngine.WorkflowApi.Services). The platform workflow adapter uses the target name, so it compiles at
// the target and here.
namespace WorkflowEngine.Application.WorkflowNotification;

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
