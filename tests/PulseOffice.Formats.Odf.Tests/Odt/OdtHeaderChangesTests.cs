using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtHeaderChangesTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

    private static TextDocument Sample()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo") { Marks = [new ChangeMark(0, "body1", MarkEdge.Start), new ChangeMark(5, "body1", MarkEdge.End)] }]);
        var page = document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        page.Header.Enabled = true;
        page.Header.Default.Add(new ParagraphBlock("hp1", "Titolo", DefaultStyleNames.Header)
        {
            Marks = [new ChangeMark(0, "head1", MarkEdge.Point), new ChangeMark(0, "head2", MarkEdge.Start), new ChangeMark(6, "head2", MarkEdge.End)],
        });
        var date = new DateTimeOffset(2014, 4, 22, 10, 38, 0, TimeSpan.Zero);
        document.Changes.Add(new Insertion("body1", "Ada", date));
        document.Changes.Add(new Deletion("head1", "Synerzip", date, new BlockContainer([new ParagraphBlock("dp1", "vecchio")])));
        document.Changes.Add(new Insertion("head2", "Synerzip", date));
        return document;
    }

    [Fact]
    public void LeRevisioniDellIntestazioneStannoNellIntestazioneENonNelCorpo()
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(Sample(), stream).Succeeded);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var content = XDocument.Load(zip.GetEntry("content.xml")!.Open());
        var styles = XDocument.Load(zip.GetEntry("styles.xml")!.Open());

        var bodyRegions = content.Descendants(Text + "changed-region").Select(region => (string?)region.Attribute(Text + "id")).ToList();
        Assert.Equal(["body1"], bodyRegions);
        var header = styles.Descendants(Style + "header").Single();
        var tracked = Assert.Single(header.Elements(Text + "tracked-changes"));
        Assert.Same(tracked, header.Elements().First());
        Assert.Equal(["head1", "head2"], tracked.Elements(Text + "changed-region").Select(region => (string?)region.Attribute(Text + "id")));
    }

    [Fact]
    public void LeRevisioniDellIntestazioneSopravvivonoAlGiro()
    {
        var again = OdtRoundTripTests.RoundTrip(Sample());
        Assert.Equal(["body1", "head1", "head2"], again.Changes.All.Select(change => change.Id).Order(StringComparer.Ordinal));
        var deletion = Assert.IsType<Deletion>(again.Changes.All.Single(change => change.Id == "head1"));
        Assert.Equal("vecchio", Assert.IsType<ParagraphBlock>(deletion.Removed[0]).Text);
        var header = Assert.IsType<ParagraphBlock>(again.Styles.Page.Find(DefaultStyleNames.Standard)!.Header.Default[0]);
        Assert.Contains(header.Marks.OfType<ChangeMark>(), mark => mark.ChangeId == "head2");
    }
}
