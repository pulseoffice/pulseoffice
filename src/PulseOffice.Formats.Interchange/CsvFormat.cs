using System.Globalization;
using System.Text;
using PulseOffice.Formats;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Formats.Interchange;

public static class CsvFormats
{
    public static readonly SpreadsheetFormat Csv = new()
    {
        Id = "csv",
        DisplayName = "CSV",
        Extensions = [".csv"],
        MimeType = "text/csv",
        Capabilities = FormatCapabilities.PlainTabular,
    };

    public static readonly SpreadsheetFormat Text = new()
    {
        Id = "tsv",
        DisplayName = "Text",
        Extensions = [".tsv", ".tab", ".txt"],
        MimeType = "text/tab-separated-values",
        Capabilities = FormatCapabilities.PlainTabular,
    };
}

public sealed class CsvSpreadsheetReader(
    IDelimitedText delimited, ICsvValueReader values, ICsvSniffer sniffer, CsvOptions? options = null,
    Text.Plain.ITextEncodingDetector? encodings = null)
    : ISpreadsheetReader
{
    private readonly CsvOptions _options = options ?? CsvOptions.Default;

    private readonly Text.Plain.ITextEncodingDetector _encodings = encodings ?? new Text.Plain.TextEncodingDetector(new Text.Plain.CodePages());

    private const int SampleLength = 8192;

    public CsvSpreadsheetReader WithOptions(CsvOptions chosen) => new(delimited, values, sniffer, chosen, _encodings);

    public CsvOptions Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var origin = stream.CanSeek ? stream.Position : 0;
        var probe = new byte[SampleLength * 4];
        var read = stream.ReadAtLeast(probe, probe.Length, throwOnEndOfStream: false);
        if (stream.CanSeek) stream.Position = origin;
        var detected = _encodings.Decode(probe[..(read == probe.Length ? WholeSequences(probe) : read)]);
        return sniffer.Sniff(Head(detected.Text), _options) with { Encoding = detected.Encoding, AutoDetect = false };
    }

    private int WholeSequences(byte[] sample)
    {
        for (var start = sample.Length - 1; start >= Math.Max(0, sample.Length - 4); start--)
        {
            var lead = sample[start];
            if ((lead & 0xC0) == 0x80) continue;
            var length = lead >= 0xF0 ? 4 : lead >= 0xE0 ? 3 : lead >= 0xC0 ? 2 : 1;
            return start + length > sample.Length ? start : sample.Length;
        }
        return sample.Length;
    }

    private readonly Dictionary<CsvFieldKind, int> _formats = new()
    {
        [CsvFieldKind.Percent] = 10,
        [CsvFieldKind.Date] = 14,
        [CsvFieldKind.DateTime] = 22,
        [CsvFieldKind.Time] = 21,
    };

    public SpreadsheetFormat Format => CsvFormats.Csv;

    public bool CanRead(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var probe = new byte[512];
        var read = stream.Read(probe, 0, probe.Length);

        if (read == 0) return false;

        var sample = _encodings.Decode(probe[..read]).Text;
        if (sample.Contains('\0', StringComparison.Ordinal)) return false;

        return delimited.GuessSeparator(sample) is not null;
    }

    public OperationResult<Workbook> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!_options.AutoDetect)
        {
            using var chosen = new StreamReader(stream, _options.Encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return OperationResult<Workbook>.Ok(Fill(chosen, _options));
        }

        if (stream.CanSeek)
        {
            var origin = stream.Position;
            var detected = Detect(stream);
            try
            {
                using var streamed = new StreamReader(stream, Strict(detected.Encoding), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                return OperationResult<Workbook>.Ok(Fill(streamed, detected));
            }
            catch (DecoderFallbackException)
            {
                stream.Position = origin;
            }
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var whole = _encodings.Decode(buffer.ToArray());
        var options = sniffer.Sniff(Head(whole.Text), _options) with { Encoding = whole.Encoding };
        using var decoded = new StringReader(whole.Text);
        return OperationResult<Workbook>.Ok(Fill(decoded, options));
    }

    private Encoding Strict(Encoding encoding) =>
        encoding.CodePage == Encoding.UTF8.CodePage ? new UTF8Encoding(false, true) : encoding;

    private Workbook Fill(TextReader reader, CsvOptions options)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet(_options.SheetName);

        var rowIndex = 0;
        var skipped = 0;

        foreach (var fields in delimited.Read(reader, options.Delimiter))
        {
            if (skipped++ < options.SkipRows) continue;

            for (var column = 0; column < Math.Min(fields.Count, CellAddress.MaxColumns); column++)
            {
                var field = values.Interpret(fields[column], options);
                if (field.Value.IsEmpty) continue;

                var address = new CellAddress(rowIndex, column);
                sheet.SetValue(address, field.Value);
                if (_formats.TryGetValue(field.Kind, out var format))
                    sheet.SetStyle(address, workbook.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { NumberFormatId = format }));
            }

            rowIndex++;

            if (rowIndex >= CellAddress.MaxRows) break;
        }

        return workbook;
    }

    private string Head(string text) => text.Length > SampleLength ? text[..SampleLength] : text;
}

public sealed class CsvSpreadsheetWriter(
    IDelimitedText delimited, CsvOptions? options = null) : ISpreadsheetWriter
{
    private readonly CsvOptions _options = options ?? CsvOptions.Default;

    public SpreadsheetFormat Format => CsvFormats.Csv;

    public FidelityReport CheckFidelity(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var losses = new List<Diagnostic>();

        if (workbook.Sheets.Count > 1)
            losses.Add(Diagnostic.Warning("CSV001", $"CSV holds a single sheet: only '{workbook.Sheets[0].Name}' is saved.").With(workbook.Sheets[0].Name));

        var sheet = workbook.Sheets[0];

        if (sheet.NonEmptyCells().Any(pair => pair.Value.HasFormula))
            losses.Add(Diagnostic.Warning("CSV002", "Formulas are saved as their result."));

        if (sheet.NonEmptyCells().Any(pair => pair.Value.HasStyle))
            losses.Add(Diagnostic.Warning("CSV003", "Formatting does not exist in CSV."));

        if (sheet.Objects.Count > 0 || sheet.Merges.Count > 0)
            losses.Add(Diagnostic.Warning("CSV004", "Objects and merged cells do not exist in CSV."));

        if (!_options.GuardFormulas && sheet.NonEmptyCells().Any(pair => pair.Value.Value.Kind == ValueKind.Text && LooksLikeFormula(pair.Value.Value.AsText())))
            losses.Add(Diagnostic.Warning("ODF259", "Some text cells start with =, +, - or @: other spreadsheet programs may run them as formulas when they open the CSV file."));

        return new FidelityReport(losses);
    }

    private bool LooksLikeFormula(string text) => text.Length > 1 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r';

    private string Guarded(string text) => _options.GuardFormulas && LooksLikeFormula(text) ? "'" + text : text;

    public OperationResult<bool> Write(Workbook workbook, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(stream);

        var sheet = workbook.Sheets[0];
        var area = sheet.UsedRange();

        using var writer = new StreamWriter(stream, _options.Encoding, leaveOpen: true)
        {
            NewLine = _options.LineEnding,
        };

        if (area is not { } range)
        {
            writer.Flush();
            return OperationResult<bool>.Ok(true);
        }

        var formats = workbook.NumberFormats;

        for (var row = 0; row <= range.End.Row; row++)
        {
            var fields = new List<string>();

            for (var column = 0; column <= range.End.Column; column++)
                fields.Add(Render(sheet[new CellAddress(row, column)] with { StyleId = sheet.StyleIdAt(new CellAddress(row, column)) }, workbook, formats));

            writer.WriteLine(delimited.JoinLine(fields, _options.Delimiter));
        }

        writer.Flush();
        return OperationResult<bool>.Ok(true);
    }

    private string Render(Cell cell, Workbook workbook, NumberFormatCatalog formats)
    {
        var value = cell.Value;

        return value.Kind switch
        {
            ValueKind.Empty => string.Empty,
            ValueKind.Text => Guarded(value.AsText()),
            ValueKind.Boolean => value.AsBoolean()
                ? bool.TrueString.ToUpper(_options.Culture)
                : bool.FalseString.ToUpper(_options.Culture),
            ValueKind.Error => value.ErrorText,
            _ => Number(cell, value, workbook, formats),
        };
    }

    private string Number(Cell cell, CellValue value, Workbook workbook, NumberFormatCatalog formats)
    {
        var number = value.AsNumber();
        var format = formats.Get(workbook.Styles.Get(cell.StyleId).NumberFormatId);

        if (double.IsFinite(number) && number > -657435 && number < 2958466
            && format.Select(number).ValueKind == FormatValueKind.DateTime)
            return Moment(number);

        return number.ToString("R", _options.Culture);
    }

    private string Moment(double serial)
    {
        var moment = new DateTime(1899, 12, 30).AddDays(serial);
        var invariant = CultureInfo.InvariantCulture.Equals(_options.Culture);
        var hasTime = moment.TimeOfDay != TimeSpan.Zero;

        if (serial is >= 0 and < 1 && hasTime)
            return moment.ToString(invariant ? "HH:mm:ss" : "T", _options.Culture);

        if (hasTime)
            return moment.ToString(invariant ? "yyyy-MM-dd HH:mm:ss" : "G", _options.Culture);

        return moment.ToString(invariant ? "yyyy-MM-dd" : "d", _options.Culture);
    }
}
