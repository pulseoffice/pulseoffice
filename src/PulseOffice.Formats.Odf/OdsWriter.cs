using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdsWriter(bool template, SignatureLineImage signatureImages, VectorFallback vectors)
{
    public OdsWriter(bool template = false)
        : this(template, new SignatureLineImage())
    {
    }

    public OdsWriter(bool template, SignatureLineImage signatureImages)
        : this(template, signatureImages, new VectorFallback())
    {
    }

    private readonly OdfAltTextMapper _altText = new();
    private readonly OdfParagraphText _paragraphs = new();

    private readonly string _mimeType = template ? MimeTypes.SpreadsheetTemplate : MimeTypes.Spreadsheet;

    public sealed record Metadata(string? Title = null, string? Creator = null)
    {
        public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;
    }

    public void Write(Workbook workbook, Stream stream, Metadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(stream);

        using var package = OdfPackage.Create(stream, _mimeType);

        var builder = new ContentBuilder(workbook, _altText, package.Characters, _paragraphs, signatureImages, vectors);
        using (var body = builder.WriteBody())
            package.WriteXml("content.xml", writer => builder.WriteDocument(writer, body));
        package.WriteXml("styles.xml", new OdsStylesDocumentBuilder().Build(builder.ConditionalStyles, builder.ShapeDefinitions, workbook));
        package.WriteXml("meta.xml", BuildMeta(workbook.Properties, metadata));

        var entries = new List<(string, string)>
        {
            ("content.xml", "text/xml"),
            ("styles.xml", "text/xml"),
            ("meta.xml", "text/xml"),
        };

        foreach (var (path, document) in builder.Objects)
        {
            package.WriteXml($"{path}/content.xml", document);
            entries.Add(($"{path}/", path.StartsWith("Formula", StringComparison.Ordinal)
                ? "application/vnd.oasis.opendocument.formula"
                : MimeTypes.Chart));
            entries.Add(($"{path}/content.xml", "text/xml"));
        }

        foreach (var (path, mediaType, bytes) in builder.Pictures)
        {
            package.WriteBytes(path, bytes);
            entries.Add((path, mediaType));
        }

        entries.AddRange(new OdfFontFaceWriter().WriteAll(package, builder.FontFiles, workbook.Fonts, new OdsViewSettingsMapper().Build(workbook)));
        entries.AddRange(new OdfMacroMapper().Write(package, workbook.Macros, workbook.MacroSignature));
        entries.AddRange(new OdfVersionMapper().Write(package, workbook.Versions, MimeTypes.Spreadsheet));

        package.WriteManifest(_mimeType, entries);
    }

    private sealed class ContentBuilder(Workbook workbook, OdfAltTextMapper altText, XmlCharacterFilter characters, IOdfParagraphText paragraphs, SignatureLineImage signatureImages, VectorFallback vectors)
    {
        private readonly OdfStyleMapper _styles = new();
        private readonly OdfConditionalMapper _conditionals = new();
        private readonly OdfChartMapper _charts = new();
        private OdfShapeMapper? _shapeMapper;
        private OdfShapeMapper Shapes => _shapeMapper ??= new OdfShapeMapper(_images);
        private readonly OdsObjectLinks _objectLinks = new();
        private readonly OdsFrameAnchoring _frameAnchoring = new();
        private readonly OdfFormControlMapper _forms = new();
        private readonly OdfFormulaMapper _formulas = new();
        private readonly OdfImageMapper _images = new(signatureImages, vectors);
        private readonly OdfSheetMetadataMapper _metadata = new();
        private readonly OdfLayoutMapper _layout = new();
        private readonly OdfTableStyleMapper _tableStyles = new();
        private readonly OutlineNesting _nesting = new();
        private readonly OdsRowGrouping _rowGroups = new();
        private readonly OdsAnnotationBuilder _annotations = new(paragraphs);
        private readonly OdsNamedExpressionWriter _names = new();
        private OdsCellWriter? _cellWriter;
        private AnchoredFrameSet? _frames;

        private OdsCellWriter Cells => _cellWriter ??= new OdsCellWriter(_styles, paragraphs);

        private AnchoredFrameSet FrameSet => _frames ??= new AnchoredFrameSet(
        [
            new FrameSource(sheet => _charts.BuildFrames(sheet, workbook)),
            new FrameSource(Shapes.BuildFrames),
            new FrameSource(FormulaFrames),
            new FrameSource(_images.BuildFrames),
            new FrameSource(_forms.BuildFrames),
        ]);

        public IReadOnlyList<XElement> ConditionalStyles => _conditionals.NamedStyles;

        public IReadOnlyList<XElement> ShapeDefinitions => Shapes.Definitions;
        public IReadOnlyList<(string Path, XDocument Content)> Objects => [.. _charts.Objects, .. _formulaObjects];

        private readonly List<(string Path, XDocument Content)> _formulaObjects = [];

        public IReadOnlyList<(string Path, string MediaType, byte[] Content)> Pictures => _images.Pictures;

        private IReadOnlyDictionary<CellAddress, List<XElement>> FormulaFrames(Worksheet sheet)
        {
            var start = _formulaObjects.Count + 1;

            _formulaObjects.AddRange(_formulas.Documents(sheet, start));

            return _formulas.BuildFrames(sheet, start);
        }

        private readonly OdfExternalDataMapper _external = new();
        private readonly XmlWriterSettings _bodySettings = new() { Encoding = new UTF8Encoding(false), CloseOutput = false };

        public Stream WriteBody()
        {
            var file = new FileStream(Path.Combine(Path.GetTempPath(), $"pulseoffice-{Guid.NewGuid():N}.xml"), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None, 1 << 16, FileOptions.DeleteOnClose);
            try
            {
                using (var writer = XmlWriter.Create(file, _bodySettings)) WriteBody(writer);
                file.Position = 0;
                return file;
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        public void WriteDocument(XmlWriter writer, Stream body)
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("office", "document-content", Namespaces.Office.NamespaceName);
            DeclareNamespaces(writer);
            writer.WriteAttributeString("version", Namespaces.Office.NamespaceName, "1.3");
            Emit(writer, FontDeclarations());
            Emit(writer, new XElement(Namespaces.Office + "automatic-styles",
                _styles.Declarations, _layout.Declarations, _tableStyles.Declarations, Shapes.Declarations, _images.Declarations));

            using (var reader = XmlReader.Create(body, new XmlReaderSettings { CloseInput = false }))
            {
                reader.MoveToContent();
                writer.WriteStartElement(reader.LocalName, reader.NamespaceURI);
                if (!reader.IsEmptyElement)
                {
                    reader.Read();
                    while (reader.NodeType != XmlNodeType.EndElement) writer.WriteNode(reader, true);
                }
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        private void WriteBody(XmlWriter writer)
        {
            writer.WriteStartElement("office", "body", Namespaces.Office.NamespaceName);
            DeclareNamespaces(writer);

            var spreadsheet = new XElement(Namespaces.Office + "spreadsheet");
            new OdfProtectionMapper().Write(workbook, spreadsheet);
            if (new OdfTrackedChangesMapper().Build(workbook) is { } changes) spreadsheet.Add(changes);
            spreadsheet.Add(new OdfCalculationSettingsMapper().Build(workbook.Calculation));
            if (_metadata.BuildValidations(workbook) is { } validations) spreadsheet.Add(validations);
            if (new OdfLabelRangeMapper().Build(workbook) is { } labels) spreadsheet.Add(labels);
            Open(writer, spreadsheet);

            var scenarios = new OdfScenarioMapper();
            for (var sheetIndex = 0; sheetIndex < workbook.Sheets.Count; sheetIndex++)
            {
                var sheet = workbook.Sheets[sheetIndex];
                WriteTable(writer, sheet, sheetIndex);
                foreach (var scenario in scenarios.Build(sheet)) Emit(writer, scenario);
            }

            if (_names.Build(workbook) is { } names) Emit(writer, names);
            foreach (var element in _external.BuildWorkbookElements(workbook)) Emit(writer, element);
            if (new OdfDatabaseRangeMapper().Build(workbook) is { } databaseRanges) Emit(writer, databaseRanges);
            if (_external.BuildMappings(workbook) is { } mappings) Emit(writer, mappings);
            if (new OdfPivotMapper().Build(workbook) is { } pivots) Emit(writer, pivots);
            if (new OdfConsolidationMapper().Build(workbook) is { } consolidation) Emit(writer, consolidation);

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private IEnumerable<(string Prefix, XNamespace Namespace)> NamespaceDeclarations =>
        [
            ("office", Namespaces.Office), ("table", Namespaces.Table), ("text", Namespaces.Text),
            ("style", Namespaces.Style), ("fo", Namespaces.Fo), ("of", Namespaces.Of),
            ("calcext", Namespaces.Calcext), ("svg", Namespaces.Svg), ("draw", Namespaces.Draw),
            ("form", Namespaces.Form), ("ooo", Namespaces.Ooo), ("script", Namespaces.Script), ("dom", Namespaces.Dom),
            ("xlink", Namespaces.Xlink), ("dc", Namespaces.Dc), ("loext", Namespaces.Loext),
            ("number", Namespaces.Number), ("officeooo", OdfSheetEventMapper.OfficeExtension),
            ("tableooo", OdfTableStyleMapper.TableOoo),
        ];

        private void DeclareNamespaces(XmlWriter writer)
        {
            foreach (var (prefix, ns) in NamespaceDeclarations)
                writer.WriteAttributeString("xmlns", prefix, null, ns.NamespaceName);
        }

        private void Open(XmlWriter writer, XElement shell)
        {
            characters.Clean(shell);
            writer.WriteStartElement(shell.Name.LocalName, shell.Name.NamespaceName);
            foreach (var attribute in shell.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
                writer.WriteAttributeString(attribute.Name.LocalName, attribute.Name.NamespaceName, attribute.Value);
            foreach (var child in shell.Elements()) child.WriteTo(writer);
        }

        private void Emit(XmlWriter writer, XElement element)
        {
            characters.Clean(element);
            element.WriteTo(writer);
        }

        private Worksheet? _currentSheet;

        private PlacedObjectIndex? _placedObjects;

        private OdfSheetValidationNames? _validationNames;

        public IReadOnlyList<OdfFontFile> FontFiles { get; private set; } = [];

        private XElement FontDeclarations()
        {
            var (declarations, files) = new OdfFontFaceWriter().Build(
                _styles.FontFamilies.Concat(new PulseOffice.Sheets.WorkbookFontNames().Used(workbook)), workbook.Fonts);
            FontFiles = files;
            return declarations;
        }

        private IEnumerable<XElement> BuildColumns(Worksheet sheet, int count)
        {
            var columns = new List<XElement>();

            for (var index = 0; index < count; index++)
            {
                var element = new XElement(Namespaces.Table + "table-column");
                var width = sheet.Layout.ColumnWidths.GetValueOrDefault(index);

                var pageBreak = sheet.ColumnBreaks.Contains(index);
                element.Add(new XAttribute(
                    Namespaces.Table + "style-name", _layout.ColumnStyle(width.Value > 0 ? width : sheet.Layout.DefaultColumnWidth, pageBreak)));
                if (width.Value <= 0 && sheet.Layout.ColumnWidths.ContainsKey(index))
                    element.Add(new XAttribute(Namespaces.Table + "visibility", "collapse"));
                AddDefaultStyle(element, sheet.Layout.ColumnStyle(index));

                columns.Add(element);
            }

            var nested = _nesting.Nest(columns, sheet.ColumnGroups, "table-column-group").ToList();
            var trailing = TrailingColumns(sheet, count).ToList();
            var covered = Math.Max(count, StyledWidth(sheet));
            if (covered < CellAddress.MaxColumns)
                trailing.Add(Repeated(new XElement(Namespaces.Table + "table-column"), CellAddress.MaxColumns - covered));
            foreach (var column in trailing.Where(column => column.Attribute(Namespaces.Table + "style-name") is null))
                column.Add(new XAttribute(Namespaces.Table + "style-name", _layout.ColumnStyle(sheet.Layout.DefaultColumnWidth, false)));
            var header = sheet.RepeatedPrintColumns;
            if (header <= 0) return [.. nested, .. trailing];

            var leading = nested.TakeWhile((element, index) => index < header && element.Name == Namespaces.Table + "table-column").ToList();
            if (leading.Count == 0) return [.. nested, .. trailing];
            return [new XElement(Namespaces.Table + "table-header-columns", leading), .. nested.Skip(leading.Count), .. trailing];
        }

        private IEnumerable<XElement> TrailingColumns(Worksheet sheet, int count)
        {
            var next = count;

            foreach (var run in sheet.Layout.ColumnStyles.RunsIn(count, CellAddress.MaxColumns - 1))
            {
                var start = Math.Max(run.First, count);
                var end = run.Last;
                var style = run.Value;

                if (start > next) yield return Repeated(new XElement(Namespaces.Table + "table-column"), start - next);

                var element = new XElement(Namespaces.Table + "table-column");
                AddDefaultStyle(element, style);
                yield return Repeated(element, end - start + 1);
                next = end + 1;
            }
        }

        private XElement Repeated(XElement element, int count)
        {
            if (count > 1)
                element.Add(new XAttribute(Namespaces.Table + "number-columns-repeated", count.ToString(CultureInfo.InvariantCulture)));
            return element;
        }

        private void AddDefaultStyle(XElement element, int? styleId)
        {
            if (styleId is { } id && Cells.DefaultStyleName(id, workbook) is { } name)
                element.Add(new XAttribute(Namespaces.Table + "default-cell-style-name", name));
        }

        private void WriteTable(XmlWriter writer, Worksheet sheet, int sheetIndex)
        {
            _currentSheet = sheet;
            _placedObjects = sheet.Objects.Count > 0 ? new PlacedObjectIndex(sheet.Objects) : null;
            _validationNames = _metadata.ValidationNames(workbook, sheet);

            var table = new XElement(Namespaces.Table + "table",
                new XAttribute(Namespaces.Table + "name", sheet.Name));
            _tableStyles.Write(sheet, table);
            new OdfProtectionMapper().Write(sheet, table);
            if (new OdfSheetEventMapper().Build(sheet) is { } events) table.Add(events);

            if (sheet.PrintRanges.Count > 0)
                table.Add(new XAttribute(Namespaces.Table + "print-ranges", string.Join(' ',
                    sheet.PrintRanges.Select(range => new OdfFormulaSyntax()
                        .ToOdfExpression($"='{sheet.Name.Replace("'", "''", StringComparison.Ordinal)}'.{range.Start.ToA1()}:{range.End.ToA1()}")
                        .Trim('[', ']')))));

            if ((_forms.BuildForms(sheet, sheet.Name + "_form", workbook.FormSettings)
                ?? (sheetIndex == 0 ? _forms.SettingsOnly(workbook.FormSettings, sheet.Name + "_form") : null)) is { } forms)
                table.Add(forms);

            var frames = FrameSet.Collect(sheet);
            if (_frameAnchoring.Arrange(sheet, frames) is { } shapes)
            {
                if (_placedObjects is { } pagePlaced)
                {
                    altText.WriteByName(shapes, pagePlaced.Content);
                    _objectLinks.Wrap(shapes, pagePlaced.Links);
                }
                table.Add(shapes);
            }
            Open(writer, table);

            var sources = _external.AreaLinkSources(workbook, sheetIndex);
            var index = new OdsRowIndex(sheet, frames.Keys.Concat(sources.Keys));
            var lastRow = new[] { index.LastRow }
                .Concat(sheet.Layout.RowHeights.Runs.Select(run => run.Last))
                .Concat(sheet.Layout.AutomaticRowHeights.Runs.Select(run => run.Last))
                .Concat(sheet.Layout.OptimalRows.Runs.Select(run => run.Last))
                .Concat(sheet.Layout.RowStyles.Runs.Select(run => run.Last))
                .Append(sheet.Layout.StyleBlocks.LastRow)
                .Concat(sheet.RowGroups.Select(group => group.Last))
                .Concat(sheet.RowBreaks)
                .Max();
            var columnCount = new[] { index.ColumnCount }
                .Concat(sheet.Layout.ColumnWidths.Runs.Select(run => run.Last + 1))
                .Concat(sheet.ColumnGroups.Select(group => group.Last + 1))
                .Concat(sheet.ColumnBreaks.Select(column => column + 1))
                .Append(sheet.RepeatedPrintColumns)
                .Max();

            foreach (var column in BuildColumns(sheet, Math.Max(columnCount, 1))) Emit(writer, column);

            if (lastRow < 0)
            {
                var emptyRows = Padded(sheet, RepeatedEmptyRows(CellAddress.MaxRows), -1);
                emptyRows.Add(new XAttribute(Namespaces.Table + "style-name", _layout.RowStyle(sheet.Layout.DefaultRowHeight, false, !sheet.Layout.DefaultRowHeightManual)));
                Emit(writer, emptyRows);
            }
            else
            {
                _rowGroups.Write(writer, RowSpans(sheet, index, lastRow, frames, sources), sheet.RowGroups, sheet.RepeatedPrintRows,
                    element => Emit(writer, element));
            }

            if (_names.Build(workbook, sheet.Name) is { } local) Emit(writer, local);
            if (_conditionals.Build(sheet) is { } rules) Emit(writer, rules);
            if (new OdfSparklineMapper().Build(sheet) is { } sparklines) Emit(writer, sparklines);

            writer.WriteEndElement();
        }

        private IEnumerable<OdsRowSpan> RowSpans(
            Worksheet sheet,
            OdsRowIndex index,
            int lastRow,
            IReadOnlyDictionary<CellAddress, List<XElement>> frames,
            IReadOnlyDictionary<CellAddress, List<XElement>> sources)
        {
            var header = sheet.RepeatedPrintRows;
            var boundaries = new SortedSet<int>(sheet.RowGroups.SelectMany(group => new[] { group.First, group.Last + 1 }));
            if (header > 0) boundaries.Add(header);

            bool Significant(int row) =>
                index.Contains(row) || sheet.Layout.RowHeights.ContainsKey(row)
                || sheet.Layout.AutomaticRowHeights.ContainsKey(row) || sheet.Layout.OptimalRows.Contains(row) || sheet.RowBreaks.Contains(row)
                || sheet.Layout.RowStyles.ContainsKey(row) || sheet.Layout.StyleBlocks.TouchesRow(row);

            for (var row = 0; row <= lastRow;)
            {
                if (Significant(row))
                {
                    var element = index.Columns(row) is { } columns
                        ? BuildRow(row, columns, frames, sources)
                        : new XElement(Namespaces.Table + "table-row", new XElement(Namespaces.Table + "table-cell"));
                    var shape = RowShape(sheet, row);
                    element.Add(new XAttribute(Namespaces.Table + "style-name", shape.StyleName));
                    if (shape.Collapsed) element.Add(new XAttribute(Namespaces.Table + "visibility", "collapse"));

                    var last = row;
                    var banded = index.Contains(row);
                    var limit = banded ? Math.Min(lastRow, index.NextBandBoundary(row) - 1) : lastRow;
                    if (!index.HasPoints(row) && !sheet.RowBreaks.Contains(row))
                        while (last < limit && !index.HasPoints(last + 1) && (banded || !index.Contains(last + 1))
                               && !boundaries.Contains(last + 1) && Significant(last + 1) && RowShape(sheet, last + 1) == shape)
                            last++;
                    if (last > row)
                        element.Add(new XAttribute(Namespaces.Table + "number-rows-repeated", (last - row + 1).ToString(CultureInfo.InvariantCulture)));

                    yield return new OdsRowSpan(row, last, Padded(sheet, element, row));
                    row = last + 1;
                    continue;
                }

                var next = row + 1;
                while (next <= lastRow && !Significant(next) && !boundaries.Contains(next)) next++;

                yield return new OdsRowSpan(row, next - 1, DefaultStyled(sheet, Padded(sheet, RepeatedEmptyRows(next - row), next - row == 1 ? row : -1)));
                row = next;
            }

            if (lastRow < CellAddress.MaxRows - 1)
                yield return new OdsRowSpan(lastRow + 1, CellAddress.MaxRows - 1,
                    DefaultStyled(sheet, Padded(sheet, RepeatedEmptyRows(CellAddress.MaxRows - 1 - lastRow), -1)));
        }

        private sealed record OdsRowShape(string StyleName, bool Collapsed, int? RowStyle, string Blocks);

        private OdsRowShape RowShape(Worksheet sheet, int row)
        {
            var height = sheet.Layout.RowHeights.GetValueOrDefault(row);
            var kept = sheet.Layout.HiddenRowSize(row);
            var manual = !sheet.Layout.IsRowAutomatic(row);
            var shown = height.Value > 0 ? height
                : kept is { } keptHeight ? keptHeight
                : sheet.Layout.AutomaticRowHeights.TryGetValue(row, out var automatic) ? automatic
                : sheet.Layout.DefaultRowHeight;
            return new OdsRowShape(
                _layout.RowStyle(shown, sheet.RowBreaks.Contains(row), !manual),
                height.Value <= 0 && sheet.Layout.RowHeights.ContainsKey(row),
                sheet.Layout.RowStyle(row),
                sheet.Layout.StyleBlocks.IsEmpty ? string.Empty
                    : string.Join(';', sheet.Layout.StyleBlocks.RowRuns(row).Select(run => $"{run.First}-{run.Last}:{run.Value}")));
        }

        private XElement DefaultStyled(Worksheet sheet, XElement row)
        {
            row.Add(new XAttribute(Namespaces.Table + "style-name",
                _layout.RowStyle(sheet.Layout.DefaultRowHeight, false, !sheet.Layout.DefaultRowHeightManual)));
            return row;
        }

        private int StyledWidth(Worksheet sheet) =>
            sheet.Layout.ColumnStyles.Runs is { Count: > 0 } runs ? runs[^1].Last + 1 : 0;

        private XElement Padded(Worksheet sheet, XElement row, int index)
        {
            var target = index >= 0 && sheet.Layout.RowStyle(index) is not null ? CellAddress.MaxColumns
                : index >= 0 && sheet.Layout.StyleBlocks.RowRuns(index) is { Count: > 0 } runs ? Math.Max(StyledWidth(sheet), runs[^1].Last + 1)
                : StyledWidth(sheet);
            var covered = row.Elements()
                .Where(cell => cell.Name == Namespaces.Table + "table-cell" || cell.Name == Namespaces.Table + "covered-table-cell")
                .Sum(cell => int.TryParse((string?)cell.Attribute(Namespaces.Table + "number-columns-repeated"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var repeat) && repeat > 0 ? repeat : 1);

            if (target > covered)
                row.Add(new XElement(Namespaces.Table + "table-cell",
                    new XAttribute(Namespaces.Table + "number-columns-repeated", (target - covered).ToString(CultureInfo.InvariantCulture))));

            if (index >= 0 && !sheet.Layout.StyleBlocks.IsEmpty) SplitAtBlocks(sheet, row, index);

            if (index >= 0 && sheet.Layout.RowStyle(index) is { } rowStyle && Cells.DefaultStyleName(rowStyle, workbook) is { } name)
                foreach (var cell in row.Elements(Namespaces.Table + "table-cell").Where(cell => cell.Attribute(Namespaces.Table + "style-name") is null))
                    cell.Add(new XAttribute(Namespaces.Table + "style-name", name));

            return row;
        }

        private void SplitAtBlocks(Worksheet sheet, XElement row, int index)
        {
            var runs = sheet.Layout.StyleBlocks.RowRuns(index);
            if (runs.Count == 0) return;

            var column = 0;
            foreach (var cell in row.Elements().Where(cell => cell.Name == Namespaces.Table + "table-cell" || cell.Name == Namespaces.Table + "covered-table-cell").ToList())
            {
                var repeat = Repeat(cell);
                var first = column;
                var last = (int)Math.Min((long)first + repeat - 1, CellAddress.MaxColumns - 1);
                column = (int)Math.Min((long)first + repeat, int.MaxValue);
                if (cell.Name != Namespaces.Table + "table-cell" || cell.HasElements
                    || cell.Attributes().Any(attribute => attribute.Name != Namespaces.Table + "number-columns-repeated" && attribute.Name != _validationName))
                    continue;

                var touching = runs.Where(run => run.Last >= first && run.First <= last).ToList();
                if (touching.Count == 0) continue;

                var pieces = new List<XElement>();
                var cursor = first;
                foreach (var run in touching)
                {
                    var start = Math.Max(run.First, first);
                    var end = Math.Min(run.Last, last);
                    if (start > cursor) pieces.Add(Repeated(new XElement(Namespaces.Table + "table-cell"), start - cursor));
                    pieces.Add(Repeated(new XElement(Namespaces.Table + "table-cell",
                        new XAttribute(Namespaces.Table + "style-name", Cells.DefaultStyleName(run.Value, workbook) ?? "Default")), end - start + 1));
                    cursor = end + 1;
                }

                if (cursor <= last) pieces.Add(Repeated(new XElement(Namespaces.Table + "table-cell"), last - cursor + 1));
                if (cell.Attribute(_validationName) is { } validation)
                    foreach (var piece in pieces) piece.Add(new XAttribute(validation));
                cell.ReplaceWith(pieces);
            }
        }

        private readonly XName _validationName = Namespaces.Table + "content-validation-name";

        private int Repeat(XElement cell) =>
            int.TryParse((string?)cell.Attribute(Namespaces.Table + "number-columns-repeated"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var repeat) && repeat > 0 ? repeat : 1;

        private bool InheritsColumnStyle(Worksheet sheet, CellAddress address) =>
            sheet.Layout.RowStyle(address.Row) is null
            && !sheet.Layout.StyleBlocks.TryGetValue(address.Row, address.Column, out _)
            && sheet.Layout.ColumnStyle(address.Column) is { } column
            && (!sheet.TryGetCell(address, out var cell) || (!cell.ExplicitStyle && (cell.StyleId == column || !cell.HasStyle)));

        private XElement RepeatedEmptyRows(int count) =>
            new(Namespaces.Table + "table-row",
                new XAttribute(Namespaces.Table + "number-rows-repeated", count.ToString(CultureInfo.InvariantCulture)),
                new XElement(Namespaces.Table + "table-cell"));

        private XElement BuildRow(
            int rowIndex,
            IReadOnlyList<int> columns,
            IReadOnlyDictionary<CellAddress, List<XElement>> frames,
            IReadOnlyDictionary<CellAddress, List<XElement>> sources)
        {
            var row = new XElement(Namespaces.Table + "table-row");
            var previousColumn = -1;
            var sheet = _currentSheet!;

            foreach (var address in columns.Select(column => new CellAddress(rowIndex, column)))
            {
                var gap = address.Column - previousColumn - 1;
                if (gap > 0)
                {
                    var gapStart = new CellAddress(rowIndex, previousColumn + 1);
                    var filler = new XElement(Namespaces.Table + (sheet.Merges.At(gapStart) is null ? "table-cell" : "covered-table-cell"));
                    if (gap > 1) filler.Add(new XAttribute(Namespaces.Table + "number-columns-repeated", gap.ToString(CultureInfo.InvariantCulture)));
                    if (_validationNames?.For(gapStart) is { } gapValidation)
                        filler.Add(new XAttribute(_validationName, gapValidation));
                    row.Add(filler);
                }

                var merge = _currentSheet?.Merges.At(address);

                var element = sheet.TryGetCell(address, out var cell)
                    ? Cells.Write(cell with { StyleId = sheet.StyleIdAt(address) }, workbook)
                    : new XElement(Namespaces.Table + "table-cell");

                if (InheritsColumnStyle(sheet, address) && cell?.Hyperlink is not { Length: > 0 })
                    element.Attribute(Namespaces.Table + "style-name")?.Remove();

                if (cell is { HasStyle: true } && _currentSheet is { } styledSheet
                    && styledSheet.StyleIdAt(address) == Sheets.Styling.StyleCatalog.DefaultStyleId
                    && styledSheet.Layout.DefaultStyleAt(address) != Sheets.Styling.StyleCatalog.DefaultStyleId
                    && element.Attribute(Namespaces.Table + "style-name") is null)
                    element.Add(new XAttribute(Namespaces.Table + "style-name", "Default"));

                if (merge is { } region)
                {
                    if (region.Start == address)
                    {
                        element.Add(new XAttribute(
                            Namespaces.Table + "number-columns-spanned",
                            region.ColumnCount.ToString(CultureInfo.InvariantCulture)));
                        element.Add(new XAttribute(
                            Namespaces.Table + "number-rows-spanned",
                            region.RowCount.ToString(CultureInfo.InvariantCulture)));
                    }
                    else
                    {
                        element.Name = Namespaces.Table + "covered-table-cell";
                    }
                }

                if (_currentSheet is { } owner)
                {
                    if (_validationNames?.For(address) is { } validation)
                        element.Add(new XAttribute(
                            Namespaces.Table + "content-validation-name", validation));

                    if (owner.Comments.TryGetValue(address, out var comment))
                        element.AddFirst(_annotations.Build(comment));
                }

                if (frames.TryGetValue(address, out var anchored))
                    foreach (var frame in anchored)
                        element.Add(frame);

                if (sources.TryGetValue(address, out var linked) && element.Name == Namespaces.Table + "table-cell")
                    foreach (var source in linked)
                        element.AddFirst(source);

                row.Add(element);
                previousColumn = address.Column;
            }

            if (_placedObjects is { } placed)
            {
                altText.WriteByName(row, placed.Content);
                _objectLinks.Wrap(row, placed.Links);
            }
            return row;
        }
    }



    private XDocument BuildMeta(
        PulseOffice.Document.Metadata.DocumentProperties properties, Metadata? legacy) =>
        new OdfMetaMapper().Build(properties, legacy?.Title, legacy?.Creator, legacy?.Created);
}
