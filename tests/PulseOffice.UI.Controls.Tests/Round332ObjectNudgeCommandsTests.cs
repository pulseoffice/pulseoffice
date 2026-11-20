using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round332ObjectNudgeCommandsTests
{
    [Fact]
    public async Task Each_module_gets_move_fine_move_resize_and_fine_resize_commands_routed_by_the_view()
    {
        var applied = new List<ObjectNudge>();
        var commands = new ObjectNudgeCommands().For(ModuleKind.Presentations, "deck", () => true, nudge => { applied.Add(nudge); return true; });

        Assert.Equal(16, commands.Count);
        Assert.Equal(16, commands.Select(command => command.Id.Value).Distinct().Count());
        Assert.All(commands, command => Assert.True(command.GestureRoutedByView));
        var fineResize = Assert.Single(commands, command => command.Id.Value == "deck.object.nudge.resizeFineRight");
        Assert.Equal("Ctrl+Shift+Right", fineResize.DefaultGesture);

        await fineResize.Execute(new CommandContext(ModuleKind.Presentations, SelectionKind.None, DocumentState.DocumentOpen), CancellationToken.None);

        Assert.Equal(new ObjectNudge(1, 0, ObjectNudgeKind.Resize, true), Assert.Single(applied));
    }
}
