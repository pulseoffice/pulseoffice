using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class DeckPointerCaptureTests(HeadlessDesktopSession headless)
{
    private sealed class BlankRenderer : ISlideRenderer
    {
        public void Render(DrawingContext context, Slide slide, SlideSize size, Rect target, Theme theme, SlideMaster master, SlidePalette palette,
            Func<string, ElementVisual>? visuals = null, SlideChrome chrome = default, TextFields fields = default)
        {
        }

        public IReadOnlyList<SlideLayer> Layers(Slide slide, SlideSize size, Rect target, Theme theme, SlideMaster master, SlidePalette palette,
            Func<string, ElementVisual>? visuals = null, SlideChrome chrome = default, TextFields fields = default) => [];
    }

    private Window Host(Control content)
    {
        var window = new Window { Width = 600, Height = 800, Content = content };
        window.Show();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private void LoseCapture(Control control) =>
        control.RaiseEvent(new PointerCaptureLostEventArgs(control, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));

    [Fact]
    public Task Il_riquadro_della_pagina_note_non_segue_il_mouse_dopo_aver_perso_la_cattura() => headless.Session.Dispatch(() =>
    {
        var view = new PrintMasterView(new BlankRenderer(), new HandoutLayout(), "notes") { Deck = new Presentation(), Kind = PrintMasterKind.Notes };
        var changes = 0;
        view.BoxChanged += (_, _) => changes++;
        var window = Host(view);
        try
        {
            window.MouseDown(new Point(300, 220), MouseButton.Left);
            Assert.Equal(NotesMasterBox.Slide, view.Selected);

            LoseCapture(view);
            window.MouseMove(new Point(360, 320));
            window.MouseUp(new Point(360, 320), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, changes);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);

    [Fact]
    public Task Il_tratto_a_mano_libera_si_chiude_quando_la_cattura_si_perde() => headless.Session.Dispatch(() =>
    {
        var drawing = new DeckDrawingState();
        var view = new SlideView(new BlankRenderer()) { Editable = true };
        view.Show(new Slide("s1", SlideLayout.Blank));
        view.Drawing = drawing;
        var completed = 0;
        view.FreeformCompleted += (_, _) => completed++;
        var window = Host(view);
        try
        {
            drawing.Choose(DeckPen.Freehand, false);
            window.MouseDown(new Point(200, 380), MouseButton.Left);
            window.MouseMove(new Point(260, 400), RawInputModifiers.LeftMouseButton);
            window.MouseMove(new Point(320, 430), RawInputModifiers.LeftMouseButton);

            LoseCapture(view);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, completed);
        }
        finally
        {
            window.Close();
        }
    }, CancellationToken.None);
}
