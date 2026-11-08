using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public class SurfaceTreeCacheTests
{
    private static CommandDefinition Command(string id, DocumentState required = DocumentState.None) => new()
    {
        Id = new CommandId(id),
        MenuPath = "file/save",
        RibbonPath = "home/clipboard",
        RequiredState = required,
        Execute = (_, _) => Task.CompletedTask,
    };

    private static readonly CommandContext Open = new(ModuleKind.Shell, SelectionKind.None, DocumentState.DocumentOpen);

    [Fact]
    public void The_same_presence_reuses_the_built_ribbon_tree()
    {
        var registry = new CommandRegistry(new FakeTextCatalog());
        registry.RegisterRange([Command("c.one"), Command("c.two")]);

        Assert.Same(registry.BuildRibbon(Open), registry.BuildRibbon(Open));
        Assert.Same(registry.BuildMenu(Open), registry.BuildMenu(Open));
    }

    [Fact]
    public void A_change_of_presence_or_registration_builds_a_new_tree()
    {
        var registry = new CommandRegistry(new FakeTextCatalog());
        registry.RegisterRange([Command("c.one"), Command("c.save", DocumentState.DocumentOpen)]);
        var closed = registry.BuildRibbon(CommandContext.Empty);
        var opened = registry.BuildRibbon(Open);
        Assert.NotSame(closed, opened);
        Assert.Equal(2, opened.Single().Children.Single().Children.Count);

        var before = registry.BuildRibbon(Open);
        registry.Rebind(new CommandId("c.one"), "Ctrl+J");
        var rebound = registry.BuildRibbon(Open);
        Assert.NotSame(before, rebound);
        Assert.Equal("Ctrl+J", rebound.Single().Children.Single().Children.Single(node => node.Command?.Id.Value == "c.one").Command!.DefaultGesture);

        registry.Register(Command("c.three"));
        Assert.Equal(3, registry.BuildRibbon(Open).Single().Children.Single().Children.Count);
    }
}
