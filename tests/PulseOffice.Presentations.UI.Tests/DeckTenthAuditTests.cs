using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckTenthAuditTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulseoffice-deck-media-" + Guid.NewGuid().ToString("N"));
    private readonly IDeckMediaFiles _media = new DeckMediaFiles(new PulseOffice.Platform.ExecutableFileTypes(), new PulseOffice.Foundation.Network.NetworkSharePaths());

    public DeckTenthAuditTests() => Directory.CreateDirectory(Path.Combine(_folder, "clips"));

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories)) File.Delete(file);
        Directory.Delete(Path.Combine(_folder, "clips"));
        Directory.Delete(_folder);
    }

    private MediaElement Linked(string link) => new()
    {
        Id = "m",
        Kind = MediaKind.Video,
        Data = [],
        MimeType = "video/mp4",
        LinkedPath = link,
    };

    [Fact]
    public void Un_media_collegato_con_percorso_relativo_si_risolve_nella_cartella_del_documento()
    {
        var clip = Path.Combine(_folder, "clips", "intro video.mp4");
        File.WriteAllBytes(clip, [1, 2, 3]);
        var document = Path.Combine(_folder, "deck.odp");

        var source = Assert.IsType<LinkedMediaFile>(_media.Source(Linked("clips/intro%20video.mp4"), document));
        Assert.True(source.Exists);
        Assert.Equal(clip, source.Path);

        var missing = Assert.IsType<LinkedMediaFile>(_media.Source(Linked("clips/assente.mp4"), document));
        Assert.False(missing.Exists);
        Assert.IsType<LinkedMediaAddress>(_media.Source(Linked("https://example.org/a.mp4"), document));
        Assert.True(Assert.IsType<LinkedMediaFile>(_media.Source(Linked(new Uri(clip).AbsoluteUri), null)).Exists);
        Assert.IsType<EmbeddedMediaSource>(_media.Source(Linked(string.Empty), document));
    }

    [Fact]
    public void Un_media_collegato_a_un_file_che_esegue_codice_non_si_apre()
    {
        var script = Path.Combine(_folder, "clips", "intro.command");
        File.WriteAllBytes(script, [1]);
        var document = Path.Combine(_folder, "deck.odp");

        Assert.IsType<BlockedMediaFile>(_media.Source(Linked("clips/intro.command"), document));
        Assert.IsType<BlockedMediaFile>(_media.Source(Linked(script), document));
        Assert.IsType<BlockedMediaFile>(_media.Source(Linked(new Uri(script).AbsoluteUri), null));
    }

    [Fact]
    public void Raggruppare_un_gruppo_letto_con_un_altra_forma_lo_annida_e_l_annullamento_lo_ripristina()
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var factory = new SlideFactory();
        var history = new UndoStack();
        var actions = new DeckActions(deck, history, factory, new FakeTextCatalog());
        var editing = new SlideGroupEditing();
        var slide = factory.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var members = new[] { "a", "b", "c" }.Select((id, index) => new ObjectElement
        {
            Id = id,
            Bounds = new DocumentRect(new Twip(index * 2000), new Twip(0), new Twip(1000), new Twip(1000)),
            Content = new ShapeObject { Id = id, Shape = ShapeKind.Rectangle },
        }).ToList();
        foreach (var member in members) slide.Add(member);
        slide.AddGroup(new SlideGroup("read", ["a", "b"]));

        actions.ReplaceGroups(slide, editing.Group(slide, members, null, "g")!, "deck.action.group");
        Assert.Equal(["read", "c"], slide.Groups.Single(group => group.Id == "g").Members);
        Assert.Equal(2, slide.Groups.Count);
        history.Undo();
        Assert.Equal("read", Assert.Single(slide.Groups).Id);
        history.Redo();

        actions.ReplaceGroups(slide, editing.Dissolve(slide, slide.Groups.Single(group => group.Id == "g")), "deck.action.ungroup");
        Assert.Equal("read", Assert.Single(slide.Groups).Id);
        history.Undo();
        Assert.Equal(2, slide.Groups.Count);
    }
}
