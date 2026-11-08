using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioProtectionOutsideHistoryTests
{
    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private sealed class AnsweringForms(IReadOnlyDictionary<string, object> answer) : IFolioForms
    {
        public Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, string explanationKey, IReadOnlyList<FormField> fields, CancellationToken ct, double width = 460) =>
            Task.FromResult<IReadOnlyDictionary<string, object>?>(answer);
    }

    public class Silent : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType is { IsValueType: true } type && type != typeof(void) ? Activator.CreateInstance(type) : null;
    }

    private static CommandDefinition Review(FolioScene scene, string id, IReadOnlyDictionary<string, object> answer)
    {
        var services = scene.Services;
        return new FolioReviewCommands(
                services.GetRequiredService<IActiveFolioContext>(), new AnsweringForms(answer), null!, null!, null!, null!, null!,
                services.GetRequiredService<IProtectionKeys>(), services.GetRequiredService<IEditGuard>(), null!, null!, null!, null!, null!, null!,
                null!, System.Reflection.DispatchProxy.Create<PulseOffice.UI.Controls.Services.IStatusBarService, Silent>(),
                services.GetRequiredService<PulseOffice.Localization.ITextCatalog>())
            .GetCommands().Single(command => command.Id.Value == id);
    }

    private static CommandDefinition Tool(FolioScene scene, string id) =>
        new FolioToolCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), scene.Services.GetRequiredService<FolioToolServices>())
            .GetCommands().Single(command => command.Id.Value == id);

    public static TheoryData<string> Protections => ["document", "changes", "record", "fields", "bookmarks"];

    private static (CommandDefinition Command, Func<bool> Applied) Protection(FolioScene scene, string kind) => kind switch
    {
        "document" => (Review(scene, "folio.protect.document", new Dictionary<string, object> { ["restriction"] = 1, ["password"] = string.Empty }),
            () => scene.Document.Protection.Restriction == DocumentRestriction.ReadOnly),
        "changes" => (Review(scene, "folio.changes.protect", new Dictionary<string, object> { ["protected"] = true, ["password"] = "segreto" }),
            () => scene.Document.Changes.ProtectionKey is not null && scene.Document.Changes.IsRecording),
        "record" => (Review(scene, "folio.changes.record", new Dictionary<string, object>()),
            () => scene.Document.Changes.IsRecording),
        "fields" => (Tool(scene, "folio.tools.protectFields"), () => scene.Document.Marks.Fields),
        _ => (Tool(scene, "folio.tools.protectBookmarks"), () => scene.Document.Marks.Bookmarks),
    };

    [Theory]
    [MemberData(nameof(Protections))]
    public Task ProteggereNonEntraNelloStoricoEDaComunqueUnaModifica(string kind) => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var outside = 0;
        scene.History.ChangedOutsideHistory += (_, _) => outside++;
        var (command, applied) = Protection(scene, kind);

        command.Execute(Context, CancellationToken.None).GetAwaiter().GetResult();
        scene.History.Undo();

        Assert.True(command.OutsideHistory);
        Assert.True(applied());
        Assert.False(scene.History.CanUndo);
        Assert.Equal(1, outside);
    }, CancellationToken.None);

    [Theory]
    [MemberData(nameof(Protections))]
    public Task AnnullareERipetereUnaModificaPrecedenteNonToccaLaProtezione(string kind) => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Alfa");
        var (command, applied) = Protection(scene, kind);

        command.Execute(Context, CancellationToken.None).GetAwaiter().GetResult();
        scene.History.Undo();

        Assert.Equal([""], scene.Paragraphs);
        Assert.True(applied());

        scene.History.Redo();

        Assert.Equal(["Alfa"], scene.Paragraphs);
        Assert.True(applied());
    }, CancellationToken.None);
}
