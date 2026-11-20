using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round296StyleReferenceTests
{
    private readonly DocxFieldInstructions _fields = new(new PulseOffice.Formats.PreservedFieldTable(), new WordDatePictures());

    [Theory]
    [InlineData(" STYLEREF \"Heading 2\" \\n ", TextFieldKind.Chapter)]
    [InlineData(" STYLEREF 1 \\* MERGEFORMAT ", TextFieldKind.Chapter)]
    [InlineData(" STYLEREF \"Quote\" ", TextFieldKind.Preserved)]
    [InlineData(" STYLEREF \"Überschrift 2\" ", TextFieldKind.Chapter)]
    [InlineData(" STYLEREF \"Titolo 3\" \\n ", TextFieldKind.Chapter)]
    [InlineData(" STYLEREF \"Heading 1\" \\l ", TextFieldKind.Preserved)]
    public void Only_heading_style_references_become_chapter_fields(string instruction, TextFieldKind kind)
    {
        Assert.Equal(kind, _fields.Parse(instruction)!.Kind);
    }
}
