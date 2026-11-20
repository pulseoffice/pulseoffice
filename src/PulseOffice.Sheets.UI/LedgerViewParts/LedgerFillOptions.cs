using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls.Lists;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerFillOptions
{
    private const double Size = 18;

    private readonly SheetGridView _grid;
    private readonly LedgerCellOperations _operations;
    private readonly ITextCatalog _texts;
    private readonly Popup _popup;
    private CellAddress? _corner;
    private readonly IReadOnlyList<(string Key, FillMode Mode)> _options =
    [
        ("ledger.fillOptions.copy", FillMode.Copy),
        ("ledger.fillOptions.series", FillMode.Series),
        ("ledger.fillOptions.formattingOnly", FillMode.FormattingOnly),
        ("ledger.fillOptions.withoutFormatting", FillMode.WithoutFormatting),
    ];

    public LedgerFillOptions(SheetGridView grid, LedgerCellOperations operations, ITextCatalog texts, PulseOffice.UI.Icons.IIconCatalog icons)
    {
        _grid = grid;
        _operations = operations;
        _texts = texts;

        Button = new Button
        {
            Width = Size,
            Height = Size,
            Padding = new Thickness(0),
            Content = icons.Find(IconKey) is { } icon ? new PulseOffice.UI.Controls.IconView(icon, Size - 4) : null,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(Button, texts["ledger.fillOptions.button"]);
        ToolTip.SetTip(Button, texts["ledger.fillOptions.button"]);
        Button.Click += (_, _) => Open();
        _popup = new Popup
        {
            PlacementTarget = grid,
            Placement = PlacementMode.AnchorAndGravity,
            PlacementAnchor = PopupAnchor.BottomRight,
            PlacementGravity = PopupGravity.BottomRight,
            IsLightDismissEnabled = false,
            Child = Button,
        };
        ((ISetLogicalParent)_popup).SetParent(grid);
        operations.Filled += (_, request) => ShowAt(request.Target.End);
        grid.ViewportMoved += (_, _) => Follow();
        grid.ZoomChanged += (_, _) => Follow();
    }

    public const string IconKey = "ledger.fill.series";

    public Rect? Placement => _corner is null ? null : _popup.PlacementRect;

    public Button Button { get; }

    public IChoiceListPopup List { get; set; } = new ChoiceListPopup();

    public IReadOnlyList<string> Labels => [.. _options.Select(option => _texts[option.Key])];

    public bool IsShown => _popup.IsOpen;

    public void Hide()
    {
        _corner = null;
        _popup.IsOpen = false;
        List.Close();
    }

    public bool Choose(int index)
    {
        if (index < 0 || index >= _options.Count) return false;
        Hide();
        return _operations.RefillAs(_options[index].Mode);
    }

    private void ShowAt(CellAddress corner)
    {
        _corner = corner;
        _popup.PlacementRect = _grid.CellBounds(corner);
        if (TopLevel.GetTopLevel(_grid) is not null) _popup.IsOpen = true;
    }

    private void Follow()
    {
        if (_corner is not { } corner) return;
        var bounds = _grid.CellBounds(corner);
        _popup.PlacementRect = bounds;
        var visible = new Rect(_grid.Bounds.Size).Intersects(bounds);
        if (!visible) List.Close();
        _popup.IsOpen = visible && TopLevel.GetTopLevel(_grid) is not null;
    }

    private void Open()
    {
        var labels = Labels;
        List.Show(Button, new Rect(Button.Bounds.Size), labels, null, chosen =>
        {
            var index = labels.ToList().IndexOf(chosen);
            Choose(index);
            _grid.Focus();
        });
    }
}
