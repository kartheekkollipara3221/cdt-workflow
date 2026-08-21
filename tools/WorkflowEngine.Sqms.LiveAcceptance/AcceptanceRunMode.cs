namespace WorkflowEngine.Sqms.LiveAcceptance;

public enum AcceptanceRunMode
{
    Full,
    VerifyOnly
}

public static class AcceptanceRunModeParser
{
    public static AcceptanceRunMode Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Count switch
        {
            0 => AcceptanceRunMode.Full,
            1 when string.Equals(args[0], "--verify-only", StringComparison.Ordinal) =>
                AcceptanceRunMode.VerifyOnly,
            _ => throw new ArgumentException(
                "Usage: WorkflowEngine.Sqms.LiveAcceptance [--verify-only]",
                nameof(args))
        };
    }
}
