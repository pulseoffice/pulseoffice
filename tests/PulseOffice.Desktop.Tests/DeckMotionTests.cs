using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Presentations.UI.Motion;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DeckMotionTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-motion-" + Guid.NewGuid().ToString("N"));

    public DeckMotionTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class Shell(ServiceProvider provider, CommandRegistry registry, ShellWindow window) : IAsyncDisposable
    {
        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public DeckTarget Target => provider.GetRequiredService<IActiveDeckContext>().Target!;

        public DeckView View => (DeckView)provider.GetRequiredService<IDocumentWorkspace>().Active!.View;

        public CommandDefinition Command(string id) => registry.Find(new CommandId(id))!;

        public void Settle()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
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
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        return new Shell(provider, registry, window);
    }

    private static Window Host(Control content, double width = 1200, double height = 760)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public Task Il_dialogo_delle_transizioni_ha_la_galleria_a_gruppi_e_le_opzioni_a_icone() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var slide = shell.Target.CurrentSlide()!;
        var dialog = shell.Service<IDeckMotionEditors>().TransitionDialog(shell.Target.Deck, slide);
        var window = Host(dialog);

        var editor = dialog.Editor;
        Assert.Equal(Enum.GetValues<TransitionKind>().Length, editor.TransitionGallery.Items.Count);
        Assert.Equal(3, editor.TransitionGallery.Items.Select(item => item.Group).Distinct().Count());
        Assert.False(editor.DirectionVisible);

        editor.ChooseKind(TransitionKind.Push);
        shell.Settle();
        Assert.True(editor.DirectionVisible);
        Assert.NotNull(editor.Directions);
        Assert.Equal(4, editor.Directions!.Items.Count);
        Assert.True(dialog.Preview.IsPlaying);

        editor.Directions.SelectedIndex = 2;
        Assert.Equal(TransitionDirection.Up, editor.Transition.Direction);

        editor.Duration.Value = 1.5;
        editor.Duration.StepBy(1, large: false);
        Assert.Equal("s", editor.Duration.UnitLabel);

        editor.Advance.SelectedIndex = 1;
        Assert.Equal(AdvanceMode.AfterTime, editor.Transition.Advance);
        Assert.True(editor.AdvanceAfter.IsEnabled);

        editor.ChooseKind(TransitionKind.Fade);
        Assert.False(editor.DirectionVisible);
        Assert.Equal(TransitionDirection.None, editor.Transition.Direction);

        editor.ChooseSound("/tmp/applause.wav");
        Assert.Equal(2, editor.Sound.ItemCount);
        Assert.Equal(1, editor.Sound.SelectedIndex);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Le_miniature_delle_transizioni_si_muovono_fra_le_due_diapositive() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        var stage = shell.Service<TransitionStage>();
        var thumbnail = new TransitionThumbnail(stage, new SlideTransition { Kind = TransitionKind.Push, Direction = TransitionDirection.Left },
            new StylizedTransitionSamples(), new Size(96, 54));
        var window = Host(thumbnail, 200, 120);

        thumbnail.ShowAt(0);
        var start = Offset(thumbnail.IncomingLayer);
        thumbnail.ShowAt(0.5);
        var middle = Offset(thumbnail.IncomingLayer);
        thumbnail.ShowAt(1);
        var end = Offset(thumbnail.IncomingLayer);
        Assert.NotEqual(start, middle);
        Assert.Equal(0, end, 3);

        thumbnail.Play();
        Assert.True(thumbnail.IsPlaying);
        thumbnail.Stop();
        Assert.False(thumbnail.IsPlaying);

        var none = new TransitionThumbnail(stage, SlideTransition.None, new StylizedTransitionSamples(), new Size(96, 54));
        Assert.True(none.IncomingLayer.IsVisible);
        Assert.False(none.OutgoingLayer.IsVisible);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Il_pannello_laterale_e_la_ribbon_applicano_le_transizioni_con_annulla() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        shell.Settle();
        var slide = shell.Target.CurrentSlide()!;
        var panel = shell.View.TransitionPanelEditor;
        Assert.NotNull(panel);

        panel!.ChooseKind(TransitionKind.Wipe);
        shell.Settle();
        Assert.Equal(TransitionKind.Wipe, slide.Transition.Kind);
        Assert.Equal(TransitionDirection.Left, slide.Transition.Direction);

        shell.Target.Actions.AddSlide(SlideLayout.TitleAndContent, 0);
        shell.Settle();
        shell.View.TransitionPanelEditor!.ApplyAllButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        shell.Settle();
        Assert.All(shell.Target.Deck.Slides, each => Assert.Equal(shell.Target.CurrentSlide()!.Transition.Kind, each.Transition.Kind));

        var gallery = shell.Command("deck.transition.gallery");
        var spec = Assert.IsType<ChoiceSpec>(gallery.RibbonControl);
        Assert.Equal(RibbonChoiceLook.Band, spec.Look);
        var choices = spec.Choices();
        Assert.All(choices, choice => Assert.IsType<TransitionRibbonThumbnail>(choice.Thumbnail));
        Assert.Equal(3, choices.Select(choice => choice.Section).Distinct().Count());
        var painter = shell.Service<IEnumerable<PulseOffice.UI.Controls.Ribbon.IRibbonThumbnailPainter>>().Single(candidate => candidate.Accepts(choices[0].Thumbnail!));
        Assert.IsType<TransitionThumbnail>(painter.Paint(choices[0].Thumbnail!, 80, 45));

        await shell.Service<CommandExecutor>().ExecuteAsync(gallery, shell.Service<IDocumentWorkspace>().Context with { Argument = nameof(TransitionKind.Zoom) });
        Assert.Equal(TransitionKind.Zoom, shell.Target.CurrentSlide()!.Transition.Kind);
        Assert.False(shell.Command("deck.transition.fade").Surface.HasFlag(CommandSurface.Ribbon));
    }, CancellationToken.None);

    [Fact]
    public Task Il_riquadro_animazioni_aggiunge_riordina_modifica_e_disegna_la_linea_temporale() => _session.Dispatch(async () =>
    {
        await using var shell = await Open();
        shell.Settle();
        var slide = shell.Target.CurrentSlide()!;
        var pane = shell.View.AnimationPanelPane!;
        Assert.NotNull(pane);
        Assert.Empty(pane.Rows);

        pane.Add(new AnimationPresetChoice(AnimationCategory.Entrance, "fly-in"));
        shell.Settle();
        pane = shell.View.AnimationPanelPane!;
        pane.Add(new AnimationPresetChoice(AnimationCategory.Emphasis, "spin"));
        shell.Settle();
        pane = shell.View.AnimationPanelPane!;
        Assert.Equal(2, slide.Animations.Count);
        Assert.Equal(2, pane.Rows.Count);
        Assert.Equal("spin", slide.Animations[1].Preset);

        pane.Move(1, 0);
        shell.Settle();
        Assert.Equal("spin", slide.Animations[0].Preset);

        shell.View.AnimationPanelPane!.Edit(slide.Animations[0] with { Trigger = AnimationTrigger.AfterPrevious, DurationMs = 2000 });
        shell.Settle();
        Assert.Equal(2000, slide.Animations[0].DurationMs);
        Assert.NotNull(shell.View.AnimationPanelPane!.Options.Content);

        var gallery = shell.View.AnimationPanelPane!.EffectGallery(null);
        var window = Host(gallery, 600, 500);
        Assert.Equal(4, gallery.Items.Select(item => item.Group).Distinct().Count());
        var thumbnail = gallery.GetVisualDescendants().OfType<AnimationPresetThumbnail>().First();
        thumbnail.ShowAt(0);
        Assert.True(thumbnail.Frame.Opacity < 1 || thumbnail.Frame.Reveal < 1 || thumbnail.Frame.Scale < 1);
        window.Close();

        await shell.Service<CommandExecutor>().ExecuteAsync(shell.Command("shell.edit.undo"), shell.Service<IDocumentWorkspace>().Context);
        shell.Settle();
        Assert.Equal(500, slide.Animations[0].DurationMs);
    }, CancellationToken.None);

    private static double Offset(Control layer) =>
        layer.RenderTransform is TransformGroup group ? group.Children.OfType<TranslateTransform>().Sum(transform => Math.Abs(transform.X) + Math.Abs(transform.Y)) : 0;
}
