using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ReviewRoundTripTests
{
    private const string Folder = "Fixtures/Review";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ReviewSample _sample = new();
    private readonly ChangeRegions _regions = new(new StoryCatalog(new BlockTree()));

    public static TheoryData<string> Routes() => new() { "odt-lo", "docx-lo", "odt-lo-docx", "docx-lo-odt" };

    private TextDocument Fixture(string name, ITextDocumentReader reader)
    {
        using var stream = File.OpenRead(Path.Combine(Folder, name));
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private TextDocument Route(string route) => route switch
    {
        "odt-lo" => Fixture("review.lo.odt", new OdtReader()),
        "docx-lo" => Fixture("review.lo.docx", _composer.Reader(DocxFlavour.Document)),
        "odt-lo-docx" => Fixture("review.odt.lo.docx", _composer.Reader(DocxFlavour.Document)),
        _ => Fixture("review.docx.lo.odt", new OdtReader()),
    };

    [Fact]
    public void IFileDiPartenzaSonoQuelliDelCampione()
    {
        var folder = Environment.GetEnvironmentVariable("PULSE_REVIEW_FIXTURES");
        if (string.IsNullOrEmpty(folder)) return;
        using (var odt = File.Create(Path.Combine(folder, "review.odt"))) Assert.True(new OdtWriter().Write(_sample.Build(), odt).Succeeded);
        using (var docx = File.Create(Path.Combine(folder, "review.docx"))) Assert.True(_composer.Writer(DocxFlavour.Document).Write(_sample.Build(), docx).Succeeded);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void LeRevisioniPassanoPerUnAltraSuite(string route)
    {
        var document = Route(route);
        var map = _regions.Map(document);
        var insertion = Assert.Single(map.Regions, region => region.Change is Insertion);
        Assert.Equal("Bruno", insertion.Change.Author);
        if (route is "odt-lo" or "odt-lo-docx") Assert.NotEqual(insertion.Start.ParagraphId, insertion.End.ParagraphId);
        var deletion = Assert.Single(map.Regions, region => region.Change is Deletion).Change as Deletion;
        Assert.Equal("cinque ", string.Concat(deletion!.Removed.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)));
        Assert.Contains(map.Regions, region => region.Change is FormatChange && region.Change.Author == "Anna");
        Assert.True(document.Changes.IsRecording);
        Assert.Equal(["Uno due", "tre quattro"], document.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void ICommentiConRisposteERisoltiPassanoPerUnAltraSuite(string route)
    {
        var document = Route(route);
        var root = Assert.Single(document.Comments.All, comment => comment.ParentId is null);
        Assert.Equal("Carla", root.Author);
        var reply = Assert.Single(document.Comments.All, comment => comment.ParentId is not null);
        Assert.Equal(root.Id, reply.ParentId);
        Assert.Equal("Anna", reply.Author);
        Assert.Equal("Così è.", string.Concat(reply.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)));
        Assert.True(reply.Resolved);
    }
}
