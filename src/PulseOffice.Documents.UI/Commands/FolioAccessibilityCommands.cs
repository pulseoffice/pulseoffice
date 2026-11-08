using PulseOffice.Documents.Accessibility;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public interface IFolioAccessibilityMonitor
{
    bool Enabled { get; set; }
}

public sealed class FolioAccessibilityMonitor(IActiveFolioContext context, IAccessibilityAudit audit, IStatusBarService status, ITextCatalog texts) : IFolioAccessibilityMonitor
{
    private const string Segment = "folio.accessibility";

    private FolioEditor? _watched;
    private bool _pending;

    public bool Enabled
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (value)
            {
                context.Changed += OnContext;
                Watch();
                Refresh();
            }
            else
            {
                context.Changed -= OnContext;
                Unwatch();
                status.Remove(Segment);
            }
        }
    }

    private void OnContext(object? sender, EventArgs e)
    {
        Watch();
        Refresh();
    }

    private void Watch()
    {
        Unwatch();
        _watched = context.Target?.Editor;
        if (_watched is not null) _watched.Document.Changed += OnDocument;
    }

    private void Unwatch()
    {
        if (_watched is not null) _watched.Document.Changed -= OnDocument;
        _watched = null;
    }

    private void OnDocument(object? sender, EventArgs e)
    {
        if (_pending) return;
        _pending = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _pending = false;
            Refresh();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void Refresh()
    {
        if (!Enabled) return;
        if (_watched is not { } editor)
        {
            status.Remove(Segment);
            return;
        }
        var count = audit.Check(editor.Document).Count;
        status.Register(Segment, StatusSegmentKind.Contextual).Text = texts.Format("folio.accessibility.count", count);
    }
}

public sealed class FolioAccessibilityCommands(
    IActiveFolioContext context,
    IAccessibilityAudit audit,
    IFolioAccessibilityMonitor monitor,
    IFolioForms forms,
    IStatusBarService status,
    ITextCatalog texts) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Viewing("folio.tools.accessibilityCheck") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => CheckAsync(ct) },
        Viewing("folio.tools.accessibilityOnline") with
        {
            Surface = CommandSurface.Advanced,
            CheckedWhen = _ => monitor.Enabled,
            Execute = (_, _) =>
            {
                monitor.Enabled = !monitor.Enabled;
                return Task.CompletedTask;
            },
        },
    ];

    private async Task CheckAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var issues = audit.Check(editor.Document);
        if (issues.Count == 0)
        {
            status.Report(texts["folio.accessibility.clean"], NotificationSeverity.Success);
            return;
        }
        List<string> labels = [.. issues.Select(Describe)];
        if (await forms.AskAsync("command.folio.tools.accessibilityCheck.title", "folio.accessibility.explanation",
                [new ChoiceField("issue", texts.Format("folio.accessibility.found", issues.Count), labels)], ct, 620).ConfigureAwait(true) is not { } values
            || Editor is not { } current) return;
        var issue = issues[values.Choice("issue", issues.Count)];
        if (issue.ObjectId is { } id) current.SelectObject(id);
        else if (issue.ParagraphId is { } paragraph)
        {
            var at = new DocumentPosition(paragraph, 0);
            current.Select(at, at);
        }
    }

    private string Describe(AccessibilityIssue issue)
    {
        var kind = issue.Kind.ToString();
        var text = texts["folio.accessibility." + char.ToLowerInvariant(kind[0]) + kind[1..]];
        return issue.Detail is { Length: > 0 } detail ? text + " — " + (detail.Length > 40 ? detail[..40] + "…" : detail) : text;
    }
}
