using PulseOffice.Foundation.Modules;

namespace PulseOffice.UI.Commands.Shortcuts;

public sealed class ShortcutScope
{
    public bool ActiveTogether(ModuleKind first, ModuleKind second) =>
        first == ModuleKind.Shell || second == ModuleKind.Shell || first == second;

    public bool ActiveTogether(CommandDefinition first, CommandDefinition second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return ActiveTogether(first.Module, second.Module);
    }
}
