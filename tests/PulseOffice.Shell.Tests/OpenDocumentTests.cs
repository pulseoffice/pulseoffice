using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class OpenDocumentTests
{
    private sealed class Action(string description) : IUndoableAction
    {
        public string Description => description;

        public void Undo() { }

        public void Redo() { }

        public bool TryMergeWith(IUndoableAction previous) => false;
    }

    private static OpenDocument Document(out UndoStack history)
    {
        history = new UndoStack();
        return new OpenDocument(ModuleKind.Sheets, "Conti", new Avalonia.Controls.Panel(), history);
    }

    [Fact]
    public void UnDocumentoAppenaApertoNonEModificato()
    {
        var document = Document(out _);
        Assert.False(document.IsModified);
    }

    [Fact]
    public void UnaModificaLoSegnaModificato()
    {
        var document = Document(out var history);

        history.Push(new Action("scrittura"));

        Assert.True(document.IsModified);
    }

    [Fact]
    public void AnnullareFinoAlPuntoDiSalvataggioLoRiportaIntatto()
    {
        var document = Document(out var history);
        history.Push(new Action("scrittura"));

        history.Undo();

        Assert.False(document.IsModified);
    }

    [Fact]
    public void DopoIlSalvataggioIlPuntoSiSposta()
    {
        var document = Document(out var history);
        history.Push(new Action("scrittura"));

        document.MarkSaved("/tmp/conti.ods", "conti.ods");

        Assert.False(document.IsModified);

        history.Undo();
        Assert.True(document.IsModified);
    }

    [Fact]
    public void OgniModificaAvvisaChiOsserva()
    {
        var document = Document(out var history);
        var seen = 0;
        document.Changed += (_, _) => seen++;

        history.Push(new Action("scrittura"));

        Assert.Equal(1, seen);
    }

    [Fact]
    public void UnDocumentoRipristinatoRestaModificatoFinoAlSalvataggio()
    {
        var document = Document(out _);

        document.MarkModified();

        Assert.True(document.IsModified);
    }

    [Fact]
    public void UnCambioFuoriDalloStoricoLoSegnaModificatoESopravviveAdAnnulla()
    {
        var document = Document(out var history);
        var protectedSheet = false;

        history.ApplyOutsideHistory(() => protectedSheet = true);
        history.Undo();

        Assert.True(protectedSheet);
        Assert.False(history.CanUndo);
        Assert.True(document.IsModified);
    }
}
