namespace WorkflowEngine.Api.Services;

public sealed class WorkflowActorResolutionException : Exception
{
    public WorkflowActorResolutionException(int statusCode, string title, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
    }

    public int StatusCode { get; }

    public string Title { get; }

    public string Detail { get; }
}
