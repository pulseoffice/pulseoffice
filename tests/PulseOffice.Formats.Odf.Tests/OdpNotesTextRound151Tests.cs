using System.IO.Compression;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdpNotesTextRound151Tests
{
    private readonly ThemeCatalog _themes = new();
    private readonly MasterFactory _masters = new();

    [Fact]
    public void Spaces_tabs_and_line_breaks_in_read_notes_are_kept()
    {
        using var stream = Written("first");
        Rewrite(stream, content => content.Replace(">first</text:p>",
            ">a<text:s text:c=\"2\"/>b<text:tab/>c<text:line-break/>d</text:p>", StringComparison.Ordinal));

        stream.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(stream).Value!;

        Assert.Equal("a  b\tc\nd", back.Slides[0].Notes);
    }

    [Fact]
    public void Repeated_spaces_and_tabs_in_notes_are_written_as_odf_whitespace_elements()
    {
        using var stream = Written("a   b\tc");
        string content;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var reader = new StreamReader(archive.GetEntry("content.xml")!.Open()))
            content = reader.ReadToEnd();

        Assert.Contains("<text:s", content, StringComparison.Ordinal);
        Assert.Contains("<text:tab", content, StringComparison.Ordinal);
        stream.Position = 0;
        Assert.Equal("a   b\tc", new OdpReader(_themes, _masters).Read(stream).Value!.Slides[0].Notes);
    }

    private MemoryStream Written(string notes)
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Notes = notes;
        var stream = new MemoryStream();
        new OdpWriter().Write(deck, stream);
        return stream;
    }

    private static void Rewrite(MemoryStream stream, Func<string, string> change)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true);
        var entry = archive.GetEntry("content.xml")!;
        string content;
        using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
        var changed = change(content);
        Assert.NotEqual(content, changed);
        entry.Delete();
        using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
        writer.Write(changed);
    }
}
