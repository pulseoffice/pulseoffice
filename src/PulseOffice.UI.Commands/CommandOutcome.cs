namespace PulseOffice.UI.Commands;

public enum CommandOutcomeKind
{
    Ran,
    NotApplicable,
    Refused,
    Cancelled,
    Failed,
}

public sealed record CommandOutcome(CommandOutcomeKind Kind, string? Reason = null, IReadOnlyList<string>? Messages = null)
{
    public IReadOnlyList<string> Notes => Messages ?? [];

    public bool Ran => Kind == CommandOutcomeKind.Ran;
}
