using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Text;
using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioParagraphPageStartTests
{
    private sealed class DefaultsWith(IReadOnlyDictionary<string, object> overrides) : ITabbedForms
    {
        public Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, IReadOnlyList<FormTab> tabs, CancellationToken ct, int initialTab = 0, double width = 620)
        {
            var answers = tabs.SelectMany(tab => tab.Fields).ToDictionary(field => field.Key, field => field switch
            {
                CheckField check => (object)check.Initial,
                NumberField number => number.Initial,
                ChoiceField choice => choice.Initial,
                ColorField color => new ColorFieldValue().Normalize(color.Initial),
            PulseOffice.UI.Controls.Fills.SolidBackgroundField background => new ColorFieldValue().Normalize(background.InitialHex),
                FontField font => font.Initial,
                TabStopsField stops => stops.Initial,
                PulseOffice.UI.Controls.Borders.BorderEditorField borders => borders.Initial,
                PulseOffice.UI.Controls.Choices.IconChoiceField icon => icon.Initial,
                _ => string.Empty,
            });
            foreach (var (key, value) in overrides) answers[key] = value;
            return Task.FromResult<IReadOnlyDictionary<string, object>?>(answers);
        }
    }

    private static readonly CommandContext Open = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private static void Run(FolioScene scene, IReadOnlyDictionary<string, object> overrides)
    {
        var texts = scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>();
        var tabs = new FolioParagraphTabs(new ParagraphFormatTabs(new PulseOffice.UI.Icons.TextFormatIcons(), null!, new ParagraphSampleLayout(), texts), texts);
        var dialog = new FolioParagraphDialog(tabs, new DefaultsWith(overrides));
        new FolioDialogCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), null!, null!, dialog,
                scene.Services.GetRequiredService<IDirectFormatting>(), scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>())
            .GetCommands().Single(command => command.Id.Value == "folio.format.paragraph")
            .Execute(Open, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static FolioScene Scene() => new(document => document.Body.Replace(
    [
        new ParagraphBlock("p1", "uno"),
        new ParagraphBlock("p2", new TextParagraph("due") { PageStyleName = DefaultStyleNames.Landscape, PageNumberRestart = 5 }),
    ]));

    private static TextParagraph Second(FolioScene scene) => scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single(paragraph => paragraph.Id == "p2").Content;

    [Fact]
    public Task SceglierePaginaUgualeToglieLoStileDiPaginaESiAnnulla() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene();
        scene.Editor.PlaceCaret(new DocumentPosition("p2", 1), false);

        Run(scene, new Dictionary<string, object> { ["pageStyle"] = 0 });

        Assert.Equal(((string?)null, (int?)null), (Second(scene).PageStyleName, Second(scene).PageNumberRestart));
        scene.History.Undo();
        Assert.Equal(((string?)DefaultStyleNames.Landscape, (int?)5), (Second(scene).PageStyleName, Second(scene).PageNumberRestart));
    }, CancellationToken.None);

    [Fact]
    public Task LaRipartenzaAZeroToglieSoloLaNumerazione() => FolioScene.Session.Dispatch(() =>
    {
        var scene = Scene();
        scene.Editor.PlaceCaret(new DocumentPosition("p2", 1), false);

        Run(scene, new Dictionary<string, object> { ["pageNumber"] = 0.0 });

        Assert.Equal(((string?)DefaultStyleNames.Landscape, (int?)null), (Second(scene).PageStyleName, Second(scene).PageNumberRestart));
    }, CancellationToken.None);
}
