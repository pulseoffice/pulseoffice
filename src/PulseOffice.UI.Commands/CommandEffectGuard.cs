namespace PulseOffice.UI.Commands;

public interface ICommandEffectGuard
{
    ICommandEffectCheck? Begin(CommandDefinition command);
}

public interface ICommandEffectCheck
{
    bool Finish();
}

public interface ICommandEffectChecks
{
    CommandEffectScope Open(CommandDefinition command);
}

public sealed class CommandEffectChecks(IEnumerable<ICommandEffectGuard> guards) : ICommandEffectChecks
{
    public CommandEffectScope Open(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new CommandEffectScope([.. guards.Select(guard => guard.Begin(command)).OfType<ICommandEffectCheck>()]);
    }
}

public sealed class CommandEffectScope(IReadOnlyList<ICommandEffectCheck> checks)
{
    private bool _finished;

    public bool Refused { get; private set; }

    public bool Finish()
    {
        if (_finished) return Refused;
        _finished = true;
        foreach (var check in checks)
            Refused |= check.Finish();
        return Refused;
    }
}
