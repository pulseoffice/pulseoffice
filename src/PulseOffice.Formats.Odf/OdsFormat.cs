using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.Odf;

public static class OdsFormat
{
    public static readonly SpreadsheetFormat Descriptor = new()
    {
        Id = "ods",
        DisplayName = "OpenDocument Spreadsheet",
        Extensions = [".ods"],
        MimeType = OdfPackage.MimeTypes.Spreadsheet,
        IsNative = true,
        Capabilities = FormatCapabilities.Formulas
                     | FormatCapabilities.MultipleSheets
                     | FormatCapabilities.CellFormatting
                     | FormatCapabilities.ConditionalFormatting
                     | FormatCapabilities.Charts
                     | FormatCapabilities.PivotTables
                     | FormatCapabilities.QueryBindings,
    };

    public static readonly SpreadsheetFormat TemplateDescriptor = Descriptor with
    {
        Id = "ots",
        DisplayName = "OpenDocument Spreadsheet Template",
        Extensions = [".ots"],
        MimeType = OdfPackage.MimeTypes.SpreadsheetTemplate,
        IsNative = false,
    };

    public static readonly SpreadsheetFormat FlatDescriptor = Descriptor with
    {
        Id = "fods",
        DisplayName = "Flat OpenDocument Spreadsheet",
        Extensions = [".fods"],
        MimeType = "application/vnd.oasis.opendocument.spreadsheet-flat-xml",
        IsNative = false,
    };

    public static readonly SpreadsheetFormat ReadableDescriptor = Descriptor with
    {
        Extensions = [".ods", ".fods", ".ots", ".sxc", ".stc"],
    };
}

public sealed class OdsSpreadsheetReader : ISpreadsheetReader
{
    private readonly OdsReader _reader = new();
    private readonly Formats.Packaging.FlatOdfUnpacker _flat = new();

    public SpreadsheetFormat Format => OdsFormat.ReadableDescriptor;

    public bool CanRead(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) return false;

        Span<byte> header = stackalloc byte[128];
        var origin = stream.Position;

        try
        {
            if (_flat.IsFlat(stream))
                return _flat.MimeTypeOf(stream) is Formats.Packaging.OdfPackage.MimeTypes.Spreadsheet or Formats.Packaging.OdfPackage.MimeTypes.SpreadsheetTemplate;

            var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            if (read < 40) return false;
            if (header[0] != 'P' || header[1] != 'K') return false;

            var text = System.Text.Encoding.ASCII.GetString(header[..read]);
            return text.Contains("mimetype", StringComparison.Ordinal)
                && (text.Contains("opendocument.spreadsheet", StringComparison.Ordinal) || text.Contains("vnd.sun.xml.calc", StringComparison.Ordinal));
        }
        finally
        {
            stream.Position = origin;
        }
    }

    public OperationResult<Workbook> Read(Stream stream) => _reader.Read(stream);
}

public sealed class OdsSpreadsheetWriter(bool template, SignatureLineImage signatureImages, VectorFallback vectors) : ISpreadsheetWriter
{
    private readonly OdsWriter _writer = new(template, signatureImages, vectors);

    public OdsSpreadsheetWriter(bool template, SignatureLineImage signatureImages)
        : this(template, signatureImages, new VectorFallback())
    {
    }

    public OdsSpreadsheetWriter(bool template = false)
        : this(template, new SignatureLineImage())
    {
    }

    public SpreadsheetFormat Format => template ? OdsFormat.TemplateDescriptor : OdsFormat.Descriptor;

    private int UnwrittenGroupMembers(object content, Func<object, bool> written) => content is Drawing.Shapes.ShapeGroupObject group
        ? group.Children.Sum(child => child.Content is Drawing.Shapes.ShapeGroupObject ? UnwrittenGroupMembers(child.Content, written) : written(child.Content) ? 0 : 1)
        : 0;

    public FidelityReport CheckFidelity(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var losses = new List<Diagnostic>();

        var orphans = workbook.Sheets.SelectMany(sheet => sheet.Objects).Sum(placed => UnwrittenGroupMembers(placed.Content,
            content => content is Drawing.Shapes.ShapeObject or Drawing.Freeform.FreeformObject or Drawing.Shapes.ConnectorObject or Drawing.Fontwork.FontworkObject
                or Drawing.Assets.RasterImageObject { Data.IsEmpty: false } or Drawing.Assets.RasterImageObject { LinkPath.Length: > 0 }));
        if (orphans > 0)
            losses.Add(Diagnostic.Warning("ODF042", $"{orphans} objects inside groups are charts, vector pictures, embedded objects or controls: ODF groups in spreadsheets keep shapes, lines and pictures, the others are lost.").With(orphans));

        var stopping = workbook.Sheets.Sum(sheet => sheet.ConditionalRules.Count(r => r.StopIfTrue));
        if (stopping > 0)
            losses.Add(Diagnostic.Warning("ODF020", $"{stopping} conditional rules use \"stop if true\": the option does not exist in ODF.").With(stopping));

        var unknownErrors = workbook.Sheets.Sum(sheet => sheet.NonEmptyCells()
            .Count(entry => entry.Value.Value.Error is Sheets.Values.CellError.Calc or Sheets.Values.CellError.Spill));
        if (unknownErrors > 0)
            losses.Add(Diagnostic.Info("ODF271", $"{unknownErrors} cells show #CALC! or #SPILL!: ODF defines neither error; PulseOffice reads them back, other applications show a generic error.").With(unknownErrors));

        var clashing = workbook.Sheets.Count(sheet => sheet is { AutoFilter.Range: { } filtered, RangeFilter: { } stored } && stored.Range != filtered);
        if (clashing > 0)
            losses.Add(Diagnostic.Warning("ODF272", $"{clashing} sheets hold a standard or advanced filter on a range other than their AutoFilter: ODF keeps one unnamed filtered range per sheet, so the AutoFilter is written and the other filter is lost. Define a database range for it to keep both.").With(clashing));

        var unsharedSorts = workbook.Sheets.Count(sheet => sheet.RangeSort is { } sorted
            && (sheet.AutoFilter.IsActive || sheet.RangeFilter is { } stored && (stored.Range != sorted.Range || stored.HasHeader != sorted.HasHeader)));
        if (unsharedSorts > 0)
            losses.Add(Diagnostic.Info("ODF274", $"{unsharedSorts} sheets remember the sort of a plain range next to an AutoFilter or a filter on another range: ODF keeps one unnamed database range per sheet, so the filter is written and the remembered sort settings are lost. The sorted cells are not affected. XLSX keeps them.").With(unsharedSorts));

        var formattedBands = workbook.PageStyles.All.Count(style => _headerFooterText.HasFormatting(style.Setup.Header) || _headerFooterText.HasFormatting(style.Setup.Footer));
        if (formattedBands > 0)
            losses.Add(Diagnostic.Info("ODF273", $"{formattedBands} page styles have headers or footers with Excel formatting codes (font, size, bold, colour, picture): ODF page regions keep the text and the fields, the formatting is lost.").With(formattedBands));

        var exotic = workbook.Sheets
            .SelectMany(sheet => sheet.Objects)
            .OfType<PlacedObject>()
            .Count(o => o.Content is Charts.ChartDefinition { Type: var type }
                        && _charts.KeptOnlyByExtension(type));

        if (exotic > 0)
            losses.Add(Diagnostic.Info("ODF021", $"{exotic} charts of types that ODF does not define, such as waterfall and statistical charts, keep their type in a PulseOffice extension: other applications show them as the nearest standard chart, usually columns.").With(exotic));

        var duotoneImages = workbook.Sheets
            .SelectMany(sheet => sheet.Objects)
            .OfType<PlacedObject>()
            .Count(o => o.Content is Drawing.Assets.RasterImageObject { Duotone: not null });

        if (duotoneImages > 0)
            losses.Add(Diagnostic.Warning("ODF044", $"{duotoneImages} duotone images: ODF has no two-colour recolouring, the original image is written. XLSX keeps it.").With(duotoneImages));

        var filteredImages = workbook.Sheets
            .SelectMany(sheet => sheet.Objects)
            .OfType<PlacedObject>()
            .Count(o => o.Content is Drawing.Assets.RasterImageObject img
                && (img.Filter != Drawing.Assets.ImageFilter.None || img.Crop != default));

        if (filteredImages > 0)
            losses.Add(Diagnostic.Warning("ODF022", $"{filteredImages} images with a filter or crop: the look is shown on screen but not written to ODF.").With(filteredImages));

        var systemDates = workbook.Styles.Styles.Count(style => style.NumberFormatId is
            Sheets.Formatting.NumberFormatCatalog.SystemShortDateId or Sheets.Formatting.NumberFormatCatalog.SystemDateTimeId);
        if (systemDates > 0)
            losses.Add(Diagnostic.Warning("ODF170", $"{systemDates} cell styles use the system date format: ODF has no locale-following date, the current short date pattern is written as a fixed format. XLSX keeps it.").With(systemDates));


        var pivots = new OdfPivotMapper();
        var pivotFields = workbook.Pivots.SelectMany(pivot => pivot.Fields).Where(field => field.Area == Sheets.Pivot.PivotArea.Value).ToList();
        var unwritten = pivotFields.Count(field => !pivots.WrittenAggregations.Contains(field.Aggregation)
            || !pivots.WrittenDisplayModes.Contains(field.DisplayMode));
        if (unwritten > 0)
            losses.Add(Diagnostic.Warning("ODF024", $"{unwritten} pivot data fields use an aggregation (first, last, distinct count) or a display mode (percent of parent, running total in percent, rank) that ODF cannot describe: they are saved as sum or as plain value.").With(unwritten));

        var emptyText = workbook.Pivots.Count(pivot => pivot.EmptyCellText is not null || pivot.AutoRefreshOnOpen);
        if (emptyText > 0)
            losses.Add(Diagnostic.Info("ODF025", $"{emptyText} pivot tables with text for empty cells or refresh on open: neither option exists in ODF.").With(emptyText));

        var calculated = workbook.Pivots.Sum(pivot => pivot.Fields.Count(field => field.IsCalculated));
        if (calculated > 0)
            losses.Add(Diagnostic.Warning("ODF028", $"{calculated} calculated pivot fields: ODF data pilots have no calculated fields and they are not saved.").With(calculated));

        var names = new SheetNamePortability();
        var unportable = workbook.Sheets.Count(sheet => !names.AcceptedByOdfSuites(sheet.Name));
        if (unportable > 0)
            losses.Add(Diagnostic.Warning("ODF027", $"{unportable} sheets have a name other ODF applications reject (apostrophe at the start or end, or one of [ ] * ? : / \\): they rename them on open and references break.").With(unportable));

        var protections = workbook.Sheets.Count(sheet => sheet.Protection.IsEnabled
            && (sheet.Protection.AllowFormatCells || sheet.Protection.AllowSort));
        if (protections > 0)
            losses.Add(Diagnostic.Info("ODF026", $"{protections} protected sheets allow formatting cells or sorting: ODF has neither exception and the sheet forbids both.").With(protections));

        var excelKeys = workbook.Sheets.Count(sheet => sheet.Protection.IsEnabled && IsExcelKey(sheet.Protection.PasswordHash))
            + (workbook.Protection.Structure && IsExcelKey(workbook.Protection.PasswordHash) ? 1 : 0);
        if (excelKeys > 0)
            losses.Add(Diagnostic.Warning("ODF029", $"{excelKeys} protections carry an Excel password: ODF cannot store it, the protection is kept and unlocks without a password.").With(excelKeys));

        var reversed = workbook.Sheets.SelectMany(sheet => sheet.ConditionalRules).Count(rule => rule.Visual == Sheets.Formatting.ConditionalVisual.IconSet && rule.ReverseIcons);
        if (reversed > 0)
            losses.Add(Diagnostic.Warning("ODF040", $"{reversed} icon sets shown in reverse order: ODF cannot store the reversal and the icons go back to their normal order.").With(reversed));

        var deviating = workbook.Sheets.SelectMany(sheet => sheet.ConditionalRules).Count(rule => rule.Visual == Sheets.Formatting.ConditionalVisual.Style && rule.Formula is null && rule.StandardDeviations > 0);
        if (deviating > 0)
            losses.Add(Diagnostic.Warning("ODF102", $"{deviating} above or below average rules use standard deviations: ODF compares only with the average and the deviations are lost.").With(deviating));

        var bars = workbook.Sheets.SelectMany(sheet => sheet.ConditionalRules).Where(rule => rule.Visual == Sheets.Formatting.ConditionalVisual.DataBar).ToList();
        var bordered = bars.Count(rule => rule.Bar.BorderColorHex is not null || rule.Bar.NegativeBorderColorHex is not null);
        if (bordered > 0)
            losses.Add(Diagnostic.Info("ODF086", $"{bordered} data bars with a border: ODF has no border colour for data bars and the border is lost.").With(bordered));
        var directed = bars.Count(rule => rule.Bar.Direction != Sheets.Formatting.DataBarDirection.Context);
        if (directed > 0)
            losses.Add(Diagnostic.Info("ODF087", $"{directed} data bars with a fixed direction (left to right or right to left): ODF does not keep the direction and the bars follow the sheet.").With(directed));

        return losses.Count == 0 ? FidelityReport.Perfect : new FidelityReport(losses);
    }

    private bool IsExcelKey(string? hash) =>
        hash is not null && (hash.StartsWith(PasswordKey.OoxmlPrefix, StringComparison.Ordinal) || hash.StartsWith(PasswordKey.OoxmlLegacyPrefix, StringComparison.Ordinal));

    private readonly OdfChartVocabulary _charts = new();
    private readonly OdfHeaderFooterText _headerFooterText = new(new PulseOffice.Foundation.Pages.HeaderFooterCodes());

    public OperationResult<bool> Write(Workbook workbook, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        try
        {
            _writer.Write(workbook, stream);
            return OperationResult<bool>.Ok(true);
        }
        catch (IOException ex)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("ODF100", $"Writing the ODS failed: {ex.Message}").With(ex.Message));
        }
    }
}

public sealed class FlatOdsSpreadsheetWriter(OdsSpreadsheetWriter package) : ISpreadsheetWriter
{
    private readonly Formats.Packaging.FlatOdfPacker _packer = new();

    public FlatOdsSpreadsheetWriter() : this(new OdsSpreadsheetWriter())
    {
    }

    public SpreadsheetFormat Format => OdsFormat.FlatDescriptor;

    public FidelityReport CheckFidelity(Workbook workbook) => package.CheckFidelity(workbook);

    public OperationResult<bool> Write(Workbook workbook, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        var result = package.Write(workbook, buffer);
        if (!result.Succeeded) return result;

        buffer.Position = 0;
        try
        {
            using var flat = _packer.Pack(buffer);
            flat.CopyTo(stream);
            return result;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or IOException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("ODF100", $"Writing the ODS failed: {ex.Message}").With(ex.Message));
        }
    }
}
