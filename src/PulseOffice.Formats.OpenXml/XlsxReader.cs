using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Addressing;
using Workbook = PulseOffice.Sheets.Workbook;
using M = System.Math;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxReader
{
    private readonly OoxmlAttributeSanitizer _sanitizer = new();
    private readonly OoxmlReadFailures _failures = new();
    private readonly XlsxStyleReader _styles = new();
    private readonly XlsxConditionalMapper _conditionals = new();
    private readonly XlsxFormControlMapper _controls = new();
    private readonly XlsxMetadataMapper _metadata = new();
    private readonly SharedStrings _strings = new();
    private readonly Xlsx1904DateShift _dateShift = new();
    private readonly XlsxLeapYearSerials _leapYear = new();
    private readonly IReadOnlyList<IXlsxSheetPartReader> _parts;

    public XlsxReader() =>
        _parts =
        [
            new SheetDataReader(_sanitizer),
            new HyperlinksReader(),
            new SheetPartReader((part, sheet, context) => new XlsxPageSetupCodec().Read(part, sheet, context.Workbook)),
            new SheetPartReader((part, sheet, _) =>
            {
                if (part.Worksheet?.GetFirstChild<SheetProtection>() is not { } protection || protection.Sheet?.Value != true) return;
                sheet.Protection = sheet.Protection with
                {
                    IsEnabled = true,
                    PasswordHash = new OoxmlProtectionKeys().Encode(new OoxmlProtectionKey(
                        protection.AlgorithmName?.Value, protection.SpinCount?.Value, protection.SaltValue?.Value, protection.HashValue?.Value, protection.Password?.Value)),
                    AllowFormatCells = protection.FormatCells?.Value == false,
                    AllowInsertRows = protection.InsertRows?.Value == false,
                    AllowInsertColumns = protection.InsertColumns?.Value == false,
                    AllowDeleteRows = protection.DeleteRows?.Value == false,
                    AllowDeleteColumns = protection.DeleteColumns?.Value == false,
                    AllowSort = protection.Sort?.Value == false,
                    AllowFilter = protection.AutoFilter?.Value == false,
                    AllowSelectLockedCells = protection.SelectLockedCells?.Value != true,
                    AllowSelectUnlockedCells = protection.SelectUnlockedCells?.Value != true,
                };
            }),
            new SheetPartReader((part, sheet, _) => new XlsxCommentMapper(new XlsxVmlDocuments()).Read(part, sheet)),
            new XlsxThreadedComments(new OoxmlTextEscape()),
            new SheetPartReader((part, sheet, context) =>
            {
                if (part.Worksheet is { } content)
                    foreach (var rule in _conditionals.ReadRules(content, context.Stylesheet)) sheet.ConditionalRules.Add(rule);
            }),
            new SheetPartReader((part, sheet, _) =>
            {
                foreach (var placed in new XlsxDrawingMapper().Read(part, sheet.Layout)) sheet.Objects.Add(placed);
            }),
            new SheetPartReader((part, sheet, _) => _controls.Read(part, sheet)),
            new SheetPartReader((part, _, context) =>
            {
                if (_unknownPresets.Present(part.DrawingsPart?.WorksheetDrawing) && !context.Diagnostics.Any(diagnostic => diagnostic.Code == _rectangles.Code))
                    context.Diagnostics.Add(_rectangles);
                if (_unreadTextLinks.Present(part.DrawingsPart?.WorksheetDrawing)
                    && !context.Diagnostics.Any(diagnostic => diagnostic.Code == _textLinks.Code))
                    context.Diagnostics.Add(_textLinks);
            }),
            new SheetPartReader((part, sheet, _) => new XlsxSignatureLineMapper(new VmlSignatureLineCodec(new SignatureLineImage()), new VmlStyle(), new XlsxVmlAnchor(new PulseOffice.Sheets.SheetPlacement()), new XlsxVmlDocuments()).Read(part, sheet)),
            new SheetPartReader((part, sheet, context) =>
            {
                if (part.Worksheet is not { } content) return;
                _metadata.ReadValidations(content, sheet);
                foreach (var merge in content.Descendants<MergeCell>())
                    if (merge.Reference?.Value is { Length: > 0 } reference && CellRange.TryParseA1(reference, out var region))
                        _overlappingMerges.Add(sheet.Merges, region, context.Diagnostics);
                new XlsxSparklineMapper().Read(content.GetFirstChild<WorksheetExtensionList>(), sheet);
                new XlsxScenarioMapper().Read(content.GetFirstChild<Scenarios>(), sheet);
                var formats = context.Stylesheet?.DifferentialFormats?.Elements<DifferentialFormat>().ToList() ?? [];
                _tableFilters.Read(content.GetFirstChild<AutoFilter>(), part, sheet, context.Workbook, formats);
                _consolidation.Read(content.GetFirstChild<DataConsolidate>(), sheet, context.Workbook);
                if (content.GetFirstChild<SheetProperties>()?.TabColor?.Rgb?.Value is { Length: >= 6 } tab)
                    sheet.TabColorHex = "#" + tab[^6..].ToUpperInvariant();
                var view = content.GetFirstChild<SheetViews>()?.GetFirstChild<SheetView>();
                sheet.RightToLeft = view?.RightToLeft?.Value == true;
                if (view?.GetFirstChild<Pane>() is { } pane && pane.State?.Value is { } paneState
                    && (paneState == PaneStateValues.Frozen || paneState == PaneStateValues.FrozenSplit))
                    sheet.Frozen = new PulseOffice.Sheets.FrozenPanes(
                        (int)M.Max(0, M.Min(pane.VerticalSplit?.Value ?? 0, CellAddress.MaxRows - 1)),
                        (int)M.Max(0, M.Min(pane.HorizontalSplit?.Value ?? 0, CellAddress.MaxColumns - 1)));
            }),
        ];

    private readonly Diagnostic _textLinks = Diagnostic.Warning("XLSX182",
        "Hyperlinks inside the text of freeforms, WordArt and other drawing objects of spreadsheets are not imported: the text is kept without its links. Hyperlinks in the text of preset shapes and text boxes, and on whole shapes and pictures, are kept.");

    private readonly XlsxUnreadTextLinks _unreadTextLinks = new();

    private readonly XlsxQueryTableMapper _queries = new();

    private readonly XlsxThemeColours _themeColours = new();

    private readonly UnknownPresetShapes _unknownPresets = new(new PulseOffice.Drawing.Shapes.ShapePresets());

    private readonly Diagnostic _rectangles = Diagnostic.Warning("XLSX183",
        "Shapes with a preset geometry name that is not part of the DrawingML standard are imported as rectangles.");

    private readonly OverlappingMerges _overlappingMerges = new("XLSX181");

    private readonly XlsxTableFilters _tableFilters = new(new XlsxAutoFilterCodec());

    private readonly XlsxConsolidationCodec _consolidation = new();

    public OperationResult<Workbook> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var diagnostics = new List<Diagnostic>();

        try
        {
            var damage = new List<PartDamage>();
            using var repaired = new OoxmlPackageRepair().Repair(stream, damage);
            diagnostics.AddRange(damage.Select(part => part.Kind != PartDamageKind.Skipped
                ? Diagnostic.Warning("XLSX049", $"The part '{part.Part}' was damaged: what could be read was recovered.").With(part.Part)
                : Diagnostic.Warning("XLSX050", $"The part '{part.Part}' was unreadable and was skipped.").With(part.Part)));
            using var document = SpreadsheetDocument.Open(repaired, isEditable: true, new OpenSettings { AutoSave = false });
            _sanitizer.Sanitize(document, part => part is WorksheetPart);

            var workbookPart = document.WorkbookPart;
            if (workbookPart?.Workbook?.Sheets is null)
                return OperationResult<Workbook>.Fail(Diagnostic.Error("XLSX001", "The document does not contain a valid workbook."));

            _themeColours.Resolve(workbookPart);
            var workbook = new Workbook();
            new OoxmlCorePropertiesMapper().Read(document, workbook.Properties);

            var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
            var numberCodes = NumberCodes(stylesheet);
            foreach (var (name, style) in _styles.ReadNamedStyles(stylesheet))
            {
                var builtIn = workbook.NamedStyles.Find(name) is { IsBuiltIn: true } existing ? existing : null;
                workbook.NamedStyles.Define(builtIn?.Name ?? name, style with { NumberFormatId = NumberFormatIdOf(workbook, numberCodes, style.NumberFormatId) },
                    builtIn?.Parent, builtIn is not null);
            }
            var context = new XlsxReadContext(
                workbook, _strings.Read(workbookPart),
                [.. _styles.ReadStyles(stylesheet).Select(style => style with { NumberFormatId = NumberFormatIdOf(workbook, numberCodes, style.NumberFormatId) }).Select(workbook.Styles.Intern)],
                _styles.ReadStyleNames(stylesheet), stylesheet, diagnostics)
            {
                DynamicArrays = new XlsxDynamicArrays().DynamicCells(workbookPart),
            };

            context.Tables.Load(workbookPart);
            var chartSheets = 0;
            var activeXControls = 0;
            foreach (var sheet in workbookPart.Workbook.Sheets.Elements<Sheet>())
            {
                var name = sheet.Name?.Value ?? workbook.UniqueSheetName(Workbook.CanonicalSheetPrefix);
                if (workbook.FindSheet(name) is not null)
                {
                    diagnostics.Add(Diagnostic.Warning("XLSX040", $"Duplicate sheet '{name}': renamed.").With(name));
                    var original = name;
                    for (var suffix = workbook.Sheets.Count + 1; workbook.FindSheet(name) is not null; suffix++)
                        name = $"{original}_{suffix}";
                }

                var target = workbook.AddSheet(name);
                target.IsHidden = sheet.State?.Value is { } state && state != SheetStateValues.Visible;
                if (sheet.Id?.Value is not { } relationshipId) continue;
                if (!workbookPart.TryGetPartById(relationshipId, out var sheetPart)) continue;
                if (sheetPart is ChartsheetPart chartsheet)
                {
                    foreach (var placed in new XlsxDrawingMapper().Read(chartsheet.DrawingsPart, target.Layout)) target.Objects.Add(placed);
                    chartSheets++;
                    continue;
                }
                if (sheetPart is not WorksheetPart part) continue;

                foreach (var reader in _parts) reader.Read(part, target, context);
                _queries.Read(workbookPart, part, target, workbook.Sheets.Count - 1);
                activeXControls += part.EmbeddedControlPersistenceParts.Count();
            }

            context.Tables.Publish(workbook);
            if (activeXControls > 0)
                diagnostics.Add(Diagnostic.Warning("XLSX089",
                    $"{activeXControls} ActiveX controls are not supported by design and were not kept: their shapes are dropped.").With(activeXControls));
            if (chartSheets > 0)
                diagnostics.Add(Diagnostic.Info("XLSX080",
                    $"{chartSheets} chart sheets were read as sheets holding their chart: on saving they become worksheets with the chart placed on them.").With(chartSheets));

            if (workbookPart.Workbook?.GetFirstChild<WorkbookProtection>() is { LockStructure.Value: true } locked)
                workbook.Protection = workbook.Protection with
                {
                    Structure = true,
                    Windows = locked.LockWindows?.Value == true,
                    PasswordHash = new OoxmlProtectionKeys().Encode(new OoxmlProtectionKey(
                        locked.WorkbookAlgorithmName?.Value, locked.WorkbookSpinCount?.Value, locked.WorkbookSaltValue?.Value, locked.WorkbookHashValue?.Value, locked.WorkbookPassword?.Value)),
                };

            _metadata.ReadDefinedNames(workbookPart, workbook);
            workbook.Calculation = workbook.Calculation with { BooleansAreNumbersInArrays = false };
            if (workbookPart.Workbook?.GetFirstChild<CalculationProperties>() is { } calculation)
                workbook.Calculation = new PulseOffice.Sheets.CalculationSettings
                {
                    BooleansAreNumbersInArrays = false,
                    Iterative = calculation.Iterate?.Value == true,
                    MaxIterations = calculation.IterateCount?.Value is { } count and > 0 ? (int)count : PulseOffice.Sheets.CalculationSettings.DefaultMaxIterations,
                    MaxChange = calculation.IterateDelta?.Value is { } delta and >= 0 ? delta : PulseOffice.Sheets.CalculationSettings.DefaultMaxChange,
                };
            new XlsxFormulaMapper().Read(workbookPart, workbook);
            if (new XlsxPivotMapper().Read(workbookPart, workbook) is var external and > 0)
                diagnostics.Add(Diagnostic.Warning("XLSX081",
                    $"{external} pivot tables draw on an OLAP cube, a consolidation of ranges, a non-database connection or a named range of another workbook: their results stay as values, the pivot table itself is not kept.").With(external));
            if (workbookPart.Workbook?.WorkbookProperties?.Date1904?.Value == true)
                diagnostics.Add(Diagnostic.Info("XLSX057",
                    $"The workbook uses the 1904 date system: {_dateShift.ShiftToStandardSystem(workbook)} date values were moved to the 1900 system."));
            if (_leapYear.ShiftToStandardSystem(workbook).Fictitious is var fictitious and > 0)
                diagnostics.Add(Diagnostic.Info("XLSX191",
                    $"{fictitious} dates are 29 February 1900, a day that exists only in Excel's calendar: they are read as 28 February 1900.").With(fictitious));

            if (workbook.Sheets.Count == 0) workbook.AddSheet(workbook.UniqueSheetName(Workbook.CanonicalSheetPrefix));
            return OperationResult<Workbook>.Ok(workbook, diagnostics);
        }
        catch (OpenXmlPackageException ex)
        {
            return OperationResult<Workbook>.Fail(Diagnostic.Error("XLSX002", $"Unreadable XLSX package: {ex.Message}").With(ex.Message));
        }
        catch (Exception ex) when (_failures.IsMalformedInput(ex))
        {
            return OperationResult<Workbook>.Fail(Diagnostic.Error("XLSX003", $"Unreadable XLSX package: {ex.Message}").With(ex.Message));
        }
    }

    private Dictionary<uint, string?> NumberCodes(Stylesheet? stylesheet)
    {
        var codes = new Dictionary<uint, string?>();
        foreach (var format in stylesheet?.NumberingFormats?.Elements<NumberingFormat>() ?? [])
            if (format.NumberFormatId?.Value is { } id) codes.TryAdd(id, format.FormatCode?.Value);
        return codes;
    }

    private int NumberFormatIdOf(Workbook workbook, IReadOnlyDictionary<uint, string?> codes, int fileId)
    {
        if (fileId < PulseOffice.Sheets.Formatting.NumberFormatCatalog.FirstCustomId
            && PulseOffice.Sheets.Formatting.NumberFormatCatalog.BuiltInFormats.ContainsKey(fileId)) return fileId;
        return codes.GetValueOrDefault((uint)fileId) is { Length: > 0 } code ? workbook.NumberFormats.Register(code) : 0;
    }
}
