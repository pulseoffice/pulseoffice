using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public interface IDocumentStateWitness
{
    IDocumentStateMark? Mark(OpenDocument document);
}

public interface IDocumentStateMark
{
    string? Broken();
}

public sealed class WorkspaceDialogAdmission(
    IDocumentWorkspace workspace,
    IEnumerable<IDocumentStateWitness> witnesses,
    IStatusBarService status,
    ITextCatalog texts,
    ILogger<WorkspaceDialogAdmission> logger) : IDialogResultAdmission
{
    public IDialogAdmissionTicket Open()
    {
        if (workspace.Active is not { } document) return new Unbound();
        return new Ticket(this, document, document.IsReadOnly, [.. witnesses.Select(witness => witness.Mark(document)).OfType<IDocumentStateMark>()]);
    }

    private string? Broken(OpenDocument document, bool wasReadOnly, IReadOnlyList<IDocumentStateMark> marks)
    {
        if (!workspace.Documents.Contains(document)) return "status.dialog.stale.closed";
        if (!wasReadOnly && document.IsReadOnly) return "status.dialog.stale.readOnly";
        return marks.Select(mark => mark.Broken()).FirstOrDefault(key => key is not null);
    }

    private bool Admit(OpenDocument document, bool wasReadOnly, IReadOnlyList<IDocumentStateMark> marks)
    {
        if (Broken(document, wasReadOnly, marks) is not { } key) return true;
        logger.LogInformation("Dialog result dropped: {Reason}", key);
        status.Report(texts[key], NotificationSeverity.Warning);
        return false;
    }

    private sealed class Ticket(WorkspaceDialogAdmission admission, OpenDocument document, bool wasReadOnly, IReadOnlyList<IDocumentStateMark> marks) : IDialogAdmissionTicket
    {
        public bool Admits() => admission.Admit(document, wasReadOnly, marks);
    }

    private sealed class Unbound : IDialogAdmissionTicket
    {
        public bool Admits() => true;
    }
}
