using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ShapeColourCommandsTests
{
    private sealed class NoDialog : IColorDialog
    {
        public Task<RibbonColor?> ChooseAsync(string? initial, bool allowNone, string titleKey, CancellationToken ct) =>
            Task.FromResult<RibbonColor?>(null);
    }

    private sealed class Memory : IRibbonMemory
    {
        public Dictionary<string, string?> Values { get; } = [];

        public string? Recall(string key, string? fallback) => Values.TryGetValue(key, out var value) ? value : fallback;

        public void Remember(string key, string? value) => Values[key] = value;
    }

    private static readonly ShapeColourChanges Changes = new();

    [Fact]
    public void Fill_and_outline_become_solid_or_none()
    {
        var style = new ShapeStyle { Line = ShapeLine.None, Fill = ShapeFill.Default with { Kind = ShapeFillKind.Gradient } };

        Assert.Null(Changes.FillOf(style));
        Assert.Null(Changes.OutlineOf(style));

        var coloured = Changes.WithOutline(Changes.WithFill(style, "#FF0000"), "#00FF00");
        Assert.Equal("#FF0000", Changes.FillOf(coloured));
        Assert.Equal("#00FF00", Changes.OutlineOf(coloured));
        Assert.Equal(ShapeStrokeStyle.Solid, coloured.Line.Dash);

        var cleared = Changes.WithOutline(Changes.WithFill(coloured, null), string.Empty);
        Assert.False(cleared.Fill.IsVisible);
        Assert.False(cleared.Line.IsVisible);
    }

    [Fact]
    public async Task A_palette_colour_restyles_the_target_and_is_remembered()
    {
        var memory = new Memory();
        var commands = new ShapeColourCommands(new NoDialog(), memory, Changes);
        var style = new ShapeStyle();
        string? key = null;
        var target = new ShapeColourTarget(() => style, (name, change) => { key = name; style = change(style); }, () => new RibbonPalette([], []));
        var fill = commands.Fill(new CommandDefinition { Id = new CommandId("test.object.fill"), Module = ModuleKind.Sheets, Execute = (_, _) => Task.CompletedTask }, target);

        await fill.Execute(new CommandContext(ModuleKind.Sheets, SelectionKind.None, DocumentState.DocumentOpen) { Argument = new RibbonColor("#123456") }, CancellationToken.None);

        Assert.Equal("test.object.fill", key);
        Assert.Equal("#123456", style.Fill.ColorHex);
        Assert.Equal("#123456", memory.Values["test.object.fill"]);
        Assert.Equal("#123456", ((ColorSplitSpec)fill.RibbonControl!).Selected!());
    }
}
