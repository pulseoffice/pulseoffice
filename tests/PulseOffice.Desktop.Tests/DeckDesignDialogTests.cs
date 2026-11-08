using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Presentations.UI.Design;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DeckDesignDialogTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-design-" + Guid.NewGuid().ToString("N"));

    public DeckDesignDialogTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class Shell(ServiceProvider provider, ShellWindow window) : IAsyncDisposable
    {
        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public DeckTarget Target => provider.GetRequiredService<IActiveDeckContext>().Target!;

        public Presentation Deck => Target.Deck;

        public Slide Slide => Target.CurrentSlide()!;

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
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        return new Shell(provider, window);
    }

    private static Window Host(Control content)
    {
        var window = new Window { Width = 1100, Height = 760, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public Task Lo_sfondo_ha_il_solo_editor_del_riempimento_e_si_applica_a_tutte_con_un_annulla() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        shell.Target.Actions.AddSlide(SlideLayout.TitleAndContent, 0);
        var view = new SlideBackgroundView(shell.Deck, shell.Slide, shell.Service<SlideBackgroundServices>());
        var window = Host(view);
        Assert.NotNull(view.Preview);

        view.Editor.Model.ShowPage(PulseOffice.UI.Controls.Fills.FillPage.Solid);
        view.HideGraphics.IsChecked = true;
        var choice = view.Choice();
        Assert.True(choice.HideGraphics);
        Assert.Null(choice.Image);

        shell.Target.Actions.SetBackgrounds([.. shell.Deck.Slides], choice);
        Assert.All(shell.Deck.Slides, slide => Assert.False(slide.ShowMasterObjects));
        shell.Target.Actions.History.Undo();
        Assert.All(shell.Deck.Slides, slide => Assert.True(slide.ShowMasterObjects));
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Intestazione_e_piè_evidenziano_i_segnaposto_e_scrivono_note_e_stampati() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var view = new HeaderFooterView(shell.Deck, shell.Slide, shell.Service<HeaderFooterServices>());
        var window = Host(view);
        Assert.All(view.Placeholders, box => Assert.False(box.Active));

        view.FooterBox.IsChecked = true;
        view.FooterText.Text = "Riservato";
        view.Number.IsChecked = true;
        Assert.True(view.Placeholders.Single(box => box.Kind == "footer").Active);
        Assert.True(view.Placeholders.Single(box => box.Kind == "number").Active);
        Assert.False(view.Placeholders.Single(box => box.Kind == "date").Active);

        view.Date.IsChecked = true;
        Assert.Equal(13, view.DateFormat.Items.Count);
        Assert.Equal(HeaderFooterDateFormat.ShortDate, view.DateFormat.SelectedItem);
        view.DateFormat.Select(HeaderFooterDateFormat.Time24);
        Assert.Equal(HeaderFooterDateFormat.Time24, view.Settings().DateFormat);
        Assert.True(view.DateFormat.IsVisible);

        view.Tabs.Select(1);
        Assert.NotNull(view.NotesPreview.Sketch);
        var result = view.Result();
        Assert.Equal("Riservato", result.Slides.FooterText);
        Assert.True(result.Slides.ShowSlideNumber);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_formato_si_sceglie_dalla_galleria_e_l_orientamento_ruota_la_misura() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var view = new SlideSizeView(SlideSize.Widescreen, shell.Service<SlideSizeServices>());
        var window = Host(view);
        Assert.Equal(SlideSizePreset.Widescreen, view.Gallery.SelectedItem);
        Assert.Equal(Enum.GetValues<SlideSizePreset>().Length, view.Gallery.Items.Count);

        view.Gallery.Select(SlideSizePreset.Standard);
        view.Orientation.SelectedIndex = 1;
        Assert.True(view.Size.Height.Value > view.Size.Width.Value);
        view.ChooseScaling(false);
        Assert.False(view.Result().FitContents);
        Assert.Equal(2, view.ScalingCards.Count);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Imposta_presentazione_usa_schede_campioni_e_schermi() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        shell.Target.Actions.AddSlide(SlideLayout.TitleAndContent, 0);
        shell.Target.Actions.AddSlide(SlideLayout.TitleAndContent, 1);
        var view = new ShowSetupView(shell.Deck, -1, shell.Service<ShowSetupServices>());
        var window = Host(view);
        Assert.False(view.CustomRange);

        view.ChooseRange(true);
        view.From.Value = 2;
        view.To.Value = 3;
        view.Pen.Select("blue");
        view.LoadScreens([new ShowScreen("A", 1920, 1080, true), new ShowScreen("B", 2560, 1440, false)]);
        view.Screens.SelectedIndex = 2;
        var result = view.Result();
        Assert.Equal(2, result.Setup.FromSlide);
        Assert.Equal(3, result.Setup.ToSlide);
        Assert.Equal("#2563EB", result.Setup.PenColor);
        Assert.Equal(1, result.MonitorIndex);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Master_galleria_ed_elementi_sull_anteprima() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var themes = shell.Service<IThemeCatalog>().Themes;
        var masters = shell.Service<IMasterFactory>();
        var request = new MasterGalleryRequest(
            [.. themes.Select(theme => new MasterGalleryItem(masters.CreateStandard(theme, shell.Deck.Size), theme.Name))], 0, "?",
            [new MasterGalleryAction("create", "Crea", Primary: true)], shell.Deck.Size)
        {
            NamePrompt = "Nome",
            SuggestedName = item => item.Caption + " 2",
        };
        var view = new MasterGalleryView(request, shell.Service<MasterGalleryServices>());
        var window = Host(view);
        Assert.Equal(themes.Count, view.Gallery.Items.Count);
        Assert.Equal(themes[0].Name + " 2", view.NameBox!.Text);
        MasterGalleryResult? result = null;
        view.Completed += (_, value) => result = value as MasterGalleryResult;
        view.Complete("create");
        Assert.Equal("create", result!.Action);
        window.Close();

        var elements = new MasterElementsView(shell.Deck, shell.Deck.Master, shell.Service<MasterElementsServices>());
        window = Host(elements);
        Assert.Equal(3, elements.Placeholders.Count);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Vai_alla_diapositiva_presentazioni_personalizzate_album_ed_esportazione() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        shell.Target.Actions.AddSlide(SlideLayout.TitleAndContent, 0);
        shell.Target.Actions.AddSlide(SlideLayout.TitleAndContent, 1);

        var picker = new SlidePickerView(shell.Deck, 1, "?", shell.Service<SlidePickerServices>(), shell.Service<ISlideCaptions>());
        var window = Host(picker);
        Assert.Equal(3, picker.Gallery.Items.Count);
        Assert.Equal(1, picker.Gallery.SelectedIndex);
        window.Close();

        var shows = new CustomShowsView(shell.Deck, shell.Service<PulseOffice.Localization.ITextCatalog>(),
            shell.Service<PulseOffice.Presentations.UI.Motion.IDeckSlidePictures>(), shell.Service<ICustomShowEditing>());
        window = Host(shows);
        shows.AddShow();
        Assert.Equal(3, shows.CurrentIds.Count);
        shows.MoveInShow(2, 0);
        Assert.Equal(shell.Deck.Slides[2].Id, shows.CurrentIds[0]);
        shows.RemoveFromShow(0);
        shows.AddToShow(0, 1);
        Assert.Equal(shell.Deck.Slides[0].Id, shows.CurrentIds[1]);
        window.Close();

        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var album = new PhotoAlbumView([new AlbumPhoto(png, "a.png"), new AlbumPhoto(png, "b.png")], shell.Service<PhotoAlbumServices>());
        window = Host(album);
        album.Move(1, 0);
        Assert.Equal("b.png", album.Photos[0].Name);
        album.Layouts.Select(2);
        Assert.Equal(4, album.Result().PerSlide);
        Assert.Equal(2, album.CachedThumbnails);
        album.Remove(0);
        Assert.Equal(1, album.CachedThumbnails);
        Assert.StartsWith("1", album.Note, StringComparison.Ordinal);
        window.Close();
        Assert.Equal(0, album.CachedThumbnails);

        var export = new ImageExportView(shell.Deck, shell.Slide, 1280, shell.Service<ImageExportServices>());
        window = Host(export);
        export.WidthField.Value = 1920;
        export.WidthField.StepBy(1, large: false);
        Assert.Equal(1921, export.Result().Width);
        export.Range.SelectedIndex = 1;
        Assert.True(export.Result().CurrentOnly);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_commento_si_modifica_dal_pannello_Revisione_e_si_annulla() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        shell.Target.Actions.AddComment(shell.Slide, new SlideComment { Id = "c1", Author = "Anna", Text = "Prima" });
        var source = shell.Service<DeckReviewSource>();
        Assert.True(source.Abilities.Edit);

        source.Edit("c1", "Dopo");
        Assert.Equal("Dopo", shell.Slide.Comments.Single().Text);
        Assert.Equal("Dopo", source.Comments.Single().Text);

        shell.Target.Actions.History.Undo();
        Assert.Equal("Prima", shell.Slide.Comments.Single().Text);
    }, CancellationToken.None);

    [Fact]
    public Task Vai_alla_diapositiva_e_un_comando_con_scorciatoia_e_posto_nella_ribbon() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var command = shell.Service<DeckNavigationCommands>().GetCommands().Single(definition => definition.Id.Value == "deck.navigate.goTo");
        Assert.Equal("Ctrl+G", command.DefaultGesture);
        Assert.True(command.Surface.HasFlag(CommandSurface.Ribbon));
        Assert.True(command.Surface.HasFlag(CommandSurface.Palette));
        Assert.True(command.Surface.HasFlag(CommandSurface.Menu));
    }, CancellationToken.None);
}
