using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Formats;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed record AutoTextFolder(string Path);

public interface IAutoTextLibrary
{
    IReadOnlyList<string> Names { get; }

    bool Save(string name, DocumentFragment fragment);

    DocumentFragment? Load(string name);

    bool Delete(string name);
}

public sealed class AutoTextLibrary(AutoTextFolder folder, ITextDocumentFormatRegistry formats, ITextDocumentFactory documents, IDocumentClipboard clipboard, ISubdocuments whole, PulseOffice.Platform.IAtomicFileWriter files)
    : IAutoTextLibrary
{
    private const string Extension = ".odt";

    public IReadOnlyList<string> Names =>
        Directory.Exists(folder.Path)
            ? [.. Directory.EnumerateFiles(folder.Path, "*" + Extension).Select(file => System.IO.Path.GetFileNameWithoutExtension(file)).Order(StringComparer.CurrentCultureIgnoreCase)]
            : [];

    public bool Save(string name, DocumentFragment fragment)
    {
        if (File(name) is not { } path || formats.WriterFor(path) is not { } writer) return false;
        var document = documents.Create();
        var first = document.Body.Blocks.OfType<Blocks.ParagraphBlock>().First();
        clipboard.Paste(document, new Document.Commands.UndoStack(), new DocumentPosition(first.Id, 0), fragment);
        Directory.CreateDirectory(folder.Path);
        return files.Replace(path, stream => writer.Write(document, stream), written => written.Succeeded).Succeeded;
    }

    public DocumentFragment? Load(string name)
    {
        if (File(name) is not { } path || !System.IO.File.Exists(path)) return null;
        using var stream = System.IO.File.OpenRead(path);
        return formats.Read(path, stream) is { Succeeded: true, Value: { } document } ? whole.Whole(document) : null;
    }

    public bool Delete(string name)
    {
        if (File(name) is not { } path || !System.IO.File.Exists(path)) return false;
        System.IO.File.Delete(path);
        return true;
    }

    private string? File(string name)
    {
        var clean = string.Concat(name.Split(System.IO.Path.GetInvalidFileNameChars())).Trim().TrimStart('.');
        return clean.Length == 0 ? null : System.IO.Path.Combine(folder.Path, clean + Extension);
    }
}

public sealed class FolioAutoTextCommands(
    IActiveFolioContext context,
    IAutoTextLibrary library,
    IDialogService dialogs,
    Blocks.IBlockTree tree,
    IStatusBarService status,
    ITextCatalog texts) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.tools.autoText") with { DefaultGesture = "Ctrl+F3", Surface = CommandSurface.Advanced, Execute = (_, ct) => ManageAsync(ct) },
        Action(Editing("folio.tools.autoTextExpand") with { DefaultGesture = "Ctrl+Shift+F3", Surface = CommandSurface.Advanced }, Expand),
    ];

    private async Task ManageAsync(CancellationToken ct)
    {
        if (Editor is not { } current) return;
        var view = new AutoTextView(library, tree, texts, current.HasSelection);
        if (await dialogs.ShowAsync<AutoTextChoice>(view, new DialogOptions { TitleKey = "command.folio.tools.autoText.title", PreferredWidth = 640 }, ct).ConfigureAwait(true) is not { } choice
            || Editor is not { } editor) return;
        switch (choice.Action)
        {
            case AutoTextAction.Insert when library.Load(choice.Name) is { } fragment:
                editor.Paste(fragment);
                break;
            case AutoTextAction.Create:
                if (editor.Copy() is { } selected && library.Save(choice.Name, selected))
                    status.Report(texts.Format("folio.autotext.saved", choice.Name), NotificationSeverity.Success);
                else status.Report(texts["folio.autotext.needSelection"], NotificationSeverity.Warning);
                break;
            case AutoTextAction.Delete when library.Delete(choice.Name):
                status.Report(texts.Format("folio.autotext.deleted", choice.Name), NotificationSeverity.Info);
                break;
        }
    }

    private void Expand(FolioEditor editor)
    {
        var caret = editor.Caret;
        var text = editor.TextOf(caret.ParagraphId) ?? string.Empty;
        var start = caret.Offset;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        if (start == caret.Offset)
        {
            status.Report(texts["folio.autotext.noWord"], NotificationSeverity.Warning);
            return;
        }
        var word = text[start..caret.Offset];
        var name = library.Names.FirstOrDefault(candidate => string.Equals(candidate, word, StringComparison.CurrentCultureIgnoreCase));
        if (name is null || library.Load(name) is not { } fragment)
        {
            status.Report(texts.Format("folio.autotext.unknown", word), NotificationSeverity.Warning);
            return;
        }
        using (editor.History.BeginTransaction("autotext"))
        {
            editor.Select(new DocumentPosition(caret.ParagraphId, start), caret);
            editor.Paste(fragment);
        }
    }
}
