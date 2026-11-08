using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.UI.Commands.Tests;

public sealed class RibbonControlsTests
{
    private readonly RibbonReductionPlanner _planner = new();

    [Fact]
    public void LaRiduzioneProcedePerGradiDaDestra()
    {
        var groups = new List<RibbonGroupState> { RibbonGroupState.Initial(6, 2), RibbonGroupState.Initial(4, 2) };
        var levels = new List<string>();

        while (_planner.Next(groups) is { } step)
        {
            groups[step.Index] = step.State;
            levels.Add($"{step.Index}:{step.State.Level}:{step.State.Visible}");
        }

        Assert.Equal("1:Medium:4", levels[0]);
        Assert.Equal("0:Medium:6", levels[1]);
        Assert.Equal("1:Compact:4", levels[2]);
        Assert.Equal("0:Compact:6", levels[3]);
        Assert.Equal("0:Partial:5", levels[4]);
        Assert.Contains("1:Collapsed:2", levels);
        Assert.All(groups, group => Assert.Equal(RibbonGroupLevel.Collapsed, group.Level));
    }

    [Fact]
    public void IlTaglioPrendeIlGruppoPiuPienoENonScendeSottoIlMinimo()
    {
        RibbonGroupState[] groups =
        [
            new(RibbonGroupLevel.Compact, 3, 3, 2),
            new(RibbonGroupLevel.Compact, 5, 5, 2),
        ];

        var step = _planner.Next(groups)!;
        Assert.Equal(1, step.Index);
        Assert.Equal(4, step.State.Visible);

        groups = [new(RibbonGroupLevel.Partial, 2, 5, 2), new(RibbonGroupLevel.Partial, 2, 3, 2)];
        Assert.Equal(RibbonGroupLevel.Collapsed, _planner.Next(groups)!.State.Level);
    }

    [Fact]
    public void UnaStrisciaSiDivideInRigheEquilibrate()
    {
        var layout = new RibbonStripLayout();

        Assert.Equal(2, layout.Lines([150, 70, 28, 28], 2).Sum() / 2);
        Assert.Equal(new[] { 1, 3 }, layout.Lines([160, 28, 28, 28], 2));
        Assert.Equal(new[] { 1, 1, 1 }, layout.Lines([10, 10, 10], 5));
        Assert.Equal(new[] { 4 }, layout.Lines([10, 10, 10, 10], 1));
        Assert.Empty(layout.Lines([], 2));
    }

    [Fact]
    public void LaTavolozzaHaSeiTintePerColore()
    {
        var builder = new RibbonPaletteBuilder();
        var palette = builder.Build(["#336699", "#FFFFFF", "nonsense"]);

        Assert.Equal(2, palette.ThemeColumns.Count);
        Assert.All(palette.ThemeColumns, column => Assert.Equal(6, column.Count));
        Assert.Equal("#336699", palette.ThemeColumns[0][0]);
        Assert.Equal("#FFFFFF", builder.Shade("#000000", 1));
        Assert.Equal("#000000", builder.Shade("#808080", -1));
        Assert.Equal(10, palette.Standard.Count);
    }

    [Fact]
    public void LaMappaDegliAspettiRiconoscePrefissiEAsterischi()
    {
        var map = new RibbonDisplayMap([("a.b", RibbonDisplay.Large), ("a.", RibbonDisplay.Icon), ("x*", RibbonDisplay.Tile)]);

        Assert.Equal(RibbonDisplay.Large, map.DisplayOf("a.b"));
        Assert.Equal(RibbonDisplay.Icon, map.DisplayOf("a.c"));
        Assert.Equal(RibbonDisplay.Tile, map.DisplayOf("xyz"));
        Assert.Equal(RibbonDisplay.Auto, map.DisplayOf("z"));
    }

    [Fact]
    public void LoStatoAccesoSeguiLaCondizioneSoloSeIlComandoEAbilitato()
    {
        var texts = new FakeTextCatalog();
        texts.Add("command.t.bold.title", "Bold");
        var on = true;

        var registry = new CommandRegistry(texts);
        registry.Use(CommandSurface.Ribbon, new CommandLayout([("t.bold", "home/font")]));
        registry.RegisterRange([new CommandDefinition
        {
            Id = new CommandId("t.bold"),
            CheckedWhen = _ => on,
            Execute = (_, _) => Task.CompletedTask,
        }]);

        var projection = new RibbonProjection(registry, new RibbonDisplayMap([("t.bold", RibbonDisplay.Icon)]));
        var context = new CommandContext(ModuleKind.Shell, SelectionKind.None, DocumentState.None);

        var item = projection.Build(context).Tabs.Single().Groups.Single().Items.Single();
        Assert.True(item.IsChecked);
        Assert.Equal(RibbonDisplay.Icon, item.Display);
        Assert.True(item.IsStrip);

        on = false;
        Assert.False(projection.Build(context).Tabs.Single().Groups.Single().Items.Single().IsChecked);
    }

    [Fact]
    public void LArgomentoViaggiaNelContestoSenzaCambiarneIlResto()
    {
        var context = new CommandContext(ModuleKind.Sheets, SelectionKind.Cell, DocumentState.DocumentOpen);
        var withArgument = context with { Argument = new RibbonColor("#FF0000") };

        Assert.Equal(context.Module, withArgument.Module);
        Assert.Equal("#FF0000", ((RibbonColor)withArgument.Argument!).Hex);
        Assert.NotEqual(context, withArgument);
    }
}
