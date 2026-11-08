using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxChartMapper
{
    private readonly XlsxChartOrientation _orientation = new();
    private readonly ChartSpaceBuilder _builder;
    private readonly ChartSpaceReader _reader;
    private readonly ChartAnchorBuilder _anchors = new();
    private readonly SheetReferenceUnion _union = new();
    private readonly PivotChartExtent _pivotExtent = new();
    private readonly ChartSourceReferences _references = new();
    private readonly ChartSourceLayout _layout = new();
    private readonly ModelSeriesSlots _slots = new();
    private readonly ChartDataExtractor _data = new(new CanonicalSeriesNames(), new DocumentLanguageChartCellText());

    public XlsxChartMapper()
    {
        var families = new ChartFamilies();
        _builder = new ChartSpaceBuilder(families);
        _reader = new ChartSpaceReader(families);
    }

    public IEnumerable<OpenXmlCompositeElement> BuildAnchors(
        DrawingsPart drawingsPart, Worksheet sheet, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(drawingsPart);
        ArgumentNullException.ThrowIfNull(sheet);

        foreach (var placed in sheet.Objects.Where(o => o.Content is ChartDefinition))
        {
            var definition = (ChartDefinition)placed.Content;
            var chartPart = drawingsPart.AddNewPart<ChartPart>();
            chartPart.ChartSpace = _builder.Build(
                definition, _data.Extract(workbook, definition),
                definition.HasSeriesSources ? new ReferencedSeriesSource(definition, _references) : new SheetSeriesSource(definition));
            chartPart.ChartSpace.Save();

            yield return _anchors.Build(sheet.Layout, placed, drawingsPart.GetIdOfPart(chartPart));
        }
    }

    public IReadOnlyList<PlacedObject> ReadCharts(DrawingsPart? drawings, SheetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var anchors = new XlsxAnchorLayout(layout);
        if (drawings?.WorksheetDrawing is null) return [];

        var placed = new List<PlacedObject>();
        var ordinal = 0;

        foreach (var anchor in drawings.WorksheetDrawing.ChildElements.OfType<OpenXmlCompositeElement>())
        {
            var frame = anchor.GetFirstChild<Xdr.GraphicFrame>();
            var reference = frame?.Descendants<C.ChartReference>().FirstOrDefault();
            if (reference?.Id?.Value is not { } id) continue;
            if (!drawings.TryGetPartById(id, out var found) || found is not ChartPart { ChartSpace: { } space }) continue;
            if (_reader.Read(space) is not { } content) continue;

            var pivot = space.Descendants<C.PivotSource>().Any();
            var formulas = content.Series
                .SelectMany(s => s.Descendants<C.Formula>())
                .Select(f => pivot ? _pivotExtent.Drawn(f) : f.Text)
                .Where(t => t.Contains('!'))
                .ToList();

            var sourceSheet = formulas.Count > 0 ? SheetOf(formulas[0]) : string.Empty;
            var definition = content.Decorate(_layout.Settle(_orientation.Infer(content.Series, new ChartDefinition
            {
                Id = $"grafico{++ordinal}",
                Type = content.Type,
                SourceSheet = sourceSheet,
                SourceRange = _union.Of([.. formulas.Where(formula => string.Equals(SheetOf(formula), sourceSheet, StringComparison.CurrentCultureIgnoreCase))]),
            }), Sources(content.Series, pivot)));

            placed.Add(anchors.Place(anchor, new PlacedObject
            {
                Id = frame?.GetFirstChild<Xdr.NonVisualGraphicFrameProperties>()
                        ?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value
                     ?? definition.Id,
                Content = definition,
                Anchor = new CellAddress(0, 0),
            }, new DocumentSize(Twip.FromCentimeters(12), Twip.FromCentimeters(7))));
        }

        return placed;
    }

    private IReadOnlyList<ChartSeriesSource> Sources(IReadOnlyList<OpenXmlCompositeElement> series, bool pivot)
    {
        var sources = new List<ChartSeriesSource>();
        foreach (var element in series)
        {
            var name = Reference(element.GetFirstChild<C.SeriesText>(), pivot);
            var categories = Reference((OpenXmlElement?)element.GetFirstChild<C.CategoryAxisData>() ?? element.GetFirstChild<C.XValues>(), pivot);
            if (element is C.BubbleChartSeries)
            {
                sources.Add(new ChartSeriesSource(null, Reference(element.GetFirstChild<C.XValues>(), pivot), null));
                sources.Add(new ChartSeriesSource(name, Reference(element.GetFirstChild<C.YValues>(), pivot), null));
                sources.Add(new ChartSeriesSource(null, Reference(element.GetFirstChild<C.BubbleSize>(), pivot), null));
            }
            else sources.Add(new ChartSeriesSource(name, Reference((OpenXmlElement?)element.GetFirstChild<C.Values>() ?? element.GetFirstChild<C.YValues>(), pivot), categories));
        }

        return sources.Any(source => source.Values is not null) ? sources : [];
    }

    private string? Reference(OpenXmlElement? container, bool pivot)
    {
        if (container?.Descendants<C.Formula>().FirstOrDefault() is not { } formula) return null;
        var text = pivot ? _pivotExtent.Drawn(formula) : formula.Text;
        return text.Contains('!') && _references.Read(text) is { } reference ? _references.Write(reference.Sheet, reference.Range) : null;
    }

    private string SheetOf(string formula)
    {
        var sheet = formula[..formula.LastIndexOf('!')];
        return sheet.Length >= 2 && sheet[0] == '\'' && sheet[^1] == '\''
            ? sheet[1..^1].Replace("''", "'", StringComparison.Ordinal)
            : sheet;
    }
}

internal sealed class ChartAnchorBuilder
{
    private readonly XlsxCellAnchors _cells = new();

    private const string ChartUri = "http://schemas.openxmlformats.org/drawingml/2006/chart";

    public Xdr.TwoCellAnchor Build(SheetLayout layout, PlacedObject placed, string relationshipId)
    {
        var width = Emu.FromTwips(placed.Size.Width).Value;
        var height = Emu.FromTwips(placed.Size.Height).Value;

        var frame = new Xdr.GraphicFrame(
            new Xdr.NonVisualGraphicFrameProperties(
                new Xdr.NonVisualDrawingProperties { Id = 2u, Name = placed.Id },
                new Xdr.NonVisualGraphicFrameDrawingProperties()),
            new Xdr.Transform(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height }),
            new A.Graphic(new A.GraphicData(new C.ChartReference { Id = relationshipId }) { Uri = ChartUri }));

        return _cells.Create(layout, placed, frame, new Xdr.ClientData());
    }
}

internal sealed class SheetReferenceUnion
{
    public string Of(IReadOnlyList<string> formulas)
    {
        int top = int.MaxValue, left = int.MaxValue, bottom = -1, right = -1;

        foreach (var formula in formulas)
        {
            var text = formula[(formula.LastIndexOf('!') + 1)..].Replace("$", string.Empty, StringComparison.Ordinal);
            if (!CellRange.TryParseA1(text, out var range)) continue;

            top = Math.Min(top, range.Start.Row);
            left = Math.Min(left, range.Start.Column);
            bottom = Math.Max(bottom, range.End.Row);
            right = Math.Max(right, range.End.Column);
        }

        return bottom < 0
            ? "A1"
            : new CellRange(new CellAddress(top, left), new CellAddress(bottom, right)).ToString();
    }
}

internal sealed class PivotChartExtent
{
    public string Drawn(C.Formula formula)
    {
        var text = formula.Text;
        var cached = formula.Parent?.Descendants<C.PointCount>().FirstOrDefault()?.Val?.Value;
        var separator = text.LastIndexOf('!');
        if (cached is not { } count || count == 0 || separator < 0) return text;

        var reference = text[(separator + 1)..].Replace("$", string.Empty, StringComparison.Ordinal);
        if (!CellRange.TryParseA1(reference, out var range)) return text;

        var drawn = range.ColumnCount == 1 && range.RowCount > count
            ? new CellRange(range.Start, new CellAddress(range.Start.Row + (int)count - 1, range.End.Column))
            : range.RowCount == 1 && range.ColumnCount > count
                ? new CellRange(range.Start, new CellAddress(range.End.Row, range.Start.Column + (int)count - 1))
                : range;

        return drawn == range ? text : $"{text[..separator]}!{drawn}";
    }
}
