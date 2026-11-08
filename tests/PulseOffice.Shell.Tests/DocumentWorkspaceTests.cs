using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class DocumentWorkspaceTests
{
    private sealed class Action(string description) : IUndoableAction
    {
        public string Description => description;

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(IUndoableAction previous) => false;
    }

    private static OpenDocument Document(out UndoStack history, string title = "Conti")
    {
        history = new UndoStack();
        return new OpenDocument(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), history);
    }

    [Fact]
    public void LaSelezioneSopravviveAUnaModificaDelDocumento()
    {
        var workspace = new DocumentWorkspace();
        var document = Document(out var history);

        workspace.Open(document);
        workspace.UpdateContext(context => context with
        {
            Selection = SelectionKind.Cell,
            State = context.State | DocumentState.HasSelection,
        });

        history.Push(new Action("scrittura"));

        Assert.Equal(SelectionKind.Cell, workspace.Context.Selection);
        Assert.True(workspace.Context.Has(DocumentState.HasSelection));
        Assert.True(workspace.Context.Has(DocumentState.Modified));
    }

    [Fact]
    public void ModificareUnDocumentoNonLoRiattiva()
    {
        var workspace = new DocumentWorkspace();
        var document = Document(out var history);

        workspace.Open(document);

        var activations = 0;
        var updates = 0;

        workspace.ActiveChanged += (_, _) => activations++;
        workspace.ActiveUpdated += (_, _) => updates++;

        history.Push(new Action("scrittura"));

        Assert.Equal(0, activations);
        Assert.Equal(1, updates);
        Assert.True(workspace.Context.Has(DocumentState.Modified));
    }

    [Fact]
    public void CambiandoModuloLaSelezioneNonSiTrascina()
    {
        var workspace = new DocumentWorkspace();
        var sheet = Document(out _);

        workspace.Open(sheet);
        workspace.UpdateContext(context => context with { Selection = SelectionKind.Cell });

        var text = new OpenDocument(
            ModuleKind.Documents, "Lettera", new Avalonia.Controls.Panel(), new UndoStack());

        workspace.Open(text);

        Assert.Equal(SelectionKind.None, workspace.Context.Selection);
    }

    [Fact]
    public void AnnullareEDisponibileSoloQuandoCEQualcosaDaAnnullare()
    {
        var workspace = new DocumentWorkspace();
        var document = Document(out var history);

        workspace.Open(document);
        Assert.False(workspace.Context.Has(DocumentState.CanUndo));

        history.Push(new Action("scrittura"));
        Assert.True(workspace.Context.Has(DocumentState.CanUndo));
    }

    [Fact]
    public void ChiudendoLUltimoDocumentoIlContestoSiSvuota()
    {
        var workspace = new DocumentWorkspace();
        var document = Document(out _);

        workspace.Open(document);
        workspace.Close(document);

        Assert.Equal(CommandContext.Empty, workspace.Context);
    }

    [Fact]
    public void CambiandoDocumentoIFattiDellaVistaPrecedenteSpariscono()
    {
        var workspace = new DocumentWorkspace();
        var first = Document(out _, "Primo");
        var second = Document(out _, "Secondo");

        workspace.Open(first);
        workspace.Open(second);
        workspace.UpdateContext(context => context with
        {
            Selection = SelectionKind.Cell,
            State = context.State | DocumentState.HasSelection,
        });

        workspace.Activate(first);

        Assert.Equal(SelectionKind.None, workspace.Context.Selection);
        Assert.False(workspace.Context.Has(DocumentState.HasSelection));
    }
}
