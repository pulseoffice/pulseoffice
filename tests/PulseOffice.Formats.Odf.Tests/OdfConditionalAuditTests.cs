using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfConditionalAuditTests
{
    private readonly XNamespace _calcext = OdfPackage.Namespaces.Calcext;

    [Theory]
    [InlineData(ConditionOperator.ContainsText, "a,b")]
    [InlineData(ConditionOperator.BeginsWith, "dice \"ciao\"")]
    [InlineData(ConditionOperator.EndsWith, "\"")]
    [InlineData(ConditionOperator.Equal, "\"x\"")]
    public void Il_testo_della_condizione_attraversa_il_giro_intatto(ConditionOperator op, string operand)
    {
        var rule = Reopened(new ConditionalRule { Range = Range(), Operator = op, Operands = [operand] }, _ => { });

        Assert.Equal([operand], rule.Operands);
    }

    [Fact]
    public void Un_numero_con_separatore_delle_migliaia_resta_un_operando_solo()
    {
        var rule = Reopened(new ConditionalRule { Range = Range(), Operator = ConditionOperator.Between, Operands = ["1,000", "5"] }, _ => { });

        Assert.Equal(["1,000", "5"], rule.Operands);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Una_soglia_non_finita_non_entra_nel_modello(string value)
    {
        var rule = Reopened(new ConditionalRule { Range = Range(), Visual = ConditionalVisual.ColorScale, ScaleColors = ["#F8696B", "#63BE7B"],
                Thresholds = [new ConditionalThreshold(ConditionalThresholdKind.Number, 1), new ConditionalThreshold(ConditionalThresholdKind.Number, 9)] },
            content => content.Descendants(_calcext + "color-scale-entry").First().SetAttributeValue(_calcext + "value", value));

        Assert.All(rule.Thresholds, threshold => Assert.True(double.IsFinite(threshold.Value)));
    }

    private CellRange Range()
    {
        Assert.True(CellRange.TryParseA1("A1:A3", out var range));
        return range;
    }

    private ConditionalRule Reopened(ConditionalRule rule, Action<XDocument> change)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.ConditionalRules.Add(rule);
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var input = entry.Open()) content = XDocument.Load(input);
            change(content);
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            content.Save(output, SaveOptions.DisableFormatting);
        }
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return Assert.Single(result.Value!.Sheets[0].ConditionalRules);
    }
}
