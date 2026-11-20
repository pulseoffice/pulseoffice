using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using X14 = DocumentFormat.OpenXml.Office2010.Excel;
using ExcelFormula = DocumentFormat.OpenXml.Office.Excel.Formula;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxSparklineMapper : IWorksheetExtensionSource
{
    private readonly SpreadsheetColours _colours = new();
    private readonly OoxmlValueMap<SparklineKind, X14.SparklineTypeValues> _kinds = new(SparklineKind.Line, X14.SparklineTypeValues.Line,
        (SparklineKind.Column, X14.SparklineTypeValues.Column), (SparklineKind.Stacked, X14.SparklineTypeValues.Stacked));
    private readonly OoxmlValueMap<SparklineEmptyCells, X14.DisplayBlanksAsValues> _empty = new(SparklineEmptyCells.Zero, X14.DisplayBlanksAsValues.Zero,
        (SparklineEmptyCells.Gap, X14.DisplayBlanksAsValues.Gap), (SparklineEmptyCells.Span, X14.DisplayBlanksAsValues.Span));
    private readonly OoxmlValueMap<SparklineAxisBound, X14.SparklineAxisMinMaxValues> _bounds = new(SparklineAxisBound.Individual, X14.SparklineAxisMinMaxValues.Individual,
        (SparklineAxisBound.Group, X14.SparklineAxisMinMaxValues.Group), (SparklineAxisBound.Custom, X14.SparklineAxisMinMaxValues.Custom));

    public WorksheetExtensionList? Build(Worksheet sheet) =>
        Extension(sheet) is { } extension ? new WorksheetExtensionList(extension) : null;

    public WorksheetExtension? Extension(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = sheet.Sparklines.Groups;
        if (groups.Count == 0) return null;

        var container = new X14.SparklineGroups();

        foreach (var group in groups)
            container.AppendChild(Group(sheet, group));

        var extension = new WorksheetExtension
        {
            Uri = "{05C60535-1F16-4fd2-B633-F4F36F0B64E0}",
        };

        extension.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");
        extension.AddNamespaceDeclaration("xm", "http://schemas.microsoft.com/office/excel/2006/main");
        extension.AppendChild(container);

        return extension;
    }

    private X14.SparklineGroup Group(Worksheet sheet, SparklineGroup group)
    {
        var style = group.Style;
        var element = new X14.SparklineGroup
        {
            Type = _kinds.Sdk(group.Kind),
            DisplayEmptyCellsAs = _empty.Sdk(group.EmptyCells),
            LineWeight = style.LineWidth,
            Markers = style.ShowMarkers ? new BooleanValue(true) : null,
            High = style.ShowHighPoint,
            Low = style.ShowLowPoint,
            First = style.ShowFirstPoint,
            Last = style.ShowLastPoint,
            Negative = style.ShowNegative,
            DisplayXAxis = style.ShowAxis ? new BooleanValue(true) : null,
            DisplayHidden = group.ShowHidden ? new BooleanValue(true) : null,
            RightToLeft = group.RightToLeft ? new BooleanValue(true) : null,
            DateAxis = group.DateRange is not null ? new BooleanValue(true) : null,
        };

        if (group.MinimumAxis != SparklineAxisBound.Individual) element.MinAxisType = _bounds.Sdk(group.MinimumAxis);
        if (group.MaximumAxis != SparklineAxisBound.Individual) element.MaxAxisType = _bounds.Sdk(group.MaximumAxis);
        if (group.MinimumAxis == SparklineAxisBound.Custom && group.ManualMinimum is { } minimum) element.ManualMin = minimum;
        if (group.MaximumAxis == SparklineAxisBound.Custom && group.ManualMaximum is { } maximum) element.ManualMax = maximum;

        element.SeriesColor = new X14.SeriesColor { Rgb = _colours.ToArgb(style.ColorHex) };
        element.NegativeColor = new X14.NegativeColor { Rgb = _colours.ToArgb(style.NegativeHex) };
        element.AxisColor = new X14.AxisColor { Rgb = _colours.ToArgb(style.AxisHex) };
        element.MarkersColor = new X14.MarkersColor { Rgb = _colours.ToArgb(style.MarkerHex) };
        element.FirstMarkerColor = new X14.FirstMarkerColor { Rgb = _colours.ToArgb(style.FirstHex ?? style.MarkerHex) };
        element.LastMarkerColor = new X14.LastMarkerColor { Rgb = _colours.ToArgb(style.LastHex ?? style.MarkerHex) };
        element.HighMarkerColor = new X14.HighMarkerColor { Rgb = _colours.ToArgb(style.HighHex ?? style.MarkerHex) };
        element.LowMarkerColor = new X14.LowMarkerColor { Rgb = _colours.ToArgb(style.LowHex ?? style.MarkerHex) };
        if (group.DateRange is { } dates) element.Formula = new ExcelFormula(Reference(group.DateSheet ?? sheet.Name, dates));

        var sparklines = new X14.Sparklines();
        foreach (var sparkline in group.Sparklines)
            sparklines.AppendChild(new X14.Sparkline
            {
                Formula = new ExcelFormula(Reference(sparkline.SourceSheetOr(sheet.Name), sparkline.Source)),
                ReferenceSequence = new DocumentFormat.OpenXml.Office.Excel.ReferenceSequence(sparkline.Cell.ToA1()),
            });

        element.Sparklines = sparklines;
        return element;
    }

    public void Read(WorksheetExtensionList? extensions, Worksheet sheet, Stylesheet? stylesheet = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (extensions is null) return;
        _colours.UsePalette(stylesheet);

        foreach (var group in extensions.Descendants<X14.SparklineGroup>())
        {
            var lines = new List<Sparkline>();

            foreach (var sparkline in group.Descendants<X14.Sparkline>())
            {
                var target = sparkline.ReferenceSequence?.Text;

                if (target is null || Parse(sparkline.Formula?.Text) is not { } source) continue;
                if (!CellAddress.TryParseA1(target.Replace("$", string.Empty, StringComparison.Ordinal), out var address)) continue;

                lines.Add(new Sparkline(address, source.Range, Foreign(source.Sheet, sheet)));
            }

            if (lines.Count == 0) continue;

            var defaults = new SparklineStyle();
            var marker = Colour(group.MarkersColor);
            var minimum = _bounds.Model(group.MinAxisType?.Value);
            var maximum = _bounds.Model(group.MaxAxisType?.Value);
            var dates = group.DateAxis?.Value == true ? Parse(group.Formula?.Text) : null;
            sheet.Sparklines.Add(new SparklineGroup
            {
                Id = Guid.NewGuid().ToString("N"),
                Sparklines = lines,
                Kind = _kinds.Model(group.Type?.Value),
                MinimumAxis = minimum,
                MaximumAxis = maximum,
                ManualMinimum = minimum == SparklineAxisBound.Custom ? Finite(group.ManualMin?.Value) : null,
                ManualMaximum = maximum == SparklineAxisBound.Custom ? Finite(group.ManualMax?.Value) : null,
                EmptyCells = group.DisplayEmptyCellsAs?.Value is { } blanks ? _empty.Model(blanks) : SparklineEmptyCells.Zero,
                ShowHidden = group.DisplayHidden?.Value ?? false,
                RightToLeft = group.RightToLeft?.Value ?? false,
                DateRange = dates?.Range,
                DateSheet = dates is { } date ? Foreign(date.Sheet, sheet) : null,
                Style = new SparklineStyle
                {
                    ColorHex = Colour(group.SeriesColor) ?? defaults.ColorHex,
                    NegativeHex = Colour(group.NegativeColor) ?? defaults.NegativeHex,
                    MarkerHex = marker ?? defaults.MarkerHex,
                    AxisHex = Colour(group.AxisColor) ?? defaults.AxisHex,
                    FirstHex = Distinct(Colour(group.FirstMarkerColor), marker),
                    LastHex = Distinct(Colour(group.LastMarkerColor), marker),
                    HighHex = Distinct(Colour(group.HighMarkerColor), marker),
                    LowHex = Distinct(Colour(group.LowMarkerColor), marker),
                    ShowMarkers = group.Markers?.Value ?? false,
                    ShowAxis = group.DisplayXAxis?.Value ?? false,
                    ShowHighPoint = group.High?.Value ?? false,
                    ShowLowPoint = group.Low?.Value ?? false,
                    ShowFirstPoint = group.First?.Value ?? false,
                    ShowLastPoint = group.Last?.Value ?? false,
                    ShowNegative = group.Negative?.Value ?? false,
                    LineWidth = group.LineWeight?.Value is { } weight && double.IsFinite(weight) && weight > 0
                        ? Math.Clamp(weight, SparklineStyle.ThinnestLine, SparklineStyle.WidestLine)
                        : 0.75,
                },
            });
        }
    }

    private string? Colour(OpenXmlElement? colour) =>
        colour is null ? null : _colours.Read(System.Xml.Linq.XElement.Parse(colour.OuterXml));

    private string? Distinct(string? colour, string? marker) =>
        colour is not null && !string.Equals(colour, marker, StringComparison.OrdinalIgnoreCase) ? colour : null;

    private double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;

    private string? Foreign(string? source, Worksheet sheet) =>
        source is { Length: > 0 } && !string.Equals(source, sheet.Name, StringComparison.OrdinalIgnoreCase) ? source : null;

    private (string? Sheet, CellRange Range)? Parse(string? formula)
    {
        if (formula is null) return null;
        var cut = formula.LastIndexOf('!');
        string? name = null;
        if (cut >= 0)
        {
            name = formula[..cut];
            if (name.Length >= 2 && name[0] == '\'' && name[^1] == '\'') name = name[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }
        var source = (cut >= 0 ? formula[(cut + 1)..] : formula).Replace("$", string.Empty, StringComparison.Ordinal);
        return CellRange.TryParseA1(source, out var range) ? (name, range) : null;
    }

    private string Reference(string sheet, CellRange range) =>
        string.Create(CultureInfo.InvariantCulture, $"'{sheet.Replace("'", "''", StringComparison.Ordinal)}'!{range.Start.ToA1()}:{range.End.ToA1()}");
}
