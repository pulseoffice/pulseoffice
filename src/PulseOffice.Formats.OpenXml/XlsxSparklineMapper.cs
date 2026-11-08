using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using X14 = DocumentFormat.OpenXml.Office2010.Excel;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxSparklineMapper : IWorksheetExtensionSource
{
    private readonly SpreadsheetColours _colours = new();
    private readonly OoxmlValueMap<SparklineKind, X14.SparklineTypeValues> _kinds = new(SparklineKind.Line, X14.SparklineTypeValues.Line,
        (SparklineKind.Column, X14.SparklineTypeValues.Column), (SparklineKind.Stacked, X14.SparklineTypeValues.Stacked));

    public WorksheetExtensionList? Build(Worksheet sheet) =>
        Extension(sheet) is { } extension ? new WorksheetExtensionList(extension) : null;

    public WorksheetExtension? Extension(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var groups = sheet.Sparklines.Groups;
        if (groups.Count == 0) return null;

        var container = new X14.SparklineGroups();

        foreach (var group in groups)
        {
            var style = group.Style;
            var element = new X14.SparklineGroup
            {
                Type = _kinds.Sdk(group.Kind),
                DisplayEmptyCellsAs = X14.DisplayBlanksAsValues.Gap,
                LineWeight = style.LineWidth,
                High = style.ShowHighPoint,
                Low = style.ShowLowPoint,
                First = style.ShowFirstPoint,
                Last = style.ShowLastPoint,
                Negative = style.ShowNegative,
            };

            if (group.SameScale)
            {
                element.MinAxisType = X14.SparklineAxisMinMaxValues.Group;
                element.MaxAxisType = X14.SparklineAxisMinMaxValues.Group;
            }

            element.AppendChild(new X14.SeriesColor { Rgb = _colours.ToArgb(style.ColorHex) });
            element.AppendChild(new X14.NegativeColor { Rgb = _colours.ToArgb(style.NegativeHex) });
            element.AppendChild(new X14.MarkersColor { Rgb = _colours.ToArgb(style.MarkerHex) });

            var sparklines = new X14.Sparklines();

            foreach (var sparkline in group.Sparklines)
                sparklines.AppendChild(new X14.Sparkline
                {
                    Formula = new DocumentFormat.OpenXml.Office.Excel.Formula(Reference(sheet, sparkline.Source)),
                    ReferenceSequence = new DocumentFormat.OpenXml.Office.Excel.ReferenceSequence(sparkline.Cell.ToA1()),
                });

            element.AppendChild(sparklines);
            container.AppendChild(element);
        }

        var extension = new WorksheetExtension
        {
            Uri = "{05C60535-1F16-4fd2-B633-F4F36F0B64E0}",
        };

        extension.AddNamespaceDeclaration("x14", "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main");
        extension.AppendChild(container);

        return extension;
    }

    public void Read(WorksheetExtensionList? extensions, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (extensions is null) return;

        foreach (var group in extensions.Descendants<X14.SparklineGroup>())
        {
            var lines = new List<Sparkline>();

            foreach (var sparkline in group.Descendants<X14.Sparkline>())
            {
                var formula = sparkline.Formula?.Text;
                var target = sparkline.ReferenceSequence?.Text;

                if (formula is null || target is null) continue;
                if (!CellAddress.TryParseA1(target.Replace("$", string.Empty, StringComparison.Ordinal), out var address)) continue;

                var cut = formula.LastIndexOf('!');
                var source = (cut >= 0 ? formula[(cut + 1)..] : formula).Replace("$", string.Empty, StringComparison.Ordinal);

                if (!CellRange.TryParseA1(source, out var range)) continue;
                lines.Add(new Sparkline(address, range));
            }

            if (lines.Count == 0) continue;

            var defaults = new SparklineStyle();
            sheet.Sparklines.Add(new SparklineGroup
            {
                Id = Guid.NewGuid().ToString("N"),
                Sparklines = lines,
                Kind = _kinds.Model(group.Type?.Value),
                SameScale = group.MinAxisType?.Value is { } axis && axis == X14.SparklineAxisMinMaxValues.Group,
                Style = new SparklineStyle
                {
                    ColorHex = _colours.Parse(group.GetFirstChild<X14.SeriesColor>()?.Rgb?.Value) ?? defaults.ColorHex,
                    NegativeHex = _colours.Parse(group.GetFirstChild<X14.NegativeColor>()?.Rgb?.Value) ?? defaults.NegativeHex,
                    MarkerHex = _colours.Parse(group.GetFirstChild<X14.MarkersColor>()?.Rgb?.Value) ?? defaults.MarkerHex,
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

    private string Reference(Worksheet sheet, CellRange range) =>
        string.Create(CultureInfo.InvariantCulture, $"'{sheet.Name.Replace("'", "''", StringComparison.Ordinal)}'!{range.Start.ToA1()}:{range.End.ToA1()}");
}
