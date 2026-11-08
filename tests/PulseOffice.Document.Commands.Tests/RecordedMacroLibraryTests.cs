using PulseOffice.Document.Commands;
using PulseOffice.Document.Forms;
using Xunit;

namespace PulseOffice.Document.Commands.Tests;

public sealed class RecordedMacroLibraryTests
{
    private readonly MacroLibrary _inner = new();
    private readonly UndoStack _history = new();

    private RecordedMacroLibrary Library() => new(_inner, _history, "Macro");

    [Fact]
    public void SalvareUnaMacroEntraNellaCronologiaESiAnnulla()
    {
        Library().Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "app.Log(1)"));

        Assert.True(_history.CanUndo);
        _history.Undo();
        Assert.Null(_inner.Find("Saluta"));
    }

    [Fact]
    public void AnnullareUnaModificaRiportaIlCodiceDiPrima()
    {
        var library = Library();
        library.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "app.Log(1)"));
        library.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "app.Log(2)"));

        _history.Undo();

        Assert.Equal("app.Log(1)", _inner.Find("Saluta")!.Source);
    }

    [Fact]
    public void UnSalvataggioIdenticoNonContaComeModifica()
    {
        var macro = new DocumentMacro("Saluta", MacroLanguage.Python, "print(1)");
        var library = Library();
        library.Store(macro);
        var point = _history.StatePoint;

        library.Store(macro);

        Assert.Same(point, _history.StatePoint);
    }

    [Fact]
    public void EliminareSiAnnullaRimettendoLaMacro()
    {
        var library = Library();
        library.Store(new DocumentMacro("Saluta", MacroLanguage.JavaScript, "app.Log(1)"));
        library.Remove("Saluta");

        _history.Undo();

        Assert.NotNull(_inner.Find("Saluta"));
    }
}
