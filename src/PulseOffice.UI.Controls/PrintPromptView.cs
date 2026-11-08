using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Printing;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed record PrintTarget(string Id, string Name, bool IsDefault);

public sealed record PrintChoice(string? PrinterId, bool ToFile, PrintJobOptions Options);

public sealed record PrintPromptRequest(
    IReadOnlyList<PrintTarget> Targets,
    PrintCapabilities Capabilities,
    Func<bool, IPrintableDocument> Source,
    IPrintImposition Imposition,
    IPageRangeParser Ranges)
{
    public int InitialCopies { get; init; } = 1;

    public bool InitialDuplex { get; init; }

    public bool SupportsComments { get; init; }

    public bool InitialComments { get; init; }
}

public sealed class PrintPromptView : UserControl, IDialogContent, IDialogFooterOwner
{
    private static readonly int[] PerSheet = [1, 2, 4, 6, 9, 16];

    private readonly PrintPromptRequest _request;
    private readonly ITextCatalog _texts;
    private readonly ComboBox _printer = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown _copies = new() { Minimum = 1, Maximum = 999, Increment = 1, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _collate = new() { IsChecked = true };
    private readonly TextBox _range = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _rangeError = new() { IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ComboBox _subset = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _reverse = new();
    private readonly ComboBox _perSheet = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _order = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _borders = new();
    private readonly CheckBox _brochure = new();
    private readonly ComboBox _duplex = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _comments = new();
    private readonly CheckBox _graphics = new() { IsChecked = true };
    private readonly CheckBox _black = new();
    private readonly PrintPagesView _sheet;
    private readonly TextBlock _sheetLabel = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _confirm;
    private IPrintableDocument? _imposed;
    private bool? _commentsBuilt;
    private IPrintableDocument? _source;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public PrintPromptView(PrintPromptRequest request, ITextCatalog texts, IPageCanvasFactory canvases, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _sheet = new PrintPagesView(new PreviewSpreads(), canvases) { Zoom = PreviewZoom.Fit, MinHeight = 320, MinWidth = 260 };
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        var metrics = DesignTokens.For(density);
        var parts = new DialogParts(metrics, null);

        _printer.ItemsSource = request.Targets.Select(target => target.Name).Append(texts["print.toPdf"]).ToList();
        _printer.SelectedIndex = request.Targets.Count == 0 ? 0 : Math.Max(request.Targets.ToList().FindIndex(target => target.IsDefault), 0);
        _copies.Value = Math.Clamp(request.InitialCopies, 1, 999);
        _range.PlaceholderText = texts["print.range.all"];
        _rangeError.Text = texts["print.range.invalid"];
        _rangeError.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        _subset.ItemsSource = new[] { texts["print.subset.all"], texts["print.subset.odd"], texts["print.subset.even"] };
        _subset.SelectedIndex = 0;
        _perSheet.ItemsSource = PerSheet.Select(count => count.ToString(System.Globalization.CultureInfo.CurrentCulture)).ToList();
        _perSheet.SelectedIndex = 0;
        _order.ItemsSource = new[] { texts["print.order.across"], texts["print.order.down"] };
        _order.SelectedIndex = 0;
        _duplex.ItemsSource = new[] { texts["print.duplex.none"], texts["print.duplex.long"], texts["print.duplex.short"] };
        _duplex.SelectedIndex = request.InitialDuplex && request.Capabilities.Duplex ? 1 : 0;
        _comments.IsChecked = request.InitialComments;

        var form = new StackPanel { Spacing = metrics.SpacingMedium, MinWidth = 300 };
        form.Children.Add(Section(texts["print.section.printer"], metrics,
            parts.Field(texts["print.printer"], _printer),
            Row(metrics, parts.Field(texts["print.copies"], _copies), Check(_collate, texts["print.collate"]))));
        form.Children.Add(Section(texts["print.section.pages"], metrics,
            parts.Field(texts["print.range"], _range), _rangeError,
            parts.Field(texts["print.subset"], _subset),
            Check(_reverse, texts["print.reverse"])));
        form.Children.Add(Section(texts["print.section.layout"], metrics,
            Row(metrics, parts.Field(texts["print.perSheet"], _perSheet), parts.Field(texts["print.order"], _order)),
            Check(_borders, texts["print.borders"]),
            Check(_brochure, texts["print.brochure"]),
            request.Capabilities.Duplex ? parts.Field(texts["print.duplex"], _duplex) : new Border()));
        var content = Section(texts["print.section.content"], metrics,
            Check(_graphics, texts["print.graphics"]),
            Check(_black, texts["print.blackText"]));
        if (request.SupportsComments) ((StackPanel)((Border)content).Child!).Children.Add(Check(_comments, texts["print.comments"]));
        form.Children.Add(content);

        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, HorizontalAlignment = HorizontalAlignment.Center };
        navigation.Children.Add(Small("‹", texts["print.preview.previous"], () => Sheet(-1)));
        navigation.Children.Add(_sheetLabel);
        navigation.Children.Add(Small("›", texts["print.preview.next"], () => Sheet(1)));
        var preview = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(navigation, Dock.Bottom);
        preview.Children.Add(navigation);
        preview.Children.Add(new Border { Child = _sheet, CornerRadius = new CornerRadius(metrics.RadiusSoft), Margin = new Thickness(0, 0, 0, metrics.SpacingSmall) }
            .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        var scroller = new ScrollViewer { Content = form, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, MaxHeight = 560 };
        Grid.SetColumn(scroller, 0);
        Grid.SetColumn(preview, 2);
        grid.Children.Add(scroller);
        grid.Children.Add(preview);
        Content = grid;

        var footer = new DialogFooter(density)
            .WithCancel(texts["print.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["print.confirm"], Confirm, out _confirm, primary: true);
        if (!request.Capabilities.Verified) footer = footer.WithNote(texts["print.unverified"]);
        Footer = footer;

        foreach (var box in new[] { _collate, _reverse, _borders, _brochure, _comments, _graphics, _black })
            box.IsCheckedChanged += (_, _) => Update();
        foreach (var combo in new[] { _subset, _perSheet, _order, _duplex, _printer })
            combo.SelectionChanged += (_, _) => Update();
        _copies.ValueChanged += (_, _) => Update();
        _range.TextChanged += (_, _) => Update();
        Update();
    }

    private PrintJobOptions Options() => new()
    {
        PageRange = string.IsNullOrWhiteSpace(_range.Text) ? null : _range.Text.Trim(),
        Subset = (PageSubset)Math.Max(0, _subset.SelectedIndex),
        Reverse = _reverse.IsChecked == true,
        Copies = (int)(_copies.Value ?? 1),
        Collate = _collate.IsChecked == true,
        PagesPerSheet = PerSheet[Math.Max(0, _perSheet.SelectedIndex)],
        Order = (SheetOrder)Math.Max(0, _order.SelectedIndex),
        SheetBorders = _borders.IsChecked == true,
        Brochure = _brochure.IsChecked == true,
        Duplex = _request.Capabilities.Duplex ? (DuplexMode)Math.Max(0, _duplex.SelectedIndex) : DuplexMode.None,
        Graphics = _graphics.IsChecked == true,
        BlackText = _black.IsChecked == true,
        Comments = _request.SupportsComments && _comments.IsChecked == true,
    };

    private void Update()
    {
        var options = Options();
        _perSheet.IsEnabled = !options.Brochure;
        _order.IsEnabled = !options.Brochure && options.PagesPerSheet > 1;
        _collate.IsEnabled = options.Copies > 1;
        if (_source is null || _commentsBuilt != options.Comments)
        {
            _source = _request.Source(options.Comments);
            _commentsBuilt = options.Comments;
        }
        var valid = _request.Ranges.Parse(options.PageRange, _source.PageCount) is { Count: > 0 };
        _rangeError.IsVisible = !valid;
        _confirm.IsEnabled = valid;
        if (!valid) return;
        _imposed = _request.Imposition.Impose(_source, options with { Copies = 1 }, nativeCopies: true);
        _sheet.Document = _imposed;
        Sheet(0);
    }

    private void Sheet(int direction)
    {
        if (_imposed is null || _imposed.PageCount == 0) return;
        _sheet.First = Math.Clamp(_sheet.First + direction, 0, _imposed.PageCount - 1);
        _sheetLabel.Text = _texts.Format("print.sheet", _sheet.First + 1, _imposed.PageCount);
    }

    private void Confirm()
    {
        var file = _printer.SelectedIndex >= _request.Targets.Count;
        Completed?.Invoke(this, new PrintChoice(file ? null : _request.Targets[_printer.SelectedIndex].Id, file, Options()));
    }

    private Control Section(string title, DesignTokens.Metrics metrics, params Control[] children)
    {
        var stack = new StackPanel { Spacing = metrics.SpacingSmall };
        stack.Children.Add(new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = metrics.FontSize });
        foreach (var child in children) stack.Children.Add(child);
        return new Border { Child = stack };
    }

    private Control Row(DesignTokens.Metrics metrics, Control left, Control right)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 2);
        right.VerticalAlignment = VerticalAlignment.Bottom;
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private Control Check(CheckBox box, string label)
    {
        box.Content = label;
        return box;
    }

    private Button Small(string caption, string name, Action action)
    {
        var button = new Button { Content = caption, Padding = new Thickness(10, 2) };
        Avalonia.Automation.AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        button.Click += (_, _) => action();
        return button;
    }
}
