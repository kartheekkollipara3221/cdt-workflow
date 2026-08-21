namespace WorkflowEngine.Sqms.LiveAcceptance;

public sealed class AcceptanceTimeProvider(DateTimeOffset initialUtc) : TimeProvider
{
    private DateTimeOffset utcNow = initialUtc;

    public override DateTimeOffset GetUtcNow() => utcNow;

    public void SetUtcNow(DateTimeOffset value) => utcNow = value;
}
