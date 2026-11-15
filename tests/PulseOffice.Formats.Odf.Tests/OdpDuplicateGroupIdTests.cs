using System.IO.Compression;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdpDuplicateGroupIdTests
{
    [Fact]
    public void Groups_sharing_an_id_on_one_slide_do_not_make_the_file_unreadable()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));

        using var stream = new MemoryStream();
        new OdpWriter().Write(deck, stream);
        const string group = "<draw:g xml:id=\"id1\" draw:id=\"id1\" draw:name=\"Diagram 1\"><draw:rect svg:x=\"0cm\" svg:y=\"0cm\" svg:width=\"1cm\" svg:height=\"1cm\"/><draw:rect svg:x=\"2cm\" svg:y=\"0cm\" svg:width=\"1cm\" svg:height=\"1cm\"/></draw:g>";
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            var page = System.Text.RegularExpressions.Regex.Match(content, "<draw:page [^>]*?(/?)>");
            Assert.True(page.Success);
            content = page.Groups[1].Value == "/"
                ? content[..page.Index] + page.Value[..^2] + ">" + group + group + "</draw:page>" + content[(page.Index + page.Length)..]
                : content[..(page.Index + page.Length)] + group + group + content[(page.Index + page.Length)..];
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }

        stream.Position = 0;
        var result = new OdpReader(themes, masters).Read(stream);

        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var slide = Assert.Single(result.Value!.Slides);
        Assert.Equal(2, slide.Groups.Count);
        Assert.Equal(4, slide.Groups.SelectMany(member => member.Members).Distinct().Count());
    }

    [Fact]
    public void Shapes_sharing_an_id_stay_in_their_own_groups()
    {
        const string first = "<draw:g draw:name=\"Diagram 1\"><draw:rect xml:id=\"s1\" draw:id=\"s1\" svg:x=\"0cm\" svg:y=\"0cm\" svg:width=\"1cm\" svg:height=\"1cm\"/><draw:rect xml:id=\"s1\" draw:id=\"s1\" svg:x=\"2cm\" svg:y=\"0cm\" svg:width=\"1cm\" svg:height=\"1cm\"/></draw:g>";
        const string second = "<draw:g draw:name=\"Diagram 2\"><draw:rect xml:id=\"s1\" draw:id=\"s1\" svg:x=\"4cm\" svg:y=\"0cm\" svg:width=\"1cm\" svg:height=\"1cm\"/><draw:rect svg:x=\"6cm\" svg:y=\"0cm\" svg:width=\"1cm\" svg:height=\"1cm\"/></draw:g>";

        var slide = ReadWith(first + second);

        Assert.Equal(2, slide.Groups.Count);
        Assert.All(slide.Groups, group => Assert.Equal(2, group.Members.Count));
        Assert.Equal(4, slide.Groups.SelectMany(group => group.Members).Distinct().Count());
    }

    private static Slide ReadWith(string shapes)
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));

        using var stream = new MemoryStream();
        new OdpWriter().Write(deck, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            var page = System.Text.RegularExpressions.Regex.Match(content, "<draw:page [^>]*?(/?)>");
            Assert.True(page.Success);
            content = page.Groups[1].Value == "/"
                ? content[..page.Index] + page.Value[..^2] + ">" + shapes + "</draw:page>" + content[(page.Index + page.Length)..]
                : content[..(page.Index + page.Length)] + shapes + content[(page.Index + page.Length)..];
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }

        stream.Position = 0;
        var result = new OdpReader(themes, masters).Read(stream);
        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.Single(result.Value!.Slides);
    }
}
