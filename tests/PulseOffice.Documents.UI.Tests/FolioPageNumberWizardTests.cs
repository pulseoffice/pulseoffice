using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Pages;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioPageNumberWizardTests
{
    private static readonly CommandContext Open = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    [Fact]
    public Task IlNumeroDiPaginaConIlTotaleSiAnnullaInUnSoloPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new ParagraphBlock("p1", new TextParagraph("vecchia") { Runs = [new TextRun("vecchia") { Field = TextFieldKind.Date }] }),
            new ParagraphBlock("p2", new TextParagraph("due") { PageBreakBefore = true }),
            new ParagraphBlock("p3", new TextParagraph("tre") { PageBreakBefore = true }),
        ]));
        var services = scene.Services.GetRequiredService<FolioToolServices>() with
        {
            Forms = new AnsweringForms(new Dictionary<string, object>
            {
                ["position"] = new PageNumberPosition(false, PageNumberAlignment.Center),
                ["total"] = true,
            }),
        };
        var footer = scene.Document.Styles.Page.All[0].Footer;
        Assert.False(footer.Enabled);

        new FolioToolCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), services)
            .GetCommands().Single(command => command.Id.Value == "folio.insert.pageNumberWizard")
            .Execute(Open, CancellationToken.None).GetAwaiter().GetResult();

        var page = scene.Document.Styles.Page.All[0];
        Assert.True(page.Footer.Enabled);
        Assert.Contains(page.Footer.Default.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs), run => run.Field == TextFieldKind.PageCount);
        Assert.Equal("vecchia", scene.Paragraphs[0]);

        scene.History.Undo();

        var restored = scene.Document.Styles.Page.All[0];
        Assert.False(restored.Footer.Enabled);
        Assert.DoesNotContain(restored.Footer.Default.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs), run => run.Field == TextFieldKind.PageNumber);
    }, CancellationToken.None);
}
