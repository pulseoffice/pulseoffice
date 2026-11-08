using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.UI.Controls.Services;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Scripting;

public sealed class DeckScriptHost(PulseOffice.Exchange.IObjectExchange exchange, IScriptClipboardOperations operations) : IScriptHost
{
    public string ModuleName => "Deck";

    public bool HasClipboard => exchange.CanPaste(ModuleKind.Presentations);

    public Task Copy() => operations.RunAsync("command.deck.clipboard.copy.title", () => exchange.CopyAsync(ModuleKind.Presentations));

    public Task Cut() => operations.RunAsync("command.deck.clipboard.cut.title", () => exchange.CutAsync(ModuleKind.Presentations));

    public Task Paste() => operations.RunAsync("command.deck.clipboard.paste.title", () => exchange.PasteAsync(ModuleKind.Presentations));

    public Task PasteValues() => operations.RunAsync("command.deck.clipboard.paste.title", () => exchange.PasteAsync(ModuleKind.Presentations, PulseOffice.Exchange.ExchangePasteMode.UnformattedText));
}

public sealed class DeckFacade(IScriptDispatcher dispatcher, IActiveDeckContext context) : ScriptFacade(dispatcher)
{
    public string Name => Read(() => Target()?.Deck.FileName ?? string.Empty);

    public int SlideCount => Read(() => Target()?.Deck.Slides.Count ?? 0);

    public int CurrentSlide => Read(() => Target() is { } t ? t.CurrentIndex() + 1 : 0);

    public int MacroCount => Read(() => Target()?.Deck.Macros.All.Count ?? 0);

    public bool GoTo(double number)
    {
        Show(() =>
        {
            var target = Open();
            Existing(number);
            target.GoToSlide(Index(number));
        });
        return true;
    }

    public int AddSlide()
    {
        var added = 0;
        Change(() =>
        {
            var target = Writable();
            var before = target.Deck.Slides.Count;
            if (target.Actions.AddSlide(SlideLayout.TitleAndContent, before - 1) is not { } slide)
                throw new InvalidOperationException("The slide could not be added.");
            added = target.Deck.IndexOf(slide) + 1;
        });
        return added;
    }

    public bool DeleteSlide(double number)
    {
        Change(() =>
        {
            var target = Writable();
            var slide = Existing(number);
            if (target.Deck.Slides.Count <= 1)
                throw new InvalidOperationException("A presentation keeps at least one slide: the only slide cannot be deleted.");
            target.Actions.DeleteSlide(slide);
        });
        return true;
    }

    public string Title(double number) => Text(number, _ => PlaceholderRole.Title);

    public bool SetTitle(double number, string text) => SetText(number, _ => PlaceholderRole.Title, text);

    public string Body(double number) => Text(number, BodyRole);

    public bool SetBody(double number, string text) => SetText(number, BodyRole, text);

    public string Notes(double number) => Read(() => Existing(number).Notes ?? string.Empty);

    public bool SetNotes(double number, string text)
    {
        Change(() =>
        {
            var target = Writable();
            target.Actions.SetNotes(Existing(number), text ?? string.Empty);
        });
        return true;
    }

    public bool Hide(double number, bool hidden)
    {
        Change(() =>
        {
            var target = Writable();
            target.Actions.SetHidden(Existing(number), hidden);
        });
        return true;
    }

    private DeckTarget? Target() => context.Target;

    private DeckTarget Open() => Target() ?? throw new InvalidOperationException("No presentation is open.");

    private DeckTarget Writable()
    {
        var target = Open();
        if (target.Actions.IsReadOnly) throw new InvalidOperationException("The presentation is read-only.");
        return target;
    }

    private int Index(double number) =>
        double.IsFinite(number)
            ? (int)Math.Round(number) - 1
            : throw new ArgumentException($"The slide must be a number, not {Number(number)}.", "slide");

    private Slide Existing(double number)
    {
        var target = Open();
        var index = Index(number);
        var count = target.Deck.Slides.Count;
        return index >= 0 && index < count
            ? target.Deck.Slides[index]
            : throw new ArgumentOutOfRangeException("slide",
                $"There is no slide {Number(number)}: the presentation has {count} slides, numbered from 1.");
    }

    private string Number(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string Text(double number, Func<Slide, PlaceholderRole> role) =>
        Read(() => Existing(number) is var slide && slide.Placeholder(role(slide)) is { } placeholder ? placeholder.Text.PlainText : string.Empty);

    private bool SetText(double number, Func<Slide, PlaceholderRole> role, string text)
    {
        var done = false;
        Change(() =>
        {
            var target = Writable();
            var slide = Existing(number);
            if (slide.Placeholder(role(slide)) is null) return;
            target.Actions.SetSlideText(slide, role(slide), text ?? string.Empty);
            done = true;
        });
        return done;
    }

    private PlaceholderRole BodyRole(Slide slide) =>
        slide.Placeholder(PlaceholderRole.Body) is null && slide.Placeholder(PlaceholderRole.Subtitle) is not null
            ? PlaceholderRole.Subtitle
            : PlaceholderRole.Body;
}

public sealed class DeckScriptApi : IScriptApiSection
{
    public IEnumerable<ApiRootSpec> Roots { get; } =
    [
        new(ModuleKind.Presentations, "deck", "Presentation",
        [
            new("Name", "string", "Name: string", []),
            new("SlideCount", "number", "SlideCount: number", []),
            new("CurrentSlide", "number", "CurrentSlide: number", []),
            new("MacroCount", "number", "MacroCount: number", []),
            new("GoTo", "boolean", "GoTo(slide)", ["slide:slideNumber"]),
            new("AddSlide", "number", "AddSlide()", []),
            new("DeleteSlide", "boolean", "DeleteSlide(slide)", ["slide:slideNumber"]),
            new("Title", "string", "Title(slide)", ["slide:slideNumber"]),
            new("SetTitle", "boolean", "SetTitle(slide, text)", ["slide:slideNumber", "text:slideTitle"]),
            new("Body", "string", "Body(slide)", ["slide:slideNumber"]),
            new("SetBody", "boolean", "SetBody(slide, text)", ["slide:slideNumber", "text:slideBody"]),
            new("Notes", "string", "Notes(slide)", ["slide:slideNumber"]),
            new("SetNotes", "boolean", "SetNotes(slide, text)", ["slide:slideNumber", "text:slideNotes"]),
            new("Hide", "boolean", "Hide(slide, hidden)", ["slide:slideNumber", "hidden:flag"]),
        ]),
    ];
}

public sealed class DeckScriptGlobals(
    IScriptDispatcher dispatcher,
    IScriptOutput output,
    IStatusBarService status,
    DeckScriptHost host,
    IScriptCommands commands,
    DeckFacade deck,
    ScriptFilesFacade files) : IModuleScriptGlobals
{
    public ModuleKind Module => ModuleKind.Presentations;

    public IReadOnlyDictionary<string, object> Globals { get; } = new Dictionary<string, object>
    {
        ["app"] = new AppFacade(dispatcher, output, status, host, commands),
        ["deck"] = deck,
        ["files"] = files,
    };
}
