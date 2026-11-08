using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Review;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioReviewCommands(
    IActiveFolioContext context,
    IFolioForms forms,
    IRevisionResolver resolver,
    IChangeRegions regions,
    IChangeMarking marking,
    IDocumentComparison comparison,
    IRevisionAuthor author,
    IProtectionKeys keys,
    IEditGuard guard,
    ICommentAnchors anchors,
    ICommentEditing comments,
    IFolioDocumentService files,
    IFileDialogService dialogs,
    ISidePanelHost panel,
    PulseOffice.UI.Controls.Review.IReviewPanelRequests requests,
    IFolioViewMarks marks,
    IStatusBarService status,
    ITextCatalog texts) : FolioCommandSet(context)
{
    public const string ReviewPage = "review";

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.changes.record") with
        {
            DefaultGesture = "Ctrl+Shift+C",
            CheckedWhen = _ => Editor?.Document.Changes.IsRecording == true,
            OutsideHistory = true,
            Execute = (_, _) => Run(Record),
        },
        Viewing("folio.changes.show") with
        {
            CheckedWhen = _ => Editor?.Document.Changes.ShowChanges == true,
            Execute = (_, _) => Run(Show),
        },
        Editing("folio.changes.protect") with { Surface = CommandSurface.Advanced, OutsideHistory = true, Execute = (_, ct) => ProtectChangesAsync(ct) },
        Viewing("folio.changes.next") with { EnabledWhen = _ => HasChanges(), Execute = (_, _) => Run(editor => Step(editor, forward: true)) },
        Viewing("folio.changes.previous") with { EnabledWhen = _ => HasChanges(), Execute = (_, _) => Run(editor => Step(editor, forward: false)) },
        Resolving("folio.changes.accept", editor => Here(editor, RevisionVerdict.Accept)),
        Resolving("folio.changes.reject", editor => Here(editor, RevisionVerdict.Reject)),
        Resolving("folio.changes.acceptNext", editor => HereThenNext(editor, RevisionVerdict.Accept)),
        Resolving("folio.changes.rejectNext", editor => HereThenNext(editor, RevisionVerdict.Reject)),
        Resolving("folio.changes.reinstate", editor => Here(editor, RevisionVerdict.Reinstate)),
        Resolving("folio.changes.reinstateNext", editor => HereThenNext(editor, RevisionVerdict.Reinstate)),
        Resolving("folio.changes.reinstateAll", editor => All(editor, RevisionVerdict.Reinstate)),
        Resolving("folio.changes.acceptAll", editor => All(editor, RevisionVerdict.Accept)),
        Resolving("folio.changes.rejectAll", editor => All(editor, RevisionVerdict.Reject)),
        Viewing("folio.changes.manage") with { Execute = (_, _) => Run(_ => OpenPanel()) },
        Editing("folio.changes.comment") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && ChangeHere(editor) is not null,
            Execute = (_, ct) => CommentChangeAsync(ct),
        },
        Editing("folio.changes.compare") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => CompareAsync(ct, merge: false) },
        Editing("folio.changes.merge") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => CompareAsync(ct, merge: true) },
        Commenting("folio.comment.reply", _ => ReplyAsync()),
        Commenting("folio.comment.edit", _ => EditCommentAsync()),
        Commenting("folio.comment.resolve", _ => Run(ToggleResolved)),
        Commenting("folio.comment.delete", _ => Run(editor => DeleteComments(editor, anchors.At(editor.Document, editor.Caret) is { } here ? [here.Comment.Id] : []))),
        Prototype("folio.comment.deleteAuthor") with
        {
            ForbiddenState = DocumentState.ReadOnly,
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.Document.Comments.All.Count > 0,
            Execute = (_, ct) => DeleteByAuthorAsync(ct),
        },
        Prototype("folio.comment.deleteAll") with
        {
            ForbiddenState = DocumentState.ReadOnly,
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.Document.Comments.All.Count > 0,
            Execute = (_, _) => Run(editor => DeleteComments(editor, [.. editor.Document.Comments.All.Where(comment => comment.ParentId is null).Select(comment => comment.Id)])),
        },
        Viewing("folio.comment.next") with { EnabledWhen = _ => Editor?.Document.Comments.All.Count > 0, Execute = (_, _) => Run(editor => StepComment(editor, forward: true)) },
        Viewing("folio.comment.previous") with { EnabledWhen = _ => Editor?.Document.Comments.All.Count > 0, Execute = (_, _) => Run(editor => StepComment(editor, forward: false)) },
        Commenting("folio.comment.promote", _ => Run(Promote)),
        Viewing("folio.view.resolvedComments") with
        {
            CheckedWhen = _ => marks.ResolvedComments,
            Execute = (_, _) =>
            {
                marks.ResolvedComments = !marks.ResolvedComments;
                Editor?.Refresh();
                return Task.CompletedTask;
            },
        },
        Viewing("folio.print.comments") with
        {
            Surface = CommandSurface.Advanced,
            CheckedWhen = _ => marks.PrintComments,
            Execute = (_, _) =>
            {
                marks.PrintComments = !marks.PrintComments;
                status.Report(texts[marks.PrintComments ? "folio.print.comments.on" : "folio.print.comments.off"]);
                return Task.CompletedTask;
            },
        },
        Viewing("folio.view.comments") with
        {
            CheckedWhen = _ => marks.Comments,
            Execute = (_, _) =>
            {
                marks.Comments = !marks.Comments;
                Editor?.Refresh();
                return Task.CompletedTask;
            },
        },
        Prototype("folio.protect.document") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is not null,
            OutsideHistory = true,
            Execute = (_, ct) => ProtectDocumentAsync(ct),
        },
    ];

    private CommandDefinition Resolving(string id, Func<FolioEditor, int> run) =>
        Editing(id) with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.Document.Changes.All.Count > 0 && guard.CanResolveChanges(editor.Document),
            Execute = (_, _) => Run(editor =>
            {
                var count = 0;
                editor.Change(() => count = run(editor));
                status.Report(count switch
                {
                    0 => texts["folio.changes.noneHere"],
                    1 => texts["folio.changes.resolved.one"],
                    _ => texts.Format("folio.changes.resolved", count),
                });
            }),
        };

    private CommandDefinition Commenting(string id, Func<CancellationToken, Task> run) =>
        Prototype(id) with
        {
            ForbiddenState = DocumentState.ReadOnly,
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && guard.CanComment(editor.Document) && anchors.At(editor.Document, editor.Caret) is not null,
            Execute = (_, ct) => run(ct),
        };

    private Task Run(Action<FolioEditor> action)
    {
        if (Editor is { } editor) action(editor);
        return Task.CompletedTask;
    }

    private bool HasChanges() => Editor?.Document.Changes.All.Count > 0;

    private void Record(FolioEditor editor)
    {
        var log = editor.Document.Changes;
        if (log.IsRecording && guard.ForcesRecording(editor.Document))
        {
            status.Report(texts["folio.changes.protected"], NotificationSeverity.Warning);
            return;
        }
        editor.History.ApplyOutsideHistory(() => log.IsRecording = !log.IsRecording);
        editor.Refresh();
        status.Report(texts[log.IsRecording ? "changes.recording.on" : "changes.recording.off"]);
    }

    private void Show(FolioEditor editor)
    {
        editor.Document.Changes.ShowChanges = !editor.Document.Changes.ShowChanges;
        editor.Document.Raise();
    }

    private void Step(FolioEditor editor, bool forward)
    {
        var map = regions.Map(editor.Document);
        var from = forward ? editor.Selection.End : editor.Selection.Start;
        var target = forward ? map.After(from) ?? map.Regions.FirstOrDefault() : map.Before(from) ?? map.Regions.LastOrDefault();
        if (target is null) return;
        editor.Select(target.Start, target.End);
    }

    private ChangeRegion? ChangeHere(FolioEditor editor) => regions.Map(editor.Document).Touching(editor.Selection).FirstOrDefault();

    private int Here(FolioEditor editor, RevisionVerdict verdict) =>
        resolver.ResolveIn(editor.Document, editor.History, editor.Selection, verdict);

    private int HereThenNext(FolioEditor editor, RevisionVerdict verdict)
    {
        var count = Here(editor, verdict);
        Step(editor, forward: true);
        return count;
    }

    private void Promote(FolioEditor editor)
    {
        if (anchors.At(editor.Document, editor.Caret) is not { } anchor) return;
        var replies = editor.Document.Comments.RepliesTo(anchor.Comment.Id).ToList();
        if (replies.Count == 0) return;
        editor.Change(() => comments.Promote(editor.Document, editor.History, replies[0].Id, anchor.Start));
    }

    private int All(FolioEditor editor, RevisionVerdict verdict) =>
        resolver.ResolveAll(editor.Document, editor.History, verdict);

    private void OpenPanel() => panel.Show(ReviewPage);

    private async Task CommentChangeAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || ChangeHere(editor) is not { } region) return;
        var values = await forms.AskAsync("command.folio.changes.comment.title", "folio.changes.comment.explanation",
            [new TextField("text", texts["folio.comment.text"], region.Change.Comment ?? string.Empty) { Multiline = true }], ct).ConfigureAwait(true);
        if (values is null) return;
        var text = values.Text("text").Trim();
        var copy = region.Change.Clone();
        copy.Comment = text.Length == 0 ? null : text;
        editor.Change(() =>
        {
            var recorder = new EditRecorder(editor.Document, "comment-change");
            marking.Replace(editor.Document, recorder, copy);
            recorder.Commit(editor.History);
        });
    }

    private async Task CompareAsync(CancellationToken ct, bool merge)
    {
        if (Editor is not { } editor) return;
        var chosen = await dialogs.OpenFileAsync(texts[merge ? "folio.changes.merge.choose" : "folio.changes.compare.choose"], files.ReadableFilters, ct).ConfigureAwait(true);
        if (chosen is null) return;
        var other = await files.ReadAsync(chosen.Path, ct).ConfigureAwait(true);
        if (other is null)
        {
            status.Report(texts.Format("compare.unreadable", chosen.Name), NotificationSeverity.Error);
            return;
        }
        var count = 0;
        editor.Change(() =>
        {
            count = merge
                ? comparison.Merge(editor.Document, other, editor.History, new RevisionSignature(
                    other.Properties.LastModifiedBy is { Length: > 0 } modifier ? modifier : other.Properties.Author is { Length: > 0 } writer ? writer : texts["folio.changes.otherAuthor"],
                    other.Properties.Modified ?? DateTimeOffset.Now))
                : comparison.Compare(editor.Document, other, editor.History, author.Sign());
            editor.Document.Changes.ShowChanges = true;
        });
        status.Report(count == 0 ? texts["compare.identical"] : texts.Format("folio.changes.compared", count), count == 0 ? NotificationSeverity.Success : NotificationSeverity.Info);
        if (count > 0) OpenPanel();
    }

    private async Task ProtectChangesAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var log = editor.Document.Changes;
        var values = await forms.AskAsync("command.folio.changes.protect.title", "folio.changes.protect.explanation",
        [
            new CheckField("protected", texts["review.protect.enabled"], log.ProtectionKey is not null),
            new TextField("password", texts["protect.password"]) { Secret = true },
        ], ct).ConfigureAwait(true);
        if (values is null) return;
        var password = values.Text("password");
        if (log.ProtectionKey is not null && !keys.Matches(log.ProtectionKey, password))
        {
            status.Report(texts["protect.wrongPassword"], NotificationSeverity.Error);
            return;
        }
        var protect = values.Flag("protected");
        if (protect && password.Length == 0)
        {
            status.Report(texts["protect.passwordRequired"], NotificationSeverity.Warning);
            return;
        }
        editor.History.ApplyOutsideHistory(() =>
        {
            log.ProtectionKey = protect ? keys.Digest(password) : null;
            if (protect) log.IsRecording = true;
        });
        editor.Document.Raise();
        status.Report(texts[log.ProtectionKey is null ? "review.unprotected" : "review.protected"]);
    }

    private async Task ProtectDocumentAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var current = editor.Document.Protection;
        var options = Enum.GetValues<DocumentRestriction>();
        var values = await forms.AskAsync("command.folio.protect.document.title", "folio.protect.explanation",
        [
            new ChoiceField("restriction", texts["folio.protect.restriction"], [.. options.Select(option => texts[$"folio.protect.{option.ToString().ToLowerInvariant()}"])], Array.IndexOf(options, current.Restriction)),
            new TextField("password", texts["protect.password"]) { Secret = true },
        ], ct).ConfigureAwait(true);
        if (values is null) return;
        var password = values.Text("password");
        if (current.Key is not null && !keys.Matches(current.Key, password))
        {
            status.Report(texts["protect.wrongPassword"], NotificationSeverity.Error);
            return;
        }
        var restriction = options[values.Choice("restriction", options.Length)];
        editor.History.ApplyOutsideHistory(() =>
        {
            editor.Document.Protection = restriction == DocumentRestriction.None
                ? DocumentProtection.None
                : new DocumentProtection(restriction, password.Length > 0 ? keys.Create(password) : null);
            if (restriction == DocumentRestriction.TrackedChanges) editor.Document.Changes.IsRecording = true;
        });
        editor.Document.Raise();
        editor.Refresh();
        status.Report(texts[restriction == DocumentRestriction.None ? "folio.protect.off" : "folio.protect.on"]);
    }

    private Task ReplyAsync()
    {
        if (Editor is not { } editor || anchors.At(editor.Document, editor.Caret) is not { } anchor) return Task.CompletedTask;
        var last = editor.Document.Comments.Thread(anchor.Comment.Id).Last();
        requests.Request(new ReviewPanelRequest(ReviewPanelRequestKind.Reply, last.Id, string.Empty));
        panel.Show(ReviewPage);
        return Task.CompletedTask;
    }

    private Task EditCommentAsync()
    {
        if (Editor is not { } editor || anchors.At(editor.Document, editor.Caret) is not { } anchor) return Task.CompletedTask;
        var current = string.Join("\n", anchor.Comment.Content.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Select(paragraph => paragraph.Text));
        requests.Request(new ReviewPanelRequest(ReviewPanelRequestKind.Edit, anchor.Comment.Id, current));
        panel.Show(ReviewPage);
        return Task.CompletedTask;
    }

    private void ToggleResolved(FolioEditor editor)
    {
        if (anchors.At(editor.Document, editor.Caret) is not { } anchor) return;
        editor.Change(() => comments.Resolve(editor.Document, editor.History, anchor.Comment.Id, !anchor.Comment.Resolved));
    }

    private void DeleteComments(FolioEditor editor, IReadOnlyList<string> ids)
    {
        if (ids.Count == 0 || !guard.CanComment(editor.Document)) return;
        editor.Change(() => comments.Delete(editor.Document, editor.History, ids));
    }

    private async Task DeleteByAuthorAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var authors = editor.Document.Comments.All.Select(comment => comment.Author).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.CurrentCulture).ToList();
        if (authors.Count == 0) return;
        var values = await forms.AskAsync("command.folio.comment.deleteAuthor.title", "folio.comment.deleteAuthor.explanation",
            [new ChoiceField("author", texts["folio.comment.author"], authors)], ct, 400).ConfigureAwait(true);
        if (values is null) return;
        var chosen = authors[values.Choice("author", authors.Count)];
        DeleteComments(editor, [.. editor.Document.Comments.All.Where(comment => comment.Author == chosen).Select(comment => comment.Id)]);
    }

    private void StepComment(FolioEditor editor, bool forward)
    {
        var all = anchors.All(editor.Document);
        if (all.Count == 0) return;
        var caret = forward ? editor.Selection.End : editor.Selection.Start;
        var order = all.ToList();
        order.Sort((first, second) => Compare(editor, first.Start, second.Start));
        var target = forward
            ? order.FirstOrDefault(anchor => Compare(editor, anchor.Start, caret) > 0) ?? order[0]
            : order.LastOrDefault(anchor => Compare(editor, anchor.Start, caret) < 0) ?? order[^1];
        editor.Select(target.Start, target.End);
    }

    private int Compare(FolioEditor editor, DocumentPosition first, DocumentPosition second) =>
        editor.Services.Order.Compare(editor.Document, first, second);
}
