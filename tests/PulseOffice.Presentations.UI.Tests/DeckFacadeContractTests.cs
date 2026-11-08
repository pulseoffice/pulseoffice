using PulseOffice.Foundation.Zoom;
using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Scripting;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckFacadeContractTests
{
    private sealed class DirectDispatcher : IScriptDispatcher
    {
        public T Read<T>(Func<T> read) => read();

        public void Change(Action change) => change();

        public void Show(Action show) => show();

        public T Write<T>(Func<T> write) => write();
    }

    private sealed class Fixture
    {
        private int _current;

        public Fixture()
        {
            Deck.Master = new MasterFactory().CreateStandard(Deck.Theme);
            Actions = new DeckActions(Deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog(), () => IsReadOnly);
            Actions.AddSlide(SlideLayout.TitleSlide, -1);
            Actions.AddSlide(SlideLayout.TitleAndContent, 0);
            var context = new ActiveDeckContext();
            context.Attach(new DeckTarget(Deck, Actions, () => null, () => _current, () => { },
                _ => { }, () => false, () => null, () => [], _ => { }, index => _current = index, () => null,
                () => Task.CompletedTask, _ => { }, () => DeckViewMode.Normal, () => null,
                () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => false, _ => false,
                new DeckViewOptions(), _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null));
            Facade = new DeckFacade(new DirectDispatcher(), context);
        }

        public Presentation Deck { get; } = new();

        public DeckActions Actions { get; }

        public DeckFacade Facade { get; }

        public bool IsReadOnly { get; set; }

        public void ReadOnly(Action write)
        {
            IsReadOnly = true;
            Assert.Equal("The presentation is read-only.", Assert.Throws<InvalidOperationException>(write).Message);
            IsReadOnly = false;
        }

        public void Missing(Action use)
        {
            Assert.Contains($"has {Deck.Slides.Count} slides", Assert.Throws<ArgumentOutOfRangeException>(use).Message);
        }
    }

    private readonly IReadOnlyDictionary<string, Action<Fixture>> Cases = new Dictionary<string, Action<Fixture>>
    {
        ["Name"] = f => Assert.Equal(string.Empty, f.Facade.Name),
        ["SlideCount"] = f => Assert.Equal(2, f.Facade.SlideCount),
        ["CurrentSlide"] = f =>
        {
            f.Facade.GoTo(2);
            Assert.Equal(2, f.Facade.CurrentSlide);
        },
        ["MacroCount"] = f => Assert.Equal(f.Deck.Macros.All.Count, f.Facade.MacroCount),
        ["GoTo"] = f =>
        {
            Assert.True(f.Facade.GoTo(2));
            Assert.Equal(2, f.Facade.CurrentSlide);
            f.Missing(() => f.Facade.GoTo(3));
            Assert.Throws<ArgumentException>(() => f.Facade.GoTo(double.NaN));
        },
        ["AddSlide"] = f =>
        {
            Assert.Equal(3, f.Facade.AddSlide());
            Assert.Equal(3, f.Deck.Slides.Count);
            f.ReadOnly(() => f.Facade.AddSlide());
            Assert.Equal(3, f.Deck.Slides.Count);
        },
        ["DeleteSlide"] = f =>
        {
            f.Missing(() => f.Facade.DeleteSlide(0));
            f.ReadOnly(() => f.Facade.DeleteSlide(1));
            Assert.True(f.Facade.DeleteSlide(1));
            Assert.Single(f.Deck.Slides);
            Assert.Contains("at least one slide", Assert.Throws<InvalidOperationException>(() => f.Facade.DeleteSlide(1)).Message);
            Assert.Single(f.Deck.Slides);
        },
        ["Title"] = f =>
        {
            f.Facade.SetTitle(2, "Agenda");
            Assert.Equal("Agenda", f.Facade.Title(2));
            f.Missing(() => f.Facade.Title(3));
        },
        ["SetTitle"] = f =>
        {
            Assert.True(f.Facade.SetTitle(1, "Benvenuti"));
            Assert.Equal("Benvenuti", f.Deck.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
            f.Missing(() => f.Facade.SetTitle(9, "fuori"));
            f.ReadOnly(() => f.Facade.SetTitle(1, "bloccato"));
            var blank = f.Actions.AddSlide(SlideLayout.Blank, 1)!;
            Assert.False(f.Facade.SetTitle(f.Deck.IndexOf(blank) + 1, "senza segnaposto"));
        },
        ["Body"] = f =>
        {
            f.Facade.SetBody(2, "uno\ndue");
            Assert.Equal("uno\ndue", f.Facade.Body(2));
            f.Missing(() => f.Facade.Body(0));
        },
        ["SetBody"] = f =>
        {
            Assert.True(f.Facade.SetBody(1, "Sottotitolo"));
            Assert.Equal("Sottotitolo", f.Deck.Slides[0].Placeholder(PlaceholderRole.Subtitle)!.Text.PlainText);
            f.Missing(() => f.Facade.SetBody(3, "fuori"));
            f.ReadOnly(() => f.Facade.SetBody(2, "bloccato"));
        },
        ["Notes"] = f =>
        {
            f.Facade.SetNotes(1, "ricordare");
            Assert.Equal("ricordare", f.Facade.Notes(1));
            f.Missing(() => f.Facade.Notes(3));
        },
        ["SetNotes"] = f =>
        {
            Assert.True(f.Facade.SetNotes(2, "appunti"));
            Assert.Equal("appunti", f.Deck.Slides[1].Notes);
            f.Missing(() => f.Facade.SetNotes(3, "fuori"));
            f.ReadOnly(() => f.Facade.SetNotes(2, "bloccato"));
            Assert.Equal("appunti", f.Deck.Slides[1].Notes);
        },
        ["Hide"] = f =>
        {
            Assert.True(f.Facade.Hide(2, true));
            Assert.True(f.Deck.Slides[1].Hidden);
            f.Missing(() => f.Facade.Hide(3, true));
            f.ReadOnly(() => f.Facade.Hide(2, false));
            Assert.True(f.Deck.Slides[1].Hidden);
        },
    };

    [Fact]
    public void Ogni_membro_del_catalogo_di_Deck_ha_il_suo_caso()
    {
        var members = new DeckScriptApi().Roots.Single().Members.Select(member => member.Member).ToList();

        Assert.Empty(members.Except(Cases.Keys));
        Assert.Empty(Cases.Keys.Except(members));
    }

    [Fact]
    public void Ogni_membro_di_Deck_da_errore_chiaro_sugli_argomenti_sbagliati_ed_effetto_sui_giusti()
    {
        var failures = new List<string>();
        foreach (var member in new DeckScriptApi().Roots.Single().Members)
        {
            try
            {
                Cases[member.Member](new Fixture());
            }
            catch (Exception failure)
            {
                failures.Add($"{member.Member}: {failure.Message}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
