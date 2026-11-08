using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtFieldTextTests
{
    private readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    [Theory]
    [InlineData(TextFieldKind.Database, "Name")]
    [InlineData(TextFieldKind.CustomProperty, "Client")]
    [InlineData(TextFieldKind.Preserved, null)]
    public void Spaces_tabs_and_line_breaks_in_a_field_result_are_encoded_and_survive(TextFieldKind kind, string? argument)
    {
        const string shown = " A  B\tC\nD";
        var model = new PulseOffice.Documents.TextDocument();
        var field = new TextRun(shown) { Field = kind, FieldArgument = argument };
        model.Body.Replace([new ParagraphBlock("p1", new ParagraphBlock("p1", shown).Content.WithRuns([field]))]);

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(model, stream).Succeeded);
        var xml = Content(stream);
        Assert.Contains("<text:tab", xml, StringComparison.Ordinal);
        Assert.Contains("<text:line-break", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("\t", xml, StringComparison.Ordinal);

        stream.Position = 0;
        var read = new OdtReader().Read(stream);
        Assert.True(read.Succeeded);
        var run = read.Value!.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single(candidate => candidate.Field == kind);
        Assert.Equal(shown, run.Text);
    }

    [Fact]
    public void A_field_written_by_another_suite_with_encoded_spaces_keeps_them()
    {
        var model = new PulseOffice.Documents.TextDocument();
        model.Body.Replace([new ParagraphBlock("p1", new ParagraphBlock("p1", "x").Content.WithRuns([new TextRun("x") { Field = TextFieldKind.Title }]))]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(model, stream).Succeeded);
        var patched = Patched(stream, document =>
        {
            var title = document.Descendants(_text + "title").Single();
            title.ReplaceNodes("A", new XElement(_text + "s", new XAttribute(_text + "c", "2")), "B", new XElement(_text + "tab"), "C");
        });

        var read = new OdtReader().Read(patched);

        Assert.True(read.Succeeded);
        Assert.Equal("A  B\tC", read.Value!.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single(run => run.Field == TextFieldKind.Title).Text);
    }

    private string Content(MemoryStream stream)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private MemoryStream Patched(MemoryStream source, Action<XDocument> change)
    {
        var copy = new MemoryStream();
        source.Position = 0;
        source.CopyTo(copy);
        using (var archive = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument document;
            using (var input = entry.Open()) document = XDocument.Load(input);
            change(document);
            entry.Delete();
            using var output = archive.CreateEntry("content.xml").Open();
            document.Save(output, SaveOptions.DisableFormatting);
        }
        copy.Position = 0;
        return copy;
    }
}
