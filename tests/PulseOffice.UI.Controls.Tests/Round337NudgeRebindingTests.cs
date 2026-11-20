using Avalonia.Input;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Shortcuts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round337NudgeRebindingTests
{
    [Fact]
    public void A_rebound_nudge_follows_its_new_key_in_its_module_only()
    {
        var nudges = new ObjectNudgeCommands();
        var registry = new CommandRegistry(new TextCatalog(UiLanguage.English));
        registry.RegisterRange(nudges.For(ModuleKind.Sheets, () => true, _ => true));
        var keys = new ObjectNudgeKeys(nudges, new RegistryObjectNudgeBindings(registry, nudges), new ShortcutCapture(macOS: false));

        Assert.True(registry.Rebind(nudges.IdOf(ModuleKind.Sheets, "moveRight"), "Ctrl+Alt+L"));
        Assert.True(registry.Rebind(nudges.IdOf(ModuleKind.Sheets, "moveFineRight"), null));

        Assert.Null(keys.Read(ModuleKind.Sheets, Key.Right, KeyModifiers.None));
        Assert.Null(keys.Read(ModuleKind.Sheets, Key.Right, KeyModifiers.Alt));
        Assert.Equal(new ObjectNudge(1, 0, ObjectNudgeKind.Move, false), keys.Read(ModuleKind.Sheets, Key.L, KeyModifiers.Control | KeyModifiers.Alt));
        Assert.Equal(new ObjectNudge(1, 0, ObjectNudgeKind.Move, false), keys.Read(ModuleKind.Presentations, Key.Right, KeyModifiers.None));
        Assert.Equal(new ObjectNudge(0, 1, ObjectNudgeKind.Move, true), keys.Read(ModuleKind.Sheets, Key.Down, KeyModifiers.Alt));
    }
}
