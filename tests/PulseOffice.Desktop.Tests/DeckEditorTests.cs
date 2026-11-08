using PulseOffice.Testing;
using PulseOffice.Foundation.Zoom;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Settings;
using PulseOffice.Text.Rich;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DeckEditorTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-deck-" + Guid.NewGuid().ToString("N"));

    public DeckEditorTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class Deck(ServiceProvider provider, CommandRegistry registry, ShellWindow window) : IAsyncDisposable
    {
        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public DeckTarget Target => provider.GetRequiredService<IActiveDeckContext>().Target!;

        public Slide Slide => Target.CurrentSlide()!;

        public ShellWindow Window => window;

        public bool CanRun(string id) =>
            registry.Find(new CommandId(id)) is { } command
            && command.IsEnabled(provider.GetRequiredService<IDocumentWorkspace>().Context);

        public bool IsChecked(string id) =>
            registry.Find(new CommandId(id)) is { CheckedWhen: { } isChecked }
            && isChecked(provider.GetRequiredService<IDocumentWorkspace>().Context);

        public async Task Run(string id)
        {
            var command = registry.Find(new CommandId(id));
            Assert.NotNull(command);
            var workspace = provider.GetRequiredService<IDocumentWorkspace>();
            await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(command, workspace.Context);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public ObjectElement Shape(double leftCm, double topCm, double widthCm = 3, double heightCm = 2)
        {
            var element = new ObjectElement
            {
                Id = Target.Deck.NextId(),
                Bounds = new DocumentRect(Twip.FromCentimeters(leftCm), Twip.FromCentimeters(topCm), Twip.FromCentimeters(widthCm), Twip.FromCentimeters(heightCm)),
                ZOrder = Slide.Elements.Count,
                Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = Target.Deck.NextId(), Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle },
            };
            Target.Actions.InsertElement(Slide, element);
            Dispatcher.UIThread.RunJobs();
            return element;
        }

        public SlideElement Find(string id) => Slide.Find(id)!;

        public double Brightness()
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
            using var frame = window.CaptureRenderedFrame()!;
            using var stream = new MemoryStream();
            frame.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            using var bitmap = SkiaSharp.SKBitmap.Decode(stream.ToArray());
            var samples = Enumerable.Range(0, 400).Select(index => bitmap.GetPixel(index * 37 % bitmap.Width, index * 53 % bitmap.Height));
            return samples.Average(pixel => (pixel.Red + pixel.Green + pixel.Blue) / 3.0);
        }

        public async ValueTask DisposeAsync()
        {
            window.Close();
            await provider.DisposeAsync();
        }
    }

    private async Task<Deck> Open()
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        return new Deck(provider, registry, window);
    }

    [Fact]
    public Task Allineare_raggruppare_ordinare_e_annullare() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var first = deck.Shape(2, 2);
        var second = deck.Shape(6, 5);
        var third = deck.Shape(12, 8);

        deck.Target.Select([first, second, third]);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, deck.Target.SelectedMany().Count);

        await deck.Run("deck.align.left");
        Assert.All(new[] { first, second, third }, element => Assert.Equal((double)Twip.FromCentimeters(2).Value, (double)deck.Find(element.Id).Bounds.Left.Value, 1));

        await deck.Run("shell.edit.undo");
        Assert.Equal((double)Twip.FromCentimeters(12).Value, (double)deck.Find(third.Id).Bounds.Left.Value, 1);
        await deck.Run("shell.edit.redo");
        Assert.Equal((double)Twip.FromCentimeters(2).Value, (double)deck.Find(third.Id).Bounds.Left.Value, 1);
        await deck.Run("shell.edit.undo");

        deck.Target.Select([first, second]);
        Dispatcher.UIThread.RunJobs();
        var before = deck.Slide.Elements.Count;
        await deck.Run("deck.object.group");
        Assert.Equal(before, deck.Slide.Elements.Count);
        var group = Assert.Single(deck.Slide.Groups);
        Assert.Equal(new[] { first.Id, second.Id }.Order(StringComparer.Ordinal), group.Members.Order(StringComparer.Ordinal));

        deck.Target.Select([deck.Find(first.Id), deck.Find(second.Id)]);
        Dispatcher.UIThread.RunJobs();
        await deck.Run("deck.edit.duplicate");
        Assert.Equal(2, deck.Slide.Groups.Count);
        Assert.Equal(before + 2, deck.Slide.Elements.Count);
        await deck.Run("shell.edit.undo");
        Assert.Single(deck.Slide.Groups);

        deck.Target.Select([deck.Find(first.Id), deck.Find(second.Id)]);
        Dispatcher.UIThread.RunJobs();
        await deck.Run("deck.object.rotateRight");
        Assert.Equal(90, deck.Slide.Groups[0].RotationDegrees);
        Assert.Equal(90, deck.Find(first.Id).RotationDegrees);
        await deck.Run("shell.edit.undo");
        Assert.Equal(0, deck.Slide.Groups[0].RotationDegrees);

        deck.Target.Select([deck.Find(first.Id), deck.Find(second.Id)]);
        Dispatcher.UIThread.RunJobs();
        await deck.Run("deck.object.ungroup");
        Assert.Empty(deck.Slide.Groups);
        await deck.Run("shell.edit.undo");
        Assert.Single(deck.Slide.Groups);
        await deck.Run("shell.edit.undo");
        Assert.Empty(deck.Slide.Groups);
        Assert.NotNull(deck.Slide.Find(first.Id));

        deck.Target.Select([deck.Find(first.Id)]);
        Dispatcher.UIThread.RunJobs();
        await deck.Run("deck.order.front");
        Assert.Equal(first.Id, deck.Slide.InZOrder().Last().Id);
        await deck.Run("deck.order.back");
        Assert.Equal(first.Id, deck.Slide.InZOrder().First().Id);
    }, CancellationToken.None);

    [Fact]
    public Task Scrivere_in_una_forma_creata_nell_editor_formatta_esce_con_Esc_e_si_annulla_in_un_passo() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var shape = deck.Shape(3, 3, 6, 3);
        deck.Target.Select([shape]);
        Dispatcher.UIThread.RunJobs();

        deck.Window.KeyTextInput("Ciao");
        Dispatcher.UIThread.RunJobs();
        Assert.True(deck.Target.IsTyping());
        await deck.Run("deck.format.bold");
        deck.Window.KeyTextInput(" Deck");
        Dispatcher.UIThread.RunJobs();
        deck.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        deck.Window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        var written = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Assert.IsType<ObjectElement>(deck.Find(shape.Id)).Content);
        Assert.Equal("Ciao Deck", written.Text);
        Assert.Contains(written.Runs!, run => run.Bold);
        Assert.False(deck.Target.IsTyping());

        Assert.Equal(" Deck", string.Concat(written.Runs!.Where(run => run.Bold).Select(run => run.Text)));
        await deck.Run("shell.edit.undo");
        Assert.Null(Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(Assert.IsType<ObjectElement>(deck.Find(shape.Id)).Content).Text);
    }, CancellationToken.None);

    [Fact]
    public Task Maiusc_F10_apre_il_menu_della_forma_selezionata_da_tastiera() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var shape = deck.Shape(3, 3, 6, 3);
        deck.Target.Select([shape]);
        Dispatcher.UIThread.RunJobs();
        deck.Window.KeyPress(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
        deck.Window.KeyRelease(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
        Dispatcher.UIThread.RunJobs();
        var menu = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(deck.Window).OfType<Avalonia.Controls.Control>()
            .Select(control => control.ContextMenu).OfType<Avalonia.Controls.ContextMenu>().FirstOrDefault(open => open.IsOpen);
        Assert.NotNull(menu);
        deck.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        deck.Window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(menu!.IsOpen);
    }, CancellationToken.None);

    [Fact]
    public Task La_ribbon_mostra_il_grassetto_del_testo_della_forma_in_modifica() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var shape = deck.Shape(3, 3, 6, 3);
        deck.Target.Select([shape]);
        Dispatcher.UIThread.RunJobs();

        deck.Window.KeyTextInput("Ciao");
        Dispatcher.UIThread.RunJobs();
        Assert.False(deck.IsChecked("deck.format.bold"));
        await deck.Run("deck.format.bold");
        Assert.True(deck.IsChecked("deck.format.bold"));
        await deck.Run("deck.format.italic");
        await deck.Run("deck.format.underline");
        Assert.True(deck.IsChecked("deck.format.italic") && deck.IsChecked("deck.format.underline"));
        await deck.Run("deck.format.italic");
        Assert.False(deck.IsChecked("deck.format.italic"));
        deck.Window.KeyTextInput(" Deck");
        Dispatcher.UIThread.RunJobs();
        Assert.True(deck.IsChecked("deck.format.bold"));

        deck.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        deck.Window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(deck.IsChecked("deck.format.bold"));
        await deck.Run("deck.format.bold");
        Assert.True(deck.IsChecked("deck.format.bold"));
    }, CancellationToken.None);

    [Fact]
    public Task Le_miniature_si_scorrono_da_tastiera_e_hanno_un_nome_accessibile() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        await deck.Run("deck.slide.add");
        await deck.Run("deck.slide.add");
        await deck.Run("deck.slide.add");
        deck.Target.GoToSlide(0);
        Dispatcher.UIThread.RunJobs();

        var thumbnails = deck.Window.GetVisualDescendants().OfType<Avalonia.Controls.Border>()
            .Where(border => border.Focusable && Avalonia.Automation.AutomationProperties.GetName(border) is { Length: > 0 })
            .ToList();
        Assert.Equal(4, thumbnails.Count);
        Assert.StartsWith("Diapositiva 1 di 4", Avalonia.Automation.AutomationProperties.GetName(thumbnails[0]));

        thumbnails[0].Focus();
        Dispatcher.UIThread.RunJobs();
        deck.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, deck.Target.CurrentIndex());
        deck.Window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, deck.Target.CurrentIndex());
        deck.Window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, deck.Target.CurrentIndex());
        Assert.True(deck.Window.FocusManager?.GetFocusedElement() is Avalonia.Controls.Border { Focusable: true });

        deck.Target.Select([]);
        Dispatcher.UIThread.RunJobs();
        deck.Window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, deck.Target.CurrentIndex());
    }, CancellationToken.None);

    [Fact]
    public Task Casella_di_testo_scrittura_e_sola_lettura() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var before = deck.Slide.Elements.Count;
        await deck.Run("deck.insert.textBox");
        Assert.Equal(before + 1, deck.Slide.Elements.Count);
        var box = Assert.IsType<TextElement>(deck.Target.Selected());

        deck.Window.KeyTextInput("Ciao Deck");
        Dispatcher.UIThread.RunJobs();
        deck.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        deck.Window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        var written = Assert.IsType<TextElement>(deck.Find(box.Id)).Text.PlainText;
        Assert.True(written.Contains("Ciao Deck", StringComparison.Ordinal), $"[{written}] typing={deck.Target.IsTyping()} focus={deck.Window.FocusManager?.GetFocusedElement()}");

        var shape = deck.Shape(3, 3);
        deck.Target.Select([shape]);
        deck.Service<IDocumentWorkspace>().Active!.IsReadOnly = true;
        Dispatcher.UIThread.RunJobs();
        Assert.False(deck.CanRun("deck.insert.textBox"));
        Assert.False(deck.CanRun("deck.object.flipHorizontal"));
        Assert.False(deck.CanRun("deck.order.front"));
    }, CancellationToken.None);

    [Fact]
    public Task Zoom_righelli_linee_guida_e_viste() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var zoom = deck.Target.EffectiveZoom();
        await deck.Run("deck.zoom.in");
        Assert.True(deck.Target.Zoom().Factor > zoom);
        deck.Target.SetZoom(ViewZoom.Whole);
        Dispatcher.UIThread.RunJobs();
        await deck.Run("deck.zoom.out");
        Assert.True(deck.Target.Zoom().Factor < zoom);
        await deck.Run("deck.zoom.fit");

        var rulers = deck.Target.Deck.Grid.ShowRulers;
        await deck.Run("deck.view.rulers");
        Assert.NotEqual(rulers, deck.Target.Deck.Grid.ShowRulers);
        await deck.Run("deck.view.rulers");

        var guides = deck.Target.Deck.Grid.ShowGuides;
        await deck.Run("deck.view.guides");
        Assert.NotEqual(guides, deck.Target.Deck.Grid.ShowGuides);
        await deck.Run("deck.view.guides");

        var theme = deck.Service<PulseOffice.UI.Theming.IThemeService>();
        var before = theme.Preference;
        void Repaint(object? sender, EventArgs e) => PulseOffice.UI.Theming.Palette.Apply(Application.Current!, theme.IsDark);
        theme.ThemeChanged += Repaint;
        try
        {
            theme.Apply(PulseOffice.UI.Theming.ThemePreference.Light);
            var light = deck.Brightness();
            theme.Apply(PulseOffice.UI.Theming.ThemePreference.Dark);
            var dark = deck.Brightness();
            Assert.True(dark < light - 60, $"chiaro {light}, scuro {dark}");
        }
        finally
        {
            theme.Apply(before);
            theme.ThemeChanged -= Repaint;
            PulseOffice.UI.Theming.Palette.Apply(Application.Current!, theme.IsDark);
        }

        foreach (var (command, mode) in new[]
        {
            ("deck.view.master", DeckViewMode.Master), ("deck.view.notes", DeckViewMode.Notes),
            ("deck.view.outline", DeckViewMode.Outline), ("deck.view.normal", DeckViewMode.Normal),
        })
        {
            await deck.Run(command);
            Assert.Equal(mode, deck.Target.ViewMode());
        }
    }, CancellationToken.None);

    [Fact]
    public Task Presentazione_navigazione_animazioni_uscita() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var title = deck.Slide.Elements.First();
        deck.Target.Actions.AddAnimation(deck.Slide, new AnimationEffect
        {
            Id = "fly", TargetId = title.Id, Category = AnimationCategory.Entrance, Preset = "fly-in", Subtype = "from-left", DurationMs = 300,
        });
        deck.Target.Actions.SetTransition(deck.Slide, new SlideTransition { Kind = TransitionKind.Push, Direction = TransitionDirection.Left, DurationMs = 200 });
        var second = deck.Target.Actions.AddSlide(SlideLayout.TitleOnly, 0)!;
        deck.Target.Actions.SetTransition(second, new SlideTransition
        {
            Kind = TransitionKind.Wipe,
            Native = new NativeTransition(NativeTransition.Odf, "clockWipe clockwiseTwelve", TransitionKind.Wipe, TransitionDirection.None),
            DurationMs = 200,
        });
        Dispatcher.UIThread.RunJobs();

        var renderer = new SlideRenderer(deck.Service<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(), deck.Service<PulseOffice.Localization.ITextCatalog>());
        var window = new PresentationWindow(deck.Target.Deck, deck.Service<IAnimationScheduler>(), renderer, new PresentationOptions(0));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal(1, window.SlideNumber);
            Press(window, Key.Right);
            Assert.Equal(1, window.SlideNumber);
            Press(window, Key.Right);
            Assert.Equal(1, window.SlideNumber);
            Press(window, Key.Right);
            Assert.Equal(2, window.SlideNumber);
            Press(window, Key.Right);
            Press(window, Key.Left);
            Assert.Equal(1, window.SlideNumber);
            Press(window, Key.End);
            Assert.Equal(2, window.SlideNumber);
            var closed = false;
            window.Closed += (_, _) => closed = true;
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(closed);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Gli_appunti_di_Deck_parlano_con_le_altre_app() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var rich = new ClipboardProbe(deck.Service<PulseOffice.Exchange.IExchangeClipboard>());
        var shape = deck.Shape(2, 2);
        deck.Target.Select([shape]);
        var before = deck.Slide.Elements.Count;

        await deck.Run("deck.clipboard.copy");
        var published = await rich.GetAsync();
        Assert.NotNull(published.Png);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, published.Png![..4]);
        Assert.NotNull(published.Native);

        await deck.Run("deck.clipboard.paste");
        Assert.Equal(before + 1, deck.Slide.Elements.Count);

        await rich.SetAsync(new ClipboardContent { Text = "Da un'altra app" });
        deck.Target.Select([]);
        await deck.Run("deck.clipboard.paste");
        Assert.Contains(deck.Slide.Elements.OfType<TextElement>(), element => element.Text.PlainText.Contains("Da un'altra app", StringComparison.Ordinal));

        var count = deck.Slide.Elements.Count;
        await rich.SetAsync(new ClipboardContent { Png = published.Png });
        await deck.Run("deck.clipboard.paste");
        Assert.Equal(count + 1, deck.Slide.Elements.Count);
        Assert.Contains(deck.Slide.Elements.OfType<ObjectElement>(), element => element.Content is PulseOffice.Drawing.Assets.RasterImageObject);
    }, CancellationToken.None);

    [Fact]
    public Task Il_testo_formattato_di_altre_app_arriva_in_Deck_formattato() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var rich = new ClipboardProbe(deck.Service<PulseOffice.Exchange.IExchangeClipboard>());
        deck.Target.Select([]);
        await rich.SetAsync(new ClipboardContent
        {
            Text = "Uno due\nTre",
            Rtf = @"{\rtf1\ansi{\fonttbl\f0\fswiss Helvetica;}{\colortbl;\red255\green0\blue0;}\pard\f0 {\b Uno} {\i\cf1 due}\par Tre\par}",
        });
        await deck.Run("deck.clipboard.paste");

        var pasted = deck.Slide.Elements.OfType<TextElement>().Single(element => element.Text.PlainText.StartsWith("Uno", StringComparison.Ordinal));
        Assert.Equal(2, pasted.Text.Paragraphs.Count);
        var runs = pasted.Text.Paragraphs[0].Runs;
        Assert.Contains(runs, run => run.Text == "Uno" && run.Bold);
        Assert.Contains(runs, run => run.Text == "due" && run.Italic && run.Color == 0xFFFF0000u);
        Assert.All(runs, run => Assert.Equal("Helvetica", run.Font));

        deck.Target.Select([pasted]);
        await deck.Run("deck.clipboard.copy");
        var outgoing = await rich.GetAsync();
        Assert.Contains("<strong>Uno</strong>", outgoing.Html, StringComparison.Ordinal);
        Assert.Matches(@"\\b[^}]* Uno}", outgoing.Rtf);

        deck.Target.Select([pasted]);
        await rich.SetAsync(new ClipboardContent { Text = "Quattro", Html = "<p><u>Quattro</u></p>" });
        await deck.Run("deck.clipboard.paste");
        var appended = (TextElement)deck.Slide.Find(pasted.Id)!;
        Assert.Contains(appended.Text.Paragraphs[^1].Runs, run => run.Text == "Quattro" && run.Underline);

        var elements = deck.Slide.Elements.Count;
        deck.Target.EditText(pasted.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(deck.Target.IsTyping());
        await rich.SetAsync(new ClipboardContent { Text = "Cinque", Html = "<p><b>Cinque</b></p>" });
        await deck.Run("deck.clipboard.paste");
        Assert.Equal(elements, deck.Slide.Elements.Count);
        var typed = (TextElement)deck.Slide.Find(pasted.Id)!;
        Assert.Contains(typed.Text.Paragraphs.SelectMany(paragraph => paragraph.Runs), run => run.Text.Contains("Cinque", StringComparison.Ordinal) && run.Bold);
    }, CancellationToken.None);

    [Fact]
    public Task I_pixel_della_diapositiva_hanno_rosso_e_blu_al_loro_posto() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var slide = deck.Slide;
        var size = deck.Target.Deck.Size;
        deck.Target.Actions.InsertObject(slide, new ObjectElement
        {
            Id = "rosso",
            Bounds = new DocumentRect(new Twip(0), new Twip(0), size.Width, size.Height),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject
            {
                Id = "rosso",
                Shape = PulseOffice.Drawing.Shapes.ShapeKind.Rectangle,
                Style = new PulseOffice.Drawing.Shapes.ShapeStyle { Fill = new PulseOffice.Drawing.Shapes.ShapeFill { ColorHex = "#FF0000" } },
            },
        });
        Dispatcher.UIThread.RunJobs();

        var renderer = deck.Service<IDeckPublisher>().Renderer();
        var rgb = renderer.RenderPixels(deck.Target.Deck, slide, 64);
        var centre = (rgb.Height / 2 * rgb.Width + rgb.Width / 2) * 3;
        Assert.Equal((byte)255, rgb.Rgb[centre]);
        Assert.Equal((byte)0, rgb.Rgb[centre + 2]);

        var bgra = renderer.RenderBgra(deck.Target.Deck, slide, 64);
        var pixel = (bgra.Height / 2 * bgra.Width + bgra.Width / 2) * 4;
        Assert.Equal((byte)0, bgra.Rgb[pixel]);
        Assert.Equal((byte)255, bgra.Rgb[pixel + 2]);
    }, CancellationToken.None);

    [Fact]
    public Task Un_corpo_con_gli_a_capo_si_vede_nella_vista_e_non_mostra_il_segnaposto() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var slide = deck.Target.Actions.AddSlide(SlideLayout.TitleAndContent, 0)!;
        deck.Target.Actions.SetSlideText(slide, PlaceholderRole.Body, "prima riga\nseconda riga");
        Dispatcher.UIThread.RunJobs();

        var body = slide.Placeholder(PlaceholderRole.Body)!.Text;
        Assert.False(body.IsEmpty);
        Assert.Equal(["prima riga", "seconda riga"], body.Paragraphs.Select(p => p.Text));
    }, CancellationToken.None);

    [Fact]
    public Task Il_pdf_delle_diapositive_e_vettoriale_con_testo_selezionabile() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var slide = deck.Slide;
        deck.Target.Actions.SetSlideText(slide, PlaceholderRole.Title, "Titolo vettoriale");
        deck.Target.Actions.InsertObject(slide, new ObjectElement
        {
            Id = "forma",
            Bounds = new DocumentRect(Twip.FromPoints(40), Twip.FromPoints(200), Twip.FromPoints(200), Twip.FromPoints(100)),
            Content = new PulseOffice.Drawing.Shapes.ShapeObject { Id = "forma", Shape = PulseOffice.Drawing.Shapes.ShapeKind.Ellipse },
        });
        Dispatcher.UIThread.RunJobs();

        var publisher = deck.Service<IDeckPublisher>();
        var pdf = deck.Service<PulseOffice.Formats.Pdf.IPdfExporter>();
        using var stream = new MemoryStream();
        var result = pdf.Write(publisher.BuildPrintable(deck.Target.Deck, DeckExportLayout.Slides), stream,
            new PulseOffice.Formats.Pdf.PdfExportOptions("Prova", Compress: false));
        Assert.True(result.Succeeded);
        var text = System.Text.Encoding.Latin1.GetString(stream.ToArray());
        if (Environment.GetEnvironmentVariable("DECK_PDF_OUT") is { Length: > 0 } output) File.WriteAllBytes(output, stream.ToArray());

        Assert.Contains("/FontFile2", text, StringComparison.Ordinal);
        Assert.Contains("/ToUnicode", text, StringComparison.Ordinal);
        Assert.Contains(" TJ ", text, StringComparison.Ordinal);
        Assert.Contains(" c\n", text, StringComparison.Ordinal);
        Assert.Contains("/MediaBox [0 0 841.9 595.3]", text, StringComparison.Ordinal);

        using var exported = new MemoryStream();
        Assert.True(pdf.Write(publisher.BuildExportable(deck.Target.Deck, DeckExportLayout.Slides), exported,
            new PulseOffice.Formats.Pdf.PdfExportOptions("Prova", Compress: false)).Succeeded);
        var slideSized = System.Text.Encoding.Latin1.GetString(exported.ToArray());
        Assert.DoesNotContain("841.9", slideSized, StringComparison.Ordinal);
        Assert.Equal("/MediaBox [0 0 960.1 540]", System.Text.RegularExpressions.Regex.Match(slideSized, @"/MediaBox \[[^\]]*\]").Value);
        Assert.DoesNotContain("/Width 1600", text, StringComparison.Ordinal);
    }, CancellationToken.None);

    [Fact]
    public Task Una_diapositiva_nascosta_non_sposta_i_titoli_del_pdf_e_l_agente_le_vede_tutte() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        deck.Target.Actions.SetSlideText(deck.Slide, PlaceholderRole.Title, "Uno");
        var hidden = deck.Target.Actions.AddSlide(SlideLayout.TitleOnly, 0)!;
        deck.Target.Actions.SetSlideText(hidden, PlaceholderRole.Title, "Due");
        var third = deck.Target.Actions.AddSlide(SlideLayout.TitleOnly, 1)!;
        deck.Target.Actions.SetSlideText(third, PlaceholderRole.Title, "Tre");
        deck.Target.Actions.SetHidden(hidden, true);
        Dispatcher.UIThread.RunJobs();
        var publisher = deck.Service<IDeckPublisher>();

        var printed = (DeckPrintable)publisher.BuildPrintable(deck.Target.Deck, DeckExportLayout.Slides);
        var every = (DeckPrintable)publisher.BuildEverySlide(deck.Target.Deck);

        Assert.Equal(["Uno", "Tre"], printed.Titles);
        Assert.Equal(2, printed.PageCount);
        Assert.Equal(["Uno", "Due", "Tre"], every.Titles);
        Assert.Equal(3, every.PageCount);
    }, CancellationToken.None);

    [Fact]
    public Task Esportazioni_pdf_note_immagini_html_e_video() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var slide = deck.Slide;
        deck.Target.Actions.SetSlideText(slide, PlaceholderRole.Title, "Titolo esportato");
        deck.Target.Actions.SetNotes(slide, "Note del relatore");
        deck.Target.Actions.AddSlide(SlideLayout.TitleOnly, 0);
        Dispatcher.UIThread.RunJobs();
        var model = deck.Target.Deck;
        var publisher = deck.Service<IDeckPublisher>();
        var pdf = deck.Service<PulseOffice.Formats.Pdf.IPdfExporter>();

        foreach (var layout in new[] { DeckExportLayout.Slides, DeckExportLayout.Notes, DeckExportLayout.Handout4 })
        {
            using var stream = new MemoryStream();
            var result = pdf.Write(publisher.BuildPrintable(model, layout), stream);
            Assert.True(result.Succeeded);
            var text = System.Text.Encoding.Latin1.GetString(stream.ToArray());
            Assert.StartsWith("%PDF", text, StringComparison.Ordinal);
            if (layout == DeckExportLayout.Notes) Assert.Contains("/FontFile", text, StringComparison.Ordinal);
            Assert.Equal(layout == DeckExportLayout.Handout4 ? 1 : 2, result.Value);
        }

        var png = publisher.Renderer().RenderPng(model, slide, publisher.ExportWidth);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        Assert.Equal(publisher.ExportWidth, (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19]);

        var html = publisher.BuildHtml(model);
        Assert.Contains("Titolo esportato", html, StringComparison.Ordinal);

        var encoder = deck.Service<PulseOffice.Platform.IVideoEncoderProvider>().Current;
        if (!encoder.IsAvailable) return;
        var renderer = publisher.Renderer();
        var frames = model.Slides.Select(each => renderer.RenderBgra(model, each, 320)).ToList();
        var output = Path.Combine(_dataHome, "deck.mp4");
        Directory.CreateDirectory(_dataHome);
        var video = encoder.EncodeAsync(new PulseOffice.Platform.ListVideoFrameSource([.. frames.Select(frame => new PulseOffice.Platform.VideoFrame(frame.Rgb, 500))], frames[0].Width, frames[0].Height), 10, output).GetAwaiter().GetResult();
        Assert.True(video.Succeeded);
        var head = File.ReadAllBytes(output);
        Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(head, 4, 4));
    }, CancellationToken.None);

    [Fact]
    public Task Una_linea_orizzontale_si_disegna_anche_con_altezza_nulla() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        var line = new ObjectElement
        {
            Id = deck.Target.Deck.NextId(),
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(5), Twip.FromCentimeters(20), new Twip(0)),
            ZOrder = deck.Slide.Elements.Count,
            Content = new PulseOffice.Drawing.Shapes.ShapeObject
            {
                Id = deck.Target.Deck.NextId(),
                Shape = PulseOffice.Drawing.Shapes.ShapeKind.Line,
                Style = PulseOffice.Drawing.Shapes.ShapeStyle.Default with
                {
                    Line = PulseOffice.Drawing.Shapes.ShapeLine.Default with { ColorHex = "#0000FF", Width = Twip.FromCentimeters(0.5) },
                },
            },
        };
        deck.Target.Actions.InsertElement(deck.Slide, line);
        var publisher = deck.Service<IDeckPublisher>();
        var bitmap = SkiaSharp.SKBitmap.Decode(publisher.Renderer().RenderPng(deck.Target.Deck, deck.Slide, 640));
        var row = (int)Math.Round(5.0 / (deck.Target.Deck.Size.Height.Value / (double)Twip.FromCentimeters(1).Value) * bitmap.Height);
        var blue = Enumerable.Range(0, bitmap.Width).Count(x => bitmap.GetPixel(x, row) is { Blue: > 200, Red: < 60, Green: < 60 });
        Assert.True(blue > bitmap.Width / 2, $"{blue} pixel blu sulla riga {row}");
    }, CancellationToken.None);

    [Fact]
    public Task Penna_puntatore_laser_e_vista_relatore_seguono_la_presentazione() => Session.Dispatch(async () =>
    {
        await using var deck = await Open();
        deck.Slide.Notes = "Saluta il pubblico";
        var second = deck.Target.Actions.AddSlide(SlideLayout.TitleOnly, 0)!;
        second.Notes = "Mostra i numeri";
        Dispatcher.UIThread.RunJobs();

        var renderer = new SlideRenderer(deck.Service<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(), deck.Service<PulseOffice.Localization.ITextCatalog>());
        var window = new PresentationWindow(deck.Target.Deck, deck.Service<IAnimationScheduler>(), renderer, new PresentationOptions(0) { WithoutAnimation = true });
        window.Width = 800;
        window.Height = 450;
        window.WindowState = Avalonia.Controls.WindowState.Normal;
        window.Show();
        var presenter = new PresenterWindow(deck.Target.Deck, renderer, window, deck.Service<PulseOffice.Localization.ITextCatalog>());
        presenter.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            string[] Texts() => [.. presenter.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Select(block => block.Text ?? string.Empty)];
            Assert.Contains("Saluta il pubblico", Texts());
            Assert.Contains("1 / 2", Texts());
            Assert.All(presenter.GetVisualDescendants().OfType<Avalonia.Controls.Button>(), button => Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(button))));

            Press(window, Key.P);
            window.MouseDown(new Point(100, 100), MouseButton.Left);
            window.MouseMove(new Point(150, 120), RawInputModifiers.LeftMouseButton);
            window.MouseMove(new Point(200, 160), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(200, 160), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var ink = Assert.Single(window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Polyline>());
            Assert.True(ink.Points.Count >= 3);
            Assert.Equal(1, window.SlideNumber);

            Press(window, Key.E);
            Assert.Empty(window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Polyline>());

            Press(window, Key.L);
            window.MouseMove(new Point(300, 200));
            Dispatcher.UIThread.RunJobs();
            var laser = window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(dot => dot.IsVisible);
            Assert.Equal(300 - laser.Width / 2, Avalonia.Controls.Canvas.GetLeft(laser), 1);
            window.MouseDown(new Point(300, 200), MouseButton.Left);
            window.MouseUp(new Point(300, 200), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, window.SlideNumber);

            Press(window, Key.Escape);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>(), dot => dot.IsVisible);
            Press(presenter, Key.Right);
            Assert.Equal(2, window.SlideNumber);
            Assert.Contains("Mostra i numeri", Texts());
            Assert.Contains("2 / 2", Texts());
        }
        finally
        {
            presenter.Close();
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Giro_di_resa_per_il_confronto_esterno() => Session.Dispatch(async () =>
    {
        var output = Environment.GetEnvironmentVariable("PULSEOFFICE_RENDER_OUT");
        var files = Environment.GetEnvironmentVariable("PULSEOFFICE_RENDER_FILES");
        Assert.SkipWhen(string.IsNullOrEmpty(output) || string.IsNullOrEmpty(files), "Set PULSEOFFICE_RENDER_OUT and PULSEOFFICE_RENDER_FILES to render decks for comparison.");
        await using var deck = await Open();
        var formats = deck.Service<PulseOffice.Formats.IPresentationFormatRegistry>();
        var pictures = new SlideImageRenderer(new SlideRenderer(deck.Service<PulseOffice.UI.Controls.Shapes.IDrawingObjectRenderer>(), deck.Service<PulseOffice.Localization.ITextCatalog>()));
        foreach (var path in files!.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Path.Combine(output!, Path.GetFileNameWithoutExtension(path));
            try
            {
                using var stream = File.OpenRead(path);
                var read = formats.Read(path, stream);
                if (!read.Succeeded || read.Value is not { Slides.Count: > 0 } presentation)
                {
                    File.WriteAllText(name + ".error.txt", string.Join(Environment.NewLine, read.Diagnostics.Select(d => d.Message)));
                    continue;
                }
                deck.Service<PulseOffice.Presentations.UI.IDeckLinkResolver>().Resolve(presentation, path);
                var links = deck.Service<PulseOffice.UI.Controls.Objects.IImageLinks>();
                foreach (var slide in presentation.Slides)
                    foreach (var element in slide.Elements.OfType<PulseOffice.Presentations.ObjectElement>().ToList())
                        if (element.Content is PulseOffice.Drawing.Assets.RasterImageObject { LinkPath.Length: > 0 } linked && links.Update(linked) is { } loaded)
                            slide.Replace(element with { Content = loaded });
                File.WriteAllBytes(name + ".png", pictures.RenderPng(presentation, presentation.Slides[0], 1280));
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                File.WriteAllText(name + ".error.txt", failure.ToString());
            }
        }
    }, CancellationToken.None);

    private void Press(Avalonia.Controls.Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }
}
