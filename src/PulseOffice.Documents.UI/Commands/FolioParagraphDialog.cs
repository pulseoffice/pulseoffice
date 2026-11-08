using PulseOffice.Document.Borders;
using PulseOffice.Documents.Editing.Borders;
using PulseOffice.Documents.Styles;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Borders;
using PulseOffice.UI.Controls.Text;
using PulseOffice.UI.Controls.TextFormat;

namespace PulseOffice.Documents.UI.Commands;

public sealed record ParagraphDialogScope(TextParagraph Current, IReadOnlyList<string> PageStyles)
{
    public double? DefaultTabInterval { get; init; }
}

public sealed record ParagraphPageClear(bool Style, bool Restart);

public sealed record ParagraphDialogAnswer(ParagraphProperties Properties, double? DefaultTabInterval)
{
    public ParagraphPageClear? PageClear { get; init; }

    public bool IsEmpty => Properties == ParagraphProperties.None && PageClear is null;
}

public interface IFolioParagraphTabs
{
    IReadOnlyList<FormTab> Tabs(ParagraphDialogScope scope);

    ParagraphProperties Read(ParagraphDialogScope scope, IReadOnlyDictionary<string, object> values);

    double? DefaultTabInterval(ParagraphDialogScope scope, IReadOnlyDictionary<string, object> values);

    ParagraphPageClear? PageClear(ParagraphDialogScope scope, IReadOnlyDictionary<string, object> values);

    Avalonia.Controls.Control Preview(IFormValuesFeed feed, ParagraphDialogScope scope);
}

public interface IFolioParagraphDialog
{
    Task<ParagraphDialogAnswer?> AskAsync(ParagraphDialogScope scope, string titleKey, CancellationToken ct, int initialTab = 0);
}

public sealed class FolioParagraphTabs(IParagraphFormatTabs shared, ITextCatalog texts, IBorderEditors borderEditors, IBoxBorderFrames frames) : IFolioParagraphTabs
{
    public const int FlowTab = 1;

    public const int TabsTab = 2;

    public const int BordersTab = 3;

    public const int DropCapTab = 4;

    private readonly BorderFrameScope _scope = new(false, false, false);

    public FolioParagraphTabs(IParagraphFormatTabs shared, ITextCatalog texts) : this(shared, texts, new BorderEditors(texts), new BoxBorderFrames())
    {
    }

    private const uint Transparent = 0x00FFFFFF;

    private readonly ColorFieldValue _colors = new();

    private readonly BorderSide[] _sides = [BorderSide.Top, BorderSide.Bottom, BorderSide.Left, BorderSide.Right];

    public IReadOnlyList<FormTab> Tabs(ParagraphDialogScope scope)
    {
        var current = scope.Current;
        var borders = current.Borders ?? new BoxBorders();
        var drop = current.DropCap;
        return
        [
            shared.Tab(Request(current)),
            new FormTab(texts["folio.paragraph.tab.flow"], texts["folio.paragraph.flow.explanation"],
            [
                new CheckField("hyphenate", texts["folio.paragraph.hyphenate"], current.Hyphenation?.Enabled == true),
                new CheckField("break", texts["command.folio.paragraph.pageBreakBefore.title"], current.PageBreakBefore == true),
                new CheckField("columnBreak", texts["folio.paragraph.columnBreakBefore"], current.ColumnBreakBefore == true),
                new ChoiceField("pageStyle", texts["folio.paragraph.pageStyle"], [texts["folio.paragraph.pageStyle.same"], .. scope.PageStyles],
                    current.PageStyleName is { } page && scope.PageStyles.ToList().IndexOf(page) is var found and >= 0 ? found + 1 : 0),
                new NumberField("pageNumber", texts["folio.paragraph.pageNumber"], current.PageNumberRestart ?? 0, 0, 9999) { Integer = true },
                new CheckField("together", texts["folio.paragraph.keepTogether"], current.KeepTogether == true),
                new CheckField("keep", texts["command.folio.paragraph.keepWithNext.title"], current.KeepWithNext == true),
                new NumberField("orphans", texts["folio.paragraph.orphans"], current.Orphans ?? 2, 0, 9) { Integer = true },
                new NumberField("widows", texts["folio.paragraph.widows"], current.Widows ?? 2, 0, 9) { Integer = true },
            ]),
            new FormTab(texts["folio.paragraph.tab.tabs"], texts["folio.paragraph.tabs.explanation"],
            [
                new TabStopsField("tabs", texts["folio.paragraph.tab.tabs"], current.TabStops ?? []),
                .. DefaultTabField(scope),
            ]),
            new FormTab(texts["folio.paragraph.tab.borders"], texts["folio.paragraph.borders.explanation"],
            [
                new BorderEditorField("borders", texts["folio.paragraph.tab.borders"], borderEditors, _scope, frames.Frame(borders)),
                new NumberField("border.padding", texts["folio.border.padding"], _sides.Select(borders.Padding).Max(), 0, 200) { Unit = "pt" },
                new ColorField("background", texts["folio.paragraph.background"], current.Background is { } fill && fill >> 24 != 0 ? _colors.Hex(fill) : null),
            ]),
            new FormTab(texts["folio.paragraph.tab.dropCap"], texts["folio.paragraph.dropCap.explanation"],
            [
                new CheckField("drop", texts["folio.paragraph.dropCap.show"], drop?.IsActive == true),
                new CheckField("drop.word", texts["folio.paragraph.dropCap.word"], drop?.WholeWord == true),
                new NumberField("drop.characters", texts["folio.paragraph.dropCap.characters"], Math.Max(1, drop?.Characters ?? 1), 1, 9) { Integer = true },
                new NumberField("drop.lines", texts["folio.paragraph.dropCap.lines"], Math.Max(2, drop?.Lines ?? 3), 2, 9) { Integer = true },
                new NumberField("drop.distance", texts["folio.paragraph.dropCap.distance"], drop?.Distance ?? 0, 0, 100) { Unit = "pt" },
            ]),
        ];
    }

    public ParagraphProperties Read(ParagraphDialogScope scope, IReadOnlyDictionary<string, object> values)
    {
        var current = scope.Current;
        var changes = new ParagraphProperties();
        var before = State(current);
        var after = shared.Read(Request(current), values);
        if (Changed(after.Left, before.Left, out var left)) changes = changes with { MarginLeft = left };
        if (Changed(after.Right, before.Right, out var right)) changes = changes with { MarginRight = right };
        if (Changed(after.FirstLine, before.FirstLine, out var indent)) changes = changes with { Indent = indent };
        if (Changed(after.Before, before.Before, out var spaceBefore)) changes = changes with { SpaceBefore = spaceBefore };
        if (Changed(after.After, before.After, out var spaceAfter)) changes = changes with { SpaceAfter = spaceAfter };
        if (after.Contextual != before.Contextual) changes = changes with { ContextualSpacing = after.Contextual };
        if (after.LineMode != before.LineMode || Math.Abs(after.LineValue - before.LineValue) > 0.001)
        {
            changes = after.LineMode == LineHeightMode.Proportional
                ? changes with { LineSpacing = after.LineValue / 100, LineHeight = ParagraphLineHeight.Proportional(after.LineValue / 100) }
                : changes with { LineHeight = new ParagraphLineHeight(after.LineMode, after.LineValue) };
        }
        if (after.Align != current.Align) changes = changes with { Align = after.Align };
        var hyphenate = Flag(values, "hyphenate", current.Hyphenation?.Enabled == true);
        if (hyphenate != (current.Hyphenation?.Enabled == true))
            changes = changes with { Hyphenation = (current.Hyphenation ?? ParagraphHyphenation.On) with { Enabled = hyphenate } };
        var pageBreak = Flag(values, "break", current.PageBreakBefore == true);
        if (pageBreak != (current.PageBreakBefore == true)) changes = changes with { PageBreakBefore = pageBreak };
        var columnBreak = Flag(values, "columnBreak", current.ColumnBreakBefore == true);
        if (columnBreak != (current.ColumnBreakBefore == true)) changes = changes with { ColumnBreakBefore = columnBreak };
        var pageIndex = values.ContainsKey("pageStyle") ? values.Choice("pageStyle", scope.PageStyles.Count + 1) : 0;
        var pageStyle = pageIndex == 0 ? null : scope.PageStyles[pageIndex - 1];
        if (pageStyle != current.PageStyleName && pageStyle is not null) changes = changes with { PageStyleName = pageStyle };
        var number = (int)Math.Round(values.Number("pageNumber", 0));
        if (number != (current.PageNumberRestart ?? 0) && number > 0) changes = changes with { PageNumberRestart = number };
        var together = Flag(values, "together", current.KeepTogether == true);
        if (together != (current.KeepTogether == true)) changes = changes with { KeepTogether = together };
        var keep = Flag(values, "keep", current.KeepWithNext == true);
        if (keep != (current.KeepWithNext == true)) changes = changes with { KeepWithNext = keep };
        var orphans = (int)Math.Round(values.Number("orphans", current.Orphans ?? 2));
        if (orphans != (current.Orphans ?? 2)) changes = changes with { Orphans = orphans };
        var widows = (int)Math.Round(values.Number("widows", current.Widows ?? 2));
        if (widows != (current.Widows ?? 2)) changes = changes with { Widows = widows };
        if (values.TryGetValue("tabs", out var tabs) && tabs is IReadOnlyList<TabStop> stops && !stops.SequenceEqual(current.TabStops ?? []))
            changes = changes with { TabStops = stops };
        var borders = Borders(current, values);
        if (borders != (current.Borders ?? new BoxBorders())) changes = changes with { Borders = borders };
        var background = _colors.Argb(values.Text("background"));
        var visible = current.Background is { } fill && fill >> 24 != 0 ? fill : (uint?)null;
        if (background != visible) changes = changes with { Background = background ?? Transparent };
        var drop = DropCap(values, current.DropCap);
        if (drop != current.DropCap && !(drop is null && current.DropCap is null)) changes = changes with { DropCap = drop ?? new DropCap(1, 0) };
        return changes;
    }

    public double? DefaultTabInterval(ParagraphDialogScope scope, IReadOnlyDictionary<string, object> values)
    {
        if (scope.DefaultTabInterval is not { } current || !values.ContainsKey(DefaultTabsKey)) return null;
        var chosen = values.Number(DefaultTabsKey, Centimetres(current)) * PointsPerCentimetre;
        return Math.Abs(Centimetres(chosen) - Centimetres(current)) > 0.004 ? chosen : null;
    }

    public ParagraphPageClear? PageClear(ParagraphDialogScope scope, IReadOnlyDictionary<string, object> values)
    {
        var current = scope.Current;
        var style = values.ContainsKey("pageStyle") && values.Choice("pageStyle", scope.PageStyles.Count + 1) == 0 && current.PageStyleName is not null;
        var restart = style || values.ContainsKey("pageNumber") && (int)Math.Round(values.Number("pageNumber", 0)) == 0 && current.PageNumberRestart is not null;
        return style || restart ? new ParagraphPageClear(style, restart) : null;
    }

    private const string DefaultTabsKey = "defaultTabs";

    private const double PointsPerCentimetre = 72 / 2.54;

    private IEnumerable<FormField> DefaultTabField(ParagraphDialogScope scope) => scope.DefaultTabInterval is { } interval
        ? [new NumberField(DefaultTabsKey, texts["folio.paragraph.defaultTabs"], Centimetres(interval), 0.1, 10, 0.05) { Unit = "cm", Presets = [0.5, 1, 1.25, 1.27, 2.5] }]
        : [];

    private double Centimetres(double points) => Math.Round(points / PointsPerCentimetre, 2);

    public Avalonia.Controls.Control Preview(IFormValuesFeed feed, ParagraphDialogScope scope) => shared.Preview(feed, Request(scope.Current));

    private ParagraphFormatRequest Request(TextParagraph current) => new(State(current), ParagraphFeatures.All);

    private ParagraphFormatState State(TextParagraph current)
    {
        var (mode, value) = Line(current);
        return new ParagraphFormatState
        {
            Align = current.Align,
            Left = current.MarginLeft ?? 0,
            Right = current.MarginRight ?? 0,
            FirstLine = current.Indent ?? 0,
            Before = current.SpaceBefore,
            After = current.SpaceAfter,
            LineMode = mode,
            LineValue = value,
            Contextual = current.ContextualSpacing == true,
        };
    }

    private BoxBorders Borders(TextParagraph current, IReadOnlyDictionary<string, object> values)
    {
        var frame = values.TryGetValue("borders", out var value) && value is BorderFrame chosen ? chosen : frames.Frame(current.Borders);
        return frames.Apply(new BoxBorders(), frame, values.Number("border.padding", 0));
    }

    private DropCap? DropCap(IReadOnlyDictionary<string, object> values, DropCap? current)
    {
        if (!values.ContainsKey("drop")) return current;
        if (!values.Flag("drop")) return current is { IsActive: true } ? new DropCap(1, 0) : current;
        return new DropCap((int)Math.Round(values.Number("drop.lines", 3)), (int)Math.Round(values.Number("drop.characters", 1)))
        {
            WholeWord = values.Flag("drop.word"),
            Distance = values.Number("drop.distance", 0),
            CharacterStyleName = current?.CharacterStyleName,
        };
    }

    private (LineHeightMode Mode, double Value) Line(TextParagraph current) => current.LineHeight switch
    {
        { Mode: LineHeightMode.Proportional } height => (LineHeightMode.Proportional, Math.Round(height.Value * 100, 2)),
        { } height => (height.Mode, height.Value),
        null => (LineHeightMode.Proportional, Math.Round(current.LineSpacing * 100, 2)),
    };

    private bool Flag(IReadOnlyDictionary<string, object> values, string key, bool current) =>
        values.ContainsKey(key) ? values.Flag(key) : current;

    private bool Changed(double chosen, double current, out double value)
    {
        value = chosen;
        return Math.Abs(chosen - current) > 0.001;
    }
}

public sealed class FolioParagraphDialog(IFolioParagraphTabs tabs, ITabbedForms forms) : IFolioParagraphDialog
{
    public async Task<ParagraphDialogAnswer?> AskAsync(ParagraphDialogScope scope, string titleKey, CancellationToken ct, int initialTab = 0) =>
        await forms.AskAsync(titleKey, tabs.Tabs(scope), ct, initialTab).ConfigureAwait(true) is { } values
            ? new ParagraphDialogAnswer(tabs.Read(scope, values), tabs.DefaultTabInterval(scope, values)) { PageClear = tabs.PageClear(scope, values) }
            : null;
}
