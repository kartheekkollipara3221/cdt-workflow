namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowActorResolutionException(
    int statusCode,
    string title,
    string detail) : InvalidOperationException(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;

    public string Detail { get; } = detail;
}
