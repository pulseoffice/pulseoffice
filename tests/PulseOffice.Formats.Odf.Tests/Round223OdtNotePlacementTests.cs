using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round223OdtNotePlacementTests
{
    [Theory]
    [InlineData(NotePlacement.PageBottom, NotePlacement.DocumentEnd)]
    [InlineData(NotePlacement.BelowText, NotePlacement.SectionEnd)]
    [InlineData(NotePlacement.SectionEnd, NotePlacement.SectionEnd)]
    [InlineData(NotePlacement.DocumentEnd, NotePlacement.DocumentEnd)]
    public void Note_placements_cross_odt(NotePlacement footnotes, NotePlacement endnotes)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([new ParagraphBlock("sp", "inside", DefaultStyleNames.Standard)])));
        document.Notes.FootnoteSettings = document.Notes.FootnoteSettings with { Placement = footnotes };
        document.Notes.EndnoteSettings = document.Notes.EndnoteSettings with { Placement = endnotes };

        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = new OdtReader().Read(stream);

        Assert.Equal(footnotes, read.Value!.Notes.FootnoteSettings.Placement);
        Assert.Equal(endnotes, read.Value.Notes.EndnoteSettings.Placement);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODT290");
    }
}
