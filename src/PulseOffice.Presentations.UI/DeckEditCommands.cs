using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckEditCommands(
    IActiveDeckContext context,
    IDeckFileInsertion files,
    PulseOffice.UI.Controls.Services.IDialogService dialogs,
    ITextCatalog texts,
    IDeckTextInsertion insertion,
    Design.HeaderFooterServices headerFooter) : DeckCommandSet(context)
{
    private static readonly (string Id, SlideLayout Layout)[] Layouts =
    [
        ("titleSlide", SlideLayout.TitleSlide),
        ("titleAndContent", SlideLayout.TitleAndContent),
        ("sectionHeader", SlideLayout.SectionHeader),
        ("twoContent", SlideLayout.TwoContent),
        ("comparison", SlideLayout.Comparison),
        ("titleOnly", SlideLayout.TitleOnly),
        ("blank", SlideLayout.Blank),
        ("contentWithCaption", SlideLayout.ContentWithCaption),
        ("pictureWithCaption", SlideLayout.PictureWithCaption),
        ("verticalText", SlideLayout.VerticalText),
        ("verticalTitleAndText", SlideLayout.VerticalTitleAndText),
        ("centeredText", SlideLayout.CenteredText),
        ("contentAndTwoContent", SlideLayout.ContentAndTwoContent),
        ("twoContentAndContent", SlideLayout.TwoContentAndContent),
        ("contentOverContent", SlideLayout.ContentOverContent),
        ("twoContentOverContent", SlideLayout.TwoContentOverContent),
        ("fourContent", SlideLayout.FourContent),
        ("sixContent", SlideLayout.SixContent),
        ("verticalTitleTextChart", SlideLayout.VerticalTitleTextChart),
        ("titleTwoVerticalText", SlideLayout.TitleTwoVerticalText),
    ];

    private static readonly (string Id, TransitionKind Kind)[] Transitions =
    [
        ("none", TransitionKind.None),
        ("fade", TransitionKind.Fade),
        ("push", TransitionKind.Push),
        ("wipe", TransitionKind.Wipe),
        ("split", TransitionKind.Split),
        ("cover", TransitionKind.Cover),
        ("uncover", TransitionKind.Uncover),
        ("cut", TransitionKind.Cut),
        ("dissolve", TransitionKind.Dissolve),
        ("morph", TransitionKind.Morph),
        ("zoom", TransitionKind.Zoom),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        .. SlideCommands(),
        .. LayoutCommands(),
        .. TransitionCommands(),
        .. AnimationCommands(),
        .. InsertCommands(),
        .. SectionCommands(),
        HeaderFooterCommand(),
        SymbolCommand(),
    ];

    private CommandDefinition SymbolCommand() =>
        Prototype("deck.insert.symbol") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => InsertSymbolAsync(ct),
        };

    private async Task InsertSymbolAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;

        var glyph = await dialogs.ShowAsync<string>(
            new PulseOffice.UI.Controls.SymbolPickerView(texts),
            new PulseOffice.UI.Controls.Services.DialogOptions { TitleKey = "command.deck.insert.symbol.title", PreferredWidth = 900 },
            ct).ConfigureAwait(true);

        if (string.IsNullOrEmpty(glyph)) return;

        insertion.Insert(t, new TextRun(glyph), freeFontSize: 40);
    }


    private CommandDefinition HeaderFooterCommand() =>
        Prototype("deck.insert.headerFooter") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null,
            Execute = (_, ct) => HeaderFooterAsync(ct),
        };

    private async Task HeaderFooterAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        var result = await dialogs.ShowAsync<Design.HeaderFooterResult>(
            new Design.HeaderFooterView(t.Deck, slide, headerFooter),
            new PulseOffice.UI.Controls.Services.DialogOptions { TitleKey = "command.deck.insert.headerFooter.title", PreferredWidth = 780 },
            ct).ConfigureAwait(true);
        if (result is null || Context.Target is not { } target) return;
        using var step = target.Actions.History.BeginTransaction(texts["command.deck.insert.headerFooter.title"]);
        target.Actions.SetHeaderFooter(result.Slides);
        target.Actions.SetNotesMaster(result.Notes);
        target.Actions.SetHandoutMaster(result.Handout);
    }

    private IEnumerable<CommandDefinition> SectionCommands() =>
    [
        Prototype("deck.section.add") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide/section"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { SectionName: null },
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } s)
                    t.Actions.SetSection(s, DefaultSectionName(t.Deck));
                return Task.CompletedTask;
            },
        },
        Prototype("deck.section.rename") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide/section"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { } s
                && new SlideSections().StartOf(t.Deck.Slides, s) is not null,
            Execute = (_, ct) => RenameSectionAsync(ct),
        },
        Prototype("deck.section.remove") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            ContextPaths = ["slide/section"],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is { } s
                && new SlideSections().StartOf(t.Deck.Slides, s) is not null,
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } s
                    && new SlideSections().StartOf(t.Deck.Slides, s) is { } start)
                    t.Actions.SetSection(start, null);
                return Task.CompletedTask;
            },
        },
    ];

    private async Task RenameSectionAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } current) return;
        if (new SlideSections().StartOf(t.Deck.Slides, current) is not { } start) return;

        var name = await dialogs.ShowAsync<string>(
            new PulseOffice.UI.Controls.TextPromptView(
                texts["deck.section.renamePrompt"], start.SectionName ?? string.Empty, texts),
            new PulseOffice.UI.Controls.Services.DialogOptions { TitleKey = "command.deck.section.rename.title" },
            ct).ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(name)) t.Actions.SetSection(start, name.Trim());
    }

    private string DefaultSectionName(Presentation deck) =>
        $"{texts["deck.section.default"]} {new SlideSections().Group(deck.Slides).Count + 1}";

    private IEnumerable<CommandDefinition> SlideCommands() =>
    [
        SlideCommand("deck.slide.duplicate", (t, s) => t.Actions.DuplicateSlide(s),
            contextPath: "slide/manage"),
        SlideCommand("deck.slide.delete", (t, s) => t.Actions.DeleteSlide(s),
            enabled: t => t.Deck.Slides.Count > 1, contextPath: "slide/manage"),
        SlideCommand("deck.slide.moveUp", (t, s) => t.Actions.MoveSlide(s, t.CurrentIndex() - 1),
            enabled: t => t.CurrentIndex() > 0, contextPath: "slide/order"),
        SlideCommand("deck.slide.moveDown", (t, s) => t.Actions.MoveSlide(s, t.CurrentIndex() + 1),
            enabled: t => t.CurrentIndex() < t.Deck.Slides.Count - 1, contextPath: "slide/order"),
        SlideCommand("deck.slide.hide", (t, s) => t.Actions.SetHidden(s, !s.Hidden),
            contextPath: "slide/manage"),
    ];

    private IEnumerable<CommandDefinition> LayoutCommands() =>
        Layouts.Select(entry =>
            SlideCommand($"deck.layout.{entry.Id}", (t, s) => t.Actions.SetLayout(s, entry.Layout),
                enabled: t => t.CurrentSlide()?.Layout != entry.Layout) with { Surface = CommandSurface.Advanced | CommandSurface.ContextMenu });

    private IEnumerable<CommandDefinition> TransitionCommands() =>
        Transitions.Select(entry =>
            SlideCommand($"deck.transition.{entry.Id}",
                (t, s) => t.Actions.SetTransition(s, s.Transition with { Kind = entry.Kind }),
                enabled: t => t.CurrentSlide()?.Transition.Kind != entry.Kind) with { Surface = CommandSurface.Advanced });

    private IEnumerable<CommandDefinition> AnimationCommands() =>
    [
        SlideCommand("deck.anim.add", (t, s) =>
        {
            if (AnimationTarget(t, s) is { } element) t.Actions.AnimateElement(s, element);
        },
            enabled: t => t.CurrentSlide() is { } s && AnimationTarget(t, s) is not null),
        SlideCommand("deck.anim.play", (t, _) => t.Play(),
            enabled: t => t.CurrentSlide()?.Animations.Count > 0, edits: false),
    ];

    private SlideElement? AnimationTarget(DeckTarget target, Slide slide) =>
        target.Selected()
        ?? slide.InZOrder().FirstOrDefault(e => e is TextElement)
        ?? slide.InZOrder().FirstOrDefault();

    private IEnumerable<CommandDefinition> InsertCommands() =>
    [
        SlideCommand("deck.insert.media", (_, _) => { }) with { Execute = (_, ct) => files.InsertMediaAsync(ct) },
    ];

    private CommandDefinition SlideCommand(string id, Action<DeckTarget, Slide> run, Func<DeckTarget, bool>? enabled = null, string? contextPath = null, bool edits = true) =>
        Prototype(id) with
        {
            Surface = contextPath is null ? CommandSurface.Common : CommandSurface.Common | CommandSurface.ContextMenu,
            ContextPaths = contextPath is null ? [] : [contextPath],
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = edits ? DocumentState.ReadOnly : DocumentState.None,
            EnabledWhen = _ => Context.Target is { } t && t.CurrentSlide() is not null && (enabled?.Invoke(t) ?? true),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } s) run(t, s);
                return Task.CompletedTask;
            },
        };
}
