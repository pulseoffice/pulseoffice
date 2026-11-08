using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioRound57DialogTests
{
    private static (FolioScene Scene, DialogAdmissionHarness Harness) Folio()
    {
        var scene = new FolioScene(document => document.Body.Add(new SectionBlock("section-1", "Section", new BlockContainer())));
        var context = scene.Services.GetRequiredService<IActiveFolioContext>();
        context.Attach(new FolioTarget(scene.Editor, () => default!, _ => { }, () => 1, () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask));
        return (scene, new DialogAdmissionHarness(ModuleKind.Documents, new FolioStateWitness(context, new BlockTree())));
    }

    [Fact]
    public Task Document_protection_applied_while_the_dialog_is_open_drops_the_answer() => FolioScene.RunAsync(async () =>
    {
        var (scene, harness) = Folio();
        using (harness)
        {
            var answer = await harness.AnswerAfter(() => scene.Editor.Document.Protection = new DocumentProtection(DocumentRestriction.ReadOnly, null));

            Assert.Null(answer);
            Assert.Contains(harness.Texts["status.dialog.stale.protection"], harness.Reported);
        }
    });

    [Fact]
    public Task A_section_protected_while_the_dialog_is_open_drops_the_answer() => FolioScene.RunAsync(async () =>
    {
        var (scene, harness) = Folio();
        using (harness)
        {
            var section = scene.Editor.Document.Body.Blocks.OfType<SectionBlock>().Single();

            var answer = await harness.AnswerAfter(() => section.Protected = true);

            Assert.Null(answer);
        }
    });

    [Fact]
    public Task Field_protection_applied_while_the_dialog_is_open_drops_the_answer() => FolioScene.RunAsync(async () =>
    {
        var (scene, harness) = Folio();
        using (harness)
        {
            var answer = await harness.AnswerAfter(() => scene.Editor.Document.Marks = new MarkProtection(true, false));

            Assert.Null(answer);
        }
    });

    [Fact]
    public Task Locking_tracked_changes_while_the_dialog_is_open_drops_the_answer() => FolioScene.RunAsync(async () =>
    {
        var (scene, harness) = Folio();
        using (harness)
        {
            var answer = await harness.AnswerAfter(() => scene.Editor.Document.Changes.ProtectionKey = [1, 2, 3]);

            Assert.Null(answer);
        }
    });

    [Fact]
    public Task Closing_the_document_while_the_dialog_is_open_drops_the_answer() => FolioScene.RunAsync(async () =>
    {
        var (_, harness) = Folio();
        using (harness)
        {
            var answer = await harness.AnswerAfter(() => harness.Workspace.Close(harness.Document));

            Assert.Null(answer);
            Assert.Contains(harness.Texts["status.dialog.stale.closed"], harness.Reported);
        }
    });

    [Fact]
    public Task Typing_elsewhere_does_not_drop_the_answer() => FolioScene.RunAsync(async () =>
    {
        var (scene, harness) = Folio();
        using (harness)
        {
            scene.Editor.Document.Body.Blocks.OfType<SectionBlock>().Single().Protected = true;
            var witnessed = await harness.AnswerAfter(() => scene.Editor.Document.Body.Add(new SectionBlock("section-2", "Other", new BlockContainer())));

            Assert.Equal("applied", witnessed);
        }
    });
}
