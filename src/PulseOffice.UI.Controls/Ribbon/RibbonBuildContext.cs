using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public enum RibbonForm { Full, Medium, Compact, Popover }

public sealed record RibbonInvocation(string CommandId, object? Argument = null);

public interface IRibbonThumbnailPainter
{
    bool Accepts(RibbonThumbnail thumbnail);

    Control Paint(RibbonThumbnail thumbnail, double width, double height);
}

public sealed record RibbonServices(
    IIconCatalog Icons, ITextCatalog Texts, Fonts.IFontPickerFactory? Fonts, IRibbonStripLayout Strips, IRibbonReductionPlanner Planner,
    IRibbonSlotCatalog Slots, IReadOnlyList<IRibbonThumbnailPainter>? Thumbnails = null, Fonts.IDisplayFonts? Display = null,
    IFlyoutDismissal? Dismissal = null);

public sealed class RibbonBuildContext(RibbonServices services, DesignTokens.Metrics metrics, Action<RibbonInvocation> invoke)
{
    private const double ShadowRoom = 18;

    public DesignTokens.Metrics Metrics => metrics;

    public IFlyoutDismissal Dismissal { get; } = services.Dismissal ?? new FlyoutDismissal();

    public IIconCatalog Icons => services.Icons;

    public ITextCatalog Texts => services.Texts;

    public Fonts.IFontPickerFactory? Fonts => services.Fonts;

    public FontFamily FontFamily(string family) => services.Display?.Resolve(family) ?? new FontFamily(family);

    public IRibbonStripLayout Strips => services.Strips;

    public IRibbonReductionPlanner Planner => services.Planner;

    public IRibbonSlotCatalog Slots => services.Slots;

    public double ItemHeight => metrics.ControlHeight;

    public double GroupLabelHeight => metrics.FontSize + metrics.SpacingSmall;

    public void Invoke(CommandDefinition command, object? argument = null) =>
        invoke(new RibbonInvocation(command.Id.Value, argument));

    public void Invoke(string commandId) => invoke(new RibbonInvocation(commandId));

    public IconView? Icon(string? key, double? size = null) =>
        key is not null && services.Icons.Find(key) is { } geometry
            ? new IconView(geometry, size ?? metrics.IconSize)
            : null;

    public Control? Thumbnail(RibbonThumbnail? thumbnail, double width, double height) =>
        thumbnail is not null && services.Thumbnails?.FirstOrDefault(painter => painter.Accepts(thumbnail)) is { } painter
            ? painter.Paint(thumbnail, width, height)
            : null;

    public IconView? Chevron(double scale = 0.7) => Icon("ui.chevronDown", metrics.IconSize * scale);

    public string Tooltip(RibbonItem item)
    {
        var text = item.Tooltip is { Length: > 0 } tip && tip != item.Title ? $"{item.Title}\n{tip}" : item.Title;

        return item.Gesture is { Length: > 0 } gesture && Shortcuts.ShortcutGesture.Parse(gesture)?.Display() is { Length: > 0 } shown
            ? $"{text}  ({shown})"
            : text;
    }

    public Button Flat(object? content, double? width = null, double? height = null)
    {
        var button = new Button
        {
            Content = content,
            Height = height ?? ItemHeight,
            Padding = new Thickness(metrics.SpacingSmall / 2, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = default,
        };

        if (width is { } w) button.Width = w;

        button.Classes.Add("ribbon");
        return button;
    }

    public Flyout Popover(Func<Control> content)
    {
        var flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            FlyoutPresenterClasses = { "plain" },
        };
        Dismissal.PassThrough(flyout);

        flyout.Opening += (_, _) => flyout.Content = Surface(content());

        return flyout;
    }

    public Border Surface(Control content)
    {
        var surface = new Border
        {
            Margin = new Thickness(0, 0, ShadowRoom, ShadowRoom),
            Padding = new Thickness(metrics.SpacingMedium, metrics.SpacingSmall + 2),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            BoxShadow = BoxShadows.Parse("0 10 28 -6 #40101828"),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 520,
                Content = content,
            },
        };

        surface.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceOverlay);
        surface.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        return surface;
    }

    public TextBlock Caption(string text) => new TextBlock
    {
        Text = text.ToUpperInvariant(),
        FontSize = Math.Max(9, metrics.FontSizeSmall - 1),
        LetterSpacing = 0.6,
        Margin = new Thickness(0, metrics.SpacingSmall / 2, 0, 2),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    public ColorPaletteLabels PaletteLabels(string noneKey, bool more) => new(
        Texts[noneKey], Texts["ribbon.colour.theme"], Texts["ribbon.colour.standard"],
        more ? Texts["ribbon.colour.more"] : null);
}
