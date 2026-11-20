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
    private readonly ChartInlineErrorValues _inlineErrors = new();
    private readonly XlsxChartOrientation _orientation = new();
    private readonly ChartSpaceBuilder _builder;
    private readonly ChartSpaceReader _reader;
    private readonly ChartAnchorBuilder _anchors = new();
    private readonly SheetReferenceUnion _union = new();
    private readonly PivotChartExtent _pivotExtent = new();
    private readonly ChartSourceReferences _references = new();

    private readonly OoxmlRangeFormulas _rangeFormulas = new();
    private readonly ChartSourceLayout _layout = new();
    private readonly ModelSeriesSlots _slots = new();
    private readonly ChartDataExtractor _data = new(new CanonicalSeriesNames(), new DocumentLanguageChartCellText());
    private readonly ChartErrorValueRefresh _errorValues = new();
    private readonly LiteralSeriesSource _literals = new();
    private readonly XlsxPivotChartLink _pivotLink = new();
    private readonly ChartTableSources _tables = new();
    private readonly XlsxChartTableSources _tableSources = new();
    private readonly ChartExReader _chartEx = new();
    private readonly ChartExAnchors _chartExAnchors;

    public XlsxChartMapper()
    {
        var families = new ChartFamilies();
        _builder = new ChartSpaceBuilder(families);
        _reader = new ChartSpaceReader(families, new PulseOffice.Formats.OpenXml.DrawingML.DrawingPlainText());
        var layouts = new ChartExLayouts();
        _chartExAnchors = new ChartExAnchors(layouts, new ChartExSpaceWriter(layouts));
    }

    public IEnumerable<OpenXmlCompositeElement> BuildAnchors(
        DrawingsPart drawingsPart, Worksheet sheet, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(drawingsPart);
        ArgumentNullException.ThrowIfNull(sheet);

        foreach (var placed in sheet.Objects.Where(o => o.Content is ChartDefinition))
        {
            var structured = _errorValues.Refresh(workbook, (ChartDefinition)placed.Content);
            var definition = _tables.Flatten(structured, workbook);
            var chartPart = drawingsPart.AddNewPart<ChartPart>();
            chartPart.ChartSpace = IsStatic(definition)
                ? _builder.Build(definition, definition.InlineData!, _literals)
                : _builder.Build(
                    definition, _data.Extract(workbook, definition),
                    definition.HasSeriesSources ? new ReferencedSeriesSource(definition, _references) : new SheetSeriesSource(definition));
            _pivotLink.Write(chartPart.ChartSpace, definition.PivotSource, definition.SourceSheet);
            _tableSources.Write(chartPart.ChartSpace, structured);
            chartPart.ChartSpace.Save();

            var classic = _anchors.Build(sheet.Layout, placed, drawingsPart.GetIdOfPart(chartPart));
            yield return _chartExAnchors.Writes(definition)
                ? _chartExAnchors.Wrap(drawingsPart, definition, chartPart.ChartSpace, classic)
                : classic;
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
            if (anchor.LocalName == "AlternateContent")
            {
                if (_chartExAnchors.FallbackAnchor(anchor) is { } fallback && Classic(drawings, anchors, fallback, ordinal + 1) is { } kept && _chartExAnchors.Writes((ChartDefinition)kept.Content))
                {
                    ordinal++;
                    placed.Add(kept);
                    continue;
                }
                if (ChartEx(drawings, anchor, ++ordinal) is { } extended) placed.Add(anchors.Place(extended.Anchor, extended.Placed, new DocumentSize(Twip.FromCentimeters(12), Twip.FromCentimeters(7))));
                continue;
            }

            if (Classic(drawings, anchors, anchor, ordinal + 1) is not { } read) continue;
            ordinal++;
            placed.Add(read);
        }

        return placed;
    }

    private PlacedObject? Classic(DrawingsPart drawings, XlsxAnchorLayout anchors, OpenXmlCompositeElement anchor, int ordinal)
    {
        var frame = anchor.GetFirstChild<Xdr.GraphicFrame>();
        var reference = frame?.Descendants<C.ChartReference>().FirstOrDefault();
        if (reference?.Id?.Value is not { } id) return null;
        if (!drawings.TryGetPartById(id, out var found) || found is not ChartPart { ChartSpace: { } space }) return null;
        if (_reader.Read(space) is not { } content) return null;

        var pivot = space.Descendants<C.PivotSource>().Any();
        var formulas = content.Series
            .SelectMany(s => s.Descendants<C.Formula>())
            .Select(f => pivot ? _pivotExtent.Drawn(f) : f.Text)
            .SelectMany(text => _rangeFormulas.Parts(text))
            .Where(t => t.Contains('!'))
            .ToList();

        var sourceSheet = formulas.Count > 0 ? SheetOf(formulas[0]) : string.Empty;
        var definition = content.Decorate(_layout.Settle(_orientation.Infer(content.Series, new ChartDefinition
        {
            Id = $"grafico{ordinal}",
            Type = content.Type,
            SourceSheet = sourceSheet,
            SourceRange = _union.Of([.. formulas.Where(formula => string.Equals(SheetOf(formula), sourceSheet, StringComparison.CurrentCultureIgnoreCase))]),
        }), Sources(content.Series, pivot)));
        definition = definition with { PivotSource = _pivotLink.Read(space) };
        if (_tableSources.Read(space) is { } kept) definition = definition with { SeriesSources = kept };
        if (formulas.Count == 0 && _reader.Literals(content.Series) is { IsEmpty: false } literals)
            definition = _inlineErrors.Refresh(definition with { InlineData = literals, SourceSheet = string.Empty, SourceRange = string.Empty, SeriesSources = null });

        return anchors.Place(anchor, new PlacedObject
        {
            Id = frame?.GetFirstChild<Xdr.NonVisualGraphicFrameProperties>()
                ?.GetFirstChild<Xdr.NonVisualDrawingProperties>()?.Name?.Value
                 ?? definition.Id,
            Content = definition,
            Anchor = new CellAddress(0, 0),
        }, new DocumentSize(Twip.FromCentimeters(12), Twip.FromCentimeters(7)));
    }

    private (OpenXmlCompositeElement Anchor, PlacedObject Placed)? ChartEx(DrawingsPart drawings, OpenXmlElement alternate, int ordinal)
    {
        if (_chartEx.ChoiceAnchor(alternate) is not { } anchor || _chartEx.ChartId(anchor) is not { } id) return null;
        if (!drawings.TryGetPartById(id, out var part) || _chartEx.Read(part) is not { } content) return null;

        var definition = ChartExDefinition(content, $"grafico{ordinal}");
        var name = anchor.Descendants<Xdr.NonVisualDrawingProperties>().FirstOrDefault()?.Name?.Value;
        return (anchor, new PlacedObject { Id = string.IsNullOrEmpty(name) ? definition.Id : name, Content = definition, Anchor = new CellAddress(0, 0) });
    }

    private ChartDefinition ChartExDefinition(ChartExContent content, string id)
    {
        var literalNames = content.Series.Any(series => series.NameFormula is null && !string.IsNullOrEmpty(series.Name))
            ? content.Series.Select(series => series.NameFormula is null ? series.Name ?? string.Empty : string.Empty).ToList()
            : null;
        var formulas = content.Series.SelectMany(series => new[] { series.NameFormula, series.Values, series.Categories })
            .OfType<string>().SelectMany(_rangeFormulas.Parts).Where(text => text.Contains('!')).ToList();

        if (content.Series.All(series => series.Values is null) || formulas.Count == 0)
        {
            var categories = content.Series[0].CachedCategories.Count > 0
                ? content.Series[0].CachedCategories
                : [.. Enumerable.Range(1, content.Series.Max(series => series.CachedValues.Count)).Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture))];
            var data = new ChartData(categories, [.. content.Series.Select((series, index) => new ChartSeries(series.Name ?? $"Serie {index + 1}", series.CachedValues))])
            {
                OrdinalCategories = content.Series[0].CachedCategories.Count == 0,
            };
            return new ChartDefinition
            {
                Id = id, Type = content.Type, SourceSheet = string.Empty, SourceRange = string.Empty,
                InlineData = data, ChartTitle = content.Title, Legend = content.Legend,
            };
        }

        var sheet = SheetOf(formulas[0]);
        var first = content.Series[0];
        var compact = _orientation.Infer(first.NameFormula is not null, first.Categories is not null, first.Values, new ChartDefinition
        {
            Id = id,
            Type = content.Type,
            SourceSheet = sheet,
            SourceRange = _union.Of([.. formulas.Where(formula => string.Equals(SheetOf(formula), sheet, StringComparison.CurrentCultureIgnoreCase))]),
        });
        var sources = content.Series
            .Select(series => new ChartSeriesSource(_rangeFormulas.Model(series.NameFormula), _rangeFormulas.Model(series.Values), _rangeFormulas.Model(series.Categories)))
            .ToList();
        return _layout.Settle(compact, sources.Any(source => source.Values is not null) ? sources : []) with
        {
            ChartTitle = content.Title,
            Legend = content.Legend,
            SeriesNames = literalNames,
        };
    }

    private static bool IsStatic(ChartDefinition definition) =>
        definition.InlineData is not null && !definition.HasSeriesSources && string.IsNullOrEmpty(definition.SourceSheet);

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
        return _rangeFormulas.Model(text);
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

internal sealed class XlsxPivotChartLink
{
    private const string Chart2010Namespace = "http://schemas.microsoft.com/office/drawing/2007/8/2/chart";
    private const string PivotOptionsUri = "{781A3756-C4B2-4CAC-9D66-4F8BD8637D16}";

    private readonly PulseOffice.Sheets.Engine.Parsing.SheetNameQuoting _quoting = new();

    public void Write(C.ChartSpace space, ChartPivotSource? pivot, string sheet)
    {
        if (pivot is null) return;

        var name = string.IsNullOrEmpty(sheet) ? $"[]{pivot.PivotName}" : $"[]{_quoting.Quoted(sheet)}!{pivot.PivotName}";
        var source = new C.PivotSource(
            new C.PivotTableName(name),
            new C.FormatId { Val = (uint)Math.Max(0, pivot.FormatId) });

        if (space.GetFirstChild<C.Chart>() is { } chart)
        {
            space.InsertBefore(source, chart);
            var formats = new C.PivotFormats();
            var before = chart.ChildElements.FirstOrDefault(child => child.LocalName is "view3D" or "floor" or "sideWall" or "backWall" or "plotArea");
            if (before is null) chart.AppendChild(formats);
            else chart.InsertBefore(formats, before);
        }
        else space.AppendChild(source);

        if (!pivot.ShowFieldButtons) return;

        var extensions = space.GetFirstChild<C.ChartSpaceExtensionList>() ?? space.AppendChild(new C.ChartSpaceExtensionList());
        var extension = new C.ChartSpaceExtension { Uri = PivotOptionsUri };
        var options = new OpenXmlUnknownElement("c14", "pivotOptions", Chart2010Namespace);
        foreach (var zone in new[] { "dropZoneFilter", "dropZoneCategories", "dropZoneData", "dropZoneSeries", "dropZonesVisible" })
        {
            var element = new OpenXmlUnknownElement("c14", zone, Chart2010Namespace);
            element.SetAttribute(new OpenXmlAttribute("val", string.Empty, "1"));
            options.AppendChild(element);
        }
        extension.AppendChild(options);
        extensions.AppendChild(extension);
    }

    public ChartPivotSource? Read(C.ChartSpace space)
    {
        if (space.GetFirstChild<C.PivotSource>() is not { } source) return null;
        var text = source.ChildElements.FirstOrDefault(child => child.LocalName == "name")?.InnerText?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text[0] == '[' && text.IndexOf(']', StringComparison.Ordinal) is var close and > 0) text = text[(close + 1)..];
        var separator = text.LastIndexOf('!');
        var name = separator >= 0 ? text[(separator + 1)..] : text;
        if (name.Length == 0) return null;

        var formatText = source.ChildElements.FirstOrDefault(child => child.LocalName == "fmtId")?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val").Value;
        var buttons = space.Descendants().Where(element => element.LocalName == "pivotOptions").SelectMany(options => options.ChildElements)
            .Any(zone => zone.GetAttributes().Any(attribute => attribute.LocalName == "val" && attribute.Value is "1" or "true"));

        return new ChartPivotSource(name)
        {
            FormatId = int.TryParse(formatText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var format) && format >= 0 ? format : 0,
            ShowFieldButtons = buttons,
        };
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
