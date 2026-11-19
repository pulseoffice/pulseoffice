using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class Round259OdtTocSourceTests
{
    [Fact]
    public void Source_styles_and_first_level_cross_the_odt()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var definition = new IndexDefinition(IndexKind.Contents) { Levels = 4, FirstLevel = 2, SourceStyles = IndexSourceStyles.None.With(DefaultStyleNames.TextBody, 3) };
        document.Body.Replace(
        [
            new SectionBlock("ix", "Table of Contents1", new BlockContainer([new ParagraphBlock("e", "Uno\t1", DefaultStyleNames.ContentsLevel(2))])) { Index = definition },
            new ParagraphBlock("p", "Testo"),
        ]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;

        var index = Assert.Single(new OdtReader().Read(stream).Value!.Body.Blocks.OfType<SectionBlock>()).Index!;

        Assert.Equal(2, index.FirstLevel);
        Assert.Equal(3, index.SourceStyles.LevelOf(DefaultStyleNames.TextBody));
    }
}
