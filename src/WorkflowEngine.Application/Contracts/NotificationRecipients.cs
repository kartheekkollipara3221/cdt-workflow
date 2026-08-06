namespace WorkflowEngine.Application.Contracts;

public sealed class NotificationRecipients
{
    public List<string> ToEmailAddresses { get; set; } = [];

    public List<string> CcEmailAddresses { get; set; } = [];
}
