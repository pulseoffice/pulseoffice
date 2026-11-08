using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Localization;
using PulseOffice.Testing;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class ShapeFieldLanguageTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private sealed class Capturing : IRichTextFramePainter
    {
        public string? Language { get; private set; }

        public void Paint(RichTextScene scene, RichText text) => Language = scene.Fields.Language;
    }

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task ShapeListTextReceivesTheWorkbookLanguage(string system, string language) => Session.Dispatch(() =>
    {
        var workbook = Workbook.CreateDefault();
        workbook.DefaultLanguage = language;
        var painter = new Capturing();
        var renderer = new ShapeRenderer(new ShapeGeometry(), listText: new ShapeListText(new ShapeTextLists(), new ShapeRichText(), painter));
        var shape = new ShapeObject { Id = "s", Shape = ShapeKind.Rectangle, Text = "Uno", Lists = [new ShapeListItem()] };

        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(40, 40), new Vector(96, 96));
            using var context = bitmap.CreateDrawingContext();
            renderer.Render(context, shape, new Rect(0, 0, 40, 40), new ShapeSurroundings() { Language = workbook.TextLanguage });
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }

        Assert.Equal(language, painter.Language);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Theory]
    [InlineData(UiLanguage.English, "it-IT", "en-US")]
    [InlineData(UiLanguage.Italian, "en-US", "it-IT")]
    public void TheInterfaceCultureFollowsTheInterfaceNotTheSystem(UiLanguage language, string system, string expected) =>
        Assert.Equal(expected, new InterfaceCulture(new TextCatalog(language), () => CultureInfo.GetCultureInfo(system)).Culture.Name);
}
