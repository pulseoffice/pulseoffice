using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class SymbolPickerView : UserControl, IDialogContent, IDialogFooterOwner
{
    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    private static readonly (string Key, (int Start, int End)[] Ranges)[] Categories =
    [
        ("symbol.group.punctuation", [(0x2010, 0x2027), (0x2030, 0x205E), (0x00A1, 0x00BF)]),
        ("symbol.group.currency", [(0x0024, 0x0024), (0x00A2, 0x00A5), (0x20A0, 0x20BF)]),
        ("symbol.group.math", [(0x2200, 0x22FF), (0x2A00, 0x2AFF), (0x27C0, 0x27EF)]),
        ("symbol.group.arrows", [(0x2190, 0x21FF), (0x2794, 0x27BE)]),
        ("symbol.group.shapes", [(0x25A0, 0x25FF), (0x2B00, 0x2BFF)]),
        ("symbol.group.symbols", [(0x2600, 0x26FF), (0x2700, 0x27BF), (0x2300, 0x23FF)]),
        ("symbol.group.greek", [(0x0370, 0x03FF)]),
        ("symbol.group.latin", [(0x00C0, 0x024F), (0x1E00, 0x1EFF)]),
        ("symbol.group.numbers", [(0x00BC, 0x00BE), (0x2070, 0x209F), (0x2150, 0x218F)]),
        ("symbol.group.emoji", [(0x1F300, 0x1F5FF), (0x1F600, 0x1F64F), (0x1F680, 0x1F6FF), (0x1F900, 0x1F9FF)]),
    ];

    private readonly WrapPanel _grid;
    private readonly DesignTokens.Metrics _metrics;

    public SymbolPickerView(ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(texts);

        _metrics = DesignTokens.For(density);

        _grid = new WrapPanel { Margin = new Thickness(0, _metrics.SpacingMedium, 0, 0) };

        var strip = new PageTabStrip(_metrics, [.. Categories.Select(category => texts[category.Key])]);
        strip.SelectionChanged += (_, index) => ShowCategory(Categories[index].Ranges);
        var tabs = new ScrollViewer
        {
            Content = strip,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        ShowCategory(Categories[0].Ranges);

        var scroll = new ScrollViewer
        {
            Content = _grid,
            MaxHeight = 300,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                new TextBlock
                {
                    Text = texts["symbol.explanation"],
                    FontSize = _metrics.FontSizeSmall,
                    TextWrapping = TextWrapping.Wrap,
                }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                tabs,
                scroll,
            },
        };

        Footer = new DialogFooter(density).WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null));
    }

    private void ShowCategory((int Start, int End)[] ranges)
    {
        _grid.Children.Clear();

        var count = 0;
        foreach (var (start, end) in ranges)
        {
            for (var code = start; code <= end && count < 700; code++)
            {
                if (!IsPrintable(code)) continue;
                _grid.Children.Add(Cell(char.ConvertFromUtf32(code)));
                count++;
            }
        }
    }

    private static bool IsPrintable(int code)
    {
        if (code is 0x00AD or 0x2028 or 0x2029) return false;

        var category = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(code), 0);
        return category is not (UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator or UnicodeCategory.SpaceSeparator);
    }

    private Button Cell(string glyph)
    {
        var cell = new Button
        {
            Content = new TextBlock
            {
                Text = glyph,
                FontSize = _metrics.FontSize * 1.35,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 40,
            Height = 40,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        cell.Click += (_, _) => Completed?.Invoke(this, glyph);
        return cell;
    }
}
