using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests.PropertyAudit;

public sealed class CellStylePropertyAuditTests
{
    private readonly CellAddress _probed = new(1, 1);

    private readonly ModelPropertyProbe _probe;

    public CellStylePropertyAuditTests() => _probe = new(new Dictionary<string, object>
    {
        ["Language"] = "de-DE",
        ["Font.Family"] = "Arial",
        ["@Twip"] = new Twip(30),
        ["BackgroundHex"] = "#3366CC",
        ["*.ColorHex"] = "#3366CC",
        ["@BorderEdge"] = Pens(),
    });

    private object[] Pens()
    {
        var presets = new BorderPresets();
        return [.. presets.LineStyles.Select(style => presets.Pen(style, "#3366CC"))];
    }

    private Workbook Place(object style)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(_probed, CellValue.Number(1234.5));
        sheet.SetStyle(_probed, workbook.Styles.Intern((CellStyle)style));
        return workbook;
    }

    private object Recover(Workbook workbook) => workbook.Styles.Get(workbook.Sheets[0].StyleIdAt(_probed));

    private Workbook Through(ISpreadsheetWriter writer, ISpreadsheetReader reader, Workbook workbook)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(workbook, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    [Fact]
    public void Ogni_proprieta_dello_stile_di_cella_attraversa_ODS_e_XLSX_o_la_perdita_e_dichiarata()
    {
        var audit = new CodecPropertyAudit<Workbook>(_probe, Place, Recover);
        var ods = new OdsSpreadsheetWriter();
        var xlsx = new XlsxSpreadsheetWriter();

        var silent = audit.Silent(CellStyle.Default, "ods", ods.CheckFidelity,
                workbook => Through(ods, new OdsSpreadsheetReader(), workbook))
            .Concat(audit.Silent(CellStyle.Default, "xlsx", xlsx.CheckFidelity,
                workbook => Through(xlsx, new XlsxSpreadsheetReader(), workbook)))
            .Select(finding => $"{finding.Format} {finding.Path}: {finding.Expected} -> {finding.Actual}")
            .ToList();

        Assert.True(silent.Count == 0, string.Join(Environment.NewLine, silent));
    }

    [Fact]
    public void La_sonda_enumera_ogni_proprieta_del_modello_comprese_le_annidate()
    {
        var paths = _probe.Variants(CellStyle.Default).Select(variant => variant.Path).ToHashSet();

        Assert.Contains("Font.Bold", paths);
        Assert.Contains("Borders.DiagonalUp", paths);
        Assert.Contains("Locked", paths);
        Assert.Contains("NumberFormatId", paths);
    }
}
