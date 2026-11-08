using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.OpenXml;

public static class XlsxFormat
{
    public static readonly SpreadsheetFormat Descriptor = new()
    {
        Id = "xlsx",
        DisplayName = "Excel Workbook",
        Extensions = [".xlsx"],
        MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        Capabilities = FormatCapabilities.Formulas
                     | FormatCapabilities.MultipleSheets
                     | FormatCapabilities.CellFormatting
                     | FormatCapabilities.ConditionalFormatting
                     | FormatCapabilities.Charts
                     | FormatCapabilities.DynamicArrays
                     | FormatCapabilities.PivotTables,
    };

    public static readonly SpreadsheetFormat Template = Descriptor with
    {
        Id = "xltx",
        DisplayName = "Excel Template",
        Extensions = [".xltx"],
        MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
    };
}

public sealed class XlsxSpreadsheetReader(SpreadsheetFormat format) : ISpreadsheetReader
{
    private readonly XlsxReader _reader = new();

    public XlsxSpreadsheetReader()
        : this(XlsxFormat.Descriptor)
    {
    }

    public SpreadsheetFormat Format => format;

    public bool CanRead(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) return false;

        var origin = stream.Position;

        try
        {
            using var archive = new System.IO.Compression.ZipArchive(
                stream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);

            return archive.GetEntry("xl/workbook.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            stream.Position = origin;
        }
    }

    public OperationResult<Workbook> Read(Stream stream) => _reader.Read(stream);
}

public sealed class XlsxSpreadsheetWriter(SignatureLineImage signatureImages, VectorFallback vectors, bool template, DrawingML.IDrawingNumberSchemes numbers) : ISpreadsheetWriter
{
    private readonly XlsxWriter _writer = new(signatureImages, vectors);

    public XlsxSpreadsheetWriter(SignatureLineImage signatureImages, VectorFallback vectors, bool template)
        : this(signatureImages, vectors, template, new DrawingML.DrawingNumberSchemes())
    {
    }

    public XlsxSpreadsheetWriter(SignatureLineImage signatureImages, bool template)
        : this(signatureImages, new VectorFallback(), template)
    {
    }

    public XlsxSpreadsheetWriter()
        : this(new SignatureLineImage())
    {
    }

    public XlsxSpreadsheetWriter(SignatureLineImage signatureImages)
        : this(signatureImages, false)
    {
    }

    private readonly DrawingStyleFidelity _drawing = new();

    private readonly OoxmlChartFidelity _charts = new();

    private readonly XlsxDataTableWriter _dataTables = new(new XlsxFormulaSyntax());

    public SpreadsheetFormat Format => template ? XlsxFormat.Template : XlsxFormat.Descriptor;

    private int UnwrittenGroupMembers(object content) => content is Drawing.Shapes.ShapeGroupObject group
        ? group.Children.Sum(child => child.Content is Drawing.Shapes.ShapeGroupObject ? UnwrittenGroupMembers(child.Content)
            : child.Content is Drawing.Shapes.ShapeObject or Drawing.Shapes.ConnectorObject or Drawing.Fontwork.FontworkObject or Drawing.Freeform.FreeformObject
                or Drawing.Assets.RasterImageObject { Data.IsEmpty: false } or Drawing.Assets.RasterImageObject { LinkPath.Length: > 0 } ? 0 : 1)
        : 0;

    private readonly FormulaShapeFidelity _formulas = new();

    private readonly XlsxAutoFilterCodec _filters = new();

    private readonly XlsxPageSetupCodec _pageSetup = new();

    private readonly XlsxRangeFilters _rangeFilters = new();

    private readonly DrawingML.IDrawingNumberSchemes _numbers = numbers;

    public FidelityReport CheckFidelity(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var losses = new List<Diagnostic>();

        var leapYear = new XlsxLeapYearSerials();
        var early = new XlsxCalendarCells(workbook).ConstantDates().Count(date => leapYear.PrecedesExcelCalendar(date.Cell.Value.AsNumber()));
        if (early > 0)
            losses.Add(Diagnostic.Warning("XLSX192", $"{early} dates fall before 1 January 1900: Excel's calendar starts there, so it shows them as other dates or as #####.").With(early));

        var derived = workbook.NamedStyles.All.Count(style => !style.IsBuiltIn && style.Parent is not null
            && !string.Equals(style.Parent, "Default", StringComparison.OrdinalIgnoreCase) && workbook.NamedStyles.Find(style.Parent) is not null);
        if (derived > 0)
            losses.Add(Diagnostic.Info("XLSX240", $"{derived} custom cell styles inherit from another cell style: Excel cell styles have no parent, so XLSX keeps each style's complete look and drops the inheritance. ODS keeps it.").With(derived));

        var bound = workbook.Sheets.Sum(sheet => sheet.QueryBindings.Count);
        if (bound > 0)
            losses.Add(Diagnostic.Info("XLSX010", $"{bound} ranges bound to queries are written as Excel query tables with their SQL; the connection itself stays in the PulseOffice settings, so Excel cannot refresh them until a connection with the same name is set up there.").With(bound));

        var twoConditions = workbook.Sheets.SelectMany(sheet => sheet.AutoFilter.Filters)
            .Concat(workbook.DatabaseRanges.All.SelectMany(range => range.Filters)).Count(_filters.LosesBlankExclusion);
        if (twoConditions > 0)
            losses.Add(Diagnostic.Info("XLSX231", $"{twoConditions} filter columns combine two conditions with the exclusion of blank cells: an Excel filter column holds at most two conditions, so XLSX keeps the two conditions and drops the exclusion of blanks. ODS keeps all three.").With(twoConditions));

        var converted = _rangeFilters.WrittenAsAutoFilter(workbook);
        if (converted > 0)
            losses.Add(Diagnostic.Info("XLSX232", $"{converted} standard filters are written as AutoFilters: Excel keeps column conditions only in an AutoFilter, so the filtered range shows filter buttons when the file is opened again. ODS keeps the standard filter.").With(converted));

        var unwritten = _rangeFilters.Lost(workbook);
        if (unwritten > 0)
            losses.Add(Diagnostic.Warning("XLSX233", $"{unwritten} standard or advanced filters cannot be stored in XLSX (conditions that mix fields inside or-groups, more than two conditions on a column, unique records only, a second advanced filter on the same sheet or one next to an AutoFilter): the rows they hide stay hidden, that part of the filter is lost. ODS keeps it.").With(unwritten));

        var commaOrders = workbook.Sheets.Select(sheet => sheet.AutoFilter.Sort)
            .Concat(workbook.Sheets.Select(sheet => sheet.RangeSort?.Settings))
            .Concat(workbook.DatabaseRanges.All.Select(range => range.Sort))
            .SelectMany(sort => sort?.Fields ?? [])
            .Count(field => field.CustomOrder?.Any(item => item.Contains(',', StringComparison.Ordinal)) == true);
        if (commaOrders > 0)
            losses.Add(Diagnostic.Info("XLSX236", $"{commaOrders} remembered sort keys use a custom order with an item that contains a comma: Excel stores a custom order as one comma-separated list, so XLSX splits that item. The sorted cells are not affected. ODS keeps the order.").With(commaOrders));

        if (workbook.Consolidation is { Sources.Count: > 0 } consolidation && consolidation.Target != Sheets.Addressing.CellAddress.First)
            losses.Add(Diagnostic.Info("XLSX237", "The remembered consolidation settings name a destination cell: XLSX keeps the settings on the destination sheet but not the cell, so the dialog proposes the top-left cell when the file is opened again. ODS keeps it."));

        if (workbook.Macros.All.Count > 0)
            losses.Add(Diagnostic.Warning("XLSX011", $"{workbook.Macros.All.Count} macros cannot be stored in XLSX: the format accepts only VBA, and only in .xlsm.").With(workbook.Macros.All.Count));

        if (workbook.FormSettings != new Document.Forms.FormDocumentSettings())
            losses.Add(Diagnostic.Info("XLSX026", "Open in design mode, automatic focus and the data source of form controls do not exist in XLSX."));

        var charts = workbook.Sheets.SelectMany(sheet => sheet.Objects).Select(placed => placed.Content).OfType<Charts.ChartDefinition>().ToList();
        if (charts.Any(_charts.LosesType))
            losses.Add(Diagnostic.Warning("XLSX087", "Chart types that DrawingML does not have, such as stepped lines, waterfall, statistical and other special charts, are written as column charts in XLSX. ODS keeps them. PulseOffice reads the type back from an extension."));
        if (charts.Any(_charts.FlattensThreeDimensional))
            losses.Add(Diagnostic.Info("XLSX088", "DrawingML has no three-dimensional line or column-and-line charts: these 3D charts are written flat in XLSX. ODS keeps the 3D view."));

        if (workbook.Sheets.SelectMany(sheet => sheet.Objects).Any(placed => _formulas.LosesFormulas(placed.Content)))
            losses.Add(Diagnostic.Warning("XLSX190", "Shapes whose geometry is computed from ODF equations are written to XLSX with their current outline: the equations, modifiers and handles are lost. ODS keeps them."));

        var orphans = workbook.Sheets.SelectMany(sheet => sheet.Objects).Sum(placed => UnwrittenGroupMembers(placed.Content));
        if (orphans > 0)
            losses.Add(Diagnostic.Warning("XLSX059", $"{orphans} objects inside groups are charts, vector pictures, embedded objects or controls: XLSX groups keep shapes, lines, text art and pictures, the others are lost.").With(orphans));

        if (workbook.Calculation.CaseSensitive || workbook.Calculation.Criteria != CriteriaSyntax.Wildcards || !workbook.Calculation.WholeCellCriteria)
            losses.Add(Diagnostic.Warning("XLSX058", "Excel always compares whole cells, without case and with wildcards: case-sensitive comparisons, regular expressions and partial-cell matches in criteria are lost in XLSX."));

        if (workbook.Calculation.AutomaticFindLabels)
            losses.Add(Diagnostic.Info("XLSX062", "Excel does not find column and row labels automatically: formulas that refer to a label by its text give #NAME? in XLSX. ODS keeps the setting."));

        if (workbook.MacroSignature is not null)
            losses.Add(Diagnostic.Warning("XLSX012", "The macro signature is not kept."));

        if (workbook.DefaultLanguage is not null && workbook.Properties.Language is not null
                && !string.Equals(workbook.DefaultLanguage, workbook.Properties.Language, StringComparison.OrdinalIgnoreCase)
            || workbook.Styles.Styles.Any(style => style.Language is not null))
            losses.Add(Diagnostic.Warning("XLSX013", "Cell language does not exist in XLSX: it is lost on export."));

        if (workbook.Sheets.SelectMany(sheet => sheet.Objects).Select(placed => placed.Content).OfType<Drawing.Shapes.ShapeObject>()
                .SelectMany(shape => shape.Lists ?? []).Any(item => item is { Numbered: true } && !_numbers.Reaches(item.NumberFormat)))
            losses.Add(Diagnostic.Info("XLSX184", "Excel numbers lists in shape text with a fixed set of schemes (1. 1) (1) a. A. i. I. and their variants): numbered lists with other prefixes, suffixes or number formats are written with the nearest scheme in XLSX. ODS keeps them."));

        var shapeStyles = workbook.Sheets.SelectMany(sheet => sheet.Objects).Select(placed => placed.Content)
            .OfType<Drawing.Shapes.ShapeObject>().Select(shape => shape.Style).ToList();
        if (shapeStyles.Any(_drawing.LosesGradient))
            losses.Add(Diagnostic.Info("XLSX054", "DrawingML has no ellipsoid or square gradients and no gradient border: shapes take radial or rectangular gradients without border in XLSX."));
        if (shapeStyles.Any(_drawing.LosesHatch))
            losses.Add(Diagnostic.Info("XLSX055", "DrawingML hatches are fixed patterns: the angle and spacing of a shape's hatch are lost in XLSX."));
        if (shapeStyles.Any(_drawing.LosesBitmapTile))
            losses.Add(Diagnostic.Info("XLSX060", "DrawingML tiles a bitmap fill at the picture's own size: the tile width and height chosen for a shape are lost in XLSX."));
        if (shapeStyles.Any(_drawing.LosesDashShape))
            losses.Add(Diagnostic.Info("XLSX061", "DrawingML custom dashes are measured in line widths and have square ends: rounded dashes and dash lengths given in centimetres become relative lengths in XLSX."));
        if (shapeStyles.Any(_drawing.LosesArrowSize))
            losses.Add(Diagnostic.Info("XLSX056", "DrawingML arrowheads come in three sizes: other arrow scales become small, medium or large in XLSX."));

        var overline = workbook.Styles.Styles.Count(style => style.Font.Overline);
        if (overline > 0)
            losses.Add(Diagnostic.Warning("XLSX014", $"{overline} styles with overlined text: OOXML cannot describe overline.").With(overline));

        var freeform = workbook.Sheets.Sum(sheet => sheet.Objects.Count(
            placed => placed.Content is Drawing.Freeform.FreeformObject));

        if (freeform > 0)
            losses.Add(Diagnostic.Warning("XLSX015", $"{freeform} freeform paths become geometry: the editable nodes are lost.").With(freeform));

        var filteredImages = workbook.Sheets.Sum(sheet => sheet.Objects.Count(
            placed => placed.Content is Drawing.Assets.RasterImageObject img
                && (img.Filter != Drawing.Assets.ImageFilter.None || img.Crop != default)));

        if (filteredImages > 0)
            losses.Add(Diagnostic.Warning("XLSX024", $"{filteredImages} images with a filter or crop: the look is shown on screen but not written to OOXML.").With(filteredImages));

        var mappedImages = workbook.Sheets.Sum(sheet => sheet.Objects.Count(
            placed => placed.Content is Drawing.Assets.RasterImageObject { Map.IsEmpty: false }));

        if (mappedImages > 0)
            losses.Add(Diagnostic.Warning("XLSX025", $"{mappedImages} images with an image map: OOXML has no image maps, the clickable areas are lost in XLSX and kept in ODS.").With(mappedImages));


        var keys = new OoxmlProtectionKeys();
        if (!keys.IsWritable(workbook.Protection.PasswordHash) || workbook.Sheets.Any(sheet => sheet.Protection.IsEnabled && !keys.IsWritable(sheet.Protection.PasswordHash)))
            losses.Add(Diagnostic.Warning("XLSX028", "Protection is kept, but the password cannot be converted to Excel's format: in XLSX the protected sheet unlocks without a password."));

        if (workbook.Styles.Styles.Any(style =>
                style.Borders.DiagonalUp.IsVisible && style.Borders.DiagonalDown.IsVisible
                && style.Borders.DiagonalUp != style.Borders.DiagonalDown))
            losses.Add(Diagnostic.Info("XLSX037", "Cells with two diagonals of different style: in XLSX the diagonals share one line and take the rising one."));

        var pivotMapper = new XlsxPivotMapper();
        var undescribed = workbook.Pivots.Sum(pivot => pivot.Fields.Count(field => field.Area != Sheets.Pivot.PivotArea.Unused && !pivotMapper.Describes(field)));
        if (undescribed > 0)
            losses.Add(Diagnostic.Warning("XLSX030", $"{undescribed} pivot fields use a grouping, a calculation, an aggregation (median, first, last, distinct count) or a display mode XLSX cannot describe: they are saved without it.").With(undescribed));

        var repeated = workbook.Pivots.Count(pivot => pivot.RepeatItemLabels);
        if (repeated > 0)
            losses.Add(Diagnostic.Info("XLSX063", $"{repeated} pivot tables repeat item labels: XLSX keeps the table without the repetition.").With(repeated));

        if (new XlsxTablePlanner().Plan(workbook).Unwritable is var untabled and > 0)
            losses.Add(Diagnostic.Info("XLSX031", $"{untabled} named data ranges cannot become Excel tables (a name Excel does not accept, header cells that are not distinct texts, an overlap with merged cells, another table or the sheet filter): they are lost in XLSX with their filters.").With(untabled));

        if (workbook.Sheets.Any(sheet => sheet.Scenarios.Any(scenario => scenario.Formulas.Count > 0)))
            losses.Add(Diagnostic.Info("XLSX029", "Scenarios with formulas: in XLSX a scenario keeps only values, formulas become their result."));

        var signatureLines = workbook.Sheets.Sum(sheet => sheet.Objects.Count(
            placed => placed.Content is Document.Forms.SignatureLineObject { IsSigned: true }));

        if (signatureLines > 0)
            losses.Add(Diagnostic.Info("XLSX016", $"{signatureLines} signed signature lines: the line is kept in XLSX, but the signer and date shown on it are not; Excel reads them from the digital signature. ODS keeps them.").With(signatureLines));

        var events = workbook.Sheets.Count(sheet => sheet.Events.All.Count > 0);
        if (events > 0)
            losses.Add(Diagnostic.Warning("XLSX017", $"{events} sheets with events bound to macros: the bindings are lost.").With(events));

        var labels = workbook.Sheets.Sum(sheet => sheet.Labels.All.Count);
        if (labels > 0)
            losses.Add(Diagnostic.Warning("XLSX018", $"{labels} label ranges are not kept.").With(labels));

        var controlFidelity = new XlsxFormControlFidelity();
        var controls = workbook.Sheets.Sum(sheet => sheet.Objects.Count(
            placed => placed.Content is Document.Forms.FormControlObject form && controlFidelity.Lossy(form)));

        if (controls > 0)
            losses.Add(Diagnostic.Warning("XLSX020", $"{controls} form controls use a kind Excel has no form control for (text and formatted fields, image buttons and the like) or properties Excel form controls cannot hold (inline list items, fonts, colours, borders, help texts, read-only or hidden state): those parts are lost.").With(controls));

        var eventBindings = workbook.Sheets.Sum(sheet => sheet.Objects
            .Select(placed => placed.Content)
            .OfType<Document.Forms.FormControlObject>()
            .Sum(control => control.Properties.Events.Count));

        if (eventBindings > 0)
            losses.Add(Diagnostic.Warning("XLSX019", $"{eventBindings} control events bound to macros are lost.").With(eventBindings));

        if (workbook.Versions.Count > 0)
            losses.Add(Diagnostic.Warning("XLSX022", $"{workbook.Versions.Count} versions stored inside the document: OOXML cannot hold them.").With(workbook.Versions.Count));

        if (workbook.Changes.Changes.Count > 0)
            losses.Add(Diagnostic.Warning("XLSX023", $"{workbook.Changes.Changes.Count} recorded changes: OOXML shared revisions are deprecated and are not written.").With(workbook.Changes.Changes.Count));

        if (workbook.Changes.IsRecording || !workbook.Changes.ShowChanges)
            losses.Add(Diagnostic.Info("XLSX052", "Change recording and the visibility of recorded changes have no XLSX equivalent and are lost."));

        var scenarioBorders = workbook.Sheets.Sum(sheet => sheet.Scenarios.Count(scenario => scenario.BorderColorHex is not null));
        if (scenarioBorders > 0)
            losses.Add(Diagnostic.Info("XLSX053", $"{scenarioBorders} scenario border colours: XLSX scenarios have no border and the colour is lost.").With(scenarioBorders));

        if (workbook.Changes.IsProtected)
            losses.Add(Diagnostic.Warning("XLSX032", "The protection of recorded changes and its password have no XLSX equivalent and are lost."));

        var unwrittenPapers = workbook.Sheets.Count(sheet => !_pageSetup.WritesPaper(workbook.PageStyles.For(sheet).Setup.Paper));
        if (unwrittenPapers > 0)
            losses.Add(Diagnostic.Warning("XLSX234", $"{unwrittenPapers} sheets print on a custom paper size: XLSX names papers only by a fixed code, so the size is lost and Excel prints on its default paper.").With(unwrittenPapers));

        var namedPages = workbook.Sheets.Select(sheet => sheet.PageStyleName)
            .Where(name => !string.Equals(name, "Default", StringComparison.Ordinal)).Distinct().Count();
        if (namedPages > 0)
            losses.Add(Diagnostic.Info("XLSX033", $"{namedPages} named page styles: XLSX writes page setup sheet by sheet and the style name is lost.").With(namedPages));

        var commaChoices = workbook.Sheets.Sum(sheet => sheet.Validations.Count(
            validation => validation.ListValues.Any(value => value.Contains(',', StringComparison.Ordinal))));
        if (commaChoices > 0)
            losses.Add(Diagnostic.Warning("XLSX034", $"{commaChoices} validation lists have entries containing a comma: in XLSX the comma separates entries, which split in two.").With(commaChoices));

        var longChoices = workbook.Sheets.Sum(sheet => sheet.Validations.Count(
            validation => validation.ListValues.Count > 0 && validation.ListValues.Sum(value => value.Length + 1) - 1 > 255));
        if (longChoices > 0)
            losses.Add(Diagnostic.Warning("XLSX103", $"{longChoices} validation lists have entries longer than 255 characters in all: Excel refuses longer lists typed in the rule and drops the validation on open; put the entries in cells and refer to them.").With(longChoices));

        var comments = workbook.Sheets.Sum(sheet => sheet.Comments.Count);
        if (comments > 0)
            losses.Add(Diagnostic.Info("XLSX035", $"{comments} comments: in XLSX a comment does not carry the date it was written.").With(comments));

        if (workbook.Fonts.HasFiles || workbook.Fonts.EmbedSystemFonts)
            losses.Add(Diagnostic.Warning("XLSX036", $"Embedded fonts are not supported by XLSX: {string.Join(", ", workbook.Fonts.Families)} must be installed where the file is opened; they round-trip in ODS.").With(string.Join(", ", workbook.Fonts.Families)));

        var names = new SheetNamePortability();
        var unportable = workbook.Sheets.Count(sheet => !names.AcceptedByExcel(sheet.Name));
        if (unportable > 0)
            losses.Add(Diagnostic.Warning("XLSX038", $"{unportable} sheets have a name Excel and other office suites reject (more than 31 characters, apostrophe at the start or end, or one of [ ] * ? : / \\): the file may not open elsewhere.").With(unportable));

        var external = workbook.ExternalData;
        if (external.AreaLinks.Count > 0)
            losses.Add(Diagnostic.Warning("XLSX042", $"{external.AreaLinks.Count} links to external data: XLSX keeps the values written in the sheet, the link to the source is lost.").With(external.AreaLinks.Count));
        var timed = external.AreaLinks.Count(link => link.RefreshSeconds > 0);
        if (timed > 0)
            losses.Add(Diagnostic.Info("XLSX045", $"{timed} external links refresh automatically: XLSX has no automatic refresh delay and the timer is lost.").With(timed));
        if (external.Stream is not null)
            losses.Add(Diagnostic.Warning("XLSX043", "A data stream: XLSX keeps the values written in the sheet, the stream source is lost."));
        if (external.Mappings.Count > 0)
            losses.Add(Diagnostic.Warning("XLSX044", $"{external.Mappings.Count} data providers: XLSX keeps the values written in the database range, the provider is lost.").With(external.Mappings.Count));

        var rules = workbook.Sheets.SelectMany(sheet => sheet.ConditionalRules).ToList();
        var iconSets = new XlsxIconSets();
        var foreignIcons = rules.Count(iconSets.IsLost);
        if (foreignIcons > 0)
            losses.Add(Diagnostic.Warning("XLSX047", $"{foreignIcons} icon sets that XLSX does not define (stars, triangles, boxes, coloured smileys): they are saved as arrows.").With(foreignIcons));
        var longScales = rules.Count(rule => rule.Visual == Sheets.Formatting.ConditionalVisual.ColorScale && rule.ScaleColors.Count > 3);
        if (longScales > 0)
            losses.Add(Diagnostic.Warning("XLSX048", $"{longScales} colour scales with more than three colours: XLSX keeps the first three.").With(longScales));

        var formulas = workbook.Sheets.Sum(sheet => sheet.Objects.Count(
            placed => placed.Content is Document.Embedding.FormulaObject));

        if (formulas > 0)
            losses.Add(Diagnostic.Warning("XLSX021", $"{formulas} math formulas travel as OMML in a custom XML part: Excel does not show them on the sheet.").With(formulas));

        var foreignReferences = workbook.Sheets.Sum(sheet => sheet.CellAddresses.Count(address =>
            sheet[address].Formula is { } text && text.Contains("'file:", StringComparison.OrdinalIgnoreCase)));
        if (foreignReferences > 0)
            losses.Add(Diagnostic.Warning("XLSX051", $"{foreignReferences} formulas refer to other documents: XLSX needs an external link part that is not written, and the references break.").With(foreignReferences));

        var operations = workbook.Sheets.Sum(sheet =>
        {
            var tables = _dataTables.Plan(sheet);
            return sheet.CellAddresses.Count(address => sheet[address].Formula is { } text
                && text.Contains("MULTIPLE.OPERATIONS(", StringComparison.OrdinalIgnoreCase) && !tables.Covers(address, out _));
        });
        if (operations > 0)
            losses.Add(Diagnostic.Warning("XLSX082", $"{operations} cells use MULTIPLE.OPERATIONS outside the layout of an Excel data table: Excel does not have the function and shows #NAME? once it recalculates.").With(operations));

        return losses.Count == 0 ? FidelityReport.Perfect : new FidelityReport(losses);
    }

    public OperationResult<bool> Write(Workbook workbook, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        try
        {
            _writer.Write(workbook, stream, template
                ? DocumentFormat.OpenXml.SpreadsheetDocumentType.Template
                : DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("XLSX100", $"Writing the XLSX failed: {ex.Message}").With(ex.Message));
        }
    }
}
