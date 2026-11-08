using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Embedding;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class DocumentLanguageTests
{
    private sealed class CapturingControls : IFormControlRenderer
    {
        public CultureInfo? Culture { get; private set; }

        public void Render(DrawingContext context, FormControlKind kind, FormControlProperties control, IReadOnlyList<string> items, Rect bounds, FormControlPalette palette, CultureInfo culture) =>
            Culture = culture;
    }

    private sealed class CapturingPainter : IContentPainter
    {
        public string? Language { get; private set; }

        public bool CanPaint(EmbeddedObject content) => true;

        public void Paint(DrawingContext context, EmbeddedObject content, Rect bounds, double pixelsPerPoint = 1, ShapeSurroundings backdrop = default) =>
            Language = backdrop.Language;
    }

    private sealed class FixedCulture(string name) : IInterfaceCulture
    {
        public CultureInfo Culture => CultureInfo.GetCultureInfo(name);
    }

    private static T WithSystem<T>(string system, Func<T> body)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(system);
        try
        {
            return body();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task FormControlsAreDrawnInTheLanguageOfTheirSurroundings(string system, string language) => FolioScene.RunAsync(() =>
    {
        var controls = new CapturingControls();
        var painter = new FormControlContentPainter(controls, new PulseOffice.Text.Rich.TextCultures(CultureInfo.InvariantCulture));
        var form = new FormControlObject { Id = "f", ControlKind = FormControlKind.NumericField, Properties = new FormControlProperties { Value = new FormControlValue { Number = 1234.5 } } };
        using var bitmap = new RenderTargetBitmap(new PixelSize(20, 20), new Vector(96, 96));
        WithSystem(system, () =>
        {
            using var context = bitmap.CreateDrawingContext();
            painter.Paint(context, form, new Rect(0, 0, 20, 20), 1, new ShapeSurroundings() { Language = language });
            return 0;
        });
        Assert.Equal(language, controls.Culture?.Name);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task PageSurfacesHandTheDocumentLanguageToEmbeddedContent(string system, string language) => FolioScene.RunAsync(() =>
    {
        var painter = new CapturingPainter();
        WithSystem(system, () =>
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(20, 20), new Vector(96, 96));
            using var context = bitmap.CreateDrawingContext();
            var surface = new DrawingSurface(context, null!, painter, language);
            surface.Backdrop(0xFFFFFFFF);
            surface.Embedded(new FormControlObject { Id = "f", ControlKind = FormControlKind.TextBox }, new LayoutRect(0, 0, 10, 10), new ObjectTransform(0));
            return 0;
        });
        Assert.Equal(language, painter.Language);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("it-IT", "en-US")]
    [InlineData("en-US", "it-IT")]
    public Task NewDocumentsTakeTheInterfaceLanguageNotTheSystemCulture(string system, string language) => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var factory = new FolioDocumentFactory(
            scene.Services.GetRequiredService<PulseOffice.Documents.ITextDocumentFactory>(),
            scene.Services.GetRequiredService<PulseOffice.Documents.UI.Editing.FolioEditingServices>(),
            scene.Services.GetRequiredService<Func<FolioViewServices>>(),
            new FixedCulture(language));
        var document = WithSystem(system, () => (TextDocument)factory.CreateEmpty().Model!);
        Assert.Equal(language, document.Language);
        Assert.Equal(language, document.Styles.DefaultCharacter.Language);
        return Task.CompletedTask;
    });

    [Fact]
    public Task ImportedDocumentsKeepTheirDefaultStyleLanguage() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var factory = new FolioDocumentFactory(
            scene.Services.GetRequiredService<PulseOffice.Documents.ITextDocumentFactory>(),
            scene.Services.GetRequiredService<PulseOffice.Documents.UI.Editing.FolioEditingServices>(),
            scene.Services.GetRequiredService<Func<FolioViewServices>>(),
            new FixedCulture("it-IT"));
        var documents = scene.Services.GetRequiredService<PulseOffice.Documents.ITextDocumentFactory>();
        var imported = documents.Create();
        imported.Styles.DefaultCharacter = imported.Styles.DefaultCharacter with { Language = "de-DE" };
        factory.CreateFrom(imported, "x");
        Assert.Equal("de-DE", imported.Language);

        var declared = documents.Create();
        declared.Language = "fr-FR";
        factory.CreateFrom(declared, "y");
        Assert.Equal("fr-FR", declared.Language);
        return Task.CompletedTask;
    });
}
