using PulseOffice.Testing;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.UI.Controls.Spelling;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class TextReviewEditWatchTests
{
    private readonly UiThreadSession _session;

    public TextReviewEditWatchTests(HeadlessDesktopSession headless) => _session = headless.Session;

    private sealed class Session : ITextReviewSession
    {
        public TextReviewRequest? Pending { get; set; } = new Request();

        public bool IsDisplayed { get; set; }

        public bool Invalidated { get; private set; }

        public int Dismissed { get; private set; }

        public void Invalidate() => Invalidated = true;

        public event EventHandler? Changed { add { } remove { } }

        public void Answer(object? answer) { }

        public void Dismiss()
        {
            Dismissed++;
            Pending = null;
        }
    }

    private sealed record Request() : TextReviewRequest("prova");

    private static OpenDocument Document(string title) =>
        new(ModuleKind.Documents, title, new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack());

    [Fact]
    public Task Chiuso_l_ultimo_documento_la_revisione_si_chiude_e_lo_storico_non_e_piu_osservato() => _session.Dispatch(() =>
    {
        var workspace = new DocumentWorkspace();
        var document = Document("unico");
        workspace.Open(document);
        var session = new Session();
        new TextReviewEditWatch(workspace, session).Start();

        workspace.Close(document);
        session.Pending = new Request();
        document.History.Clear();

        Assert.False(session.Invalidated);
    }, CancellationToken.None);

    [Fact]
    public Task Chiuso_l_ultimo_documento_la_revisione_in_sospeso_si_scarta() => _session.Dispatch(() =>
    {
        var workspace = new DocumentWorkspace();
        var document = Document("unico");
        workspace.Open(document);
        var session = new Session();
        new TextReviewEditWatch(workspace, session).Start();

        workspace.Close(document);

        Assert.Equal(1, session.Dismissed);
    }, CancellationToken.None);
}
