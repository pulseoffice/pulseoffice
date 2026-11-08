using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Sheet = DocumentFormat.OpenXml.Spreadsheet.Sheet;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxWriter
{
    private readonly OoxmlPendingImages _pendingImages = new();

    private readonly XlsxDrawingMapper _drawings;
    private readonly XlsxMetadataMapper _metadata = new();
    private readonly XlsxSparklineMapper _sparklines = new();
    private readonly XlsxScenarioMapper _scenarios = new();
    private readonly XlsxFormControlMapper _controls = new();
    private readonly XlsxFormulaMapper _formulas = new();
    private readonly IReadOnlyList<WorksheetSection> _sections;
    private readonly SheetDataSection _sheetData = new(new XlsxDataTableWriter(new XlsxFormulaSyntax()));
    private readonly XlsxVmlDocuments _vml = new();
    private readonly XlsxCommentMapper _comments;
    private readonly XlsxSignatureLineMapper _signatures;
    private readonly XlsxAutoFilterCodec _autoFilter = new();

    private readonly XlsxSortStateCodec _sorts = new();
    private readonly XlsxConsolidationCodec _consolidation = new();
    private readonly XlsxRangeFilters _rangeFilters = new();
    private readonly XlsxTableWriter _tables = new(new XlsxAutoFilterCodec(), new XlsxFormulaSyntax());
    private readonly XlsxPageSetupCodec _pages = new();
    private readonly OoxmlProtectionKeys _keys = new();
    private readonly XlsxPivotMapper _pivots = new();
    private readonly XlsxQueryTableMapper _queries = new();

    public XlsxWriter()
        : this(new SignatureLineImage())
    {
    }

    public XlsxWriter(SignatureLineImage signatureImages)
        : this(signatureImages, new VectorFallback())
    {
    }

    public XlsxWriter(SignatureLineImage signatureImages, VectorFallback vectors)
    {
        _drawings = new XlsxDrawingMapper(new XlsxChartMapper(), new XlsxShapeMapper(), new XlsxVectorMapper(), new XlsxImageMapper(vectors));
        _comments = new XlsxCommentMapper(_vml);
        _signatures = new XlsxSignatureLineMapper(new VmlSignatureLineCodec(signatureImages), new VmlStyle(), new XlsxVmlAnchor(new PulseOffice.Sheets.SheetPlacement()), _vml);
        _sections =
        [
            new SheetPropertiesSection(),
            new SheetViewSection(),
            new SheetFormatSection(),
            new ColumnsSection(),
            _sheetData,
            new ElementSection((sheet, _) => sheet.Protection.IsEnabled
                ? new SheetProtection
                {
                    Sheet = true,
                    Objects = true,
                    Scenarios = true,
                    FormatCells = !sheet.Protection.AllowFormatCells,
                    InsertRows = !sheet.Protection.AllowInsertRows,
                    InsertColumns = !sheet.Protection.AllowInsertColumns,
                    DeleteRows = !sheet.Protection.AllowDeleteRows,
                    DeleteColumns = !sheet.Protection.AllowDeleteColumns,
                    Sort = !sheet.Protection.AllowSort,
                    AutoFilter = !sheet.Protection.AllowFilter,
                    SelectLockedCells = sheet.Protection.AllowSelectLockedCells ? null : true,
                    SelectUnlockedCells = sheet.Protection.AllowSelectUnlockedCells ? null : true,
                    AlgorithmName = _keys.Decode(sheet.Protection.PasswordHash)?.Algorithm,
                    SpinCount = _keys.Decode(sheet.Protection.PasswordHash)?.SpinCount,
                    SaltValue = _keys.Decode(sheet.Protection.PasswordHash)?.Salt,
                    HashValue = _keys.Decode(sheet.Protection.PasswordHash)?.Hash,
                    Password = _keys.Decode(sheet.Protection.PasswordHash)?.LegacyPassword,
                }
                : null),
            new ElementSection((sheet, _) => _scenarios.Build(sheet)),
            new ElementSection((sheet, context) => context.Tables.CarriesSheetFilter(IndexOf(context.Workbook, sheet)) ? null
                : _autoFilter.Build(context.Workbook, sheet, context.Conditionals)
                    ?? (_rangeFilters.SheetAutoFilter(context.Workbook, sheet) is { } stored ? _autoFilter.Build(stored.Range, stored.Columns, context.Conditionals, new XlsxFilterSource(context.Workbook, sheet, true)) : null)),
            new ElementSection((sheet, _) => _sorts.Build(sheet.RangeSort)),
            new ElementSection((sheet, context) => _consolidation.Build(context.Workbook, sheet)),
            new MergesSection(),
            new ElementsSection((sheet, context) => context.Conditionals.Build(sheet.ConditionalRules)),
            new ElementSection((sheet, _) => _metadata.BuildValidations(sheet)),
            new HyperlinksSection(),
            new ElementsSection((sheet, context) => _pages.Build(sheet, context.Workbook)),
            new PartSection((part, sheet, context) => _drawings.Write(part, sheet, context.Workbook)),
            new PartSection((part, sheet, context) =>
            {
                _comments.Write(part, sheet);
                var shapes = new XlsxVmlShapes(IndexOf(context.Workbook, sheet) + 1);
                var controls = _controls.Assign(sheet, shapes);
                _vml.Write(part, shapes, vml => _comments.Shapes(sheet, shapes).Concat(_signatures.Shapes(vml, sheet, shapes)).Concat(_controls.Shapes(sheet, controls, shapes)));
                _controls.Write(part, sheet, controls);
            }),
            new PartSection(_tables.Write),
            new ElementSection((sheet, context) => Extensions(sheet, context.Conditionals.Extensions, _sparklines)),
        ];
    }

    private WorksheetExtensionList? Extensions(PulseOffice.Sheets.Worksheet sheet, params IWorksheetExtensionSource[] sources)
    {
        var extensions = sources.Select(source => source.Extension(sheet)).OfType<WorksheetExtension>().ToList();
        return extensions.Count == 0 ? null : new WorksheetExtensionList(extensions);
    }

    public void Write(Workbook workbook, Stream stream) => Write(workbook, stream, SpreadsheetDocumentType.Workbook);

    public void Write(Workbook workbook, Stream stream, SpreadsheetDocumentType type)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(stream);

        using var document = SpreadsheetDocument.Create(stream, type);

        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();

        var context = new XlsxWriteContext(workbook, new XlsxStyleMapper(), new XlsxConditionalMapper(), new SharedStrings());
        context.Styles.DeclareNamedStyles(workbook.NamedStyles, workbook);

        if (workbook.Protection.Structure)
        {
            var key = _keys.Decode(workbook.Protection.PasswordHash);
            workbookPart.Workbook.AppendChild(new WorkbookProtection
            {
                LockStructure = true,
                LockWindows = workbook.Protection.Windows,
                WorkbookAlgorithmName = key?.Algorithm,
                WorkbookSpinCount = key?.SpinCount,
                WorkbookSaltValue = key?.Salt,
                WorkbookHashValue = key?.Hash,
                WorkbookPassword = key?.LegacyPassword,
            });
        }

        workbookPart.Workbook.AppendChild(new BookViews(new WorkbookView
        {
            ActiveTab = (uint)Math.Max(0, workbook.Sheets.ToList().FindIndex(sheet => !sheet.IsHidden)),
        }));
        var sheets = workbookPart.Workbook.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Sheets());
        var sheetId = 1u;
        var parts = new Dictionary<PulseOffice.Sheets.Worksheet, WorksheetPart>();

        foreach (var sheet in workbook.Sheets)
        {
            var part = workbookPart.AddNewPart<WorksheetPart>();
            part.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet();

            foreach (var section in _sections) section.Write(part, sheet, context);
            _sheetData.Stream(part, sheet, context);
            parts[sheet] = part;

            sheets.AppendChild(new Sheet { Id = workbookPart.GetIdOfPart(part), SheetId = sheetId++, Name = sheet.Name,
                State = sheet.IsHidden ? SheetStateValues.Hidden : null });
        }

        var definedNames = _metadata.BuildDefinedNames(workbook).Concat(_queries.DefinedNames(workbook)).Concat(_rangeFilters.DefinedNames(workbook)).ToList();
        DocumentFormat.OpenXml.OpenXmlElement last = sheets;
        if (definedNames.Count > 0) last = workbookPart.Workbook.InsertAfter(new DefinedNames(definedNames), sheets);
        if (workbook.Calculation.Iterative
            || workbook.Calculation.MaxIterations != PulseOffice.Sheets.CalculationSettings.DefaultMaxIterations
            || workbook.Calculation.MaxChange != PulseOffice.Sheets.CalculationSettings.DefaultMaxChange)
            workbookPart.Workbook.InsertAfter(new CalculationProperties
            {
                CalculationId = 191029,
                Iterate = workbook.Calculation.Iterative,
                IterateCount = (uint)workbook.Calculation.MaxIterations,
                IterateDelta = workbook.Calculation.MaxChange,
            }, last);

        _formulas.Write(workbookPart, workbook);
        _queries.Write(workbookPart, workbook, parts);
        _pivots.Write(workbookPart, workbook, parts);
        if (context.UsesDynamicArrays) new XlsxDynamicArrays().Write(workbookPart);

        var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
        sharedStringPart.SharedStringTable = context.Strings.Table;
        sharedStringPart.SharedStringTable.Save();

        var stylePart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylePart.Stylesheet = context.Styles.Build();
        if (context.Conditionals.HasFormats) stylePart.Stylesheet.AppendChild(context.Conditionals.Build());
        stylePart.Stylesheet.Save();

        new OoxmlCorePropertiesMapper().Write(document, workbook.Properties);
        if (workbook.Properties.Language is null && workbook.DefaultLanguage is { } language) document.PackageProperties.Language = language;
        workbookPart.Workbook.Save();
        _pendingImages.Resolve(document);
    }

    private int IndexOf(Workbook workbook, PulseOffice.Sheets.Worksheet sheet)
    {
        for (var index = 0; index < workbook.Sheets.Count; index++)
            if (ReferenceEquals(workbook.Sheets[index], sheet)) return index;
        return 0;
    }
}
