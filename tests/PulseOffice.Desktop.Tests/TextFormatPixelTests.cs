using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PulseOffice.Document.Pages;
using PulseOffice.Drawing.Assets.Filters;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Pages;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class TextFormatPixelTests(HeadlessDesktopSession headless)
{
    private const int Dark = 200;

    private readonly BitmapPixels _pixels = new();

    [Fact]
    public Task Il_paragrafo_allineato_a_destra_ha_inchiostro_solo_verso_il_margine_destro() => headless.Session.Dispatch(() =>
    {
        var left = Paragraph(new ParagraphFormatState());
        var right = Paragraph(new ParagraphFormatState { Align = TextAlign.Right });
        var row = Ink(left).Select((_, y) => y).First(y => DarkIn(left, y, 0, 30) > 0);

        Assert.True(DarkIn(left, row + 2, 10, 20) > 0);
        Assert.True(DarkIn(right, LastCurrentRow(right), right.Width - 22, right.Width - 12) > 0);
        Assert.Equal(0, DarkIn(right, LastCurrentRow(right), 10, 20));
    }, CancellationToken.None);

    [Fact]
    public Task Il_testo_sottolineato_disegna_una_linea_sotto_la_linea_di_base() => headless.Session.Dispatch(() =>
    {
        var plain = Character(new CharacterFormatState { Size = 24 });
        var underlined = Character(new CharacterFormatState { Size = 24, Underline = UnderlineKind.Single });

        Assert.True(Rows(underlined).Sum() > Rows(plain).Sum() + 15);
        Assert.True(Rows(underlined).Skip(Rows(plain).ToList().FindLastIndex(dark => dark > 0) + 1).Sum() > 15);
        Assert.True(Rows(plain).Sum() > 50);
    }, CancellationToken.None);

    [Fact]
    public Task La_numerazione_delle_righe_cambia_lato() => headless.Session.Dispatch(() =>
    {
        var layout = new LineNumberSampleLayout();
        var leftView = new LineNumberSampleView(layout);
        leftView.Show(new LineNumberSample(true, 1, false, 14, true));
        var rightView = new LineNumberSampleView(layout);
        rightView.Show(new LineNumberSample(true, 1, true, 14, true));
        var left = Capture(leftView);
        var right = Capture(rightView);

        Assert.True(Coloured(left, 0, 30) > 0);
        Assert.Equal(0, Coloured(right, 0, 30));
        Assert.True(Coloured(right, right.Width - 30, right.Width) > 0);
    }, CancellationToken.None);

    [Fact]
    public Task L_anteprima_della_pagina_mostra_le_colonne() => headless.Session.Dispatch(() =>
    {
        var single = Capture(new PagePreview(new PageSketchLayout()) { Width = 200, Height = 260, Sketch = new PageSketch(21, 29.7) { Margins = new PageSketchMargins(2, 2, 2, 2) } });
        var two = Capture(new PagePreview(new PageSketchLayout()) { Width = 200, Height = 260, Sketch = new PageSketch(21, 29.7) { Margins = new PageSketchMargins(2, 2, 2, 2), Columns = 2, ColumnGap = 3 } });
        var middle = single.Width / 2;

        Assert.Contains(Ink(single).Skip(60).Take(100), row => row > 0);
        Assert.True(DarkColumn(single, middle) > DarkColumn(two, middle));
    }, CancellationToken.None);

    private BgraImage Paragraph(ParagraphFormatState state)
    {
        var view = new ParagraphSampleView(new ParagraphSampleLayout());
        view.Show(state);
        return Capture(view);
    }

    private BgraImage Character(CharacterFormatState state)
    {
        var view = new CharacterSampleView(new CharacterSampleLayout());
        view.Show(state, "AH");
        return Capture(view);
    }

    private BgraImage Capture(Control view)
    {
        var window = new Window { Width = view.Width, Height = view.Height, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame()!;
        var image = _pixels.Read(frame);
        window.Close();
        return image;
    }

    private int Luma(BgraImage image, int x, int y)
    {
        var at = (y * image.Width + x) * 4;
        return (image.Pixels[at] + image.Pixels[at + 1] + image.Pixels[at + 2]) / 3;
    }

    private int DarkIn(BgraImage image, int y, int from, int to) =>
        Enumerable.Range(Math.Max(0, from), Math.Max(0, Math.Min(image.Width, to) - Math.Max(0, from))).Count(x => Luma(image, x, y) < Dark);

    private IReadOnlyList<int> Rows(BgraImage image) => [.. Enumerable.Range(0, image.Height).Select(y => DarkIn(image, y, 0, image.Width))];

    private IReadOnlyList<int> Ink(BgraImage image) => Rows(image);

    private int DarkColumn(BgraImage image, int x) => Enumerable.Range(0, image.Height).Count(y => Luma(image, x, y) < 235);

    private int LastCurrentRow(BgraImage image)
    {
        var current = Enumerable.Range(0, image.Height).Where(y => Enumerable.Range(0, image.Width).Any(x => Luma(image, x, y) < 120)).ToList();
        return current.Count == 0 ? 0 : current[^1];
    }

    private int Coloured(BgraImage image, int from, int to) =>
        Enumerable.Range(0, image.Height).Sum(y => Enumerable.Range(from, to - from).Count(x =>
        {
            var at = (y * image.Width + x) * 4;
            return image.Pixels[at] > image.Pixels[at + 2] + 60;
        }));
}
