using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ReviewParityTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly ChangeRegions _regions = new(new StoryCatalog(new BlockTree()));
    private readonly ReviewSample _sample = new();

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        var written = writer.Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    internal string Describe(TextDocument document)
    {
        var map = _regions.Map(document);
        var paragraphs = document.Body.Blocks.OfType<ParagraphBlock>().ToList();
        string Where(DocumentPosition position) => paragraphs.FindIndex(paragraph => paragraph.Id == position.ParagraphId) + ":" + position.Offset;
        var changes = map.Regions.Select(region => region.Change.Kind + "/" + region.Change.Author + "@" + Where(region.Start) + "-" + Where(region.End)
            + (region.Change is Deletion deletion ? "[" + string.Concat(deletion.Removed.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)) + "]" : string.Empty));
        var comments = document.Comments.All.Select(comment => comment.Author + (comment.ParentId is null ? string.Empty : "↳") + (comment.Resolved ? "✓" : string.Empty)
            + ":" + string.Concat(comment.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text)));
        return string.Join("\n", changes.Concat(comments).Append("recording=" + document.Changes.IsRecording).Append("protection=" + document.Protection.Restriction));
    }

    [Fact]
    public void RevisioniECommentiAttraversanoOdtEDocxUguali()
    {
        var odt = Through(_sample.Build(), new OdtWriter(), new OdtReader());
        var docx = Through(_sample.Build(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));

        var expected = Describe(_sample.Build());
        Assert.Equal(expected, Describe(odt));
        Assert.Equal(expected, Describe(docx));
    }

    [Fact]
    public void LaProtezioneDelleSezioniConPasswordSiRileggeInOdt()
    {
        var document = _sample.Build();
        var keys = new ProtectionKeys();
        document.Body.Replace([new SectionBlock("s1", "Riservata", new BlockContainer([new ParagraphBlock("sp", "segreto")])) { Protected = true, ProtectionKey = keys.Digest("pw") }]);
        var read = Through(document, new OdtWriter(), new OdtReader());
        var section = Assert.IsType<SectionBlock>(read.Body[0]);
        Assert.True(section.Protected);
        Assert.True(keys.Matches(section.ProtectionKey, "pw"));
        Assert.False(keys.Matches(section.ProtectionKey, "altro"));
    }

    [Fact]
    public void LaProtezioneDiWordConPasswordSiRileggeEDocxNonPerdeLaChiave()
    {
        var document = _sample.Build();
        var salt = new byte[16];
        var key = new ProtectionKey("SHA-512", Hash("segreta", salt, 1000)) { Salt = salt, SpinCount = 1000 };
        document.Protection = new DocumentProtection(DocumentRestriction.ReadOnly, key);
        var keys = new ProtectionKeys();
        Assert.True(keys.Matches(key, "segreta"));

        var docx = Through(document, _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));
        Assert.Equal(DocumentRestriction.ReadOnly, docx.Protection.Restriction);
        Assert.True(keys.Matches(docx.Protection.Key, "segreta"));
        var odt = Through(document, new OdtWriter(), new OdtReader());
        Assert.True(keys.Matches(odt.Protection.Key, "segreta"));
        var odfKeyed = _sample.Build();
        odfKeyed.Protection = new DocumentProtection(DocumentRestriction.ReadOnly, keys.Create("segreta"));
        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(odfKeyed).Losses, loss => loss.Code == "DOCX026");
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX026");
    }

    private byte[] Hash(string password, byte[] salt, int spin)
    {
        using var sha = System.Security.Cryptography.SHA512.Create();
        var current = sha.ComputeHash([.. salt, .. System.Text.Encoding.Unicode.GetBytes(password)]);
        for (var i = 0; i < spin; i++) current = sha.ComputeHash([.. current, .. BitConverter.GetBytes(i)]);
        return current;
    }
}
