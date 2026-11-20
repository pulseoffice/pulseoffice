using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public enum CloseDecision { Save, Discard, Cancel, SaveAll, DiscardAll }

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

        var toSave = new List<OpenDocument>();
        for (var index = 0; index < modified.Count; index++)
        {
            var document = modified[index];
            var remaining = modified.Skip(index).ToList();
            var decision = await AskAsync(document, remaining, ct).ConfigureAwait(true);

            switch (decision)
            {
                case CloseDecision.Cancel:
                    return false;
                case CloseDecision.DiscardAll:
                    return await SaveAllAsync(toSave, ct).ConfigureAwait(true);
                case CloseDecision.SaveAll:
                    toSave.AddRange(remaining.Where(item => Saver(item) is not null));
                    return await SaveAllAsync(toSave, ct).ConfigureAwait(true);
                case CloseDecision.Save:
                    if (Saver(document) is null) return false;
                    toSave.Add(document);
                    break;
            }
        }

        return await SaveAllAsync(toSave, ct).ConfigureAwait(true);
    }

    private async Task<bool> SaveAllAsync(IReadOnlyList<OpenDocument> documents, CancellationToken ct)
    {
        foreach (var document in documents)
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

    private IReadOnlyList<PromptChoice> Choices(bool savable, bool several, bool othersSavable)
    {
        var choices = new List<PromptChoice>();
        if (savable) choices.Add(new PromptChoice(texts["close.save"], CloseDecision.Save, IsDefault: true));
        choices.Add(new PromptChoice(texts["close.discard"], CloseDecision.Discard, IsDefault: !savable));
        if (several && (savable || othersSavable)) choices.Add(new PromptChoice(texts["close.saveAll"], CloseDecision.SaveAll));
        if (several) choices.Add(new PromptChoice(texts["close.discardAll"], CloseDecision.DiscardAll));
        choices.Add(new PromptChoice(texts["close.cancel"], CloseDecision.Cancel, IsCancel: true));
        return choices;
    }

    private async Task<CloseDecision> AskAsync(
        OpenDocument document, IReadOnlyList<OpenDocument> remaining, CancellationToken ct)
    {
        var savable = Saver(document) is not null;
        var several = remaining.Count > 1;
        var others = remaining.Skip(1).ToList();

        var view = new ChoicePromptView(
            several
                ? texts.Format("close.unsaved.oneOfMany", document.Title, others.Count)
                : texts.Format("close.unsaved.one", document.Title),
            texts[savable ? "close.unsaved.explanation" : "close.unsaved.unsavable"],
            Choices(savable, several, others.Any(item => Saver(item) is not null)),
            texts);

        var chosen = await dialogs.ShowAsync<object>(
            view,
            new DialogOptions
            {
                TitleKey = "close.unsaved.heading",
                PreferredWidth = 520,
                CanCancelWithEscape = true,
            },
            ct).ConfigureAwait(true);

        return chosen as CloseDecision? ?? CloseDecision.Cancel;
    }
}
