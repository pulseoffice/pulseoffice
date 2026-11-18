using System.IO.Compression;
using System.Text.RegularExpressions;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round229PptxSectionTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void Slides_before_the_first_section_are_written_in_a_leading_section_so_every_slide_has_one()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        for (var i = 0; i < 3; i++) deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[2].SectionName = "Late";

        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(zip.GetEntry("ppt/presentation.xml")!.Open());
        var xml = reader.ReadToEnd();

        Assert.Equal(2, Regex.Count(xml, "<p14:section "));
        Assert.Equal(3, Regex.Count(xml, "<p14:sldId "));
        stream.Position = 0;
        var read = new PptxReader(_themes, _masters).Read(stream).Value!;
        Assert.Equal("Late", read.Slides[2].SectionName);
        Assert.Null(read.Slides[1].SectionName);
    }
}
