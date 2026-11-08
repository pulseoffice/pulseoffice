using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public enum CloseDecision { Save, Discard, Cancel }

public interface IDocumentSaver
{
    bool CanSave(OpenDocument document);

    Task<bool> SaveAsync(OpenDocument document, CancellationToken ct = default);
}

public interface IUnsavedChangesGuard
{
    bool HasUnsaved { get; }

    Task<bool> ConfirmCloseAsync(IReadOnlyList<OpenDocument> documents, CancellationToken ct = default);

    Task<bool> ConfirmCloseAllAsync(CancellationToken ct = default);
}

public sealed class UnsavedChangesGuard(
    IDocumentWorkspace workspace,
    IEnumerable<IDocumentSaver> savers,
    IDialogService dialogs,
    ITextCatalog texts) : IUnsavedChangesGuard
{
    public bool HasUnsaved
    {
        get
        {
            foreach (var document in workspace.Documents) document.CommitPendingEdits();
            return workspace.Documents.Any(document => document.IsModified);
        }
    }

    public Task<bool> ConfirmCloseAllAsync(CancellationToken ct = default) =>
        ConfirmCloseAsync(workspace.Documents, ct);

    public async Task<bool> ConfirmCloseAsync(
        IReadOnlyList<OpenDocument> documents, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        foreach (var document in documents) document.CommitPendingEdits();
        var modified = documents
            .Where(document => document.IsModified && !KeptByAnotherView(document, documents))
            .DistinctBy<OpenDocument, object>(document => document.History, ReferenceEqualityComparer.Instance)
            .ToList();

        if (modified.Count == 0) return true;

        var unsavable = modified.Where(document => Saver(document) is null).ToList();
        var decision = await AskAsync(modified, unsavable, ct).ConfigureAwait(true);

        if (decision == CloseDecision.Cancel) return false;
        if (decision == CloseDecision.Discard) return true;
        if (unsavable.Count == modified.Count) return false;

        foreach (var document in modified.Except(unsavable))
        {
            if (Saver(document) is not { } saver) return false;

            if (!await saver.SaveAsync(document, ct).ConfigureAwait(true)) return false;
            if (document.IsModified) return false;
        }

        return true;
    }

    private bool KeptByAnotherView(OpenDocument document, IReadOnlyList<OpenDocument> closing) =>
        workspace.Documents.Any(other =>
            !ReferenceEquals(other, document)
            && ReferenceEquals(other.History, document.History)
            && !closing.Contains(other));

    private IDocumentSaver? Saver(OpenDocument document) =>
        savers.FirstOrDefault(saver => saver.CanSave(document));

    private IReadOnlyList<PromptChoice> Choices(bool savable)
    {
        var choices = new List<PromptChoice>();
        if (savable) choices.Add(new PromptChoice(texts["close.save"], CloseDecision.Save, IsDefault: true));
        choices.Add(new PromptChoice(texts["close.discard"], CloseDecision.Discard, IsDefault: !savable));
        choices.Add(new PromptChoice(texts["close.cancel"], CloseDecision.Cancel, IsCancel: true));
        return choices;
    }

    private string Explanation(IReadOnlyList<OpenDocument> modified, IReadOnlyList<OpenDocument> unsavable) =>
        unsavable.Count == 0 ? texts["close.unsaved.explanation"]
        : unsavable.Count == modified.Count ? texts["close.unsaved.unsavable"]
        : texts.Format("close.unsaved.partial", string.Join(", ", unsavable.Select(document => document.Title)));

    private async Task<CloseDecision> AskAsync(
        IReadOnlyList<OpenDocument> modified, IReadOnlyList<OpenDocument> unsavable, CancellationToken ct)
    {
        var names = string.Join(", ", modified.Select(document => document.Title));

        var view = new ChoicePromptView(
            modified.Count == 1 ? texts.Format("close.unsaved.one", names) : texts.Format("close.unsaved.many", modified.Count, names),
            Explanation(modified, unsavable),
            Choices(unsavable.Count < modified.Count),
            texts);

        var chosen = await dialogs.ShowAsync<object>(
            view,
            new DialogOptions
            {
                TitleKey = "close.unsaved.heading",
                PreferredWidth = 460,
                CanCancelWithEscape = true,
            },
            ct).ConfigureAwait(true);

        return chosen as CloseDecision? ?? CloseDecision.Cancel;
    }
}
