using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands.Shortcuts;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public sealed class Round332ViewRoutedGestureTests
{
    [Fact]
    public void A_gesture_routed_by_the_view_is_listed_but_never_dispatched_nor_rebound()
    {
        var registry = new CommandRegistry(new FakeTextCatalog());
        registry.Register(new CommandDefinition
        {
            Id = new CommandId("deck.object.nudge.resizeRight"),
            Module = ModuleKind.Presentations,
            Surface = CommandSurface.Palette,
            DefaultGesture = "Shift+Right",
            GestureRoutedByView = true,
            Execute = (_, _) => Task.CompletedTask,
        });
        var context = new CommandContext(ModuleKind.Presentations, SelectionKind.None, DocumentState.DocumentOpen);

        Assert.Equal("Shift+Right", Assert.Single(new ShortcutCatalog(registry).Entries()).Gesture);
        Assert.Empty(registry.ActiveGestures(context));
        Assert.False(registry.Rebind(new CommandId("deck.object.nudge.resizeRight"), "Ctrl+K"));
    }
}
