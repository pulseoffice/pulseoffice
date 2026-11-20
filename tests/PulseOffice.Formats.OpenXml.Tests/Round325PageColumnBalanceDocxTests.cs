using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round325PageColumnBalanceDocxTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private IReadOnlyList<string> Losses(bool marked, bool section)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Page.Find(document.InitialPageStyle)!.Columns = new SectionColumns(2, Twip.FromCentimeters(1)) { BalancedBeforeSection = marked };
        document.Body.Add(new ParagraphBlock("p", "text", DefaultStyleNames.Standard));
        if (section)
            document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([new ParagraphBlock("sp", "inside", DefaultStyleNames.Standard)])));
        return [.. _composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses.Select(loss => loss.Code)];
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Unmarked_page_columns_before_a_section_are_declared_balanced_by_word(bool marked, bool section, bool declared)
    {
        Assert.Equal(declared, Losses(marked, section).Contains("DOCX038"));
    }
}
