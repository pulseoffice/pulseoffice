using PulseOffice.Document.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;

namespace PulseOffice.Documents.UI.Shell;

public interface IFolioDocumentFactory
{
    OpenDocument CreateEmpty();

    OpenDocument CreateFrom(TextDocument document, string title);
}

public sealed class FolioDocumentFactory(
    ITextDocumentFactory documents, FolioEditingServices editing, Func<FolioViewServices> views, PulseOffice.Localization.IInterfaceCulture culture) : IFolioDocumentFactory
{
    private int _counter;

    public OpenDocument CreateEmpty()
    {
        var document = documents.Create();
        if (document.Styles.DefaultCharacter.Language is null)
            document.Styles.DefaultCharacter = document.Styles.DefaultCharacter with { Language = culture.Culture.Name };
        return CreateFrom(document, $"{ModuleIdentity.Documents.BrandName}{++_counter}");
    }

    public OpenDocument CreateFrom(TextDocument document, string title)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrEmpty(document.Language)) document.Language = document.Styles.DefaultCharacter.Language ?? culture.Culture.Name;
        var history = new UndoStack();
        OpenDocument? open = null;
        var editor = new FolioEditor(document, history, editing, () => open?.IsReadOnly == true);
        var view = new FolioView(editor, views());
        open = new OpenDocument(ModuleKind.Documents, title, view, history) { Model = document };
        return open;
    }
}
