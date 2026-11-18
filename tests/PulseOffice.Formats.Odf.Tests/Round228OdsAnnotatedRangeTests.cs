using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round228OdsAnnotatedRangeTests
{
    private static readonly CellAddress A1 = new(0, 0);

    [Theory]
    [InlineData(3, 5)]
    [InlineData(7, 3)]
    [InlineData(0, 8)]
    public void An_annotated_range_crosses_paragraphs(int start, int length)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(A1, CellValue.Text("Hello\nworld"));
        workbook.Sheets[0].Comments[A1] = new CellComment { Text = "n", AnnotatedText = new CommentTextSpan(start, length) };
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var read = new OdsReader().Read(stream).Value!.Sheets[0];

        Assert.Equal("Hello\nworld", read[A1].Value.AsText());
        Assert.Equal(new CommentTextSpan(start, length), read.Comments[A1].AnnotatedText);
    }
}
