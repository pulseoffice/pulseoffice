using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class TransitionDirectionSpecTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Theory]
    [InlineData(TransitionKind.Push, TransitionDirection.Left, "<p:push dir=\"l\"")]
    [InlineData(TransitionKind.Push, TransitionDirection.Right, "<p:push dir=\"r\"")]
    [InlineData(TransitionKind.Push, TransitionDirection.Up, "<p:push dir=\"u\"")]
    [InlineData(TransitionKind.Push, TransitionDirection.Down, "<p:push dir=\"d\"")]
    [InlineData(TransitionKind.Cover, TransitionDirection.Left, "<p:cover dir=\"l\"")]
    [InlineData(TransitionKind.Cover, TransitionDirection.Right, "<p:cover dir=\"r\"")]
    [InlineData(TransitionKind.Cover, TransitionDirection.Up, "<p:cover dir=\"u\"")]
    [InlineData(TransitionKind.Cover, TransitionDirection.Down, "<p:cover dir=\"d\"")]
    public void LaDirezioneInPptxEQuellaDelMovimentoComeInEcma376(TransitionKind kind, TransitionDirection direction, string expected)
    {
        var xml = SlideXml(Deck(new SlideTransition { Kind = kind, Direction = direction }));

        Assert.Contains(expected, xml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TransitionKind.Cover, "fromRight", "<p:cover dir=\"l\"")]
    [InlineData(TransitionKind.Cover, "fromBottom", "<p:cover dir=\"u\"")]
    [InlineData(TransitionKind.Push, "fromRight", "<p:push dir=\"l\"")]
    [InlineData(TransitionKind.Push, "fromTop", "<p:push dir=\"d\"")]
    public void UnaTransizioneCheEntraDaDestraVaVersoSinistra(TransitionKind kind, string odfSubtype, string expected)
    {
        var read = ThroughOdp(Deck(Directed(kind, odfSubtype)));

        Assert.Contains(expected, SlideXml(read), StringComparison.Ordinal);
    }

    private SlideTransition Directed(TransitionKind kind, string odfSubtype) => new()
    {
        Kind = kind,
        Direction = odfSubtype switch
        {
            "fromRight" => TransitionDirection.Left,
            "fromLeft" => TransitionDirection.Right,
            "fromBottom" => TransitionDirection.Up,
            _ => TransitionDirection.Down,
        },
    };

    private Presentation Deck(SlideTransition transition)
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        slide.Transition = transition;
        deck.AddExisting(slide);
        return deck;
    }

    private Presentation ThroughOdp(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return new OdpReader(_themes, _masters).Read(stream).Value!;
    }

    private string SlideXml(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        using var archive = new ZipArchive(stream);
        using var reader = new StreamReader(archive.GetEntry("ppt/slides/slide1.xml")!.Open());
        return reader.ReadToEnd();
    }
}
