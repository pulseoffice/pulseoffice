using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ShapeListTextTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private readonly ShapeTextEditing _editing = new();

    private ShapeObject Listed() => new()
    {
        Id = "s",
        Shape = ShapeKind.Rectangle,
        Text = "Uno\nDue",
        Lists = [new ShapeListItem { Numbered = true }, new ShapeListItem { Level = 1, Symbol = new BulletSymbol("•") { Picture = new ListBulletPicture(Png, "image/png", 8, 8) } }],
    };

    private sealed class Capturing : PulseOffice.UI.Controls.Text.Rich.IRichTextFramePainter
    {
        public RichText? Painted { get; private set; }

        public void Paint(PulseOffice.UI.Controls.Text.Rich.RichTextScene scene, RichText text) => Painted = text;
    }

    [Fact]
    public Task Una_forma_con_elenco_passa_dal_pittore_condiviso_del_testo_ricco() => Session.Dispatch(() =>
    {
        var painter = new Capturing();
        var renderer = new ShapeRenderer(new ShapeGeometry(), listText: new ShapeListText(new ShapeTextLists(), new ShapeRichText(), painter));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(200, 120));
        using (var context = bitmap.CreateDrawingContext())
            renderer.Render(context, Listed() with { Style = ShapeStyle.Default with { Text = ShapeStyle.Default.Text with { Horizontal = TextHorizontalPlacement.Right } } }, new Rect(0, 0, 200, 120));

        var painted = painter.Painted!;
        Assert.True(painted.Paragraphs[0].Numbered);
        Assert.Equal(1, painted.Paragraphs[1].BulletLevel);
        Assert.NotNull(painted.Paragraphs[1].BulletSymbol!.Picture);
        Assert.All(painted.Paragraphs, paragraph => Assert.Equal(TextAlign.Right, paragraph.Align));
        Assert.Equal(TextAnchor.Middle, painted.Frame.Anchor);
    }, CancellationToken.None);

    [Fact]
    public Task Una_forma_senza_elenco_non_passa_dal_pittore_del_testo_ricco() => Session.Dispatch(() =>
    {
        var painter = new Capturing();
        var renderer = new ShapeRenderer(new ShapeGeometry(), listText: new ShapeListText(new ShapeTextLists(), new ShapeRichText(), painter));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(200, 120));
        using (var context = bitmap.CreateDrawingContext())
            renderer.Render(context, Listed() with { Lists = null }, new Rect(0, 0, 200, 120));

        Assert.Null(painter.Painted);
    }, CancellationToken.None);

    [Fact]
    public void Andare_a_capo_in_un_punto_dell_elenco_crea_un_altro_punto_uguale()
    {
        var split = _editing.WithText(Listed(), "Uno\nDu\ne");

        Assert.Equal(3, split.Lists!.Count);
        Assert.Equal(split.Lists[1], split.Lists[2]);
        Assert.True(split.Lists[0]!.Numbered);
    }

    [Fact]
    public void Unire_due_paragrafi_tiene_il_punto_del_primo_e_svuotare_toglie_l_elenco()
    {
        var merged = _editing.WithText(Listed(), "UnoDue");

        Assert.True(Assert.Single(merged.Lists!)!.Numbered);
        Assert.Null(_editing.WithText(Listed(), string.Empty).Lists);
    }

    [Fact]
    public void Il_ponte_verso_il_testo_ricco_porta_e_riporta_l_elenco()
    {
        var bridge = new ShapeRichText();
        var rich = bridge.ToRich(Listed());

        Assert.True(rich.Paragraphs[0].Numbered);
        Assert.Equal(1, rich.Paragraphs[1].BulletLevel);
        Assert.Equal(Listed().Lists, bridge.FromRich(Listed(), rich).Lists);
    }

    [Fact]
    public Task Due_modifiche_in_paragrafi_diversi_dell_editor_tengono_i_punti_al_loro_posto() => Session.Dispatch(() =>
    {
        var editor = new ShapeTextEditor();
        var window = new Avalonia.Controls.Window { Content = new Avalonia.Controls.Canvas { Children = { editor } } };
        window.Show();
        var first = new ShapeListItem { Numbered = true };
        var second = new ShapeListItem { Level = 1 };
        var third = new ShapeListItem { Level = 2 };
        var fourth = new ShapeListItem { Level = 3 };
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Text = "A\nB\nC\nD", Lists = [first, second, third, fourth] };
        editor.Begin(new ShapeTextEditStart("s", shape, new ShapeTextPlacement(new Rect(0, 0, 200, 100), 1), new ShapeSurroundings()));

        editor.Box.Text = "A\n\nB\nC\nD";
        editor.Box.Text = "A\n\nB\nCD";

        Assert.Equal<ShapeListItem?>([first, second, second, third], editor.Live!.Lists!);
        window.Close();
    }, TestContext.Current.CancellationToken);
}
