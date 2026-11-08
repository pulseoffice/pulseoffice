using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxShapeThemeColourTests
{
    private const string Sample = "/Volumes/Dati/develop/libreoffice-ref/sw/qa/extras/ooxmlexport/data/dml-groupshape-capitalization.docx";

    [Fact]
    public void LeFormeDocxPrendonoRiempimentoBordoEColoreDelTestoDalTemaComeWord()
    {
        Assert.SkipUnless(File.Exists(Sample), "Reference corpus not available.");
        using var stream = File.OpenRead(Sample);
        var document = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Reader(DocxFlavour.Document).Read(stream).Require();

        var content = Assert.IsType<EmbeddedContent>(document.Objects.All.Single().Content);
        var group = Assert.IsType<ShapeGroupObject>(content.Value);
        var members = group.Children.Select(child => Assert.IsType<ShapeObject>(child.Content)).ToList();
        Assert.All(members, member => Assert.Equal("#4F81BD", member.Style.Fill.ColorHex, ignoreCase: true));
        var story = Assert.Single(content.MemberStories.Values);
        var runs = story.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Where(run => run.Text.Length > 0).ToList();
        Assert.NotEmpty(runs);
        Assert.All(runs, run => Assert.Equal(0xFFFFFFFFu, run.Color));
    }
}
