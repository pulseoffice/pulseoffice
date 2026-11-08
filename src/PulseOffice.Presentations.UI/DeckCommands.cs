using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Presentations.UI;

public sealed class DeckCommands(
    IDocumentWorkspace workspace,
    IActiveDeckContext deckContext,
    IDialogService dialogs,
    IAnimationScheduler scheduler,
    IDrawingObjectRenderer objectRenderer,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.UI.Controls.Forms.IFormDesignMode design,
    PulseOffice.Document.Forms.IFormControlInteraction forms,
    PulseOffice.Presentations.Remote.IRemoteSession remote,
    IShowMediaPlayer media,
    Design.ShowSetupServices showSetup,
    ICustomShowEditing customShows)
{
    private readonly ShowStart _showStart = new();

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("deck.present"),
            Module = ModuleKind.Presentations,
            MenuPath = "view/present",
            RibbonPath = "view/present",
            DefaultGesture = "F5",
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active?.Model is Presentation,
            Execute = (_, _) => PresentVisible(0, withPresenter: false),
        },

        new CommandDefinition
        {
            Id = new CommandId("deck.presentFromCurrent"),
            Module = ModuleKind.Presentations,
            MenuPath = "view/present",
            DefaultGesture = "Shift+F5",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active?.Model is Presentation,
            Execute = (_, _) => PresentVisible(CurrentIndex(), withPresenter: false),
        },

        new CommandDefinition
        {
            Id = new CommandId("deck.presentPresenter"),
            Module = ModuleKind.Presentations,
            MenuPath = "view/present",
            RibbonPath = "view/present",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active?.Model is Presentation,
            Execute = (_, _) => PresentVisible(0, withPresenter: true),
        },

        new CommandDefinition
        {
            Id = new CommandId("deck.customShows"),
            Module = ModuleKind.Presentations,
            MenuPath = "view/present",
            RibbonPath = "view/present",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => workspace.Active?.Model is Presentation,
            Execute = (_, ct) => ManageCustomShowsAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("deck.presentCustom"),
            Module = ModuleKind.Presentations,
            MenuPath = "view/present",
            RibbonPath = "view/present",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => workspace.Active?.Model is Presentation { CustomShows.Count: > 0 },
            Execute = (_, ct) => PresentCustomAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("deck.rehearse"),
            Module = ModuleKind.Presentations,
            MenuPath = "view/present",
            RibbonPath = "view/present",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => workspace.Active?.Model is Presentation,
            Execute = (_, ct) => RehearseAsync(ct),
        },

        new CommandDefinition
        {
            Id = new CommandId("deck.setupShow"),
            Module = ModuleKind.Presentations,
            MenuPath = "view/present",
            RibbonPath = "view/present",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => workspace.Active?.Model is Presentation,
            Execute = (_, ct) => SetupShowAsync(ct),
        },
    ];

    private int _monitorIndex = -1;

    private async Task SetupShowAsync(CancellationToken ct)
    {
        if (deckContext.Target is not { } target) return;
        var deck = target.Deck;

        var result = await dialogs.ShowAsync<Design.ShowSetupResult>(
            new Design.ShowSetupView(deck, _monitorIndex, showSetup),
            new DialogOptions { TitleKey = "command.deck.setupShow.title", PreferredWidth = 760 }, ct).ConfigureAwait(true);
        if (result is null) return;

        _monitorIndex = result.MonitorIndex;
        target.Actions.SetShowSetup(result.Setup);
    }

    private int CurrentIndex() =>
        workspace.Active?.View is DeckView view ? view.CurrentSlideIndex : 0;

    private Task PresentVisible(int startIndex, bool withPresenter)
    {
        if (workspace.Active?.Model is not Presentation deck) return Task.CompletedTask;

        var playlist = MainPlaylist(deck);
        var shownStart = _showStart.PositionFor(deck.Slides, playlist, startIndex);

        return Present(deck, playlist, shownStart, withPresenter);
    }

    private static List<Slide> MainPlaylist(Presentation deck)
    {
        var setup = deck.Setup;
        var from = setup.FromSlide > 0 ? setup.FromSlide : 1;
        var to = setup.ToSlide > 0 ? setup.ToSlide : deck.Slides.Count;

        return deck.Slides
            .Select((slide, index) => (slide, number: index + 1))
            .Where(entry => !entry.slide.Hidden
                && (!setup.HasRange || (entry.number >= from && entry.number <= to)))
            .Select(entry => entry.slide)
            .ToList();
    }

    private async Task ManageCustomShowsAsync(CancellationToken ct)
    {
        if (workspace.Active?.Model is not Presentation deck) return;
        if (deckContext.Target is not { } target) return;

        var result = await dialogs.ShowAsync<CustomShowsResult>(
            new CustomShowsView(deck, texts, showSetup.Pictures, customShows),
            new DialogOptions { TitleKey = "command.deck.customShows.title", PreferredWidth = 620 },
            ct).ConfigureAwait(true);

        if (result is null) return;

        target.Actions.SetCustomShows(result.Shows);

        if (result.Present is { } show)
            await Present(deck, ResolvePlaylist(deck, show), 0, withPresenter: false).ConfigureAwait(true);
    }

    private async Task PresentCustomAsync(CancellationToken ct)
    {
        if (workspace.Active?.Model is not Presentation deck || deck.CustomShows.Count == 0) return;

        var choices = deck.CustomShows
            .Select(s => new PromptChoice(s.Name, s.Name))
            .Append(new PromptChoice(texts["dialog.cancel"], string.Empty, IsCancel: true))
            .ToList();

        var picked = await dialogs.ShowAsync<string>(
            new ChoicePromptView(
                texts["deck.customShows.choose"], texts["deck.customShows.chooseHint"], choices, texts),
            new DialogOptions { TitleKey = "command.deck.presentCustom.title" },
            ct).ConfigureAwait(true);

        if (string.IsNullOrEmpty(picked)) return;
        if (deck.CustomShows.FirstOrDefault(s => s.Name == picked) is not { } show) return;

        await Present(deck, ResolvePlaylist(deck, show), 0, withPresenter: false).ConfigureAwait(true);
    }

    private async Task RehearseAsync(CancellationToken ct)
    {
        if (workspace.Active?.Model is not Presentation deck) return;
        if (deckContext.Target is not { } target) return;

        var playlist = deck.Slides.Where(s => !s.Hidden).ToList();
        if (playlist.Count == 0)
        {
            status.Report(texts["deck.present.empty"], NotificationSeverity.Warning);
            return;
        }

        var renderer = new SlideRenderer(objectRenderer, texts);
        var window = new PresentationWindow(deck, scheduler, renderer,
            new PresentationOptions(0) { Playlist = playlist, Rehearse = true });

        IReadOnlyDictionary<string, int>? recorded = null;
        window.RehearsalFinished += (_, times) => recorded = times;

        var closed = new TaskCompletionSource();
        var documentGone = false;
        void EndWithDocument(object? sender, OpenDocument closing)
        {
            if (!ReferenceEquals(closing.Model, deck)) return;
            documentGone = true;
            window.Close();
        }

        workspace.DocumentClosed += EndWithDocument;
        window.Closed += (_, _) =>
        {
            workspace.DocumentClosed -= EndWithDocument;
            closed.TrySetResult();
        };

        status.Report(texts["deck.rehearsing"]);
        window.Show();

        await closed.Task.ConfigureAwait(true);

        if (documentGone || recorded is not { Count: > 0 }) return;

        var choices = new[]
        {
            new PromptChoice(texts["dialog.no"], "no", IsCancel: true),
            new PromptChoice(texts["dialog.yes"], "yes", IsDefault: true),
        };

        var answer = await dialogs.ShowAsync<string>(
            new ChoicePromptView(
                texts["deck.rehearse.applyQuestion"], RehearsalSummary(recorded), choices, texts),
            new DialogOptions { TitleKey = "command.deck.rehearse.title" },
            ct).ConfigureAwait(true);

        if (answer == "yes") target.Actions.ApplyRehearsalTimings(recorded);
    }

    private string RehearsalSummary(IReadOnlyDictionary<string, int> times)
    {
        var total = TimeSpan.FromMilliseconds(times.Values.Sum());
        return string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            texts["deck.rehearse.summary"], times.Count, (int)total.TotalMinutes, total.Seconds);
    }

    private static List<Slide> ResolvePlaylist(Presentation deck, CustomShow show) =>
        show.SlideIds
            .Select(id => deck.Slides.FirstOrDefault(s => s.Id == id))
            .OfType<Slide>()
            .ToList();

    private Task Present(Presentation deck, IReadOnlyList<Slide> playlist, int startIndex, bool withPresenter)
    {
        if (playlist.Count == 0)
        {
            status.Report(texts["deck.present.empty"], NotificationSeverity.Warning);
            return Task.CompletedTask;
        }

        var owner = deckContext.Target is { } active && ReferenceEquals(active.Deck, deck) ? active : null;
        var documentPath = workspace.Documents.FirstOrDefault(document => ReferenceEquals(document.Model, deck))?.Path;
        var renderer = new SlideRenderer(objectRenderer, texts);
        var window = new PresentationWindow(deck, scheduler, renderer,
            new PresentationOptions(startIndex, deck.Setup.Loop, withPresenter)
            {
                Playlist = playlist,
                WithoutAnimation = deck.Setup.WithoutAnimation,
                ManualAdvance = deck.Setup.Advance == ShowAdvanceMode.Manual,
                PenColor = deck.Setup.PenColor,
                MonitorIndex = _monitorIndex,
                Forms = design.IsDesigning ? null : forms,
                ControlChanged = (slide, element) =>
                {
                    if (element is ObjectElement control && owner is not null && owner.Deck.IndexOf(slide) >= 0)
                        owner.Actions.ChangeControl(slide, control);
                },
                PlayMedia = element => media.PlayAsync(element, documentPath),
            });

        if (withPresenter)
        {
            var presenter = new PresenterWindow(deck, new SlideRenderer(objectRenderer, texts), window, texts);
            window.Closed += (_, _) => presenter.Close();
            presenter.Show();
        }

        var target = new Remote.WindowRemoteTarget(window);
        remote.Target = target;
        void EndWithDocument(object? sender, OpenDocument closed)
        {
            if (ReferenceEquals(closed.Model, deck)) window.Close();
        }

        workspace.DocumentClosed += EndWithDocument;
        window.Closed += (_, _) =>
        {
            workspace.DocumentClosed -= EndWithDocument;
            if (ReferenceEquals(remote.Target, target)) remote.Target = null;
        };

        status.Report(texts["deck.presenting"]);
        window.Show();

        return Task.CompletedTask;
    }
}
