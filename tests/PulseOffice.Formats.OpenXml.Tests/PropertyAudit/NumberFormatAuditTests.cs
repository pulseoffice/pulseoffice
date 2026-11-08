using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests.PropertyAudit;

public sealed class NumberFormatAuditTests
{
    private readonly CellAddress _probed = new(0, 0);

    private readonly IReadOnlyList<string> _custom =
    [
        "#,##0.000", "0.0%", "[Red]-#,##0.00", "#,##0.00 [$€-410]", "dd/mm/yyyy hh:mm", "[h]:mm:ss", "0.00E+00",
        "# ?/?", "@", "\"Totale:\" 0", "#,##0;(#,##0);\"zero\"", "yyyy-mm-dd", "mmmm yyyy", "[$-410]dddd d mmmm yyyy",
    ];

    private IEnumerable<string> Codes() =>
        NumberFormatCatalog.BuiltInFormats.Where(pair => pair.Key != 0).Select(pair => pair.Value).Concat(_custom).Distinct();

    private Workbook Place(string code)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(_probed, CellValue.Number(45123.375));
        sheet.SetStyle(_probed, workbook.Styles.Intern(CellStyle.Default with { NumberFormatId = workbook.NumberFormats.Register(code) }));
        return workbook;
    }

    private readonly NumberFormatter _formatter = new(System.Globalization.CultureInfo.InvariantCulture, DateTime.FromOADate);

    private readonly double[] _samples = [45123.375, -1234.5, 0, 0.25];

    private string Recover(Workbook workbook) =>
        workbook.NumberFormats.CodeOf(workbook.Styles.Get(workbook.Sheets[0].StyleIdAt(_probed)).NumberFormatId);

    private string Rendered(string code)
    {
        var catalog = new NumberFormatCatalog();
        var format = catalog.Get(catalog.Register(code));
        return string.Join(" | ", _samples.Select(sample => _formatter.Format(CellValue.Number(sample), format, 40))
            .Select(cell => $"{cell.Text}/{cell.ColorName?.ToUpperInvariant()}"));
    }

    private Workbook Through(ISpreadsheetWriter writer, ISpreadsheetReader reader, Workbook workbook)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(workbook, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    [Fact]
    public void Ogni_formato_numerico_predefinito_e_campione_attraversa_ODS_e_XLSX_o_la_perdita_e_dichiarata()
    {
        var formats = new (string Name, ISpreadsheetWriter Writer, ISpreadsheetReader Reader)[]
        {
            ("ods", new OdsSpreadsheetWriter(), new OdsSpreadsheetReader()),
            ("xlsx", new XlsxSpreadsheetWriter(), new XlsxSpreadsheetReader()),
        };

        var silent = new List<string>();
        foreach (var (name, writer, reader) in formats)
        {
            var known = writer.CheckFidelity(Place("0")).Losses.Select(loss => loss.Code).ToHashSet();
            foreach (var code in Codes())
            {
                var workbook = Place(code);
                var back = Recover(Through(writer, reader, workbook));
                if (back == code || Rendered(back) == Rendered(code)) continue;
                if (!writer.CheckFidelity(workbook).Losses.Select(loss => loss.Code).ToHashSet().IsSubsetOf(known)) continue;
                silent.Add($"{name} {code} -> {back}");
            }
        }

        Assert.True(silent.Count == 0, string.Join(Environment.NewLine, silent));
    }
}
