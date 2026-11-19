using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeColourChanges
{
    string? FillOf(ShapeStyle style);

    string? OutlineOf(ShapeStyle style);

    ShapeStyle WithFill(ShapeStyle style, string? hex);

    ShapeStyle WithOutline(ShapeStyle style, string? hex);
}

public sealed class ShapeColourChanges : IShapeColourChanges
{
    public string? FillOf(ShapeStyle style) =>
        style.Fill is { Kind: ShapeFillKind.Solid } fill ? fill.ColorHex : null;

    public string? OutlineOf(ShapeStyle style) =>
        style.Line is { } line && line.Dash != ShapeStrokeStyle.None ? line.ColorHex : null;

    public ShapeStyle WithFill(ShapeStyle style, string? hex) => style with
    {
        Fill = hex is { Length: > 0 }
            ? style.Fill with { Kind = ShapeFillKind.Solid, ColorHex = hex, Opacity = 1 }
            : ShapeFill.None,
    };

    public ShapeStyle WithOutline(ShapeStyle style, string? hex) => style with
    {
        Line = hex is { Length: > 0 }
            ? style.Line with
            {
                ColorHex = hex,
                Dash = style.Line.Dash == ShapeStrokeStyle.None ? ShapeStrokeStyle.Solid : style.Line.Dash,
                Opacity = 1,
            }
            : ShapeLine.None,
    };
}

public sealed record ShapeColourTarget(
    Func<ShapeStyle?> Selected,
    Action<string, Func<ShapeStyle, ShapeStyle>> Restyle,
    Func<RibbonPalette> Palette);

public interface IShapeColourCommands
{
    CommandDefinition Fill(CommandDefinition command, ShapeColourTarget target);

    CommandDefinition Outline(CommandDefinition command, ShapeColourTarget target);
}

public sealed class ShapeColourCommands(IColorDialog colours, IRibbonMemory memory, IShapeColourChanges changes) : IShapeColourCommands
{
    public const string DefaultFill = "#4472C4";

    public const string DefaultOutline = "#1F3864";

    public CommandDefinition Fill(CommandDefinition command, ShapeColourTarget target) =>
        Build(command, target, DefaultFill, changes.FillOf, changes.WithFill);

    public CommandDefinition Outline(CommandDefinition command, ShapeColourTarget target) =>
        Build(command, target, DefaultOutline, changes.OutlineOf, changes.WithOutline);

    private CommandDefinition Build(
        CommandDefinition command, ShapeColourTarget target, string fallback,
        Func<ShapeStyle, string?> read, Func<ShapeStyle, string?, ShapeStyle> apply)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(target);
        var key = command.Id.Value;
        return command with
        {
            RibbonControl = new ColorSplitSpec(target.Palette, () => memory.Recall(key, fallback), "ribbon.colour.none")
            {
                MoreCommandId = key,
                Selected = () => target.Selected() is { } style ? read(style) : null,
            },
            Execute = async (ctx, ct) =>
            {
                var colour = ctx.Argument as RibbonColor
                    ?? await colours.ChooseAsync(memory.Recall(key, fallback), true, $"command.{key}.title", ct).ConfigureAwait(true);

                if (colour is null) return;

                memory.Remember(key, colour.Hex);
                target.Restyle(key, style => apply(style, colour.Hex));
            },
        };
    }
}
