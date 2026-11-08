using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;

namespace PulseOffice.Shell;

public interface IStatusZoomSource
{
    CommandDefinition? Command(CommandContext context);

    double? Percent(CommandContext context);
}

public sealed class StatusZoomSource(CommandRegistry registry, IReadOnlyDictionary<ModuleKind, string> commands) : IStatusZoomSource
{
    public CommandDefinition? Command(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return commands.TryGetValue(context.Module, out var id) && registry.Find(new CommandId(id)) is { } command && command.IsEnabled(context)
            ? command
            : null;
    }

    public double? Percent(CommandContext context) =>
        Command(context)?.RibbonControl is NumberBoxSpec spec ? spec.Current() : null;
}
