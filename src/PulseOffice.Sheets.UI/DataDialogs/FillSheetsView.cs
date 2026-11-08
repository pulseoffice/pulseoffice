using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Lists;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record FillSheetsChoice(string Area, IReadOnlyList<string> Sheets, FillAcrossContent Content, bool SkipEmpty);

public sealed class FillSheetsView : LedgerDialogView
{
    private readonly string _source;
    private readonly WrapPanel _targets = new() { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4 };
    private readonly TextBlock _area;
    private readonly IReadOnlyList<(CheckBox Box, FillAcrossContent Part)> _parts;

    public FillSheetsView(
        LedgerDialogKit kit, string source, IReadOnlyList<string> others, IReadOnlyCollection<string> chosen, string area, IReferenceSource ranges)
        : base(kit, "sheet.fill.explanation")
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Area = new RangeFieldControl(new RangeField("area", kit["sheet.fill.range"], area, ranges), kit.Texts, kit.Metrics);
        Sheets = new ChecklistControl(new ChecklistModel(others, chosen), kit.Texts, kit.Metrics) { MaxHeight = 180 };
        What = kit.Choices("sheet.fill.what",
        [
            ("all", "paste.part.all", "sheet.fill.all"),
            ("choose", "paste.part.values", "sheet.fill.choose"),
        ], 0, showLabels: true);
        Text = kit.Check("sheet.fill.text", true);
        Numbers = kit.Check("sheet.fill.numbers", true);
        Formulas = kit.Check("sheet.fill.formulas", true);
        Formats = kit.Check("sheet.fill.formats", true);
        SkipEmpty = kit.Check("sheet.fill.skipEmpty");
        _parts = [(Text, FillAcrossContent.Text), (Numbers, FillAcrossContent.Numbers), (Formulas, FillAcrossContent.Formulas), (Formats, FillAcrossContent.Formats)];
        Parts = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = kit.Metrics.SpacingMedium, Children = { Text, Numbers, Formulas, Formats } };
        _area = kit.Note();

        Layout.Preview = PreviewPane("sheet.fill.preview", 220, Chip(source, true), kit.Note(kit["sheet.fill.into"]), _targets, _area);
        var where = Layout.AddSection(kit["sheet.fill.section.where"]);
        where.Add(kit["sheet.fill.range"], Area);
        where.Add(kit["sheet.fill.sheets"], Sheets, alignTop: true);
        var what = Layout.AddSection(kit["sheet.fill.section.what"]);
        what.Add(null, What, DialogRowKind.Wide);
        PartsRow = what.Add(null, Parts, DialogRowKind.Indented);
        what.Add(null, SkipEmpty, DialogRowKind.Indented);
        Finish("sheet.fill.confirm", what);

        Area.TextChanged += (_, _) => Refresh();
        What.SelectionChanged += (_, _) => Refresh();
        foreach (var (box, _) in _parts) box.IsCheckedChanged += (_, _) => Refresh();
        Sheets.Changed += (_, _) => Refresh();
        Refresh();
    }

    public RangeFieldControl Area { get; }

    public ChecklistControl Sheets { get; }

    public IconChoiceGroup What { get; }

    public CheckBox Text { get; }

    public CheckBox Numbers { get; }

    public CheckBox Formulas { get; }

    public CheckBox Formats { get; }

    public CheckBox SkipEmpty { get; }

    public WrapPanel Parts { get; }

    public DialogRow PartsRow { get; }

    public IReadOnlyList<string> Targets => [.. _targets.Children.OfType<Border>().Select(chip => (chip.Child as TextBlock)?.Text ?? string.Empty)];

    public FillSheetsChoice Choice => new(Area.Text, Sheets.Model.Checked, Chosen, SkipEmpty.IsChecked == true);

    private FillAcrossContent Chosen => What.SelectedKey == "all"
        ? FillAcrossContent.All
        : _parts.Where(part => part.Box.IsChecked == true).Aggregate(FillAcrossContent.None, (sum, part) => sum | part.Part);

    protected override object? Result() => Choice;

    private void Refresh()
    {
        PartsRow.IsVisible = What.SelectedKey != "all";
        _targets.Children.Clear();
        foreach (var name in Sheets.Model.Checked) _targets.Children.Add(Chip(name, false));
        _area.Text = Area.IsValid ? Kit.Texts.Format("sheet.fill.areaNote", Area.Text) : string.Empty;
        Report(!Area.IsValid ? Kit["status.analysis.badReference"]
            : Sheets.Model.CheckedCount == 0 ? Kit["sheet.fill.noSheets"]
            : Chosen == FillAcrossContent.None ? Kit["sheet.fill.nothing"]
            : null);
    }

    private Border Chip(string name, bool source)
    {
        var chip = new Border
        {
            Padding = new Thickness(8, 2),
            CornerRadius = new CornerRadius(Kit.Metrics.Radius, Kit.Metrics.Radius, 0, 0),
            BorderThickness = new Thickness(1, 1, 1, source ? 3 : 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = name, FontSize = Kit.Metrics.FontSizeSmall, FontWeight = source ? FontWeight.SemiBold : FontWeight.Normal },
        }.Themed(Border.BorderBrushProperty, source ? "PulseAccent" : Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface);
        return chip;
    }
}
