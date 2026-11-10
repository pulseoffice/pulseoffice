using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using static PulseOffice.Formats.Packaging.OdfPackage;

using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdsReader
{
    private readonly OdfCellStyleReader _cellStyles = new();
    private readonly NumberFormatLocale _locale = new();
    private readonly OdfLayoutMapper _layout = new();
    private readonly OdsLinkedPictures _linkedPictures = new();
    private readonly OdsGroupedObjects _groupedObjects = new();
    private readonly OdsObjectLinks _objectLinks = new();
    private readonly OdfPictureInliner _fillImages = new();
    private readonly OdsNamedExpressionReader _names = new();
    private readonly FlatOdfUnpacker _flat = new();
    private readonly Formats.Packaging.Encryption.OdfPackageEncryption _encryption = new();
    private readonly Formats.Packaging.Legacy.LegacyOdfUpgrader _legacy = new();
    private readonly OdsTableReader _tables;
    private readonly OdsContentLoader _loader;
    private readonly IReadOnlyList<IOdsSheetPartReader> _parts;

    public OdsReader()
    {
        var rows = new TableRows();
        var cells = new TableCells();
        var images = new OdfImageMapper();
        var formulas = new OdfFormulaMapper();
        var paragraphs = new OdfParagraphText();

        _tables = new OdsTableReader(rows, cells, new OdsCellReader(new OdfStyleNames(), paragraphs));
        _loader = new OdsContentLoader(rows, new OdsRowCompactor(rows));
        _parts =
        [
            new ConditionalPartReader(new OdfConditionalMapper()),
            new ObjectPartReader((table, context) => images.ReadImages(table, context.Package, context.Content)),
            new ObjectPartReader((table, context) =>
            {
                _fillImages.Inline(context.Content, context.Package.ReadBytes);
                _fillImages.Inline(context.Styles, context.Package.ReadBytes);
                return new OdfShapeMapper().ReadShapes(table, context.Content, context.Styles, context.Package.ReadBytes);
            }),
            new ObjectPartReader((table, context) => new OdfChartMapper().ReadCharts(table, context.Package)),
            new ObjectPartReader((table, context) => formulas.Read(table, context.Package)),
            new ObjectPartReader((table, _) => new OdfFormControlMapper().ReadControls(table, table.Element(Namespaces.Office + "forms"))),
            new AltTextPartReader(new OdfAltTextMapper()),
            new ObjectLinkPartReader(_objectLinks),
            new CommentPartReader(cells, paragraphs),
            new MergePartReader(cells, rows),
            new ArrayFormulaPartReader(cells, rows),
            new SparklinePartReader(new OdfSparklineMapper()),
            new PrintRangePartReader(rows),
            new LayoutPartReader(rows, _layout),
            new AnchorNormalisingPartReader(new SheetPlacement()),
            new AnchoringPartReader(),
        ];
    }

    public OperationResult<Workbook> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            if (_legacy.IsLegacy(stream))
            {
                using var upgraded = _legacy.Upgrade(stream);
                return Read(upgraded);
            }

            if (_encryption.RequiresOpenPgp(stream))
                return OperationResult<Workbook>.Fail(Diagnostic.Error("ODF030", "The document is encrypted for OpenPGP recipients: it opens only with the secret key of a recipient."));
            if (_encryption.IsEncrypted(stream))
                return OperationResult<Workbook>.Fail(Diagnostic.Error("ODF004", "The document is protected by a password."));
            if (!_flat.IsFlat(stream)) return ReadPackage(stream);
            using var unpacked = _flat.Unpack(stream);
            return ReadPackage(unpacked);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or System.Xml.XmlException
            or FormatException or ArgumentException or InvalidOperationException or ArithmeticException)
        {
            return OperationResult<Workbook>.Fail(Diagnostic.Error("ODF003", $"Unreadable ODS package: {ex.Message}").With(ex.Message));
        }
    }

    private OperationResult<Workbook> ReadPackage(Stream stream)
    {
        var diagnostics = new List<Diagnostic>();
        using var package = OdfPackage.OpenRead(stream);

        var mimeType = package.ReadMimeType();
        if (mimeType is not null and not MimeTypes.Spreadsheet and not MimeTypes.SpreadsheetTemplate)
            return OperationResult<Workbook>.Fail(Diagnostic.Error("ODF001", $"The document is not a spreadsheet but '{mimeType}'.").With(mimeType));

        var workbook = new Workbook();
        var sheets = new StreamedSheets(this, package, workbook, diagnostics);
        using (var contentStream = package.OpenEntry("content.xml"))
        {
            if (contentStream is null)
                return OperationResult<Workbook>.Fail(Diagnostic.Error("ODF002", "The package does not contain content.xml."));
            sheets.Finish(_loader.Load(contentStream, sheets));
        }
        var content = sheets.Context.Content;
        var context = sheets.Context;

        var macroMapper = new OdfMacroMapper();
        workbook.MacroSignature = macroMapper.Read(package, workbook.Macros);
        if (macroMapper.BasicLoss(package) is { } basicLoss) diagnostics.Add(basicLoss);
        new OdfVersionMapper().Read(package, workbook.Versions);
        _names.Read(content, workbook);
        new OdsViewSettingsMapper().Read(package.ReadXml("settings.xml"), workbook);
        if (content.Root?.Element(Namespaces.Office + "body")?.Element(Namespaces.Office + "spreadsheet") is { } body)
            workbook.Calculation = new OdfCalculationSettingsMapper().Read(body);
        new OdfMetaMapper().Read(package.ReadXml("meta.xml"), workbook.Properties);
        workbook.Fonts = new OdfFontFaceReader().Read(package, content, package.ReadXml("styles.xml"));
        new OdsDefaultCellStyle().Read(package.ReadXml("styles.xml"), workbook);

        var tables = content.Descendants(Namespaces.Table + "table").ToList();
        if (tables.Select(table => table.Element(Namespaces.Office + "forms")).OfType<System.Xml.Linq.XElement>().FirstOrDefault() is { } forms
            && new OdfFormControlMapper().ReadSettings(forms) is { } formSettings)
            workbook.FormSettings = formSettings;
        if (tables.Count == 0) diagnostics.Add(Diagnostic.Warning("ODF010", "The document contains no sheets."));
        if (_linkedPictures.Present(content, package.ReadBytes)) diagnostics.Add(_linkedPictures.Loss);
        if (_groupedObjects.Present(content)) diagnostics.Add(_groupedObjects.Loss);
        if (_objectLinks.Present(content)) diagnostics.Add(_objectLinks.Loss);

        var scenarios = new OdfScenarioMapper();
        foreach (var table in tables.Where(table => !scenarios.IsScenario(table)))
        {
            if (sheets.SheetOf(table) is not { } sheet) continue;
            new OdfProtectionMapper().Read(table, sheet);
            new OdfTableStyleMapper().Read(table, content, sheet);
            new OdfSheetEventMapper().Read(table, sheet);
            _objectLinks.Unwrap(table);
            foreach (var part in _parts) part.Read(table, sheet, context);
        }

        Worksheet? previous = null;
        foreach (var table in tables)
        {
            if (scenarios.IsScenario(table)) scenarios.Read(table, workbook, previous);
            else previous = workbook.FindSheet(table.Attribute(Namespaces.Table + "name")?.Value ?? string.Empty) ?? previous;
        }

        if (content.Root?.Descendants(Namespaces.Office + "spreadsheet").FirstOrDefault() is { } spreadsheetElement)
        {
            new OdfProtectionMapper().Read(spreadsheetElement, workbook);
            new OdfDatabaseRangeMapper().Read(spreadsheetElement, workbook);
            new OdfLabelRangeMapper().Read(spreadsheetElement, workbook);
            new OdfConsolidationMapper().Read(spreadsheetElement, workbook);
            new OdfTrackedChangesMapper().Read(spreadsheetElement, workbook, package.ReadXml("settings.xml"));
        }
        new OdfSheetMetadataMapper().ReadValidations(content.Root!, workbook);
        var pivots = new OdfPivotMapper().Read(content.Root!, workbook);
        if (pivots.ServicePasswords > 0)
            diagnostics.Add(Diagnostic.Warning("ODF080",
                $"{pivots.ServicePasswords} pivot table data source services carry a password: credentials never enter the document, the password is dropped and is asked for again when refreshing.").With(pivots.ServicePasswords));
        if (pivots.SourceFilters > 0)
            diagnostics.Add(Diagnostic.Warning("ODF081",
                $"{pivots.SourceFilters} standard filters on pivot table sources are not kept: the pivot table is computed from the whole source range.").With(pivots.SourceFilters));
        new OdfExternalDataMapper().Read(content.Root!, workbook);

        if (workbook.Sheets.Count == 0) workbook.AddSheet(workbook.UniqueSheetName(Workbook.CanonicalSheetPrefix));
        return OperationResult<Workbook>.Ok(workbook, diagnostics);
    }

    private void ReadStyles(OdsReadContext context)
    {
        var workbook = context.Workbook;
        var numbers = new OdfNumberStyleReader(context.Content, context.Styles);
        int NumberFormat(string name) => numbers.CodeOf(name) is { } code && _locale.Strip(code) != "General" ? workbook.NumberFormats.Register(code) : 0;
        context.StyleIds = _cellStyles.ReadStyles(context.Content, NumberFormat, context.Styles)
            .ToDictionary(pair => pair.Key, pair => workbook.Styles.Intern(pair.Value), StringComparer.Ordinal);
        context.NamedStyles = context.Styles is { } styles
            ? _cellStyles.ReadStyles(styles, NumberFormat, styles)
            : new Dictionary<string, PulseOffice.Sheets.Styling.CellStyle>(StringComparer.Ordinal);
        context.Parents = _cellStyles.ReadParents(context.Content);
        context.StyleLinks = new OdfCellStyleHyperlink().Read(context.Content);
        context.ColumnWidths = _layout.ReadColumnWidths(context.Content);
        context.RowHeights = _layout.ReadRowHeights(context.Content);
        context.OptimalRows = _layout.ReadOptimalRows(context.Content);
        context.RowBreakStyles = _layout.ReadBreaks(context.Content, "table-row", "table-row-properties");
        context.ColumnBreakStyles = _layout.ReadBreaks(context.Content, "table-column", "table-column-properties");

        var named = context.Styles is { } common ? _cellStyles.ReadNamed(common, NumberFormat) : [];
        foreach (var style in named)
            workbook.NamedStyles.Define(style.Name, style.Style, style.Parent, workbook.NamedStyles.Find(style.Name)?.IsBuiltIn ?? false, style.Declared);
        context.NamedStyleNames = named.ToDictionary(style => style.EncodedName, style => style.Name, StringComparer.Ordinal);
    }

    private sealed class StreamedSheets(OdsReader reader, OdfPackage package, Workbook workbook, List<Diagnostic> diagnostics) : IOdsRowSink
    {
        private readonly Dictionary<XElement, OdsTableState> _states = [];
        private readonly HashSet<XElement> _scenarios = [];
        private readonly OdfScenarioMapper _scenarioTables = new();
        private OdsReadContext? _context;

        public OdsReadContext Context => _context ?? throw new InvalidOperationException("The content was not loaded.");

        public void BodyStarted(XDocument content)
        {
            if (_context is not null) return;
            _context = new OdsReadContext(package, content, workbook, diagnostics);
            new OdfPageStyleMapper().Read(_context.Styles, workbook.PageStyles);
            reader.ReadStyles(_context);
        }

        public bool Row(XElement table, TableRow row)
        {
            if (_scenarios.Contains(table)) return false;
            if (!_states.TryGetValue(table, out var state))
            {
                if (_scenarioTables.IsScenario(table))
                {
                    _scenarios.Add(table);
                    return false;
                }
                _states[table] = state = reader._tables.Begin(table, Context);
            }
            reader._tables.ReadRow(state, row, Context);
            return true;
        }

        public void TableEnded(XElement table)
        {
            if (_states.ContainsKey(table) || _scenarios.Contains(table) || _scenarioTables.IsScenario(table)) return;
            _states[table] = reader._tables.Begin(table, Context);
        }

        public void Finish(XDocument content) => BodyStarted(content);

        public Worksheet? SheetOf(XElement table) => _states.GetValueOrDefault(table)?.Sheet;
    }
}
