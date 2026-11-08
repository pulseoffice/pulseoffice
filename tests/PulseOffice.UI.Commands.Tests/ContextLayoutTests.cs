using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public sealed class ContextLayoutTests
{
    private static readonly FakeTextCatalog Texts = new();

    private static CommandDefinition Command(string id, params string[] declared)
    {
        Texts.Add($"command.{id}.title", id);

        return new CommandDefinition
        {
            Id = new CommandId(id),
            Surface = declared.Length > 0 ? CommandSurface.ContextMenu : CommandSurface.Common,
            ContextPaths = declared,
            Module = ModuleKind.Shell,
            Execute = (_, _) => Task.CompletedTask,
        };
    }

    private static CommandContext Context() => new(ModuleKind.Shell, SelectionKind.None, DocumentState.None);

    [Fact]
    public void UnComandoCompareInPiuMenuContestualiUnaVoltaPerRadice()
    {
        var layout = new ContextLayout(
        [
            ("a.copy", "object/clipboard"),
            ("a.copy", "text/clipboard"),
            ("a.", "object/other"),
        ]);

        Assert.Equal(["object/clipboard", "text/clipboard"], layout.PathsFor("a.copy"));
        Assert.Equal(["object/other"], layout.PathsFor("a.cut"));
    }

    [Fact]
    public void LaMappaSostituisceIPercorsiDichiaratiSullaStessaRadiceELasciaGliAltri()
    {
        var registry = new CommandRegistry(Texts);
        registry.Use(CommandSurface.ContextMenu, new ContextLayout([("b.one", "object/format")]));
        registry.Register(Command("b.one", "object/old", "slide/manage"));

        var command = registry.Find(new CommandId("b.one"))!;

        Assert.Equal(["slide/manage", "object/format"], command.ContextPaths);
        Assert.True(command.Surface.HasFlag(CommandSurface.ContextMenu));
    }

    [Fact]
    public void LOrdineDelleVociSegueLaMappa()
    {
        var registry = new CommandRegistry(Texts);
        registry.Use(CommandSurface.ContextMenu, new ContextLayout([("c.second", "object/group"), ("c.first", "object/group")]));
        registry.RegisterRange([Command("c.first"), Command("c.second")]);

        var group = Assert.Single(Assert.Single(registry.BuildContextMenu(Context(), "object")).Children);

        Assert.Equal(["c.second", "c.first"], group.Children.Select(child => child.Command!.Id.Value));
    }

    [Fact]
    public void LeRegoleEsatteSenzaComandoSonoSegnalate() =>
        Assert.Equal(["z.gone"], new ContextLayout([("z.here", "object/x"), ("z.gone", "object/x"), ("z.", "object/y")])
            .UnusedRules(["z.here"]));
}
