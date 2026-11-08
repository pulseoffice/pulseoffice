using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public class CommandLayoutTests
{
    private static readonly FakeTextCatalog Texts = new();

    private static CommandDefinition Command(string id)
    {
        Texts.Add($"command.{id}.title", id);

        return new CommandDefinition
        {
            Id = new CommandId(id),
            Surface = CommandSurface.Common,
            Module = ModuleKind.Shell,
            Execute = (_, _) => Task.CompletedTask,
        };
    }

    private static CommandRegistry RegistryWith(
        ICommandLayout ribbon, params CommandDefinition[] commands)
    {
        var registry = new CommandRegistry(Texts);
        registry.Use(CommandSurface.Ribbon, ribbon);
        registry.RegisterRange(commands);
        return registry;
    }

    private static CommandContext Context() =>
        new(ModuleKind.Shell, SelectionKind.None, DocumentState.None);

    [Fact]
    public void LOrdineDeiMenuDiPrimoLivelloEDichiaratoNonDedottoDalleRegole()
    {
        var layout = new CommandLayout(
        [
            ("a.convert", "tools/language"),
            ("a.sheet", "sheet/manage"),
            ("a.data", "data/sort"),
        ],
        ["sheet", "data", "tools"]);

        var registry = new CommandRegistry(Texts);
        registry.Use(CommandSurface.Menu, layout);
        registry.RegisterRange([Command("a.convert"), Command("a.sheet"), Command("a.data")]);

        Assert.Equal(["sheet", "data", "tools"], registry.BuildMenu(Context()).Select(node => node.Segment));
    }

    [Fact]
    public void LaMappaDecideDoveUnComandoCompareNellaRibbon()
    {
        var layout = new CommandLayout([("a.one", "home/clipboard")]);
        var registry = RegistryWith(layout, Command("a.one"));

        var tab = Assert.Single(registry.BuildRibbon(Context()));
        var group = Assert.Single(tab.Children);

        Assert.Equal("home", tab.Segment);
        Assert.Equal("clipboard", group.Segment);
    }

    [Fact]
    public void UnComandoDelGuscioPuoStareInPostiDiversiSecondoIlModulo()
    {
        var layout = new CommandLayout(
        [
            ("shell.print@Sheets", "layout/page"),
            ("shell.print@Presentations", "presentation/export"),
        ]);
        var registry = RegistryWith(layout, Command("shell.print"));

        var inSheets = Assert.Single(registry.BuildRibbon(new CommandContext(ModuleKind.Sheets, SelectionKind.None, DocumentState.None)));
        var inDeck = Assert.Single(registry.BuildRibbon(new CommandContext(ModuleKind.Presentations, SelectionKind.None, DocumentState.None)));

        Assert.Equal("layout", inSheets.Segment);
        Assert.Equal("page", Assert.Single(inSheets.Children).Segment);
        Assert.Equal("presentation", inDeck.Segment);
        Assert.Equal("export", Assert.Single(inDeck.Children).Segment);
    }

    [Fact]
    public void LOrdineDelleSchedeUsaLaRegolaDelModuloCorrente()
    {
        var layout = new CommandLayout(
        [
            ("shell.print@Presentations", "presentation/export"),
            ("sheet.copy", "home/clipboard"),
            ("shell.print@Sheets", "layout/page"),
        ]);

        var copy = Command("sheet.copy") with { Module = ModuleKind.Sheets };
        var registry = RegistryWith(layout, Command("shell.print"), copy);

        var tabs = registry.BuildRibbon(new CommandContext(ModuleKind.Sheets, SelectionKind.None, DocumentState.None));

        Assert.Equal(["home", "layout"], tabs.Select(tab => tab.Segment).ToArray());
    }

    [Fact]
    public void UnaRegolaPerModuloNonMetteIlComandoNegliAltriModuli()
    {
        var layout = new CommandLayout([("shell.print@Sheets", "layout/page")]);
        var registry = RegistryWith(layout, Command("shell.print"));

        Assert.Empty(registry.BuildRibbon(new CommandContext(ModuleKind.Presentations, SelectionKind.None, DocumentState.None)));
        Assert.Single(registry.BuildRibbon(new CommandContext(ModuleKind.Sheets, SelectionKind.None, DocumentState.None)));
    }

    [Fact]
    public void UnPrefissoCopertoDaUnPrefissoPrecedenteEMorto()
    {
        var layout = new CommandLayout([("shell.scan.", "insert/image"), ("shell.scan.", "insert/scanner"), ("shell.sc*", "insert/other"), ("shell.scanner.", "insert/image")]);

        Assert.Equal(["shell.scan.", "shell.scanner."], layout.UnusedRules(["shell.scan.request"]));
    }

    [Fact]
    public void UnaRegolaPerModuloUsataNonEMortaEUnaSenzaComandoSi()
    {
        var layout = new CommandLayout([("shell.print@Sheets", "layout/page"), ("shell.gone@Sheets", "layout/page")]);

        Assert.Equal(["shell.gone@Sheets"], layout.UnusedRules(["shell.print"]));
    }

    [Fact]
    public void UnComandoSenzaRegolaNonCompareNellaRibbon()
    {
        var layout = new CommandLayout([("b.one", "home/clipboard")]);
        var registry = RegistryWith(layout, Command("b.one"), Command("b.two"));

        var group = Assert.Single(Assert.Single(registry.BuildRibbon(Context())).Children);

        Assert.Single(group.Children);
    }

    [Fact]
    public void LOrdineDelleRegoleDecideLOrdineDelleSchede()
    {
        var layout = new CommandLayout(
            [("c.two", "insert/text"), ("c.one", "home/clipboard")]);

        var registry = RegistryWith(layout, Command("c.one"), Command("c.two"));

        Assert.Equal(
            ["insert", "home"],
            registry.BuildRibbon(Context()).Select(tab => tab.Segment));
    }

    [Fact]
    public void UnaRegolaCheNonPrendeNessunComandoSiSegnala()
    {
        var layout = new CommandLayout(
            [("d.one", "home/clipboard"), ("d.missing", "home/edit")]);

        var registry = RegistryWith(layout, Command("d.one"));

        Assert.Equal(["d.missing"], registry.DeadLayoutRules(CommandSurface.Ribbon));
    }

    [Fact]
    public void LeDueSuperficiHannoMappeIndipendenti()
    {
        var registry = new CommandRegistry(Texts);
        registry.Use(CommandSurface.Menu, new CommandLayout([("e.one", "file/save")]));
        registry.Use(CommandSurface.Ribbon, new CommandLayout([("e.one", "home/clipboard")]));
        registry.Register(Command("e.one"));

        Assert.Equal("home", Assert.Single(registry.BuildRibbon(Context())).Segment);
        Assert.Equal("file", Assert.Single(registry.BuildMenu(Context())).Segment);
    }
}
