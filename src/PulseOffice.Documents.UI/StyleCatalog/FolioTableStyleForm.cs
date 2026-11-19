using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;

namespace PulseOffice.Documents.UI.StyleCatalog;

public interface IFolioTableStyleForm
{
    IReadOnlyList<FormTab> Tabs(TableProperties effective);

    TableProperties Read(TableProperties effective, TableProperties own, IReadOnlyDictionary<string, object> values);
}

public sealed class FolioTableStyleForm(ITextCatalog texts) : IFolioTableStyleForm
{
    private readonly ColorFieldValue _colors = new();

    private readonly IReadOnlyList<TableBandAxes> _axes = [TableBandAxes.Rows, TableBandAxes.Columns, TableBandAxes.Both];

    public IReadOnlyList<FormTab> Tabs(TableProperties effective)
    {
        ArgumentNullException.ThrowIfNull(effective);
        IReadOnlyList<string> axes = [.. _axes.Select(axis => texts["folio.tableStyle.axes." + axis.ToString().ToLowerInvariant()])];
        return
        [
            new(texts["folio.tableStyle.tab.borders"], texts["folio.tableStyle.borders.explanation"],
            [
                new ColorField("borderColor", texts["folio.tableStyle.borderColor"], _colors.Hex(effective.BorderColor)),
                new NumberField("borderWidth", texts["folio.tableStyle.borderWidth"], effective.BorderWidth ?? 0, 0, 12, 0.25) { Unit = "pt" },
                new CheckField("innerLines", texts["folio.tableStyle.innerLines"], effective.InnerLines != false),
                new ColorField("background", texts["folio.tableStyle.background"], _colors.Hex(effective.Background)),
            ]),
            new(texts["folio.tableStyle.tab.header"], texts["folio.tableStyle.header.explanation"],
            [
                new ColorField("headerBackground", texts["folio.tableStyle.headerBackground"], _colors.Hex(effective.HeaderBackground)),
                new ColorField("headerColor", texts["folio.tableStyle.headerColor"], _colors.Hex(effective.HeaderColor)),
                new CheckField("headerBold", texts["folio.tableStyle.headerBold"], effective.HeaderBold == true),
            ]),
            new(texts["folio.tableStyle.tab.bands"], texts["folio.tableStyle.bands.explanation"],
            [
                new ColorField("firstBand", texts["folio.tableStyle.firstBand"], _colors.Hex(effective.FirstBandBackground)),
                new ChoiceField("firstBandAxes", texts["folio.tableStyle.firstBandAxes"], axes, Index(effective.FirstBandAxes)),
                new ColorField("secondBand", texts["folio.tableStyle.secondBand"], _colors.Hex(effective.BandBackground)),
                new ChoiceField("secondBandAxes", texts["folio.tableStyle.secondBandAxes"], axes, Index(effective.BandAxes)),
            ]),
        ];
    }

    public TableProperties Read(TableProperties effective, TableProperties own, IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(values);
        var borderWidth = values.Number("borderWidth", effective.BorderWidth ?? 0);
        var innerLines = values.Flag("innerLines");
        var headerBold = values.Flag("headerBold");
        var firstAxes = _axes[values.Choice("firstBandAxes", _axes.Count, Index(effective.FirstBandAxes))];
        var secondAxes = _axes[values.Choice("secondBandAxes", _axes.Count, Index(effective.BandAxes))];
        return own with
        {
            BorderColor = Colour(values, "borderColor", effective.BorderColor, own.BorderColor),
            BorderWidth = Math.Abs(borderWidth - (effective.BorderWidth ?? 0)) < 0.001 ? own.BorderWidth : borderWidth,
            InnerLines = innerLines == (effective.InnerLines != false) ? own.InnerLines : innerLines,
            Background = Colour(values, "background", effective.Background, own.Background),
            HeaderBackground = Colour(values, "headerBackground", effective.HeaderBackground, own.HeaderBackground),
            HeaderColor = Colour(values, "headerColor", effective.HeaderColor, own.HeaderColor),
            HeaderBold = headerBold == (effective.HeaderBold == true) ? own.HeaderBold : headerBold,
            FirstBandBackground = Colour(values, "firstBand", effective.FirstBandBackground, own.FirstBandBackground),
            FirstBandAxes = firstAxes == (effective.FirstBandAxes ?? TableBandAxes.Rows) ? own.FirstBandAxes : firstAxes,
            BandBackground = Colour(values, "secondBand", effective.BandBackground, own.BandBackground),
            BandAxes = secondAxes == (effective.BandAxes ?? TableBandAxes.Rows) ? own.BandAxes : secondAxes,
        };
    }

    private uint? Colour(IReadOnlyDictionary<string, object> values, string key, uint? effective, uint? own)
    {
        var chosen = _colors.Argb(values.Text(key));
        return Equal(chosen, effective) ? own : chosen;
    }

    private bool Equal(uint? first, uint? second) =>
        first is null ? second is null || second >> 24 == 0 : second is { } other && (first.Value & 0xFFFFFF) == (other & 0xFFFFFF) && other >> 24 != 0;

    private int Index(TableBandAxes? axes) => axes switch
    {
        TableBandAxes.Columns => 1,
        TableBandAxes.Both => 2,
        _ => 0,
    };
}
