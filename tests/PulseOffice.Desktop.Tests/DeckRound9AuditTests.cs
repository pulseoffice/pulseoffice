using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Presentations.UI.Design;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.Presentations.UI.Themes;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DeckRound9AuditTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-round9-" + Guid.NewGuid().ToString("N"));

    public DeckRound9AuditTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class Shell(ServiceProvider provider, ShellWindow window, CommandRegistry registry) : IAsyncDisposable
    {
        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public DeckTarget Target => provider.GetRequiredService<IActiveDeckContext>().Target!;

        public async Task NewPresentation()
        {
            var workspace = Service<IDocumentWorkspace>();
            await Service<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
            Dispatcher.UIThread.RunJobs();
        }

        public async ValueTask DisposeAsync()
        {
            window.Close();
            await provider.DisposeAsync();
        }
    }

    private async Task<Shell> Open()
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var shell = new Shell(provider, window, registry);
        await shell.NewPresentation();
        return shell;
    }

    private sealed class ScriptedDialogs(Func<Control, Task<object?>> answer) : IDialogService
    {
        public bool IsDialogOpen => false;

        public async Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) =>
            await answer(content).ConfigureAwait(true) is TResult result ? result : default;

        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => answer(content);

        public void Close(object? result)
        {
        }
    }

    private sealed class CountingPictures(IDeckSlidePictures inner) : IDeckSlidePictures
    {
        public int Created { get; set; }

        public Control Create(Presentation deck, Slide slide, double width, double height, SlidePictureOptions? options = null)
        {
            Created++;
            return inner.Create(deck, slide, width, height, options);
        }
    }

    [Fact]
    public Task A_slide_removed_while_the_show_runs_does_not_break_the_show() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        shell.Target.Actions.AddSlide(SlideLayout.TitleOnly, 0);
        shell.Target.Actions.AddSlide(SlideLayout.TitleOnly, 1);
        var deck = shell.Target.Deck;
        var renderer = new SlideRenderer(shell.Service<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(), shell.Service<PulseOffice.Localization.ITextCatalog>());
        var window = new PresentationWindow(deck, shell.Service<IAnimationScheduler>(), renderer, new PresentationOptions(2) { WithoutAnimation = true });
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            shell.Target.Actions.DeleteSlide(deck.Slides[2]);
            shell.Target.Actions.DeleteSlide(deck.Slides[1]);
            Assert.Single(deck.Slides);
            window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, window.SlideTotal);
            Assert.Equal(3, window.SlideNumber);
            Assert.Null(window.NextSlide);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Editing_a_custom_show_does_not_redraw_every_slide_of_the_deck() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        for (var index = 0; index < 29; index++) shell.Target.Actions.AddSlide(SlideLayout.TitleOnly, index);
        var pictures = new CountingPictures(shell.Service<IDeckSlidePictures>());
        var view = new CustomShowsView(shell.Target.Deck, shell.Service<PulseOffice.Localization.ITextCatalog>(), pictures, shell.Service<ICustomShowEditing>());
        var window = new Window { Width = 1100, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            view.AddShow();
            pictures.Created = 0;
            view.AddToShow(3, 0);
            view.MoveInShow(0, 5);
            view.RemoveFromShow(2);
            Assert.Equal(30, view.CurrentIds.Count);
            Assert.True(pictures.Created <= 3, $"{pictures.Created} slide pictures drawn for three edits");
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task The_animation_dialog_does_not_edit_a_slide_deleted_while_it_is_open() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var target = shell.Target;
        target.Actions.AddSlide(SlideLayout.TitleOnly, 0);
        var slide = target.CurrentSlide()!;
        var element = slide.Elements[0];
        target.Actions.AddAnimation(slide, new AnimationEffect { Id = "a1", TargetId = element.Id, Category = AnimationCategory.Entrance, Preset = "fade" });
        var dialogs = new ScriptedDialogs(content =>
        {
            target.Actions.DeleteSlide(slide);
            ((AnimationDialogView)content).Pane.Remove("a1");
            return Task.FromResult<object?>(null);
        });
        var commands = new DeckMotionCommands(shell.Service<IActiveDeckContext>(), shell.Service<IDeckMotionEditors>(),
            shell.Service<ITransitionCatalog>(), dialogs, shell.Service<PulseOffice.Localization.ITextCatalog>());
        var command = commands.GetCommands().Single(each => each.Id == new CommandId("deck.animation.dialog"));
        await command.Execute(null!, CancellationToken.None);

        Assert.Single(slide.Animations);
        target.Actions.History.Undo();
        Assert.Contains(slide, target.Deck.Slides);
        Assert.Single(slide.Animations);
    }, CancellationToken.None);

    [Fact]
    public Task Show_setup_confirmed_after_switching_document_changes_the_document_it_was_opened_on() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var first = shell.Target;
        var dialogs = new ScriptedDialogs(async content =>
        {
            await shell.NewPresentation();
            return new ShowSetupResult(first.Deck.Setup with { Loop = true }, -1);
        });
        var commands = new DeckCommands(shell.Service<IDocumentWorkspace>(), shell.Service<IActiveDeckContext>(), dialogs,
            shell.Service<IAnimationScheduler>(), shell.Service<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(),
            shell.Service<PulseOffice.UI.Controls.Services.IStatusBarService>(), shell.Service<PulseOffice.Localization.ITextCatalog>(),
            shell.Service<PulseOffice.UI.Controls.Forms.IFormDesignMode>(), shell.Service<PulseOffice.Document.Forms.IFormControlInteraction>(),
            shell.Service<PulseOffice.Presentations.Remote.IRemoteSession>(),
            shell.Service<IShowMediaPlayer>(), shell.Service<ShowSetupServices>(),
            shell.Service<ICustomShowEditing>());
        var command = commands.GetCommands().Single(each => each.Id == new CommandId("deck.setupShow"));
        await command.Execute(null!, CancellationToken.None);

        var second = shell.Target;
        Assert.NotSame(first.Deck, second.Deck);
        Assert.True(first.Deck.Setup.Loop);
        Assert.False(second.Deck.Setup.Loop);
        Assert.False(second.Actions.History.CanUndo);
    }, CancellationToken.None);

    [Fact]
    public Task A_theme_for_the_current_slide_goes_to_the_slide_the_dialog_was_opened_on() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var target = shell.Target;
        target.Actions.AddSlide(SlideLayout.TitleOnly, 0);
        target.GoToSlide(0);
        var opened = target.CurrentSlide()!;
        var other = target.Deck.Slides.First(slide => !ReferenceEquals(slide, opened));
        var before = target.Deck.MasterOf(other);
        var choices = shell.Service<IThemeChoices>();
        var dialogs = new ScriptedDialogs(_ =>
        {
            target.GoToSlide(target.Deck.IndexOf(other));
            var choice = choices.For(target.Deck).First(each => each.Theme != target.Deck.Theme);
            return Task.FromResult<object?>(new PulseOffice.Presentations.UI.Themes.ThemeDialogChoice(choice, choice.Theme.Fonts, PulseOffice.Presentations.UI.Themes.ThemeScope.CurrentSlide));
        });
        var commands = new PulseOffice.Presentations.UI.Themes.DeckThemeCommands(shell.Service<IActiveDeckContext>(), choices,
            shell.Service<IThemeNames>(), shell.Service<IThemePreviews>(), shell.Service<IMasterFactory>(), dialogs,
            shell.Service<IFontPickerFactory>(), shell.Service<PulseOffice.Localization.ITextCatalog>(),
            shell.Service<PulseOffice.UI.Controls.Galleries.IGalleryNavigator>(), shell.Service<ThemeMasterLookup>());
        var command = commands.GetCommands().Single(each => each.Id == new CommandId("deck.design.themeDialog"));
        await command.Execute(null!, CancellationToken.None);

        Assert.Same(before, target.Deck.MasterOf(other));
        Assert.NotSame(before, target.Deck.MasterOf(opened));
    }, CancellationToken.None);
}
