using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests.PropertyAudit;

public sealed class TextPropertyAuditTests
{
    private readonly DirectFormatting _formatting = new();

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private readonly ModelPropertyProbe _probe = new(new Dictionary<string, object>
    {
        ["Language"] = "de-DE",
        ["Font"] = "Arial",
        ["FontSize"] = 14.0,
        ["ScaleWidth"] = 150.0,
        ["Highlight"] = 0xFFFFFF00u,
        ["PageStyleName"] = DefaultStyleNames.Standard,
        ["PageNumberRestart"] = 5,
        ["LineSpacing"] = 1.5,
        ["SpaceBefore"] = 12.0,
        ["SpaceAfter"] = 6.0,
        ["MarginLeft"] = 36.0,
        ["MarginRight"] = 18.0,
        ["Indent"] = 9.0,
        ["TabStops.Item.Position"] = 72.0,
        ["DropCap.Lines"] = 3,
        ["DropCap.Characters"] = 1,
        ["DropCap.Distance"] = 4.0,
        ["DropCap.CharacterStyleName"] = null!,
        ["LineHeight.Value"] = 1.5,
        ["*.Width"] = 1.0,
        ["DropCap.WholeWord"] = false,
    });

    private TextDocument Character(object properties)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var run = _formatting.Set(new TextRun("Sonda"), (CharacterProperties)properties);
        document.Body.Add(new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns([run])));
        return document;
    }

    private TextDocument Paragraph(object properties)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var paragraph = _formatting.Set(new TextParagraph(string.Empty) { StyleName = DefaultStyleNames.Standard }.WithRuns([new TextRun("Sonda")]), (ParagraphProperties)properties);
        document.Body.Add(new ParagraphBlock(document.NextId("p"), paragraph));
        return document;
    }

    private ParagraphProperties Effective(TextDocument document, ParagraphProperties direct) => direct with
    {
        PageStyleName = direct.PageStyleName ?? document.InitialPageStyle,
        LineSpacing = direct.LineSpacing ?? (direct.LineHeight is { Mode: LineHeightMode.Proportional } proportional ? proportional.Value : null),
    };

    private TextParagraph? First(TextDocument document) => document.Body.Blocks.OfType<ParagraphBlock>().FirstOrDefault()?.Content;

    private TextDocument Through(ITextDocumentWriter writer, ITextDocumentReader reader, TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    private IEnumerable<string> Silent(Func<object, TextDocument> place, Func<TextDocument, object?> recover, object baseline)
    {
        var audit = new CodecPropertyAudit<TextDocument>(_probe, place, recover);
        var odt = new OdtWriter();
        var docx = _composer.Writer(DocxFlavour.Document);
        return audit.Silent(baseline, "odt", odt.CheckFidelity, document => Through(odt, new OdtReader(), document))
            .Concat(audit.Silent(baseline, "docx", docx.CheckFidelity, document => Through(docx, _composer.Reader(DocxFlavour.Document), document)))
            .Select(finding => $"{finding.Format} {finding.Path}: {finding.Expected} -> {finding.Actual}");
    }

    [Fact]
    public void Ogni_proprieta_di_carattere_attraversa_ODT_e_DOCX_o_la_perdita_e_dichiarata()
    {
        var silent = Silent(Character, document => First(document)?.Runs.FirstOrDefault() is { } run ? _formatting.Run(run) : null, CharacterProperties.None).ToList();
        Assert.True(silent.Count == 0, string.Join(Environment.NewLine, silent));
    }

    [Fact]
    public void Ogni_proprieta_di_paragrafo_attraversa_ODT_e_DOCX_o_la_perdita_e_dichiarata()
    {
        var silent = Silent(Paragraph, document => First(document) is { } paragraph ? Effective(document, _formatting.Paragraph(paragraph)) : null, ParagraphProperties.None).ToList();
        Assert.True(silent.Count == 0, string.Join(Environment.NewLine, silent));
    }
}
