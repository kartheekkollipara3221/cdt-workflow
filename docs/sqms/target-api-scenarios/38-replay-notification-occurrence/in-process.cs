using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Sqms;

var request = new QueueWorkflowNotificationRequest(
    SqmsKeys.OpportunityWorkflow,
    1,
    SqmsKeys.Routes.OpportunityOpen,
    "SQMS:OpportunityOpen:2030:Q1:2030-01-02",
    "SqmsOpportunityGroup",
    "2030-Q1:2030-01-02",
    notificationInput,
    "SQMS:OpportunityOpen:2030:Q1:2030-01-02");
var first = await notificationQueue.QueueAsync(request, cancellationToken);
var replay = await notificationQueue.QueueAsync(request, cancellationToken);
if (!first.Success || !replay.Success || !replay.Replay)
    throw new InvalidOperationException("Expected a successful occurrence replay.");
if (first.NotificationOutboxId != replay.NotificationOutboxId)
    throw new InvalidOperationException("Replay must return the existing outbox row.");

// notificationInput is a server-resolved WorkflowNotificationInput. It is not public JSON.
