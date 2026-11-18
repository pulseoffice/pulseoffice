using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round186DocxHeadingLookTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TableBlock Through(bool repeat)
    {
        var document = new TableSample().Build();
        document.Body.Blocks.OfType<TableBlock>().Single().RepeatHeaderRows = repeat;
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!.Body.Blocks.OfType<TableBlock>().Single();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AHeadingKeepsWhetherItRepeatsThroughDocx(bool repeat)
    {
        var table = Through(repeat);

        Assert.Equal(1, table.HeaderRows);
        Assert.Equal(repeat, table.RepeatHeaderRows);
    }
}
